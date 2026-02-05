using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;
using System.Text.Json;

namespace DarkVelocity.Host.Grains;

public class WebhookSubscriptionGrain : Grain, IWebhookSubscriptionGrain
{
    private readonly IPersistentState<WebhookSubscriptionState> _state;
    private readonly IWebhookDeliveryService _deliveryService;
    private readonly ILogger<WebhookSubscriptionGrain> _logger;
    private IAsyncStream<IStreamEvent>? _webhookStream;

    private const int MaxRecentDeliveries = 100;
    private const int MaxConsecutiveFailuresBeforeDisable = 10;

    // Exponential backoff parameters
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    public WebhookSubscriptionGrain(
        [PersistentState("webhook", "OrleansStorage")]
        IPersistentState<WebhookSubscriptionState> state,
        IWebhookDeliveryService deliveryService,
        ILogger<WebhookSubscriptionGrain> logger)
    {
        _state = state;
        _deliveryService = deliveryService;
        _logger = logger;
    }

    private IAsyncStream<IStreamEvent> GetWebhookStream()
    {
        if (_webhookStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.WebhookStreamNamespace, _state.State.OrganizationId.ToString());
            _webhookStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _webhookStream!;
    }

    public async Task<WebhookCreatedResult> CreateAsync(CreateWebhookCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Webhook subscription already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, webhookId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new WebhookSubscriptionState
        {
            Id = webhookId,
            OrganizationId = command.OrganizationId,
            Name = command.Name,
            Url = command.Url,
            Secret = command.Secret,
            Headers = command.Headers ?? [],
            Events = command.EventTypes.Select(et => new WebhookEvent
            {
                EventType = et,
                IsEnabled = true
            }).ToList(),
            Status = WebhookStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return new WebhookCreatedResult(webhookId, command.Name, _state.State.CreatedAt);
    }

    public Task<WebhookSubscriptionState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(UpdateWebhookCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Url != null) _state.State.Url = command.Url;
        if (command.Secret != null) _state.State.Secret = command.Secret;
        if (command.Headers != null) _state.State.Headers = command.Headers;
        if (command.EventTypes != null)
        {
            _state.State.Events = command.EventTypes.Select(et => new WebhookEvent
            {
                EventType = et,
                IsEnabled = true
            }).ToList();
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.Status = WebhookStatus.Deleted;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SubscribeToEventAsync(string eventType)
    {
        EnsureExists();

        if (!_state.State.Events.Any(e => e.EventType == eventType))
        {
            _state.State.Events.Add(new WebhookEvent
            {
                EventType = eventType,
                IsEnabled = true
            });
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnsubscribeFromEventAsync(string eventType)
    {
        EnsureExists();

        var evt = _state.State.Events.FirstOrDefault(e => e.EventType == eventType);
        if (evt != null)
        {
            _state.State.Events.Remove(evt);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsSubscribedToEventAsync(string eventType) =>
        Task.FromResult(_state.State.Events.Any(e => e.EventType == eventType && e.IsEnabled));

    public async Task PauseAsync()
    {
        EnsureExists();
        _state.State.Status = WebhookStatus.Paused;
        _state.State.PausedAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        EnsureExists();
        _state.State.Status = WebhookStatus.Active;
        _state.State.PausedAt = null;
        _state.State.ConsecutiveFailures = 0;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<WebhookStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);

    public async Task<DeliveryResult> DeliverAsync(string eventType, string payload)
    {
        EnsureExists();

        if (_state.State.Status == WebhookStatus.Deleted)
            throw new InvalidOperationException("Webhook subscription has been deleted");

        if (_state.State.Status == WebhookStatus.Failed)
            throw new InvalidOperationException("Webhook endpoint is disabled due to too many failures. Call ResumeAsync to re-enable.");

        if (_state.State.Status == WebhookStatus.Paused)
            throw new InvalidOperationException("Webhook is paused. Call ResumeAsync to re-enable.");

        if (!_state.State.Events.Any(e => e.EventType == eventType && e.IsEnabled))
            throw new InvalidOperationException($"Not subscribed to event: {eventType}");

        var deliveryId = Guid.NewGuid();
        var attemptNumber = 1;

        // Publish attempt event
        await GetWebhookStream().OnNextAsync(new WebhookDeliveryAttemptedEvent(
            _state.State.Id,
            deliveryId,
            eventType,
            _state.State.Url,
            attemptNumber)
        {
            OrganizationId = _state.State.OrganizationId
        });

        // Parse payload (it's a JSON string) and deliver
        object payloadObject;
        try
        {
            payloadObject = JsonSerializer.Deserialize<JsonElement>(payload);
        }
        catch
        {
            // If not valid JSON, wrap it
            payloadObject = new { data = payload, eventType };
        }

        var result = await _deliveryService.DeliverAsync(
            _state.State.Url,
            new
            {
                id = deliveryId,
                @event = eventType,
                timestamp = DateTime.UtcNow,
                data = payloadObject
            },
            _state.State.Secret,
            _state.State.Headers);

        var delivery = new WebhookDelivery
        {
            Id = deliveryId,
            EventType = eventType,
            AttemptedAt = DateTime.UtcNow,
            StatusCode = result.StatusCode ?? 0,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            RetryCount = 0
        };

        await RecordDeliveryAsync(delivery);

        if (result.Success)
        {
            // Publish success event
            await GetWebhookStream().OnNextAsync(new WebhookDeliverySucceededEvent(
                _state.State.Id,
                deliveryId,
                eventType,
                result.StatusCode ?? 200,
                result.ResponseTimeMs)
            {
                OrganizationId = _state.State.OrganizationId
            });

            _logger.LogInformation(
                "Webhook {WebhookId} delivered successfully to {Url} for event {EventType}",
                _state.State.Id, _state.State.Url, eventType);
        }
        else
        {
            var willRetry = result.ShouldRetry && _state.State.ConsecutiveFailures < MaxConsecutiveFailuresBeforeDisable;

            // Publish failure event
            await GetWebhookStream().OnNextAsync(new WebhookDeliveryFailedEvent(
                _state.State.Id,
                deliveryId,
                eventType,
                result.StatusCode,
                result.ErrorMessage ?? "Unknown error",
                attemptNumber,
                willRetry)
            {
                OrganizationId = _state.State.OrganizationId
            });

            _logger.LogWarning(
                "Webhook {WebhookId} delivery failed to {Url} for event {EventType}. Error: {Error}. ConsecutiveFailures: {ConsecutiveFailures}",
                _state.State.Id, _state.State.Url, eventType, result.ErrorMessage, _state.State.ConsecutiveFailures);

            // Check if we need to disable the endpoint
            if (_state.State.Status == WebhookStatus.Failed)
            {
                await GetWebhookStream().OnNextAsync(new WebhookEndpointDisabledEvent(
                    _state.State.Id,
                    _state.State.Url,
                    _state.State.ConsecutiveFailures,
                    $"Disabled after {_state.State.ConsecutiveFailures} consecutive failures")
                {
                    OrganizationId = _state.State.OrganizationId
                });

                _logger.LogError(
                    "Webhook {WebhookId} endpoint {Url} has been disabled due to {ConsecutiveFailures} consecutive failures",
                    _state.State.Id, _state.State.Url, _state.State.ConsecutiveFailures);
            }
        }

        return new DeliveryResult(deliveryId, result.Success, result.StatusCode ?? 0);
    }

    /// <summary>
    /// Delivers a webhook with retry support using exponential backoff.
    /// </summary>
    public async Task<DeliveryResult> DeliverWithRetryAsync(string eventType, string payload, int maxRetries = 3)
    {
        EnsureExists();

        var lastResult = await DeliverAsync(eventType, payload);
        var attemptNumber = 1;

        while (!lastResult.Success && attemptNumber < maxRetries)
        {
            // Check if we should retry based on current status
            if (_state.State.Status != WebhookStatus.Active)
                break;

            // Wait with exponential backoff
            var delayIndex = Math.Min(attemptNumber - 1, RetryDelays.Length - 1);
            var delay = RetryDelays[delayIndex];

            _logger.LogInformation(
                "Retrying webhook {WebhookId} delivery in {Delay}. Attempt {Attempt} of {MaxRetries}",
                _state.State.Id, delay, attemptNumber + 1, maxRetries);

            await Task.Delay(delay);

            attemptNumber++;
            lastResult = await DeliverAsync(eventType, payload);
        }

        return lastResult;
    }

    public async Task RecordDeliveryAsync(WebhookDelivery delivery)
    {
        EnsureExists();

        _state.State.RecentDeliveries.Insert(0, delivery);
        if (_state.State.RecentDeliveries.Count > MaxRecentDeliveries)
        {
            _state.State.RecentDeliveries.RemoveAt(_state.State.RecentDeliveries.Count - 1);
        }

        _state.State.LastDeliveryAt = delivery.AttemptedAt;

        if (delivery.Success)
        {
            _state.State.ConsecutiveFailures = 0;
        }
        else
        {
            _state.State.ConsecutiveFailures++;
            if (_state.State.ConsecutiveFailures >= _state.State.MaxRetries)
            {
                _state.State.Status = WebhookStatus.Failed;
            }
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<WebhookDelivery>> GetRecentDeliveriesAsync() =>
        Task.FromResult<IReadOnlyList<WebhookDelivery>>(_state.State.RecentDeliveries);

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Webhook subscription does not exist");
    }
}

public class BookingCalendarGrain : Grain, IBookingCalendarGrain
{
    private readonly IPersistentState<BookingCalendarState> _state;

    public BookingCalendarGrain(
        [PersistentState("bookingcalendar", "OrleansStorage")]
        IPersistentState<BookingCalendarState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new BookingCalendarState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Date = date,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<BookingCalendarState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task AddBookingAsync(AddBookingToCalendarCommand command)
    {
        EnsureExists();

        if (_state.State.Bookings.Any(b => b.BookingId == command.BookingId))
            return;

        var reference = new BookingReference
        {
            BookingId = command.BookingId,
            ConfirmationCode = command.ConfirmationCode,
            Time = command.Time,
            PartySize = command.PartySize,
            GuestName = command.GuestName,
            Status = command.Status
        };

        _state.State.Bookings.Add(reference);
        _state.State.TotalCovers += command.PartySize;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UpdateBookingAsync(UpdateBookingInCalendarCommand command)
    {
        EnsureExists();

        var index = _state.State.Bookings.FindIndex(b => b.BookingId == command.BookingId);
        if (index < 0)
            throw new InvalidOperationException("Booking not found in calendar");

        var existing = _state.State.Bookings[index];
        var oldPartySize = existing.PartySize;

        _state.State.Bookings[index] = existing with
        {
            Status = command.Status ?? existing.Status,
            Time = command.Time ?? existing.Time,
            PartySize = command.PartySize ?? existing.PartySize,
            TableId = command.TableId ?? existing.TableId,
            TableNumber = command.TableNumber ?? existing.TableNumber
        };

        if (command.PartySize.HasValue)
        {
            _state.State.TotalCovers = _state.State.TotalCovers - oldPartySize + command.PartySize.Value;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveBookingAsync(Guid bookingId)
    {
        EnsureExists();

        var booking = _state.State.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
        if (booking != null)
        {
            _state.State.Bookings.Remove(booking);
            _state.State.TotalCovers -= booking.PartySize;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<BookingReference>> GetBookingsAsync(BookingStatus? status = null)
    {
        var bookings = status.HasValue
            ? _state.State.Bookings.Where(b => b.Status == status.Value).ToList()
            : _state.State.Bookings;

        return Task.FromResult<IReadOnlyList<BookingReference>>(
            bookings.OrderBy(b => b.Time).ToList());
    }

    public Task<IReadOnlyList<BookingReference>> GetBookingsByTimeRangeAsync(TimeOnly start, TimeOnly end)
    {
        var bookings = _state.State.Bookings
            .Where(b => b.Time >= start && b.Time <= end)
            .OrderBy(b => b.Time)
            .ToList();

        return Task.FromResult<IReadOnlyList<BookingReference>>(bookings);
    }

    public Task<int> GetCoverCountAsync() => Task.FromResult(_state.State.TotalCovers);

    public Task<int> GetBookingCountAsync(BookingStatus? status = null)
    {
        var count = status.HasValue
            ? _state.State.Bookings.Count(b => b.Status == status.Value)
            : _state.State.Bookings.Count;

        return Task.FromResult(count);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    public Task<CalendarDayView> GetDayViewAsync(TimeSpan? slotDuration = null)
    {
        EnsureExists();
        var duration = slotDuration ?? TimeSpan.FromMinutes(30);
        var slots = new List<CalendarBookingSlot>();

        var currentTime = new TimeOnly(0, 0);
        while (currentTime < new TimeOnly(23, 59))
        {
            var endTime = currentTime.Add(duration);
            if (endTime < currentTime) endTime = new TimeOnly(23, 59);

            var slotBookings = _state.State.Bookings
                .Where(b => b.Time >= currentTime && b.Time < endTime)
                .ToList();

            slots.Add(new CalendarBookingSlot
            {
                StartTime = currentTime,
                EndTime = endTime,
                BookingCount = slotBookings.Count,
                CoverCount = slotBookings.Sum(b => b.PartySize),
                Bookings = slotBookings
            });

            currentTime = endTime;
        }

        return Task.FromResult(new CalendarDayView
        {
            Date = _state.State.Date,
            Slots = slots,
            TotalBookings = _state.State.Bookings.Count,
            TotalCovers = _state.State.TotalCovers,
            ConfirmedBookings = _state.State.Bookings.Count(b => b.Status == BookingStatus.Confirmed),
            SeatedBookings = _state.State.Bookings.Count(b => b.Status == BookingStatus.Seated),
            NoShowCount = _state.State.Bookings.Count(b => b.Status == BookingStatus.NoShow)
        });
    }

    public Task<IReadOnlyList<AvailableTimeSlot>> GetAvailabilityAsync(GetCalendarAvailabilityQuery query)
    {
        EnsureExists();
        // Simple availability - return slots that have capacity
        var slots = new List<AvailableTimeSlot>();
        var duration = query.RequestedDuration ?? TimeSpan.FromMinutes(90);

        var startTime = query.PreferredTime ?? new TimeOnly(12, 0);
        for (var i = 0; i < 10; i++)
        {
            var time = startTime.Add(TimeSpan.FromMinutes(30 * i));
            slots.Add(new AvailableTimeSlot
            {
                Time = time,
                IsAvailable = true,
                AvailableCapacity = 20,
                EstimatedDuration = duration,
                SuggestedTables = []
            });
        }

        return Task.FromResult<IReadOnlyList<AvailableTimeSlot>>(slots);
    }

    public Task<IReadOnlyList<TableAllocation>> GetTableAllocationsAsync()
    {
        EnsureExists();
        var allocations = _state.State.Bookings
            .Where(b => b.TableId.HasValue)
            .GroupBy(b => b.TableId!.Value)
            .Select(g => new TableAllocation
            {
                TableId = g.Key,
                TableNumber = g.First().TableNumber ?? string.Empty,
                Bookings = g.ToList()
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TableAllocation>>(allocations);
    }

    public async Task SetTableAllocationAsync(Guid bookingId, Guid tableId, string tableNumber)
    {
        EnsureExists();
        var index = _state.State.Bookings.FindIndex(b => b.BookingId == bookingId);
        if (index < 0)
            throw new InvalidOperationException("Booking not found in calendar");

        var existing = _state.State.Bookings[index];
        _state.State.Bookings[index] = existing with
        {
            TableId = tableId,
            TableNumber = tableNumber
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking calendar not initialized");
    }
}
