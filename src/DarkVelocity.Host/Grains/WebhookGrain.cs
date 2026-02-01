using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

public class WebhookSubscriptionGrain : Grain, IWebhookSubscriptionGrain
{
    private readonly IPersistentState<WebhookSubscriptionState> _state;
    private const int MaxRecentDeliveries = 100;

    public WebhookSubscriptionGrain(
        [PersistentState("webhook", "OrleansStorage")]
        IPersistentState<WebhookSubscriptionState> state)
    {
        _state = state;
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

        if (_state.State.Status != WebhookStatus.Active)
            throw new InvalidOperationException($"Webhook is not active: {_state.State.Status}");

        if (!_state.State.Events.Any(e => e.EventType == eventType && e.IsEnabled))
            throw new InvalidOperationException($"Not subscribed to event: {eventType}");

        var deliveryId = Guid.NewGuid();
        var delivery = new WebhookDelivery
        {
            Id = deliveryId,
            EventType = eventType,
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 200, // Simulated success - actual HTTP call would be done by a delivery service
            Success = true,
            RetryCount = 0
        };

        await RecordDeliveryAsync(delivery);
        return new DeliveryResult(deliveryId, true, 200);
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

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking calendar not initialized");
    }
}
