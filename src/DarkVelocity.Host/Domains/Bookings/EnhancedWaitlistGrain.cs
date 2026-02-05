using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Enhanced waitlist grain with estimated wait calculation, SMS notifications,
/// automatic promotion, and priority for returning customers.
/// </summary>
public class EnhancedWaitlistGrain : Grain, IEnhancedWaitlistGrain
{
    private readonly IPersistentState<EnhancedWaitlistState> _state;
    private readonly IGrainFactory _grainFactory;

    private const int DefaultTurnTimeMinutes = 60;

    public EnhancedWaitlistGrain(
        [PersistentState("enhancedwaitlist", "OrleansStorage")]
        IPersistentState<EnhancedWaitlistState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new EnhancedWaitlistState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Date = date,
            CurrentPosition = 0,
            AverageWait = TimeSpan.FromMinutes(15),
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<WaitlistState> GetStateAsync()
    {
        // Convert to original WaitlistState for compatibility
        return Task.FromResult(new WaitlistState
        {
            SiteId = _state.State.SiteId,
            OrganizationId = _state.State.OrganizationId,
            Date = _state.State.Date,
            Entries = _state.State.Entries,
            CurrentPosition = _state.State.CurrentPosition,
            AverageWait = _state.State.AverageWait,
            Version = _state.State.Version
        });
    }

    public async Task<EnhancedWaitlistEntryResult> AddEntryAsync(AddToEnhancedWaitlistCommand command)
    {
        EnsureExists();

        _state.State.CurrentPosition++;
        var position = _state.State.CurrentPosition;

        // Apply returning customer priority boost
        if (command.IsReturningCustomer && _state.State.Settings.PrioritizeReturningCustomers)
        {
            var boost = _state.State.Settings.ReturningCustomerBoostPositions;
            var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();
            var insertPosition = Math.Max(1, activeEntries.Count - boost + 1);

            // Shift other entries
            foreach (var existingEntry in activeEntries.Where(e => e.Position >= insertPosition))
            {
                existingEntry.Position++;
            }

            position = insertPosition;
        }

        var entryId = Guid.NewGuid();
        var entry = new WaitlistEntry
        {
            Id = entryId,
            Guest = command.Guest,
            PartySize = command.PartySize,
            Position = position,
            Status = WaitlistStatus.Waiting,
            CheckedInAt = DateTime.UtcNow,
            QuotedWait = await CalculateEstimatedWaitAsync(command.PartySize, position),
            TablePreferences = command.TablePreferences,
            NotificationMethod = command.NotificationMethod,
            CustomerId = command.CustomerId
        };

        _state.State.Entries.Add(entry);
        _state.State.Version++;
        await _state.WriteStateAsync();

        var estimate = await GetEntryEstimateAsync(entryId);

        return new EnhancedWaitlistEntryResult
        {
            EntryId = entryId,
            Position = position,
            Estimate = estimate,
            AddedAt = entry.CheckedInAt
        };
    }

    public async Task UpdatePositionAsync(Guid entryId, int newPosition)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        var oldPosition = entry.Position;
        if (oldPosition == newPosition)
            return;

        // Adjust other entries' positions
        if (newPosition < oldPosition)
        {
            // Moving up - increment positions of entries between new and old
            foreach (var e in _state.State.Entries.Where(e =>
                IsActive(e) && e.Position >= newPosition && e.Position < oldPosition))
            {
                e.Position++;
            }
        }
        else
        {
            // Moving down - decrement positions of entries between old and new
            foreach (var e in _state.State.Entries.Where(e =>
                IsActive(e) && e.Position > oldPosition && e.Position <= newPosition))
            {
                e.Position--;
            }
        }

        entry.Position = newPosition;
        entry.QuotedWait = await CalculateEstimatedWaitAsync(entry.PartySize, newPosition);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<WaitlistNotification> NotifyTableReadyAsync(Guid entryId, Guid? tableId = null, string? tableNumber = null)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        if (!IsActive(entry))
            throw new InvalidOperationException("Entry cannot be notified");

        entry.Status = WaitlistStatus.Notified;
        entry.NotifiedAt = DateTime.UtcNow;

        bool success = false;
        string? errorMessage = null;

        try
        {
            await SendWaitlistNotificationAsync(entry, "table_ready", tableNumber);
            success = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        var notification = new WaitlistNotificationRecord
        {
            EntryId = entryId,
            NotificationId = Guid.NewGuid(),
            Type = "table_ready",
            SentAt = DateTime.UtcNow,
            Channel = entry.NotificationMethod.ToString().ToLowerInvariant(),
            Success = success,
            ErrorMessage = errorMessage
        };

        _state.State.NotificationHistory.Add(notification);
        _state.State.Version++;
        await _state.WriteStateAsync();

        return new WaitlistNotification
        {
            EntryId = entryId,
            NotificationId = notification.NotificationId,
            Type = notification.Type,
            SentAt = notification.SentAt,
            Channel = notification.Channel,
            Success = notification.Success,
            ErrorMessage = notification.ErrorMessage
        };
    }

    public async Task<WaitlistPromotionResult> SeatEntryAsync(Guid entryId, Guid tableId, string tableNumber)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        if (entry.Status == WaitlistStatus.Left || entry.Status == WaitlistStatus.Seated)
            throw new InvalidOperationException("Entry cannot be seated");

        var wasNotified = entry.Status == WaitlistStatus.Notified;

        entry.Status = WaitlistStatus.Seated;
        entry.SeatedAt = DateTime.UtcNow;
        entry.AssignedTableId = tableId;

        // Calculate actual wait time and update turn time data
        var actualWait = DateTime.UtcNow - entry.CheckedInAt;
        await UpdateTurnTimeDataAsync(entry.PartySize, actualWait);

        // Update average wait
        RecalculateAverageWait();

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new WaitlistPromotionResult
        {
            EntryId = entryId,
            TableId = tableId,
            TableNumber = tableNumber,
            WasNotified = wasNotified,
            PromotedAt = DateTime.UtcNow
        };
    }

