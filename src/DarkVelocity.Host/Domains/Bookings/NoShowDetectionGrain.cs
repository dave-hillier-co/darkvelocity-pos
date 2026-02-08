using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for no-show detection and handling using Orleans reminders.
/// </summary>
public class NoShowDetectionGrain : Grain, INoShowDetectionGrain, IRemindable
{
    private readonly IPersistentState<NoShowDetectionState> _state;
    private readonly IGrainFactory _grainFactory;

    private const string NoShowReminderPrefix = "noshow_";

    public NoShowDetectionGrain(
        [PersistentState("noshowdetection", "OrleansStorage")]
        IPersistentState<NoShowDetectionState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new NoShowDetectionState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RegisterBookingAsync(RegisterNoShowCheckCommand command)
    {
        EnsureExists();

        // Remove existing if any
        _state.State.PendingChecks.RemoveAll(c => c.BookingId == command.BookingId);

        var checkRecord = new NoShowCheckRecord
        {
            BookingId = command.BookingId,
            BookingTime = command.BookingTime,
            GuestName = command.GuestName,
            CustomerId = command.CustomerId,
            HasDeposit = command.HasDeposit,
            RegisteredAt = DateTime.UtcNow
        };

        _state.State.PendingChecks.Add(checkRecord);
        _state.State.Version++;
        await _state.WriteStateAsync();

        // Schedule reminder for booking time + grace period
        var checkTime = command.BookingTime + _state.State.Settings.GracePeriod;
        var now = DateTime.UtcNow;

        if (checkTime > now)
        {
            var delay = checkTime - now;
            var reminderName = $"{NoShowReminderPrefix}{command.BookingId}";

            await this.RegisterOrUpdateReminder(
                reminderName,
                delay,
                TimeSpan.FromDays(1)); // Fire once, we'll unregister after
        }
    }

    public async Task UnregisterBookingAsync(Guid bookingId)
    {
        EnsureExists();

        var removed = _state.State.PendingChecks.RemoveAll(c => c.BookingId == bookingId);
        if (removed > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();

            // Unregister reminder
            var reminderName = $"{NoShowReminderPrefix}{bookingId}";
            try
            {
                var reminder = await this.GetReminder(reminderName);
                if (reminder != null)
                {
                    await this.UnregisterReminder(reminder);
                }
            }
            catch
            {
                // Reminder may not exist, ignore
            }
        }
    }

    public async Task<NoShowCheckResult> CheckNoShowAsync(Guid bookingId)
    {
        EnsureExists();

        var pendingCheck = _state.State.PendingChecks.FirstOrDefault(c => c.BookingId == bookingId);
        if (pendingCheck == null)
        {
            return new NoShowCheckResult
            {
                BookingId = bookingId,
                IsNoShow = false,
                BookingTime = DateTime.MinValue,
                CheckedAt = DateTime.UtcNow,
                GracePeriod = _state.State.Settings.GracePeriod
            };
        }

        // Check booking status
        var bookingGrain = _grainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(_state.State.OrganizationId, _state.State.SiteId, bookingId));

        if (!await bookingGrain.ExistsAsync())
        {
            await UnregisterBookingAsync(bookingId);
            return new NoShowCheckResult
            {
                BookingId = bookingId,
                IsNoShow = false,
                BookingTime = pendingCheck.BookingTime,
                CheckedAt = DateTime.UtcNow,
                GracePeriod = _state.State.Settings.GracePeriod
            };
        }

        var status = await bookingGrain.GetStatusAsync();

        // If still confirmed after grace period, it's a no-show
        var isNoShow = status == BookingStatus.Confirmed &&
                       DateTime.UtcNow >= pendingCheck.BookingTime + _state.State.Settings.GracePeriod;

        var result = new NoShowCheckResult
        {
            BookingId = bookingId,
            IsNoShow = isNoShow,
            BookingTime = pendingCheck.BookingTime,
            CheckedAt = DateTime.UtcNow,
            GracePeriod = _state.State.Settings.GracePeriod,
            GuestName = pendingCheck.GuestName,
            CustomerId = pendingCheck.CustomerId
        };

        if (isNoShow && _state.State.Settings.AutoMarkNoShow)
        {
            await ProcessNoShowAsync(bookingId, pendingCheck);
        }

        return result;
    }

    public Task<IReadOnlyList<RegisterNoShowCheckCommand>> GetPendingChecksAsync()
    {
        EnsureExists();

        var checks = _state.State.PendingChecks.Select(c => new RegisterNoShowCheckCommand(
            c.BookingId,
            c.BookingTime,
            c.GuestName,
            c.CustomerId,
            c.HasDeposit)).ToList();

        return Task.FromResult<IReadOnlyList<RegisterNoShowCheckCommand>>(checks);
    }

    public Task<NoShowSettings> GetSettingsAsync()
    {
        EnsureExists();
        return Task.FromResult(_state.State.Settings);
    }

