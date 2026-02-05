using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for scheduling booking notifications using Orleans reminders.
/// </summary>
public class BookingNotificationSchedulerGrain : Grain, IBookingNotificationSchedulerGrain, IRemindable
{
    private readonly IPersistentState<BookingNotificationSchedulerState> _state;
    private readonly IGrainFactory _grainFactory;

    private const string NotificationReminderPrefix = "bookingnotif_";

    public BookingNotificationSchedulerGrain(
        [PersistentState("bookingnotifications", "OrleansStorage")]
        IPersistentState<BookingNotificationSchedulerState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new BookingNotificationSchedulerState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task ScheduleBookingNotificationsAsync(
        Guid bookingId,
        string guestName,
        string recipient,
        string channel,
        DateTime bookingTime,
        string confirmationCode,
        int partySize,
        string? siteName = null)
    {
        EnsureExists();

        var settings = _state.State.Settings;

        // Schedule confirmation immediately
        if (settings.SendConfirmation)
        {
            await SendImmediateNotificationAsync(new ScheduleBookingNotificationCommand(
                bookingId,
                BookingNotificationType.Confirmation,
                DateTime.UtcNow,
                recipient,
                channel,
                guestName,
                bookingTime,
                confirmationCode,
                partySize,
                siteName));
        }

        // Schedule 24h reminder
        if (settings.Send24hReminder)
        {
            var reminderTime = bookingTime.AddHours(-24);
            if (reminderTime > DateTime.UtcNow)
            {
                await ScheduleNotificationAsync(new ScheduleBookingNotificationCommand(
                    bookingId,
                    BookingNotificationType.Reminder24h,
                    reminderTime,
                    recipient,
                    channel,
                    guestName,
                    bookingTime,
                    confirmationCode,
                    partySize,
                    siteName));
            }
        }

        // Schedule 2h reminder
        if (settings.Send2hReminder)
        {
            var reminderTime = bookingTime.AddHours(-2);
            if (reminderTime > DateTime.UtcNow)
            {
                await ScheduleNotificationAsync(new ScheduleBookingNotificationCommand(
                    bookingId,
                    BookingNotificationType.Reminder2h,
                    reminderTime,
                    recipient,
                    channel,
                    guestName,
                    bookingTime,
                    confirmationCode,
                    partySize,
                    siteName));
            }
        }
    }

    public async Task<ScheduledBookingNotification> ScheduleNotificationAsync(ScheduleBookingNotificationCommand command)
    {
        EnsureExists();

        var notificationId = Guid.NewGuid();
        var record = new ScheduledNotificationRecord
        {
            NotificationId = notificationId,
            BookingId = command.BookingId,
            Type = command.Type,
            ScheduledFor = command.ScheduledFor,
            IsSent = false,
            Recipient = command.Recipient,
            Channel = command.Channel,
            GuestName = command.GuestName,
            BookingTime = command.BookingTime,
            ConfirmationCode = command.ConfirmationCode,
            PartySize = command.PartySize,
            SiteName = command.SiteName
        };

        _state.State.Notifications.Add(record);
        _state.State.Version++;
        await _state.WriteStateAsync();

        // Schedule reminder
        var now = DateTime.UtcNow;
        if (command.ScheduledFor > now)
        {
            var delay = command.ScheduledFor - now;
            var reminderName = $"{NotificationReminderPrefix}{notificationId}";

            await this.RegisterOrUpdateReminder(
                reminderName,
                delay,
                TimeSpan.FromDays(1)); // Fire once
        }
        else
        {
            // Send immediately if scheduled time is in the past
            await SendNotificationAsync(notificationId);
        }

        return ToScheduledNotification(record);
    }

    public async Task CancelBookingNotificationsAsync(Guid bookingId)
    {
        EnsureExists();

        var notifications = _state.State.Notifications
            .Where(n => n.BookingId == bookingId && !n.IsSent)
            .ToList();

        foreach (var notification in notifications)
        {
            // Unregister reminder
            await TryUnregisterReminder(notification.NotificationId);
        }

        _state.State.Notifications.RemoveAll(n => n.BookingId == bookingId && !n.IsSent);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CancelNotificationAsync(Guid notificationId)
    {
        EnsureExists();

        var notification = _state.State.Notifications.FirstOrDefault(n => n.NotificationId == notificationId);
        if (notification != null && !notification.IsSent)
        {
            await TryUnregisterReminder(notificationId);
            _state.State.Notifications.Remove(notification);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ScheduledBookingNotification>> GetPendingNotificationsAsync(Guid bookingId)
    {
        EnsureExists();

        var notifications = _state.State.Notifications
            .Where(n => n.BookingId == bookingId && !n.IsSent)
            .Select(ToScheduledNotification)
            .ToList();

        return Task.FromResult<IReadOnlyList<ScheduledBookingNotification>>(notifications);
    }

    public Task<IReadOnlyList<ScheduledBookingNotification>> GetAllPendingNotificationsAsync()
    {
        EnsureExists();

        var notifications = _state.State.Notifications
            .Where(n => !n.IsSent)
            .OrderBy(n => n.ScheduledFor)
            .Select(ToScheduledNotification)
            .ToList();

        return Task.FromResult<IReadOnlyList<ScheduledBookingNotification>>(notifications);
    }

    public Task<IReadOnlyList<ScheduledBookingNotification>> GetNotificationHistoryAsync(Guid bookingId)
    {
        EnsureExists();

        var notifications = _state.State.Notifications
            .Where(n => n.BookingId == bookingId)
            .OrderByDescending(n => n.ScheduledFor)
            .Select(ToScheduledNotification)
            .ToList();

        return Task.FromResult<IReadOnlyList<ScheduledBookingNotification>>(notifications);
    }

    public async Task SendImmediateNotificationAsync(ScheduleBookingNotificationCommand command)
    {
        EnsureExists();

        var notificationId = Guid.NewGuid();
        var record = new ScheduledNotificationRecord
        {
            NotificationId = notificationId,
            BookingId = command.BookingId,
            Type = command.Type,
            ScheduledFor = DateTime.UtcNow,
            IsSent = false,
            Recipient = command.Recipient,
            Channel = command.Channel,
            GuestName = command.GuestName,
            BookingTime = command.BookingTime,
            ConfirmationCode = command.ConfirmationCode,
            PartySize = command.PartySize,
            SiteName = command.SiteName
        };

        _state.State.Notifications.Add(record);
        await _state.WriteStateAsync();

        await SendNotificationAsync(notificationId);
    }

    public Task<BookingNotificationSettings> GetSettingsAsync()
    {
        EnsureExists();
        return Task.FromResult(_state.State.Settings);
    }

    public async Task UpdateSettingsAsync(UpdateBookingNotificationSettingsCommand command)
    {
        EnsureExists();

        _state.State.Settings = new BookingNotificationSettings
        {
            SendConfirmation = command.SendConfirmation ?? _state.State.Settings.SendConfirmation,
            Send24hReminder = command.Send24hReminder ?? _state.State.Settings.Send24hReminder,
            Send2hReminder = command.Send2hReminder ?? _state.State.Settings.Send2hReminder,
            SendFollowUp = command.SendFollowUp ?? _state.State.Settings.SendFollowUp,
            FollowUpDelay = command.FollowUpDelay ?? _state.State.Settings.FollowUpDelay,
            DefaultChannel = command.DefaultChannel ?? _state.State.Settings.DefaultChannel
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleFollowUpAsync(Guid bookingId, string recipient, string channel, string? guestName = null)
    {
        EnsureExists();

        if (!_state.State.Settings.SendFollowUp)
            return;

        var followUpTime = DateTime.UtcNow + _state.State.Settings.FollowUpDelay;

        await ScheduleNotificationAsync(new ScheduleBookingNotificationCommand(
            bookingId,
            BookingNotificationType.FollowUp,
            followUpTime,
            recipient,
            channel,
            guestName));
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!reminderName.StartsWith(NotificationReminderPrefix))
            return;

        var notificationIdStr = reminderName[NotificationReminderPrefix.Length..];
        if (!Guid.TryParse(notificationIdStr, out var notificationId))
            return;

        // Unregister the reminder first
        await TryUnregisterReminder(notificationId);

        // Send the notification
        await SendNotificationAsync(notificationId);
    }

    private async Task SendNotificationAsync(Guid notificationId)
    {
        var recordIndex = _state.State.Notifications.FindIndex(n => n.NotificationId == notificationId);
        if (recordIndex < 0 || _state.State.Notifications[recordIndex].IsSent)
            return;

        var record = _state.State.Notifications[recordIndex];

        // Check if booking still exists and is not cancelled
        var bookingGrain = _grainFactory.GetGrain<IBookingGrain>(
            GrainKeys.Booking(_state.State.OrganizationId, _state.State.SiteId, record.BookingId));

        if (await bookingGrain.ExistsAsync())
        {
            var status = await bookingGrain.GetStatusAsync();
            if (status == BookingStatus.Cancelled)
            {
                // Skip notification for cancelled bookings
                record.IsSent = true;
                record.SentAt = DateTime.UtcNow;
                record.ErrorMessage = "Booking was cancelled";
                await _state.WriteStateAsync();
                return;
            }
        }

        var subject = GetNotificationSubject(record);
        var body = GetNotificationBody(record);

        try
        {
            var notificationGrain = _grainFactory.GetGrain<INotificationGrain>(
                GrainKeys.Notifications(_state.State.OrganizationId));

            if (!await notificationGrain.ExistsAsync())
                await notificationGrain.InitializeAsync(_state.State.OrganizationId);

            switch (record.Channel.ToLowerInvariant())
            {
                case "email":
                    await notificationGrain.SendEmailAsync(new SendEmailCommand(
                        record.Recipient, subject, body));
                    break;

                case "sms":
                    await notificationGrain.SendSmsAsync(new SendSmsCommand(
                        record.Recipient, body));
                    break;

                case "push":
                    await notificationGrain.SendPushAsync(new SendPushCommand(
                        record.Recipient, subject, body));
                    break;

                default:
                    await notificationGrain.SendEmailAsync(new SendEmailCommand(
                        record.Recipient, subject, body));
                    break;
            }

            record.IsSent = true;
            record.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            record.ErrorMessage = ex.Message;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private static string GetNotificationSubject(ScheduledNotificationRecord record)
    {
        return record.Type switch
        {
            BookingNotificationType.Confirmation =>
                $"Booking Confirmed - {record.ConfirmationCode}",
            BookingNotificationType.Reminder24h =>
                $"Reminder: Your reservation tomorrow",
            BookingNotificationType.Reminder2h =>
                $"Reminder: Your reservation in 2 hours",
            BookingNotificationType.FollowUp =>
                $"Thank you for dining with us!",
            BookingNotificationType.TableReady =>
                $"Your table is ready!",
            BookingNotificationType.DepositReminder =>
                $"Deposit reminder for your booking",
            _ => "Booking Notification"
        };
    }

    private static string GetNotificationBody(ScheduledNotificationRecord record)
    {
        var guestName = record.GuestName ?? "Guest";
        var siteName = record.SiteName ?? "our restaurant";
        var bookingTimeStr = record.BookingTime?.ToString("g") ?? "scheduled time";

        return record.Type switch
        {
            BookingNotificationType.Confirmation =>
                $"Dear {guestName},\n\nYour reservation at {siteName} has been confirmed.\n\n" +
                $"Date & Time: {bookingTimeStr}\nParty Size: {record.PartySize}\n" +
                $"Confirmation Code: {record.ConfirmationCode}\n\n" +
                "We look forward to seeing you!",

            BookingNotificationType.Reminder24h =>
                $"Dear {guestName},\n\nThis is a friendly reminder about your reservation tomorrow at {siteName}.\n\n" +
                $"Date & Time: {bookingTimeStr}\nParty Size: {record.PartySize}\n" +
                $"Confirmation Code: {record.ConfirmationCode}",

            BookingNotificationType.Reminder2h =>
                $"Dear {guestName},\n\nYour table at {siteName} will be ready in about 2 hours.\n\n" +
                $"Time: {bookingTimeStr}\nConfirmation: {record.ConfirmationCode}",

            BookingNotificationType.FollowUp =>
                $"Dear {guestName},\n\nThank you for dining with us at {siteName}!\n\n" +
                "We hope you had a wonderful experience. We'd love to hear your feedback!",

            BookingNotificationType.TableReady =>
                $"Dear {guestName},\n\nGreat news! Your table is ready at {siteName}.\n\n" +
                "Please check in with the host.",

            BookingNotificationType.DepositReminder =>
                $"Dear {guestName},\n\nThis is a reminder that a deposit is required to secure your booking.\n\n" +
                $"Confirmation Code: {record.ConfirmationCode}",

            _ => $"Booking notification for {guestName}"
        };
    }

    private static ScheduledBookingNotification ToScheduledNotification(ScheduledNotificationRecord record) => new()
    {
        NotificationId = record.NotificationId,
        BookingId = record.BookingId,
        Type = record.Type,
        ScheduledFor = record.ScheduledFor,
        IsSent = record.IsSent,
        SentAt = record.SentAt,
        Recipient = record.Recipient,
        Channel = record.Channel,
        ErrorMessage = record.ErrorMessage
    };

    private async Task TryUnregisterReminder(Guid notificationId)
    {
        try
        {
            var reminderName = $"{NotificationReminderPrefix}{notificationId}";
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

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking notification scheduler not initialized");
    }
}
