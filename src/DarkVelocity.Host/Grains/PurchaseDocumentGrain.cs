using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain representing a purchase document (invoice or receipt).
/// </summary>
public class PurchaseDocumentGrain : Grain, IPurchaseDocumentGrain
{
    private readonly IPersistentState<PurchaseDocumentState> _state;
    private readonly ILogger<PurchaseDocumentGrain> _logger;
    private readonly IGrainFactory _grainFactory;
    private IAsyncStream<IStreamEvent>? _purchaseStream;

    public PurchaseDocumentGrain(
        [PersistentState("purchase-document", "OrleansStorage")]
        IPersistentState<PurchaseDocumentState> state,
        IGrainFactory grainFactory,
        ILogger<PurchaseDocumentGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IAsyncStream<IStreamEvent> GetPurchaseStream()
    {
        if (_purchaseStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create("purchase-document-events", _state.State.OrganizationId.ToString());
            _purchaseStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _purchaseStream!;
    }

    public async Task<PurchaseDocumentSnapshot> ReceiveAsync(ReceivePurchaseDocumentCommand command)
    {
        if (_state.State.DocumentId != Guid.Empty)
            throw new InvalidOperationException("Document already exists");

        var isPaid = command.IsPaid ?? (command.DocumentType == PurchaseDocumentType.Receipt);

        _state.State = new PurchaseDocumentState
        {
            DocumentId = command.DocumentId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            DocumentType = command.DocumentType,
            Status = PurchaseDocumentStatus.Received,
            Source = command.Source,
            StorageUrl = command.StorageUrl,
            OriginalFilename = command.OriginalFilename,
            ContentType = command.ContentType,
            FileSizeBytes = command.FileSizeBytes,
            EmailFrom = command.EmailFrom,
            EmailSubject = command.EmailSubject,
            IsPaid = isPaid,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

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

        if (_state.State.Status != PurchaseDocumentStatus.Received &&
            _state.State.Status != PurchaseDocumentStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot process document in status {_state.State.Status}");
        }

        _state.State.Status = PurchaseDocumentStatus.Processing;
        _state.State.ProcessingError = null;
        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation("Processing requested for document: {DocumentId}", _state.State.DocumentId);
    }

    public async Task ApplyExtractionResultAsync(ApplyExtractionResultCommand command)
    {
        EnsureExists();

        var data = command.Data;

        // Apply extracted vendor info
        _state.State.VendorName = data.VendorName;
        _state.State.VendorAddress = data.VendorAddress;
        _state.State.VendorPhone = data.VendorPhone;

        // Invoice-specific
        _state.State.InvoiceNumber = data.InvoiceNumber;
        _state.State.PurchaseOrderNumber = data.PurchaseOrderNumber;
        _state.State.DueDate = data.DueDate;
        _state.State.PaymentTerms = data.PaymentTerms;

        // Receipt-specific
        _state.State.TransactionTime = data.TransactionTime;
        _state.State.PaymentMethod = data.PaymentMethod;
        _state.State.CardLastFour = data.CardLastFour;

        // Common fields
        _state.State.DocumentDate = data.DocumentDate;
        _state.State.Subtotal = data.Subtotal;
        _state.State.Tax = data.Tax;
        _state.State.Tip = data.Tip;
        _state.State.DeliveryFee = data.DeliveryFee;
        _state.State.Total = data.Total;
        if (!string.IsNullOrEmpty(data.Currency))
            _state.State.Currency = data.Currency;

        // Convert line items
        _state.State.Lines = data.Lines.Select((line, index) => new PurchaseDocumentLine
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

        _state.State.ExtractionConfidence = command.Confidence;
        _state.State.ProcessorVersion = command.ProcessorVersion;
        _state.State.Status = PurchaseDocumentStatus.Extracted;
        _state.State.ProcessedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Extraction completed for document: {DocumentId}, {LineCount} lines, confidence: {Confidence:P0}",
            _state.State.DocumentId,
            _state.State.Lines.Count,
            command.Confidence);

        // Attempt auto-mapping for all lines
        await AttemptAutoMappingAsync();
    }

    /// <summary>
    /// Attempts to auto-map all unmapped line items using vendor mappings.
    /// </summary>
    private async Task AttemptAutoMappingAsync()
    {
        if (string.IsNullOrEmpty(_state.State.VendorName))
            return;

        // Get or create the vendor mapping grain
        var vendorId = NormalizeVendorId(_state.State.VendorName);
        var mappingGrain = _grainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(_state.State.OrganizationId, vendorId));

        // Initialize if needed
        if (!await mappingGrain.ExistsAsync())
        {
            var vendorType = _state.State.DocumentType == PurchaseDocumentType.Receipt
                ? VendorType.RetailStore
                : VendorType.Supplier;

            await mappingGrain.InitializeAsync(new InitializeVendorMappingCommand(
                _state.State.OrganizationId,
                vendorId,
                _state.State.VendorName,
                vendorType));
        }

        var mappedCount = 0;
        var suggestedCount = 0;

        for (var i = 0; i < _state.State.Lines.Count; i++)
        {
            var line = _state.State.Lines[i];

            // Skip if already mapped
            if (line.MappedIngredientId.HasValue)
                continue;

            // Try to find a mapping
            var result = await mappingGrain.GetMappingAsync(line.Description, line.ProductCode);

            if (result.Found && result.Mapping != null)
            {
                _state.State.Lines[i] = line with
                {
                    MappedIngredientId = result.Mapping.IngredientId,
                    MappedIngredientSku = result.Mapping.IngredientSku,
                    MappedIngredientName = result.Mapping.IngredientName,
                    MappingSource = MappingSource.Auto,
                    MappingConfidence = result.Mapping.Confidence
                };
                mappedCount++;

                // Record the usage
                await mappingGrain.RecordUsageAsync(new RecordMappingUsageCommand(
                    line.Description,
                    _state.State.DocumentId));
            }
            else
            {
                // Get suggestions for unmapped items
                var suggestions = await mappingGrain.GetSuggestionsAsync(line.Description, null, 3);
                if (suggestions.Count > 0)
                {
                    _state.State.Lines[i] = line with
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

        if (mappedCount > 0 || suggestedCount > 0)
        {
            _state.State.Version++;
            await _state.WriteStateAsync();

            _logger.LogInformation(
                "Auto-mapping for document {DocumentId}: {MappedCount} auto-mapped, {SuggestedCount} with suggestions",
                _state.State.DocumentId,
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

        _state.State.Status = PurchaseDocumentStatus.Failed;
        _state.State.ProcessingError = command.FailureReason;
        if (!string.IsNullOrEmpty(command.ProcessorError))
            _state.State.ProcessingError += $" ({command.ProcessorError})";
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogWarning(
            "Extraction failed for document: {DocumentId}, reason: {Reason}",
            _state.State.DocumentId,
            command.FailureReason);
    }

    public async Task MapLineAsync(MapLineCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        var lineIndex = command.LineIndex;
        if (lineIndex < 0 || lineIndex >= _state.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        var existingLine = _state.State.Lines[lineIndex];
        _state.State.Lines[lineIndex] = existingLine with
        {
            MappedIngredientId = command.IngredientId,
            MappedIngredientSku = command.IngredientSku,
            MappedIngredientName = command.IngredientName,
            MappingSource = command.Source,
            MappingConfidence = command.Confidence,
            Suggestions = null // Clear suggestions when mapped
        };

        _state.State.Version++;
        await _state.WriteStateAsync();

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
        if (lineIndex < 0 || lineIndex >= _state.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        var existingLine = _state.State.Lines[lineIndex];
        _state.State.Lines[lineIndex] = existingLine with
        {
            MappedIngredientId = null,
            MappedIngredientSku = null,
            MappedIngredientName = null,
            MappingSource = null,
            MappingConfidence = 0
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UpdateLineAsync(UpdateLineCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        var lineIndex = command.LineIndex;
        if (lineIndex < 0 || lineIndex >= _state.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(command.LineIndex));

        var existingLine = _state.State.Lines[lineIndex];
        _state.State.Lines[lineIndex] = existingLine with
        {
            Description = command.Description ?? existingLine.Description,
            Quantity = command.Quantity ?? existingLine.Quantity,
            Unit = command.Unit ?? existingLine.Unit,
            UnitPrice = command.UnitPrice ?? existingLine.UnitPrice,
            TotalPrice = (command.Quantity ?? existingLine.Quantity) * (command.UnitPrice ?? existingLine.UnitPrice)
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetLineSuggestionsAsync(int lineIndex, IReadOnlyList<SuggestedMapping> suggestions)
    {
        EnsureExists();
        EnsureExtracted();

        if (lineIndex < 0 || lineIndex >= _state.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var existingLine = _state.State.Lines[lineIndex];
        _state.State.Lines[lineIndex] = existingLine with
        {
            Suggestions = suggestions.ToList()
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<PurchaseDocumentSnapshot> ConfirmAsync(ConfirmPurchaseDocumentCommand command)
    {
        EnsureExists();
        EnsureExtracted();

        // Apply any overrides from the command
        if (command.VendorId.HasValue)
            _state.State.VendorId = command.VendorId;
        if (!string.IsNullOrEmpty(command.VendorName))
            _state.State.VendorName = command.VendorName;
        if (command.DocumentDate.HasValue)
            _state.State.DocumentDate = command.DocumentDate;
        if (!string.IsNullOrEmpty(command.Currency))
            _state.State.Currency = command.Currency;

        _state.State.Status = PurchaseDocumentStatus.Confirmed;
        _state.State.ConfirmedAt = DateTime.UtcNow;
        _state.State.ConfirmedBy = command.ConfirmedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish confirmed event for downstream processing
        var confirmedData = new ConfirmedDocumentData
        {
            VendorId = _state.State.VendorId,
            VendorName = _state.State.VendorName ?? "Unknown",
            DocumentDate = _state.State.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            InvoiceNumber = _state.State.InvoiceNumber,
            Lines = _state.State.Lines.Select(l => new ConfirmedLineItem
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
            Total = _state.State.Total ?? 0,
            Tax = _state.State.Tax ?? 0,
            Currency = _state.State.Currency,
            IsPaid = _state.State.IsPaid,
            DueDate = _state.State.DueDate
        };

        await GetPurchaseStream().OnNextAsync(new PurchaseDocumentConfirmedEvent(
            _state.State.DocumentId,
            _state.State.SiteId,
            _state.State.DocumentType,
            confirmedData)
        {
            OrganizationId = _state.State.OrganizationId
        });

        // Learn mappings from confirmed lines
        await LearnMappingsFromConfirmationAsync(command.ConfirmedBy);

        _logger.LogInformation(
            "Purchase document confirmed: {DocumentId} by {UserId}",
            _state.State.DocumentId,
            command.ConfirmedBy);

        return ToSnapshot();
    }

    /// <summary>
    /// Learns mappings from confirmed line items for future auto-mapping.
    /// </summary>
    private async Task LearnMappingsFromConfirmationAsync(Guid confirmedBy)
    {
        if (string.IsNullOrEmpty(_state.State.VendorName))
            return;

        var vendorId = NormalizeVendorId(_state.State.VendorName);
        var mappingGrain = _grainFactory.GetGrain<IVendorItemMappingGrain>(
            GrainKeys.VendorItemMapping(_state.State.OrganizationId, vendorId));

        var learnedCount = 0;

        foreach (var line in _state.State.Lines)
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
                _state.State.DocumentId,
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
                _state.State.DocumentId);
        }
    }

    public async Task RejectAsync(RejectPurchaseDocumentCommand command)
    {
        EnsureExists();

        _state.State.Status = PurchaseDocumentStatus.Rejected;
        _state.State.RejectedAt = DateTime.UtcNow;
        _state.State.RejectedBy = command.RejectedBy;
        _state.State.RejectionReason = command.Reason;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Purchase document rejected: {DocumentId}, reason: {Reason}",
            _state.State.DocumentId,
            command.Reason);
    }

    public Task<PurchaseDocumentSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<PurchaseDocumentState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.DocumentId != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.DocumentId == Guid.Empty)
            throw new InvalidOperationException("Document not initialized");
    }

    private void EnsureExtracted()
    {
        if (_state.State.Status != PurchaseDocumentStatus.Extracted &&
            _state.State.Status != PurchaseDocumentStatus.Confirmed)
        {
            throw new InvalidOperationException($"Document not in extracted state (current: {_state.State.Status})");
        }
    }

    private PurchaseDocumentSnapshot ToSnapshot()
    {
        return new PurchaseDocumentSnapshot(
            _state.State.DocumentId,
            _state.State.OrganizationId,
            _state.State.SiteId,
            _state.State.DocumentType,
            _state.State.Status,
            _state.State.Source,
            _state.State.StorageUrl,
            _state.State.OriginalFilename,
            _state.State.VendorName,
            _state.State.DocumentDate,
            _state.State.InvoiceNumber,
            _state.State.Lines.Select(l => new PurchaseDocumentLineSnapshot(
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
            _state.State.Total,
            _state.State.Currency,
            _state.State.IsPaid,
            _state.State.ExtractionConfidence,
            _state.State.ProcessingError,
            _state.State.CreatedAt,
            _state.State.ConfirmedAt,
            _state.State.Version);
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