    public async Task UpdateSettingsAsync(UpdateNoShowSettingsCommand command)
    {
        EnsureExists();

        _state.State.Settings = new NoShowSettings
        {
            GracePeriod = command.GracePeriod ?? _state.State.Settings.GracePeriod,
            AutoMarkNoShow = command.AutoMarkNoShow ?? _state.State.Settings.AutoMarkNoShow,
            NotifyOnNoShow = command.NotifyOnNoShow ?? _state.State.Settings.NotifyOnNoShow,
            ForfeitDepositOnNoShow = command.ForfeitDepositOnNoShow ?? _state.State.Settings.ForfeitDepositOnNoShow,
            UpdateCustomerHistory = command.UpdateCustomerHistory ?? _state.State.Settings.UpdateCustomerHistory
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<NoShowCheckResult>> GetNoShowHistoryAsync(DateOnly? from = null, DateOnly? to = null, int limit = 100)
    {
        EnsureExists();

        var query = _state.State.History.AsEnumerable();

        if (from.HasValue)
            query = query.Where(h => DateOnly.FromDateTime(h.BookingTime) >= from.Value);

        if (to.HasValue)
            query = query.Where(h => DateOnly.FromDateTime(h.BookingTime) <= to.Value);

        var results = query
            .OrderByDescending(h => h.CheckedAt)
            .Take(limit)
            .Select(h => new NoShowCheckResult
            {
                BookingId = h.BookingId,
                IsNoShow = h.IsNoShow,
                BookingTime = h.BookingTime,
                CheckedAt = h.CheckedAt,
                GracePeriod = h.GracePeriod,
                GuestName = h.GuestName,
                CustomerId = h.CustomerId
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<NoShowCheckResult>>(results);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!reminderName.StartsWith(NoShowReminderPrefix))
            return;

        var bookingIdStr = reminderName[NoShowReminderPrefix.Length..];
        if (!Guid.TryParse(bookingIdStr, out var bookingId))
            return;

        // Unregister the reminder first (one-time check)
        try
        {
            var reminder = await this.GetReminder(reminderName);
            if (reminder != null)
            {
                await this.UnregisterReminder(reminder);
            }
        }
        catch
        {
            // Ignore reminder errors
        }

        // Perform no-show check
        await CheckNoShowAsync(bookingId);
    }

    private async Task ProcessNoShowAsync(Guid bookingId, NoShowCheckRecord checkRecord)
    {
        // Mark booking as no-show
        var bookingGrain = _grainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(_state.State.OrganizationId, _state.State.SiteId, bookingId));

        await bookingGrain.MarkNoShowAsync();

        // Forfeit deposit if applicable
        if (_state.State.Settings.ForfeitDepositOnNoShow && checkRecord.HasDeposit)
        {
            try
            {
                await bookingGrain.ForfeitDepositAsync();
            }
            catch
            {
                // May fail if deposit not paid, ignore
            }
        }

        // Record in history
        _state.State.History.Insert(0, new NoShowHistoryRecord
        {
            BookingId = bookingId,
            IsNoShow = true,
            BookingTime = checkRecord.BookingTime,
            CheckedAt = DateTime.UtcNow,
            GracePeriod = _state.State.Settings.GracePeriod,
            GuestName = checkRecord.GuestName,
            CustomerId = checkRecord.CustomerId
        });

        // Trim history
        while (_state.State.History.Count > _state.State.MaxHistoryRecords)
        {
            _state.State.History.RemoveAt(_state.State.History.Count - 1);
        }

        // Remove from pending
        _state.State.PendingChecks.RemoveAll(c => c.BookingId == bookingId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Send notification if configured
        if (_state.State.Settings.NotifyOnNoShow)
        {
            var notificationGrain = _grainFactory.GetGrain<INotificationGrain>(
                GrainKeys.Notifications(_state.State.OrganizationId));

            if (await notificationGrain.ExistsAsync())
            {
                var managerEmail = await GetManagerEmailAsync();
                await notificationGrain.SendEmailAsync(new SendEmailCommand(
                    To: managerEmail,
                    Subject: $"No-Show: {checkRecord.GuestName}",
                    Body: $"Booking {bookingId} for {checkRecord.GuestName} at {checkRecord.BookingTime:g} was marked as a no-show."));
            }
        }

        // Update customer no-show history if configured
        if (_state.State.Settings.UpdateCustomerHistory && checkRecord.CustomerId.HasValue)
        {
            try
            {
                var customerGrain = _grainFactory.GetGrain<ICustomerGrain>(
                    GrainKeys.Customer(_state.State.OrganizationId, checkRecord.CustomerId.Value));
                if (await customerGrain.ExistsAsync())
                    await customerGrain.RecordNoShowAsync(checkRecord.BookingTime, bookingId);
            }
            catch
            {
                // Customer grain may not exist, ignore
            }
        }
    }

    private async Task<string> GetManagerEmailAsync()
    {
        // Try to get an email target from notification channel config
        try
        {
            var notificationGrain = _grainFactory.GetGrain<INotificationGrain>(
                GrainKeys.Notifications(_state.State.OrganizationId));
            if (await notificationGrain.ExistsAsync())
            {
                var channels = await notificationGrain.GetChannelsAsync();
                var emailChannel = channels.FirstOrDefault(c =>
                    c.Type == NotificationType.Email && c.IsEnabled);
                if (emailChannel != null && !string.IsNullOrEmpty(emailChannel.Target))
                    return emailChannel.Target;
            }
        }
        catch
        {
            // Fall back to default
        }
        return "manager@restaurant.com";
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("No-show detection not initialized");
    }
}