    public async Task RemoveEntryAsync(Guid entryId, string reason)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        entry.Status = WaitlistStatus.Left;

        // Adjust positions of remaining entries
        foreach (var e in _state.State.Entries.Where(e => IsActive(e) && e.Position > entry.Position))
        {
            e.Position--;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<WaitlistPromotionResult> PromoteToBookingAsync(Guid entryId, DateTime bookingTime, Guid? tableId = null)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        if (!IsActive(entry))
            throw new InvalidOperationException("Entry cannot be promoted");

        // Create booking
        var bookingId = Guid.NewGuid();
        var bookingGrain = _grainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(_state.State.OrganizationId, _state.State.SiteId, bookingId));

        await bookingGrain.RequestAsync(new RequestBookingCommand(
            _state.State.OrganizationId,
            _state.State.SiteId,
            entry.Guest,
            bookingTime,
            entry.PartySize,
            SpecialRequests: entry.TablePreferences,
            Source: BookingSource.Waitlist,
            CustomerId: entry.CustomerId));

        await bookingGrain.ConfirmAsync();

        if (tableId.HasValue)
        {
            await bookingGrain.AssignTableAsync(new AssignTableCommand(tableId.Value, "", 0));
        }

        entry.ConvertedToBookingId = bookingId;
        entry.Status = WaitlistStatus.Seated;
        entry.SeatedAt = DateTime.UtcNow;

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new WaitlistPromotionResult
        {
            EntryId = entryId,
            BookingId = bookingId,
            TableId = tableId,
            WasNotified = entry.NotifiedAt.HasValue,
            PromotedAt = DateTime.UtcNow
        };
    }

    public Task<WaitlistEstimate> GetWaitEstimateAsync(int partySize)
    {
        EnsureExists();

        var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();
        var position = activeEntries.Count + 1;

        return Task.FromResult(CalculateEstimateAsync(partySize, position, activeEntries));
    }

    public Task<WaitlistEstimate> GetEntryEstimateAsync(Guid entryId)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();

        return Task.FromResult(CalculateEstimateAsync(entry.PartySize, entry.Position, activeEntries));
    }

    public async Task UpdateTurnTimeDataAsync(int partySize, TimeSpan turnTime)
    {
        EnsureExists();

        var existingIndex = _state.State.TurnTimeData.FindIndex(t => t.PartySize == partySize);

        if (existingIndex >= 0)
        {
            var existing = _state.State.TurnTimeData[existingIndex];
            var newAverage = (existing.AverageTurnTimeTicks * existing.SampleCount + turnTime.Ticks) /
                             (existing.SampleCount + 1);

            _state.State.TurnTimeData[existingIndex] = new WaitlistTurnTimeData
            {
                PartySize = partySize,
                AverageTurnTimeTicks = newAverage,
                SampleCount = existing.SampleCount + 1
            };
        }
        else
        {
            _state.State.TurnTimeData.Add(new WaitlistTurnTimeData
            {
                PartySize = partySize,
                AverageTurnTimeTicks = turnTime.Ticks,
                SampleCount = 1
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<WaitlistNotification> SendPositionUpdateAsync(Guid entryId)
    {
        EnsureExists();

        var entry = _state.State.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found");

        if (!IsActive(entry))
            throw new InvalidOperationException("Entry is not active");

        bool success = false;
        string? errorMessage = null;

        try
        {
            await SendWaitlistNotificationAsync(entry, "position_update");
            success = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        var notification = new WaitlistNotificationRecord
        {
            EntryId = entryId,
            NotificationId = Guid.NewGuid(),
            Type = "position_update",
            SentAt = DateTime.UtcNow,
            Channel = entry.NotificationMethod.ToString().ToLowerInvariant(),
            Success = success,
            ErrorMessage = errorMessage
        };

        _state.State.NotificationHistory.Add(notification);
        _state.State.Version++;
        await _state.WriteStateAsync();

        return new WaitlistNotification
        {
            EntryId = entryId,
            NotificationId = notification.NotificationId,
            Type = notification.Type,
            SentAt = notification.SentAt,
            Channel = notification.Channel,
            Success = notification.Success,
            ErrorMessage = notification.ErrorMessage
        };
    }

    public Task<IReadOnlyList<WaitlistNotification>> GetNotificationHistoryAsync(Guid entryId)
    {
        EnsureExists();

        var notifications = _state.State.NotificationHistory
            .Where(n => n.EntryId == entryId)
            .Select(n => new WaitlistNotification
            {
                EntryId = n.EntryId,
                NotificationId = n.NotificationId,
                Type = n.Type,
                SentAt = n.SentAt,
                Channel = n.Channel,
                Success = n.Success,
                ErrorMessage = n.ErrorMessage
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<WaitlistNotification>>(notifications);
    }

    public Task<WaitlistEntry?> FindNextSuitableEntryAsync(int tableCapacity, IReadOnlyList<string>? tableTags = null)
    {
        EnsureExists();

        var suitableEntries = _state.State.Entries
            .Where(IsActive)
            .Where(e => e.PartySize <= tableCapacity && e.PartySize >= tableCapacity - 2)
            .OrderBy(e => e.Position)
            .ToList();

        // If tags specified, prefer entries with matching preferences
        if (tableTags != null && tableTags.Count > 0)
        {
            var withMatchingPreference = suitableEntries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.TablePreferences) &&
                tableTags.Any(t => e.TablePreferences!.Contains(t, StringComparison.OrdinalIgnoreCase)));

            if (withMatchingPreference != null)
                return Task.FromResult<WaitlistEntry?>(withMatchingPreference);
        }

        return Task.FromResult(suitableEntries.FirstOrDefault());
    }

    public async Task<IReadOnlyList<Guid>> ExpireOldEntriesAsync()
    {
        EnsureExists();

        if (!_state.State.Settings.AutoExpireEntries)
            return [];

        var maxWait = _state.State.Settings.MaxWaitTime;
        var cutoff = DateTime.UtcNow - maxWait;

        var expiredEntries = _state.State.Entries
            .Where(e => IsActive(e) && e.CheckedInAt < cutoff)
            .ToList();

        var expiredIds = new List<Guid>();
        foreach (var entry in expiredEntries)
        {
            entry.Status = WaitlistStatus.Left;
            expiredIds.Add(entry.Id);
        }

        if (expiredIds.Count > 0)
        {
            // Recalculate positions
            var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();
            for (int i = 0; i < activeEntries.Count; i++)
            {
                activeEntries[i].Position = i + 1;
            }

            _state.State.Version++;
            await _state.WriteStateAsync();
        }

        return expiredIds;
    }

    public async Task RecalculateEstimatesAsync()
    {
        EnsureExists();

        var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();

        foreach (var entry in activeEntries)
        {
            entry.QuotedWait = await CalculateEstimatedWaitAsync(entry.PartySize, entry.Position);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<int> GetWaitingCountAsync()
    {
        EnsureExists();
        return Task.FromResult(_state.State.Entries.Count(IsActive));
    }

    public Task<IReadOnlyList<WaitlistEntry>> GetEntriesAsync()
    {
        EnsureExists();
        return Task.FromResult<IReadOnlyList<WaitlistEntry>>(
            _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList());
    }

    public Task<IReadOnlyList<WaitlistEntry>> GetEntriesByStatusAsync(WaitlistStatus status)
    {
        EnsureExists();
        return Task.FromResult<IReadOnlyList<WaitlistEntry>>(
            _state.State.Entries.Where(e => e.Status == status).OrderBy(e => e.Position).ToList());
    }

    public Task<WaitlistEntry?> GetEntryAsync(Guid entryId)
    {
        EnsureExists();
        return Task.FromResult(_state.State.Entries.FirstOrDefault(e => e.Id == entryId));
    }

    public Task<WaitlistSettings> GetSettingsAsync()
    {
        EnsureExists();
        return Task.FromResult(_state.State.Settings);
    }

    public async Task UpdateSettingsAsync(UpdateWaitlistSettingsCommand command)
    {
        EnsureExists();

        _state.State.Settings = new WaitlistSettings
        {
            AutoPromoteToBooking = command.AutoPromoteToBooking ?? _state.State.Settings.AutoPromoteToBooking,
            SendTableReadyNotification = command.SendTableReadyNotification ?? _state.State.Settings.SendTableReadyNotification,
            NotificationResponseTimeout = command.NotificationResponseTimeout ?? _state.State.Settings.NotificationResponseTimeout,
            PrioritizeReturningCustomers = command.PrioritizeReturningCustomers ?? _state.State.Settings.PrioritizeReturningCustomers,
            ReturningCustomerBoostPositions = command.ReturningCustomerBoostPositions ?? _state.State.Settings.ReturningCustomerBoostPositions,
            MaxWaitTime = command.MaxWaitTime ?? _state.State.Settings.MaxWaitTime,
            AutoExpireEntries = command.AutoExpireEntries ?? _state.State.Settings.AutoExpireEntries,
            DefaultNotificationChannel = command.DefaultNotificationChannel ?? _state.State.Settings.DefaultNotificationChannel
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private static bool IsActive(WaitlistEntry entry) =>
        entry.Status == WaitlistStatus.Waiting || entry.Status == WaitlistStatus.Notified;

    private WaitlistEstimate CalculateEstimateAsync(int partySize, int position, List<WaitlistEntry> activeEntries)
    {
        var partiesAhead = Math.Max(0, position - 1);
        var coversAhead = activeEntries.Where(e => e.Position < position).Sum(e => e.PartySize);

        var averageTurnTime = GetAverageTurnTimeForPartySize(partySize);
        var estimatedWait = TimeSpan.FromMinutes(partiesAhead * averageTurnTime.TotalMinutes * 0.5);

        // Add buffer based on covers ahead
        estimatedWait += TimeSpan.FromMinutes(coversAhead * 2);

        var minWait = TimeSpan.FromMinutes(Math.Max(0, estimatedWait.TotalMinutes - 10));
        var maxWait = TimeSpan.FromMinutes(estimatedWait.TotalMinutes + 15);

        return new WaitlistEstimate
        {
            Position = position,
            EstimatedWait = estimatedWait,
            MinWait = minWait,
            MaxWait = maxWait,
            PartiesAhead = partiesAhead,
            CoversAhead = coversAhead,
            AverageTurnTime = averageTurnTime
        };
    }

    private async Task<TimeSpan> CalculateEstimatedWaitAsync(int partySize, int position)
    {
        var activeEntries = _state.State.Entries.Where(IsActive).OrderBy(e => e.Position).ToList();
        var estimate = CalculateEstimateAsync(partySize, position, activeEntries);
        return estimate.EstimatedWait;
    }

    private TimeSpan GetAverageTurnTimeForPartySize(int partySize)
    {
        var turnTimeData = _state.State.TurnTimeData.FirstOrDefault(t => t.PartySize == partySize);
        if (turnTimeData != null)
        {
            return TimeSpan.FromTicks(turnTimeData.AverageTurnTimeTicks);
        }

        // Default estimate based on party size
        return TimeSpan.FromMinutes(DefaultTurnTimeMinutes + (partySize - 2) * 5);
    }

    private void RecalculateAverageWait()
    {
        var seatedEntries = _state.State.Entries
            .Where(e => e.Status == WaitlistStatus.Seated && e.SeatedAt.HasValue)
            .ToList();

        if (seatedEntries.Count > 0)
        {
            var totalWaitMinutes = seatedEntries.Sum(e => (e.SeatedAt!.Value - e.CheckedInAt).TotalMinutes);
            _state.State.AverageWait = TimeSpan.FromMinutes(totalWaitMinutes / seatedEntries.Count);
        }
    }

    private async Task SendWaitlistNotificationAsync(WaitlistEntry entry, string notificationType, string? tableNumber = null)
    {
        var notificationGrain = _grainFactory.GetGrain<INotificationGrain>(
            GrainKeys.Notifications(_state.State.OrganizationId));

        if (!await notificationGrain.ExistsAsync())
            await notificationGrain.InitializeAsync(_state.State.OrganizationId);

        var guestName = entry.Guest.Name;
        var estimate = await GetEntryEstimateAsync(entry.Id);

        string message;
        switch (notificationType)
        {
            case "table_ready":
                message = tableNumber != null
                    ? $"Hi {guestName}! Your table ({tableNumber}) is ready. Please check in with the host within 10 minutes."
                    : $"Hi {guestName}! Your table is ready. Please check in with the host within 10 minutes.";
                break;
            case "position_update":
                message = $"Hi {guestName}! Update: You are now #{entry.Position} on the waitlist. Est. wait: {estimate.EstimatedWait.TotalMinutes:F0} min.";
                break;
            default:
                message = $"Hi {guestName}! You are #{entry.Position} on the waitlist.";
                break;
        }

        switch (entry.NotificationMethod)
        {
            case NotificationMethod.Sms:
                if (!string.IsNullOrEmpty(entry.Guest.Phone))
                {
                    await notificationGrain.SendSmsAsync(new SendSmsCommand(entry.Guest.Phone, message));
                }
                break;

            case NotificationMethod.Email:
                if (!string.IsNullOrEmpty(entry.Guest.Email))
                {
                    await notificationGrain.SendEmailAsync(new SendEmailCommand(
                        entry.Guest.Email, "Waitlist Update", message));
                }
                break;

            case NotificationMethod.Push:
                // Would need device token
                break;
        }
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Waitlist not initialized");
    }
}
