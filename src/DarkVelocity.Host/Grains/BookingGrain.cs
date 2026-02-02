using DarkVelocity.Host;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class BookingGrain : Grain, IBookingGrain
{
    private readonly IPersistentState<BookingState> _state;
    private IAsyncStream<IStreamEvent>? _bookingStream;

    public BookingGrain(
        [PersistentState("booking", "OrleansStorage")]
        IPersistentState<BookingState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetBookingStream()
    {
        if (_bookingStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.BookingStreamNamespace, _state.State.OrganizationId.ToString());
            _bookingStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _bookingStream!;
    }

    public async Task<BookingRequestedResult> RequestAsync(RequestBookingCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Booking already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, bookingId) = GrainKeys.ParseSiteEntity(key);

        var confirmationCode = GenerateConfirmationCode();

        _state.State = new BookingState
        {
            Id = bookingId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            ConfirmationCode = confirmationCode,
            Status = BookingStatus.Requested,
            RequestedTime = command.RequestedTime,
            Duration = command.Duration ?? TimeSpan.FromMinutes(90),
            PartySize = command.PartySize,
            Guest = command.Guest,
            CustomerId = command.CustomerId,
            SpecialRequests = command.SpecialRequests,
            Occasion = command.Occasion,
            Source = command.Source,
            ExternalRef = command.ExternalRef,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new BookingRequestedResult(bookingId, confirmationCode, _state.State.CreatedAt);
    }

    public Task<BookingState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task<BookingConfirmedResult> ConfirmAsync(DateTime? confirmedTime = null)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested, BookingStatus.PendingDeposit);

        if (_state.State.Deposit != null && _state.State.Deposit.Status == DepositStatus.Required)
            throw new InvalidOperationException("Deposit required but not paid");

        _state.State.ConfirmedTime = confirmedTime ?? _state.State.RequestedTime;
        _state.State.ConfirmedAt = DateTime.UtcNow;
        _state.State.Status = BookingStatus.Confirmed;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new BookingConfirmedResult(_state.State.ConfirmedTime.Value, _state.State.ConfirmationCode);
    }

    public async Task ModifyAsync(ModifyBookingCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested, BookingStatus.Confirmed);

        if (command.NewTime != null)
        {
            _state.State.RequestedTime = command.NewTime.Value;
            if (_state.State.ConfirmedTime != null)
                _state.State.ConfirmedTime = command.NewTime.Value;
        }

        if (command.NewPartySize != null)
            _state.State.PartySize = command.NewPartySize.Value;

        if (command.NewDuration != null)
            _state.State.Duration = command.NewDuration.Value;

        if (command.SpecialRequests != null)
            _state.State.SpecialRequests = command.SpecialRequests;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(CancelBookingCommand command)
    {
        EnsureExists();

        if (_state.State.Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("Booking already cancelled");

        if (_state.State.Status == BookingStatus.Completed)
            throw new InvalidOperationException("Cannot cancel completed booking");

        _state.State.Status = BookingStatus.Cancelled;
        _state.State.CancelledAt = DateTime.UtcNow;
        _state.State.CancellationReason = command.Reason;
        _state.State.CancelledBy = command.CancelledBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AssignTableAsync(AssignTableCommand command)
    {
        EnsureExists();

        var assignment = new TableAssignment
        {
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            Capacity = command.Capacity,
            AssignedAt = DateTime.UtcNow
        };

        _state.State.TableAssignments.Add(assignment);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ClearTableAssignmentAsync()
    {
        EnsureExists();

        _state.State.TableAssignments.Clear();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<DateTime> RecordArrivalAsync(RecordArrivalCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Confirmed);

        _state.State.Status = BookingStatus.Arrived;
        _state.State.ArrivedAt = DateTime.UtcNow;
        _state.State.CheckedInBy = command.CheckedInBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return _state.State.ArrivedAt.Value;
    }

    public async Task SeatGuestAsync(SeatGuestCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Arrived, BookingStatus.Confirmed);

        if (!_state.State.TableAssignments.Any(t => t.TableId == command.TableId))
        {
            _state.State.TableAssignments.Add(new TableAssignment
            {
                TableId = command.TableId,
                TableNumber = command.TableNumber,
                AssignedAt = DateTime.UtcNow
            });
        }

        _state.State.Status = BookingStatus.Seated;
        _state.State.SeatedAt = DateTime.UtcNow;
        _state.State.SeatedBy = command.SeatedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordDepartureAsync(RecordDepartureCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Seated);

        _state.State.Status = BookingStatus.Completed;
        _state.State.DepartedAt = DateTime.UtcNow;
        if (command.OrderId != null)
            _state.State.LinkedOrderId = command.OrderId;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task MarkNoShowAsync(Guid? markedBy = null)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Confirmed);

        _state.State.Status = BookingStatus.NoShow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddSpecialRequestAsync(string request)
    {
        EnsureExists();

        if (string.IsNullOrEmpty(_state.State.SpecialRequests))
            _state.State.SpecialRequests = request;
        else
            _state.State.SpecialRequests += $"; {request}";

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddNoteAsync(string note, Guid addedBy)
    {
        EnsureExists();

        if (string.IsNullOrEmpty(_state.State.Notes))
            _state.State.Notes = note;
        else
            _state.State.Notes += $"\n{note}";

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddTagAsync(string tag)
    {
        EnsureExists();

        if (_state.State.Tags.TryAddTag(tag))
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RequireDepositAsync(RequireDepositCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested);

        _state.State.Deposit = new DepositInfo
        {
            Amount = command.Amount,
            Status = DepositStatus.Required,
            RequiredAt = command.RequiredBy
        };
        _state.State.Status = BookingStatus.PendingDeposit;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish deposit required event
        if (GetBookingStream() != null)
        {
            await GetBookingStream().OnNextAsync(new BookingDepositRequiredEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.CustomerId,
                command.Amount,
                command.RequiredBy)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task RecordDepositPaymentAsync(RecordDepositPaymentCommand command)
    {
        EnsureExists();

        if (_state.State.Deposit == null)
            throw new InvalidOperationException("No deposit required");

        if (_state.State.Deposit.Status != DepositStatus.Required)
            throw new InvalidOperationException($"Deposit is not required: {_state.State.Deposit.Status}");

        var depositAmount = _state.State.Deposit.Amount;

        _state.State.Deposit = _state.State.Deposit with
        {
            Status = DepositStatus.Paid,
            PaidAt = DateTime.UtcNow,
            PaymentMethod = command.Method,
            PaymentReference = command.PaymentReference
        };
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish deposit paid event - triggers accounting journal entry
        if (GetBookingStream() != null)
        {
            await GetBookingStream().OnNextAsync(new BookingDepositPaidEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.CustomerId,
                depositAmount,
                command.Method.ToString(),
                command.PaymentReference,
                depositAmount)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task WaiveDepositAsync(Guid waivedBy)
    {
        EnsureExists();

        if (_state.State.Deposit == null)
            throw new InvalidOperationException("No deposit required");

        _state.State.Deposit = _state.State.Deposit with { Status = DepositStatus.Waived };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ForfeitDepositAsync()
    {
        EnsureExists();

        if (_state.State.Deposit == null || _state.State.Deposit.Status != DepositStatus.Paid)
            throw new InvalidOperationException("No paid deposit to forfeit");

        var depositAmount = _state.State.Deposit.Amount;

        _state.State.Deposit = _state.State.Deposit with
        {
            Status = DepositStatus.Forfeited,
            ForfeitedAt = DateTime.UtcNow
        };
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish deposit forfeited event - converts liability to income
        if (GetBookingStream() != null)
        {
            await GetBookingStream().OnNextAsync(new BookingDepositForfeitedEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.CustomerId,
                depositAmount,
                "No-show or late cancellation",
                Guid.Empty)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task RefundDepositAsync(string reason, Guid refundedBy)
    {
        EnsureExists();

        if (_state.State.Deposit == null || _state.State.Deposit.Status != DepositStatus.Paid)
            throw new InvalidOperationException("No paid deposit to refund");

        var depositAmount = _state.State.Deposit.Amount;

        _state.State.Deposit = _state.State.Deposit with
        {
            Status = DepositStatus.Refunded,
            RefundedAt = DateTime.UtcNow,
            RefundReason = reason
        };
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish deposit refunded event - reverses the liability
        if (GetBookingStream() != null)
        {
            await GetBookingStream().OnNextAsync(new BookingDepositRefundedEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.CustomerId,
                depositAmount,
                reason,
                refundedBy)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task LinkToOrderAsync(Guid orderId)
    {
        EnsureExists();

        _state.State.LinkedOrderId = orderId;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<BookingStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);
    public Task<bool> RequiresDepositAsync() =>
        Task.FromResult(_state.State.Deposit != null && _state.State.Deposit.Status == DepositStatus.Required);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Booking does not exist");
    }

    private void EnsureStatus(params BookingStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(_state.State.Status))
            throw new InvalidOperationException($"Invalid status. Expected one of [{string.Join(", ", allowedStatuses)}], got {_state.State.Status}");
    }

    private static string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class WaitlistGrain : Grain, IWaitlistGrain
{
    private readonly IPersistentState<WaitlistState> _state;

    public WaitlistGrain(
        [PersistentState("waitlist", "OrleansStorage")]
        IPersistentState<WaitlistState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date)
    {
        if (_state.State.SiteId != Guid.Empty)
            throw new InvalidOperationException("Waitlist already initialized");

        _state.State.OrganizationId = organizationId;
        _state.State.SiteId = siteId;
        _state.State.Date = date;
        _state.State.CurrentPosition = 0;
        _state.State.Version = 1;

        await _state.WriteStateAsync();
    }

    public Task<WaitlistState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task<WaitlistEntryResult> AddEntryAsync(AddToWaitlistCommand command)
    {
        EnsureExists();

        var entryId = Guid.NewGuid();
        var position = ++_state.State.CurrentPosition;

        var entry = new WaitlistEntry
        {
            Id = entryId,
            Position = position,
            Guest = command.Guest,
            PartySize = command.PartySize,
            CheckedInAt = DateTime.UtcNow,
            QuotedWait = command.QuotedWait,
            Status = WaitlistStatus.Waiting,
            TablePreferences = command.TablePreferences,
            NotificationMethod = command.NotificationMethod
        };

        _state.State.Entries.Add(entry);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new WaitlistEntryResult(entryId, position, command.QuotedWait);
    }

    public async Task UpdatePositionAsync(Guid entryId, int newPosition)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        _state.State.Entries[index] = _state.State.Entries[index] with { Position = newPosition };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task NotifyEntryAsync(Guid entryId)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        if (entry.Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException($"Entry cannot be notified: {entry.Status}");

        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Notified,
            NotifiedAt = DateTime.UtcNow
        };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SeatEntryAsync(Guid entryId, Guid tableId)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
            throw new InvalidOperationException($"Entry cannot be seated: {entry.Status}");

        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Seated,
            SeatedAt = DateTime.UtcNow
        };

        // Update average wait time
        var seatedEntries = _state.State.Entries.Where(e => e.Status == WaitlistStatus.Seated && e.SeatedAt != null).ToList();
        if (seatedEntries.Count > 0)
        {
            var avgWait = TimeSpan.FromTicks((long)seatedEntries.Average(e => (e.SeatedAt!.Value - e.CheckedInAt).Ticks));
            _state.State.AverageWait = avgWait;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveEntryAsync(Guid entryId, string reason)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        _state.State.Entries[index] = _state.State.Entries[index] with
        {
            Status = WaitlistStatus.Left,
            LeftAt = DateTime.UtcNow
        };
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<Guid?> ConvertToBookingAsync(Guid entryId, DateTime bookingTime)
    {
        EnsureExists();

        var index = _state.State.Entries.FindIndex(e => e.Id == entryId);
        if (index < 0)
            throw new InvalidOperationException("Entry not found");

        var entry = _state.State.Entries[index];
        var bookingId = Guid.NewGuid();

        _state.State.Entries[index] = entry with
        {
            Status = WaitlistStatus.Seated,
            SeatedAt = DateTime.UtcNow,
            ConvertedToBookingId = bookingId
        };
        _state.State.Version++;

        await _state.WriteStateAsync();

        return bookingId;
    }

    public Task<int> GetWaitingCountAsync()
    {
        return Task.FromResult(_state.State.Entries.Count(e => e.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified));
    }

    public Task<TimeSpan> GetEstimatedWaitAsync(int partySize)
    {
        if (_state.State.AverageWait == TimeSpan.Zero)
            return Task.FromResult(TimeSpan.FromMinutes(15)); // Default estimate

        var waitingAhead = _state.State.Entries.Count(e => e.Status == WaitlistStatus.Waiting);
        return Task.FromResult(TimeSpan.FromTicks(_state.State.AverageWait.Ticks * (waitingAhead + 1)));
    }

    public Task<IReadOnlyList<WaitlistEntry>> GetEntriesAsync()
        => Task.FromResult<IReadOnlyList<WaitlistEntry>>(_state.State.Entries.Where(e => e.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified).OrderBy(e => e.Position).ToList());

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Waitlist not initialized");
    }
}
