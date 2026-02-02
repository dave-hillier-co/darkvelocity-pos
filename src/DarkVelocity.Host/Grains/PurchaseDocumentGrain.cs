using DarkVelocity.Host.Events;
using DarkVelocity.Host.Events.JournaledEvents;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain representing a purchase document (invoice or receipt).
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class PurchaseDocumentGrain : JournaledGrain<PurchaseDocumentState, IPurchaseDocumentJournaledEvent>, IPurchaseDocumentGrain
{
    private readonly ILogger<PurchaseDocumentGrain> _logger;
    private readonly IGrainFactory _grainFactory;
    private Lazy<IAsyncStream<IStreamEvent>>? _purchaseStream;

    public PurchaseDocumentGrain(
        IGrainFactory grainFactory,
        ILogger<PurchaseDocumentGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _purchaseStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create("purchase-document-events", State.OrganizationId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });

        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(PurchaseDocumentState state, IPurchaseDocumentJournaledEvent @event)
    {
        switch (@event)
        {
            case PurchaseDocumentCreatedJournaledEvent e:
                state.DocumentId = e.DocumentId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.DocumentType = Enum.TryParse<PurchaseDocumentType>(e.DocumentType, out var dt) ? dt : PurchaseDocumentType.Invoice;
                state.Status = PurchaseDocumentStatus.Received;
                state.VendorId = e.VendorId;
                state.VendorName = e.VendorName;
                state.DocumentDate = e.DocumentDate;
                state.Source = Enum.TryParse<DocumentSource>(e.Source ?? "", out var ds) ? ds : DocumentSource.Manual;
                state.CreatedAt = e.OccurredAt;
                break;

            case PurchaseDocumentProcessingRequestedJournaledEvent:
                state.Status = PurchaseDocumentStatus.Processing;
                state.ProcessingError = null;
                break;

            case PurchaseDocumentExtractionAppliedJournaledEvent e:
                state.VendorName = e.VendorName ?? state.VendorName;
                state.DocumentDate = e.DocumentDate ?? state.DocumentDate;
                state.Total = e.Total ?? state.Total;
                state.ExtractionConfidence = e.Confidence;
                state.ProcessorVersion = e.ProcessorVersion;
                state.Status = PurchaseDocumentStatus.Extracted;
                state.ProcessedAt = e.OccurredAt;
                break;

            case PurchaseDocumentExtractionFailedJournaledEvent e:
                state.Status = PurchaseDocumentStatus.Failed;
                state.ProcessingError = e.Reason;
                break;

            case PurchaseDocumentLineMappedJournaledEvent e:
                if (e.LineIndex >= 0 && e.LineIndex < state.Lines.Count)
                {
                    var line = state.Lines[e.LineIndex];
                    state.Lines[e.LineIndex] = line with
                    {
                        MappedIngredientId = e.IngredientId,
                        MappedIngredientSku = e.IngredientSku,
                        MappedIngredientName = e.IngredientName,
                        MappingSource = Enum.TryParse<MappingSource>(e.MappingSource, out var ms) ? ms : State.MappingSource.Manual,
                        MappingConfidence = e.Confidence,
                        Suggestions = null
                    };
                }
                break;

            case PurchaseDocumentLineUnmappedJournaledEvent e:
                if (e.LineIndex >= 0 && e.LineIndex < state.Lines.Count)
                {
                    var line = state.Lines[e.LineIndex];
                    state.Lines[e.LineIndex] = line with
                    {
                        MappedIngredientId = null,
                        MappedIngredientSku = null,
                        MappedIngredientName = null,
                        MappingSource = null,
                        MappingConfidence = 0
                    };
                }
                break;

            case PurchaseDocumentLineModifiedJournaledEvent e:
                if (e.LineIndex >= 0 && e.LineIndex < state.Lines.Count)
                {
                    var line = state.Lines[e.LineIndex];
                    state.Lines[e.LineIndex] = line with
                    {
                        Description = e.Description ?? line.Description,
                        Quantity = e.Quantity ?? line.Quantity,
                        Unit = e.Unit ?? line.Unit,
                        UnitPrice = e.UnitPrice ?? line.UnitPrice,
                        TotalPrice = (e.Quantity ?? line.Quantity) * (e.UnitPrice ?? line.UnitPrice)
                    };
                }
                break;

            case PurchaseDocumentConfirmedJournaledEvent e:
                if (e.VendorId.HasValue) state.VendorId = e.VendorId;
                if (e.VendorName != null) state.VendorName = e.VendorName;
                if (e.DocumentDate.HasValue) state.DocumentDate = e.DocumentDate;
                state.Status = PurchaseDocumentStatus.Confirmed;
                state.ConfirmedAt = e.OccurredAt;
                state.ConfirmedBy = e.ConfirmedBy;
                break;

            case PurchaseDocumentRejectedJournaledEvent e:
                state.Status = PurchaseDocumentStatus.Rejected;
                state.RejectedAt = e.OccurredAt;
                state.RejectedBy = e.RejectedBy;
                state.RejectionReason = e.Reason;
                break;
        }
    }

    public async Task<PurchaseDocumentSnapshot> ReceiveAsync(ReceivePurchaseDocumentCommand command)
    {
        if (State.DocumentId != Guid.Empty)
            throw new InvalidOperationException("Document already exists");

        var isPaid = command.IsPaid ?? (command.DocumentType == PurchaseDocumentType.Receipt);

        RaiseEvent(new PurchaseDocumentCreatedJournaledEvent
        {
            DocumentId = command.DocumentId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            DocumentType = command.DocumentType.ToString(),
            DocumentNumber = "",
            VendorId = Guid.Empty,
            VendorName = "",
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = command.Source.ToString(),
            CreatedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });

        // Apply additional fields not in journaled event
        State.Source = command.Source;
        State.StorageUrl = command.StorageUrl;
        State.OriginalFilename = command.OriginalFilename;
        State.ContentType = command.ContentType;
        State.FileSizeBytes = command.FileSizeBytes;
        State.EmailFrom = command.EmailFrom;
        State.EmailSubject = command.EmailSubject;
        State.IsPaid = isPaid;

        await ConfirmEvents();

        _logger.LogInformation(
            "Purchase document received: {DocumentId} ({Type}) from {Source}",
            command.DocumentId,
            command.DocumentType,
            command.Source);

        return ToSnapshot();
    }

    public async Task RequestProcessingAsync()
    {
        EnsureExists();

        if (State.Status != PurchaseDocumentStatus.Received &&
            State.Status != PurchaseDocumentStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot process document in status {State.Status}");
        }

        RaiseEvent(new PurchaseDocumentProcessingRequestedJournaledEvent
        {
            DocumentId = State.DocumentId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Processing requested for document: {DocumentId}", State.DocumentId);
    }

    public async Task ApplyExtractionResultAsync(ApplyExtractionResultCommand command)
    {
        EnsureExists();

        var data = command.Data;

        RaiseEvent(new PurchaseDocumentExtractionAppliedJournaledEvent
        {
            DocumentId = State.DocumentId,
            VendorName = data.VendorName,
            DocumentDate = data.DocumentDate,
            Total = data.Total,
            LineCount = data.Lines.Count,
            Confidence = command.Confidence,
            ProcessorVersion = command.ProcessorVersion,
            OccurredAt = DateTime.UtcNow
        });

        // Apply extracted vendor info
        State.VendorAddress = data.VendorAddress;
        State.VendorPhone = data.VendorPhone;

        // Invoice-specific
        State.InvoiceNumber = data.InvoiceNumber;
        State.PurchaseOrderNumber = data.PurchaseOrderNumber;
        State.DueDate = data.DueDate;
        State.PaymentTerms = data.PaymentTerms;

        // Receipt-specific
        State.TransactionTime = data.TransactionTime;
        State.PaymentMethod = data.PaymentMethod;
        State.CardLastFour = data.CardLastFour;

        // Common fields
        State.Subtotal = data.Subtotal;
        State.Tax = data.Tax;
        State.Tip = data.Tip;
        State.DeliveryFee = data.DeliveryFee;
        if (!string.IsNullOrEmpty(data.Currency))
            State.Currency = data.Currency;

        // Convert line items
        State.Lines = data.Lines.Select((line, index) => new PurchaseDocumentLine
        {
            LineIndex = index,
            Description = line.Description,
            Quantity = line.Quantity,
            Unit = line.Unit,
            UnitPrice = line.UnitPrice,
            TotalPrice = line.TotalPrice,
            ProductCode = line.ProductCode,
            ExtractionConfidence = line.Confidence
        }).ToList();

        await ConfirmEvents();

        _logger.LogInformation(
            "Extraction completed for document: {DocumentId}, {LineCount} lines, confidence: {Confidence:P0}",
            State.DocumentId,
            State.Lines.Count,
            command.Confidence);

        // Attempt auto-mapping for all lines
        await AttemptAutoMappingAsync();
    }

    /// <summary>
    /// Attempts to auto-map all unmapped line items using vendor mappings.
    /// </summary>
    private async Task AttemptAutoMappingAsync()
    {
        if (string.IsNullOrEmpty(State.VendorName))
            return;

        // Get or create the vendor mapping grain
        var vendorId = NormalizeVendorId(State.VendorName);
        var mappingGrain = _grainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(State.OrganizationId, vendorId));

        // Initialize if needed
        if (!await mappingGrain.ExistsAsync())
        {
            var vendorType = State.DocumentType == PurchaseDocumentType.Receipt
                ? VendorType.RetailStore
                : VendorType.Supplier;

            await mappingGrain.InitializeAsync(new InitializeVendorMappingCommand(
                State.OrganizationId,
                vendorId,
                State.VendorName,
                vendorType));
        }

        var mappedCount = 0;
        var suggestedCount = 0;

        for (var i = 0; i < State.Lines.Count; i++)
        {
            var line = State.Lines[i];

            // Skip if already mapped
            if (line.MappedIngredientId.HasValue)
                continue;

            // Try to find a mapping
            var result = await mappingGrain.GetMappingAsync(line.Description, line.ProductCode);

            if (result.Found && result.Mapping != null)
            {
                RaiseEvent(new PurchaseDocumentLineMappedJournaledEvent
                {
                    DocumentId = State.DocumentId,
                    LineIndex = i,
                    IngredientId = result.Mapping.IngredientId,
                    IngredientSku = result.Mapping.IngredientSku,
                    IngredientName = result.Mapping.IngredientName,
                    MappingSource = MappingSource.Auto.ToString(),
                    Confidence = result.Mapping.Confidence,
                    OccurredAt = DateTime.UtcNow
                });
                mappedCount++;

                // Record the usage
                await mappingGrain.RecordUsageAsync(new RecordMappingUsageCommand(
                    line.Description,
                    State.DocumentId));
            }
            else
            {
                // Get suggestions for unmapped items
                var suggestions = await mappingGrain.GetSuggestionsAsync(line.Description, null, 3);
                if (suggestions.Count > 0)
                {
                    State.Lines[i] = line with
                    {
                        Suggestions = suggestions.Select(s => new SuggestedMapping
                        {
                            IngredientId = s.IngredientId,
                            IngredientName = s.IngredientName,
                            Sku = s.IngredientSku,
                            Confidence = s.Confidence,
                            MatchReason = s.MatchReason
                        }).ToList()
                    };
                    suggestedCount++;
                }
            }
        }

        if (mappedCount > 0)
        {
            await ConfirmEvents();
        }

        if (mappedCount > 0 || suggestedCount > 0)
        {
            _logger.LogInformation(
                "Auto-mapping for document {DocumentId}: {MappedCount} auto-mapped, {SuggestedCount} with suggestions",
                State.DocumentId,
                mappedCount,
                suggestedCount);
        }
    }

    /// <summary>
    /// Normalizes a vendor name to a consistent ID.
    /// </summary>
    private static string NormalizeVendorId(string vendorName)
    {
        // Remove common suffixes and normalize
        var normalized = vendorName.ToLowerInvariant()
            .Replace(" inc.", "")
            .Replace(" llc", "")
            .Replace(" corp.", "")
            .Replace(" co.", "")
            .Replace(",", "")
            .Replace(".", "")
            .Trim();

        // Common retailer normalization
        if (normalized.Contains("costco")) return "costco";
        if (normalized.Contains("walmart")) return "walmart";
        if (normalized.Contains("sam's club")) return "sams-club";
        if (normalized.Contains("restaurant depot")) return "restaurant-depot";
        if (normalized.Contains("sysco")) return "sysco";
        if (normalized.Contains("us foods")) return "us-foods";

        // Use the normalized name with spaces replaced by hyphens
        return System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "-");
    }

    public async Task MarkExtractionFailedAsync(MarkExtractionFailedCommand command)
    {
        EnsureExists();

        var reason = command.FailureReason;
        if (!string.IsNullOrEmpty(command.ProcessorError))
            reason += $" ({command.ProcessorError})";

        RaiseEvent(new PurchaseDocumentExtractionFailedJournaledEvent
        {
            DocumentId = State.DocumentId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogWarning(
            "Extraction failed for document: {DocumentId}, reason: {Reason}",
            State.DocumentId,
            command.FailureReason);
    }

    public async Task MapLineAsync(MapLineCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        var lineIndex = command.LineIndex;
        if (lineIndex < 0 || lineIndex >= State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        RaiseEvent(new PurchaseDocumentLineMappedJournaledEvent
        {
            DocumentId = State.DocumentId,
            LineIndex = lineIndex,
            IngredientId = command.IngredientId,
            IngredientSku = command.IngredientSku,
            IngredientName = command.IngredientName,
            MappingSource = command.Source.ToString(),
            Confidence = command.Confidence,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogDebug(
            "Line {LineIndex} mapped to {IngredientSku} via {Source}",
            lineIndex,
            command.IngredientSku,
            command.Source);
    }

    public async Task UnmapLineAsync(UnmapLineCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        var lineIndex = command.LineIndex;
        if (lineIndex < 0 || lineIndex >= State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        RaiseEvent(new PurchaseDocumentLineUnmappedJournaledEvent
        {
            DocumentId = State.DocumentId,
            LineIndex = lineIndex,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateLineAsync(UpdateLineCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        var lineIndex = command.LineIndex;
        if (lineIndex < 0 || lineIndex >= State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        RaiseEvent(new PurchaseDocumentLineModifiedJournaledEvent
        {
            DocumentId = State.DocumentId,
            LineIndex = lineIndex,
            Description = command.Description,
            Quantity = command.Quantity,
            Unit = command.Unit,
            UnitPrice = command.UnitPrice,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task SetLineSuggestionsAsync(int lineIndex, IReadOnlyList<SuggestedMapping> suggestions)
    {
        EnsureExists();
        EnsureExtracted();

        if (lineIndex < 0 || lineIndex >= State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var existingLine = State.Lines[lineIndex];
        State.Lines[lineIndex] = existingLine with
        {
            Suggestions = suggestions.ToList()
        };

        // No journaled event for suggestions - they are transient
    }

    public async Task<PurchaseDocumentSnapshot> ConfirmAsync(ConfirmPurchaseDocumentCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        RaiseEvent(new PurchaseDocumentConfirmedJournaledEvent
        {
            DocumentId = State.DocumentId,
            ConfirmedBy = command.ConfirmedBy,
            VendorId = command.VendorId,
            VendorName = command.VendorName,
            DocumentDate = command.DocumentDate,
            OccurredAt = DateTime.UtcNow
        });

        // Apply currency if provided
        if (!string.IsNullOrEmpty(command.Currency))
            State.Currency = command.Currency;

        await ConfirmEvents();

        // Publish confirmed event for downstream processing
        var confirmedData = new ConfirmedDocumentData
        {
            VendorId = State.VendorId,
            VendorName = State.VendorName ?? "Unknown",
            DocumentDate = State.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            InvoiceNumber = State.InvoiceNumber,
            Lines = State.Lines.Select(l => new ConfirmedLineItem
            {
                Description = l.Description,
                Quantity = l.Quantity ?? 1,
                Unit = l.Unit ?? "ea",
                UnitPrice = l.UnitPrice ?? 0,
                TotalPrice = l.TotalPrice ?? 0,
                IngredientId = l.MappedIngredientId,
                IngredientSku = l.MappedIngredientSku,
                MappingSource = l.MappingSource
            }).ToList(),
            Total = State.Total ?? 0,
            Tax = State.Tax ?? 0,
            Currency = State.Currency,
            IsPaid = State.IsPaid,
            DueDate = State.DueDate
        };

        await _purchaseStream!.Value.OnNextAsync(new PurchaseDocumentConfirmedEvent(
            State.DocumentId,
            State.SiteId,
            State.DocumentType,
            confirmedData)
        {
            OrganizationId = State.OrganizationId
        });

        // Learn mappings from confirmed lines
        await LearnMappingsFromConfirmationAsync(command.ConfirmedBy);

        _logger.LogInformation(
            "Purchase document confirmed: {DocumentId} by {UserId}",
            State.DocumentId,
            command.ConfirmedBy);

        return ToSnapshot();
    }

    /// <summary>
    /// Learns mappings from confirmed line items for future auto-mapping.
    /// </summary>
    private async Task LearnMappingsFromConfirmationAsync(Guid confirmedBy)
    {
        if (string.IsNullOrEmpty(State.VendorName))
            return;

        var vendorId = NormalizeVendorId(State.VendorName);
        var mappingGrain = _grainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(State.OrganizationId, vendorId));

        var learnedCount = 0;

        foreach (var line in State.Lines)
        {
            // Only learn from manually mapped or suggested mappings (not auto-mapped)
            if (!line.MappedIngredientId.HasValue)
                continue;

            if (line.MappingSource == MappingSource.Auto)
                continue; // Already learned

            await mappingGrain.LearnMappingAsync(new LearnMappingCommand(
                line.Description,
                line.MappedIngredientId.Value,
                line.MappedIngredientName ?? "Unknown",
                line.MappedIngredientSku ?? "unknown",
                line.MappingSource ?? MappingSource.Manual,
                line.MappingConfidence,
                line.ProductCode,
                State.DocumentId,
                confirmedBy,
                line.UnitPrice,
                line.Unit));

            learnedCount++;
        }

        if (learnedCount > 0)
        {
            _logger.LogInformation(
                "Learned {Count} mappings from confirmed document {DocumentId}",
                learnedCount,
                State.DocumentId);
        }
    }

    public async Task RejectAsync(RejectPurchaseDocumentCommand command)
    {
        EnsureExists();

        RaiseEvent(new PurchaseDocumentRejectedJournaledEvent
        {
            DocumentId = State.DocumentId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation(
            "Purchase document rejected: {DocumentId}, reason: {Reason}",
            State.DocumentId,
            command.Reason);
    }

    public Task<PurchaseDocumentSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<PurchaseDocumentState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.DocumentId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (State.DocumentId == Guid.Empty)
            throw new InvalidOperationException("Document not initialized");
    }

    private void EnsureExtracted()
    {
        if (State.Status != PurchaseDocumentStatus.Extracted &&
            State.Status != PurchaseDocumentStatus.Confirmed)
        {
            throw new InvalidOperationException($"Document not in extracted state (current: {State.Status})");
        }
    }

    private PurchaseDocumentSnapshot ToSnapshot()
    {
        return new PurchaseDocumentSnapshot(
            State.DocumentId,
            State.OrganizationId,
            State.SiteId,
            State.DocumentType,
            State.Status,
            State.Source,
            State.StorageUrl,
            State.OriginalFilename,
            State.VendorName,
            State.DocumentDate,
            State.InvoiceNumber,
            State.Lines.Select(l => new PurchaseDocumentLineSnapshot(
                l.LineIndex,
                l.Description,
                l.Quantity,
                l.Unit,
                l.UnitPrice,
                l.TotalPrice,
                l.MappedIngredientId,
                l.MappedIngredientSku,
                l.MappedIngredientName,
                l.MappingSource,
                l.MappingConfidence,
                l.Suggestions)).ToList(),
            State.Total,
            State.Currency,
            State.IsPaid,
            State.ExtractionConfidence,
            State.ProcessingError,
            State.CreatedAt,
            State.ConfirmedAt,
            Version);
    }
}

// ============================================================================
// Stream Events
// ============================================================================

/// <summary>
/// Event emitted when a purchase document is confirmed.
/// </summary>
[GenerateSerializer]
public record PurchaseDocumentConfirmedEvent(
    [property: Id(0)] Guid DocumentId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] PurchaseDocumentType DocumentType,
    [property: Id(3)] ConfirmedDocumentData Data) : StreamEvent;
