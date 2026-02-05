using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ChargebackGrain : JournaledGrain<ChargebackState, IChargebackEvent>, IChargebackGrain
{
    private readonly IGrainFactory _grainFactory;
    private Lazy<IAsyncStream<IStreamEvent>>? _notificationStream;

    public ChargebackGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.OrganizationId != Guid.Empty)
        {
            InitializeStream();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeStream()
    {
        var orgId = State.OrganizationId;
        _notificationStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.NotificationStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent>? NotificationStream => _notificationStream?.Value;

    protected override void TransitionState(ChargebackState state, IChargebackEvent @event)
    {
        switch (@event)
        {
            case ChargebackReceived e:
                state.Id = e.ChargebackId;
                state.OrganizationId = e.OrganizationId;
                state.PaymentId = e.PaymentId;
                state.Amount = e.Amount;
                state.ReasonCode = e.ReasonCode;
                state.ReasonDescription = e.ReasonDescription;
                state.ProcessorReference = e.ProcessorReference;
                state.DisputeDeadline = e.DisputeDeadline;
                state.Status = ChargebackStatus.Pending;
                state.ReceivedAt = e.OccurredAt;
                break;

            case ChargebackAcknowledged e:
                state.Status = ChargebackStatus.Acknowledged;
                state.AcknowledgedBy = e.AcknowledgedBy;
                state.AcknowledgedAt = e.OccurredAt;
                if (!string.IsNullOrEmpty(e.Notes))
                {
                    state.Notes.Add(new ChargebackNote
                    {
                        NoteId = Guid.NewGuid(),
                        Content = e.Notes,
                        AddedBy = e.AcknowledgedBy,
                        AddedAt = e.OccurredAt
                    });
                }
                break;

            case ChargebackEvidenceUploaded e:
                state.Status = ChargebackStatus.EvidenceGathering;
                state.Evidence.Add(new ChargebackEvidence
                {
                    EvidenceId = e.EvidenceId,
                    EvidenceType = e.EvidenceType,
                    Description = e.Description,
                    FileReference = e.FileReference,
                    UploadedBy = e.UploadedBy,
                    UploadedAt = e.OccurredAt
                });
                break;

            case ChargebackDisputed e:
                state.Status = ChargebackStatus.Disputed;
                state.DisputeReference = e.DisputeReference;
                state.DisputedBy = e.DisputedBy;
                state.DisputedAt = e.OccurredAt;
                if (!string.IsNullOrEmpty(e.DisputeNotes))
                {
                    state.Notes.Add(new ChargebackNote
                    {
                        NoteId = Guid.NewGuid(),
                        Content = e.DisputeNotes,
                        AddedBy = e.DisputedBy,
                        AddedAt = e.OccurredAt
                    });
                }
                break;

            case ChargebackAccepted e:
                state.Status = ChargebackStatus.Lost;
                state.Resolution = ChargebackResolution.AcceptedByMerchant;
                state.FinalAmount = state.Amount;
                state.AcceptedBy = e.AcceptedBy;
                state.ResolvedAt = e.OccurredAt;
                if (!string.IsNullOrEmpty(e.AcceptanceNotes))
                {
                    state.Notes.Add(new ChargebackNote
                    {
                        NoteId = Guid.NewGuid(),
                        Content = e.AcceptanceNotes,
                        AddedBy = e.AcceptedBy,
                        AddedAt = e.OccurredAt
                    });
                }
                break;

            case ChargebackResolved e:
                state.Resolution = e.Resolution;
                state.FinalAmount = e.FinalAmount;
                state.ProcessorResolutionCode = e.ProcessorResolutionCode;
                state.ResolvedAt = e.OccurredAt;
                state.Status = e.Resolution switch
                {
                    ChargebackResolution.WonByMerchant => ChargebackStatus.Won,
                    ChargebackResolution.PartiallyWonByMerchant => ChargebackStatus.Won,
                    ChargebackResolution.WonByCardholder => ChargebackStatus.Lost,
                    ChargebackResolution.AcceptedByMerchant => ChargebackStatus.Lost,
                    ChargebackResolution.Expired => ChargebackStatus.Expired,
                    _ => ChargebackStatus.Lost
                };
                if (!string.IsNullOrEmpty(e.Notes))
                {
                    state.Notes.Add(new ChargebackNote
                    {
                        NoteId = Guid.NewGuid(),
                        Content = e.Notes,
                        AddedBy = Guid.Empty, // System note
                        AddedAt = e.OccurredAt
                    });
                }
                break;

            case ChargebackNoteAdded e:
                state.Notes.Add(new ChargebackNote
                {
                    NoteId = e.NoteId,
                    Content = e.Content,
                    AddedBy = e.AddedBy,
                    AddedAt = e.OccurredAt
                });
                break;
        }
    }

    public async Task<ChargebackReceivedResult> ReceiveAsync(ReceiveChargebackCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Chargeback already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, chargebackId) = GrainKeys.ParseOrgEntity(key);
        var now = DateTime.UtcNow;

        RaiseEvent(new ChargebackReceived
        {
            ChargebackId = chargebackId,
            OrganizationId = command.OrganizationId,
            PaymentId = command.PaymentId,
            Amount = command.Amount,
            ReasonCode = command.ReasonCode,
            ReasonDescription = command.ReasonDescription,
            ProcessorReference = command.ProcessorReference,
            DisputeDeadline = command.DisputeDeadline,
            OccurredAt = now
        });
        await ConfirmEvents();

        InitializeStream();

        // Send notification about new chargeback
        await SendChargebackNotificationAsync("New chargeback received", $"Amount: {command.Amount:C}, Reason: {command.ReasonDescription}");

        return new ChargebackReceivedResult(chargebackId, command.DisputeDeadline, State.DaysUntilDeadline);
    }

    public Task<ChargebackState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task AcknowledgeAsync(Guid acknowledgedBy, string notes)
    {
        EnsureExists();

        if (State.Status != ChargebackStatus.Pending)
            throw new InvalidOperationException($"Cannot acknowledge chargeback with status: {State.Status}");

        RaiseEvent(new ChargebackAcknowledged
        {
            ChargebackId = State.Id,
            AcknowledgedBy = acknowledgedBy,
            Notes = notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<EvidenceUploadedResult> UploadEvidenceAsync(UploadEvidenceCommand command)
    {
        EnsureExists();

        if (State.Status == ChargebackStatus.Won || State.Status == ChargebackStatus.Lost || State.Status == ChargebackStatus.Expired)
            throw new InvalidOperationException($"Cannot upload evidence for resolved chargeback");

        var evidenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        RaiseEvent(new ChargebackEvidenceUploaded
        {
            ChargebackId = State.Id,
            EvidenceId = evidenceId,
            EvidenceType = command.EvidenceType,
            Description = command.Description,
            FileReference = command.FileReference,
            UploadedBy = command.UploadedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        return new EvidenceUploadedResult(evidenceId, now);
    }

    public async Task DisputeAsync(DisputeChargebackCommand command)
    {
        EnsureExists();

        if (State.Status != ChargebackStatus.Acknowledged && State.Status != ChargebackStatus.EvidenceGathering)
            throw new InvalidOperationException($"Cannot dispute chargeback with status: {State.Status}");

        if (State.IsDeadlinePassed)
            throw new InvalidOperationException("Dispute deadline has passed");

        var disputeReference = $"DISP-{State.Id.ToString()[..8].ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMdd}";

        RaiseEvent(new ChargebackDisputed
        {
            ChargebackId = State.Id,
            DisputeReference = disputeReference,
            DisputeNotes = command.DisputeNotes,
            DisputedBy = command.DisputedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await SendChargebackNotificationAsync("Chargeback disputed", $"Reference: {disputeReference}");
    }

    public async Task AcceptAsync(Guid acceptedBy, string notes)
    {
        EnsureExists();

        if (State.Status == ChargebackStatus.Won || State.Status == ChargebackStatus.Lost || State.Status == ChargebackStatus.Expired)
            throw new InvalidOperationException($"Cannot accept already resolved chargeback");

        RaiseEvent(new ChargebackAccepted
        {
            ChargebackId = State.Id,
            AcceptanceNotes = notes,
            AcceptedBy = acceptedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await SendChargebackNotificationAsync("Chargeback accepted", $"Amount: {State.Amount:C}");
    }

    public async Task ResolveAsync(ResolveChargebackCommand command)
    {
        EnsureExists();

        if (State.Status == ChargebackStatus.Won || State.Status == ChargebackStatus.Lost || State.Status == ChargebackStatus.Expired)
            throw new InvalidOperationException($"Chargeback already resolved");

        RaiseEvent(new ChargebackResolved
        {
            ChargebackId = State.Id,
            Resolution = command.Resolution,
            FinalAmount = command.FinalAmount,
            ProcessorResolutionCode = command.ProcessorResolutionCode,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        var resolutionText = command.Resolution switch
        {
            ChargebackResolution.WonByMerchant => "Won by merchant",
            ChargebackResolution.WonByCardholder => "Lost to cardholder",
            ChargebackResolution.PartiallyWonByMerchant => "Partially won",
            _ => command.Resolution.ToString()
        };

        await SendChargebackNotificationAsync("Chargeback resolved", $"Resolution: {resolutionText}, Final amount: {command.FinalAmount:C}");
    }

    public async Task AddNoteAsync(string content, Guid addedBy)
    {
        EnsureExists();

        RaiseEvent(new ChargebackNoteAdded
        {
            ChargebackId = State.Id,
            NoteId = Guid.NewGuid(),
            Content = content,
            AddedBy = addedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<ChargebackSummary> GetSummaryAsync()
    {
        EnsureExists();

        return Task.FromResult(new ChargebackSummary
        {
            ChargebackId = State.Id,
            PaymentId = State.PaymentId,
            Status = State.Status,
            Amount = State.Amount,
            ReasonCode = State.ReasonCode,
            ReasonDescription = State.ReasonDescription,
            DisputeDeadline = State.DisputeDeadline,
            DaysUntilDeadline = State.DaysUntilDeadline,
            EvidenceCount = State.Evidence.Count,
            Resolution = State.Resolution,
            FinalAmount = State.FinalAmount
        });
    }

    public Task<List<ChargebackEvidence>> GetEvidenceAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Evidence.ToList());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<ChargebackStatus> GetStatusAsync() => Task.FromResult(State.Status);
    public Task<int> GetDaysUntilDeadlineAsync() => Task.FromResult(State.DaysUntilDeadline);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Chargeback does not exist");
    }

    private async Task SendChargebackNotificationAsync(string title, string message)
    {
        if (NotificationStream == null) return;

        await NotificationStream.OnNextAsync(new ChargebackNotificationEvent(
            State.OrganizationId,
            State.Id,
            State.PaymentId,
            title,
            message,
            State.Status.ToString())
        {
            OrganizationId = State.OrganizationId
        });
    }
}

/// <summary>
/// Stream event for chargeback notifications.
/// </summary>
[GenerateSerializer]
public record ChargebackNotificationEvent(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid ChargebackId,
    [property: Id(2)] Guid PaymentId,
    [property: Id(3)] string Title,
    [property: Id(4)] string Message,
    [property: Id(5)] string Status) : IStreamEvent
{
    [Id(6)] public Guid EventId { get; init; } = Guid.NewGuid();
    [Id(7)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    Guid IStreamEvent.OrganizationId => OrganizationId;
}
