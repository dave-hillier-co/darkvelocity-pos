using DarkVelocity.Host;
using DarkVelocity.Host.Events.JournaledEvents;
using DarkVelocity.Host.Extensions;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class BookingGrain : JournaledGrain<BookingState, IBookingJournaledEvent>, IBookingGrain
{
    private Lazy<IAsyncStream<IStreamEvent>>? _bookingStream;

    protected override void TransitionState(BookingState state, IBookingJournaledEvent @event)
    {
        switch (@event)
        {
            case BookingCreatedJournaledEvent e:
                state.Id = e.BookingId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.Status = BookingStatus.Requested;
                state.RequestedTime = e.BookingDateTime;
                state.PartySize = e.PartySize;
                state.Guest = new GuestInfo
                {
                    Name = e.CustomerName,
                    Email = e.CustomerEmail,
                    Phone = e.CustomerPhone
                };
                state.CustomerId = e.CustomerId;
                state.SpecialRequests = e.SpecialRequests;
                state.Source = !string.IsNullOrEmpty(e.Source) && Enum.TryParse<BookingSource>(e.Source, out var source) ? source : BookingSource.Direct;
                state.CreatedAt = e.OccurredAt;
                break;

            case BookingConfirmedJournaledEvent e:
                state.Status = BookingStatus.Confirmed;
                state.ConfirmedTime = state.RequestedTime;
                state.ConfirmedAt = e.OccurredAt;
                break;

            case BookingModifiedJournaledEvent e:
                if (e.NewDateTime.HasValue)
                {
                    state.RequestedTime = e.NewDateTime.Value;
                    if (state.ConfirmedTime.HasValue)
                        state.ConfirmedTime = e.NewDateTime.Value;
                }
                if (e.NewPartySize.HasValue)
                    state.PartySize = e.NewPartySize.Value;
                if (e.NewSpecialRequests != null)
                    state.SpecialRequests = e.NewSpecialRequests;
                break;

            case BookingCancelledJournaledEvent e:
                state.Status = BookingStatus.Cancelled;
                state.CancelledAt = e.OccurredAt;
                state.CancellationReason = e.Reason;
                state.CancelledBy = e.CancelledBy;
                break;

            case BookingDepositRequiredJournaledEvent e:
                state.Deposit = new DepositInfo
                {
                    Amount = e.AmountRequired,
                    Status = DepositStatus.Required,
                    RequiredAt = e.DueBy
                };
                state.Status = BookingStatus.PendingDeposit;
                break;

            case BookingDepositPaidJournaledEvent e:
                if (state.Deposit != null)
                {
                    state.Deposit = state.Deposit with
                    {
                        Status = DepositStatus.Paid,
                        PaidAt = e.OccurredAt,
                        PaymentMethod = Enum.TryParse<PaymentMethod>(e.PaymentMethod, out var pm) ? pm : PaymentMethod.Cash,
                        PaymentReference = e.PaymentReference
                    };
                }
                break;

            case BookingDepositRefundedJournaledEvent e:
                if (state.Deposit != null)
                {
                    state.Deposit = state.Deposit with
                    {
                        Status = DepositStatus.Refunded,
                        RefundedAt = e.OccurredAt,
                        RefundReason = e.Reason
                    };
                }
                break;

            case BookingDepositForfeitedJournaledEvent e:
                if (state.Deposit != null)
                {
                    state.Deposit = state.Deposit with
                    {
                        Status = DepositStatus.Forfeited,
                        ForfeitedAt = e.OccurredAt
                    };
                }
                break;

            case BookingGuestArrivedJournaledEvent e:
                state.Status = BookingStatus.Arrived;
                state.ArrivedAt = e.OccurredAt;
                if (e.ActualPartySize.HasValue)
                    state.PartySize = e.ActualPartySize.Value;
                break;

            case BookingSeatedJournaledEvent e:
                state.Status = BookingStatus.Seated;
                state.SeatedAt = e.OccurredAt;
                state.SeatedBy = e.SeatedBy;
                if (e.TableId.HasValue)
                {
                    if (!state.TableAssignments.Any(t => t.TableId == e.TableId.Value))
                    {
                        state.TableAssignments.Add(new TableAssignment
                        {
                            TableId = e.TableId.Value,
                            TableNumber = e.TableNumber ?? "",
                            AssignedAt = e.OccurredAt
                        });
                    }
                }
                break;

            case BookingTableAssignedJournaledEvent e:
                state.TableAssignments.Add(new TableAssignment
                {
                    TableId = e.TableId,
                    TableNumber = e.TableNumber,
                    AssignedAt = e.OccurredAt
                });
                break;

            case BookingLinkedToOrderJournaledEvent e:
                state.LinkedOrderId = e.OrderId;
                break;

            case BookingDepartedJournaledEvent e:
                state.Status = BookingStatus.Completed;
                state.DepartedAt = e.OccurredAt;
                break;

            case BookingNoShowJournaledEvent e:
                state.Status = BookingStatus.NoShow;
                break;
        }
    }

    private IAsyncStream<IStreamEvent>? GetBookingStream()
    {
        if (_bookingStream == null && State.OrganizationId != Guid.Empty)
        {
            var orgId = State.OrganizationId;
            _bookingStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
            {
                var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
                var streamId = StreamId.Create(StreamConstants.BookingStreamNamespace, orgId.ToString());
                return streamProvider.GetStream<IStreamEvent>(streamId);
            });
        }
        return _bookingStream?.Value;
    }

    public async Task<BookingRequestedResult> RequestAsync(RequestBookingCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Booking already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, bookingId) = GrainKeys.ParseSiteEntity(key);

        var confirmationCode = GenerateConfirmationCode();

        RaiseEvent(new BookingCreatedJournaledEvent
        {
            BookingId = bookingId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            CustomerName = command.Guest.Name,
            CustomerEmail = command.Guest.Email,
            CustomerPhone = command.Guest.Phone,
            CustomerId = command.CustomerId,
            BookingDateTime = command.RequestedTime,
            PartySize = command.PartySize,
            SpecialRequests = command.SpecialRequests,
            Source = command.Source.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Set additional properties not in the event
        State.ConfirmationCode = confirmationCode;
        State.Duration = command.Duration ?? TimeSpan.FromMinutes(90);
        State.Occasion = command.Occasion;
        State.ExternalRef = command.ExternalRef;

        return new BookingRequestedResult(bookingId, confirmationCode, State.CreatedAt);
    }

    public Task<BookingState> GetStateAsync() => Task.FromResult(State);

    public async Task<BookingConfirmedResult> ConfirmAsync(DateTime? confirmedTime = null)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested, BookingStatus.PendingDeposit);

        if (State.Deposit != null && State.Deposit.Status == DepositStatus.Required)
            throw new InvalidOperationException("Deposit required but not paid");

        RaiseEvent(new BookingConfirmedJournaledEvent
        {
            BookingId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Set confirmed time if different from requested
        if (confirmedTime.HasValue)
            State.ConfirmedTime = confirmedTime.Value;

        return new BookingConfirmedResult(State.ConfirmedTime!.Value, State.ConfirmationCode);
    }

    public async Task ModifyAsync(ModifyBookingCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested, BookingStatus.Confirmed);

        RaiseEvent(new BookingModifiedJournaledEvent
        {
            BookingId = State.Id,
            NewDateTime = command.NewTime,
            NewPartySize = command.NewPartySize,
            NewSpecialRequests = command.SpecialRequests,
            ModifiedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Set duration if specified
        if (command.NewDuration != null)
            State.Duration = command.NewDuration.Value;
    }

    public async Task CancelAsync(CancelBookingCommand command)
    {
        EnsureExists();

        if (State.Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("Booking already cancelled");

        if (State.Status == BookingStatus.Completed)
            throw new InvalidOperationException("Cannot cancel completed booking");

        RaiseEvent(new BookingCancelledJournaledEvent
        {
            BookingId = State.Id,
            Reason = command.Reason,
            CancelledBy = command.CancelledBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AssignTableAsync(AssignTableCommand command)
    {
        EnsureExists();

        RaiseEvent(new BookingTableAssignedJournaledEvent
        {
            BookingId = State.Id,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Set capacity (not in event)
        var lastAssignment = State.TableAssignments.Last();
        var index = State.TableAssignments.Count - 1;
        State.TableAssignments[index] = lastAssignment with { Capacity = command.Capacity };
    }

    public async Task ClearTableAssignmentAsync()
    {
        EnsureExists();

        State.TableAssignments.Clear();
    }

    public async Task<DateTime> RecordArrivalAsync(RecordArrivalCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Confirmed);

        RaiseEvent(new BookingGuestArrivedJournaledEvent
        {
            BookingId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        State.CheckedInBy = command.CheckedInBy;

        return State.ArrivedAt!.Value;
    }

    public async Task SeatGuestAsync(SeatGuestCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Arrived, BookingStatus.Confirmed);

        RaiseEvent(new BookingSeatedJournaledEvent
        {
            BookingId = State.Id,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            ActualPartySize = State.PartySize,
            SeatedBy = command.SeatedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RecordDepartureAsync(RecordDepartureCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Seated);

        if (command.OrderId != null)
        {
            RaiseEvent(new BookingLinkedToOrderJournaledEvent
            {
                BookingId = State.Id,
                OrderId = command.OrderId.Value,
                OccurredAt = DateTime.UtcNow
            });
        }

        RaiseEvent(new BookingDepartedJournaledEvent
        {
            BookingId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task MarkNoShowAsync(Guid? markedBy = null)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Confirmed);

        RaiseEvent(new BookingNoShowJournaledEvent
        {
            BookingId = State.Id,
            MarkedBy = markedBy ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AddSpecialRequestAsync(string request)
    {
        EnsureExists();

        var newRequests = string.IsNullOrEmpty(State.SpecialRequests)
            ? request
            : $"{State.SpecialRequests}; {request}";

        RaiseEvent(new BookingModifiedJournaledEvent
        {
            BookingId = State.Id,
            NewSpecialRequests = newRequests,
            ModifiedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AddNoteAsync(string note, Guid addedBy)
    {
        EnsureExists();

        if (string.IsNullOrEmpty(State.Notes))
            State.Notes = note;
        else
            State.Notes += $"\n{note}";
    }

    public async Task AddTagAsync(string tag)
    {
        EnsureExists();

        State.Tags.TryAddTag(tag);
    }

    public async Task RequireDepositAsync(RequireDepositCommand command)
    {
        EnsureExists();
        EnsureStatus(BookingStatus.Requested);

        RaiseEvent(new BookingDepositRequiredJournaledEvent
        {
            BookingId = State.Id,
            AmountRequired = command.Amount,
            DueBy = command.RequiredBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        if (GetBookingStream() != null)
        {
            await GetBookingStream()!.OnNextAsync(new BookingDepositRequiredEvent(
                State.Id,
                State.SiteId,
                State.CustomerId,
                command.Amount,
                command.RequiredBy)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task RecordDepositPaymentAsync(RecordDepositPaymentCommand command)
    {
        EnsureExists();

        if (State.Deposit == null)
            throw new InvalidOperationException("No deposit required");

        if (State.Deposit.Status != DepositStatus.Required)
            throw new InvalidOperationException($"Deposit is not required: {State.Deposit.Status}");

        var depositAmount = State.Deposit.Amount;

        RaiseEvent(new BookingDepositPaidJournaledEvent
        {
            BookingId = State.Id,
            PaymentId = Guid.NewGuid(),
            Amount = depositAmount,
            PaymentMethod = command.Method.ToString(),
            PaymentReference = command.PaymentReference,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        if (GetBookingStream() != null)
        {
            await GetBookingStream()!.OnNextAsync(new BookingDepositPaidEvent(
                State.Id,
                State.SiteId,
                State.CustomerId,
                depositAmount,
                command.Method.ToString(),
                command.PaymentReference,
                depositAmount)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task WaiveDepositAsync(Guid waivedBy)
    {
        EnsureExists();

        if (State.Deposit == null)
            throw new InvalidOperationException("No deposit required");

        State.Deposit = State.Deposit with { Status = DepositStatus.Waived };
    }

    public async Task ForfeitDepositAsync()
    {
        EnsureExists();

        if (State.Deposit == null || State.Deposit.Status != DepositStatus.Paid)
            throw new InvalidOperationException("No paid deposit to forfeit");

        var depositAmount = State.Deposit.Amount;

        RaiseEvent(new BookingDepositForfeitedJournaledEvent
        {
            BookingId = State.Id,
            Amount = depositAmount,
            Reason = "No-show or late cancellation",
            ProcessedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        if (GetBookingStream() != null)
        {
            await GetBookingStream()!.OnNextAsync(new BookingDepositForfeitedEvent(
                State.Id,
                State.SiteId,
                State.CustomerId,
                depositAmount,
                "No-show or late cancellation",
                Guid.Empty)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task RefundDepositAsync(string reason, Guid refundedBy)
    {
        EnsureExists();

        if (State.Deposit == null || State.Deposit.Status != DepositStatus.Paid)
            throw new InvalidOperationException("No paid deposit to refund");

        var depositAmount = State.Deposit.Amount;

        RaiseEvent(new BookingDepositRefundedJournaledEvent
        {
            BookingId = State.Id,
            Amount = depositAmount,
            Reason = reason,
            RefundedBy = refundedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        if (GetBookingStream() != null)
        {
            await GetBookingStream()!.OnNextAsync(new BookingDepositRefundedEvent(
                State.Id,
                State.SiteId,
                State.CustomerId,
                depositAmount,
                reason,
                refundedBy)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task LinkToOrderAsync(Guid orderId)
    {
        EnsureExists();

        RaiseEvent(new BookingLinkedToOrderJournaledEvent
        {
            BookingId = State.Id,
            OrderId = orderId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<BookingStatus> GetStatusAsync() => Task.FromResult(State.Status);
    public Task<bool> RequiresDepositAsync() =>
        Task.FromResult(State.Deposit != null && State.Deposit.Status == DepositStatus.Required);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Booking does not exist");
    }

    private void EnsureStatus(params BookingStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(State.Status))
            throw new InvalidOperationException($"Invalid status. Expected one of [{string.Join(", ", allowedStatuses)}], got {State.Status}");
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
