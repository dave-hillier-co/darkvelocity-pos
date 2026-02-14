using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for managing vendor-specific item mappings.
/// Stores learned mappings and patterns for auto-matching vendor items to internal SKUs.
/// </summary>
public class VendorItemMappingGrain : Grain, IVendorItemMappingGrain
{
    private readonly IPersistentState<VendorItemMappingState> _state;
    private readonly IFuzzyMatchingService _fuzzyMatcher;
    private readonly ILogger<VendorItemMappingGrain> _logger;
    private IAsyncStream<DomainEvent>? _eventStream;

    public VendorItemMappingGrain(
        [PersistentState("vendor-item-mapping", "purchases")] IPersistentState<VendorItemMappingState> state,
        IFuzzyMatchingService fuzzyMatcher,
        ILogger<VendorItemMappingGrain> logger)
    {
        _state = state;
        _fuzzyMatcher = fuzzyMatcher;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        _eventStream = streamProvider.GetStream<DomainEvent>(
            StreamConstants.PurchaseDocumentStreamNamespace,
            this.GetPrimaryKeyString());

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<VendorMappingSnapshot> InitializeAsync(InitializeVendorMappingCommand command)
    {
        if (_state.State.Version > 0)
            throw new InvalidOperationException("Vendor mapping already initialized");

        _state.State.OrganizationId = command.OrganizationId;
        _state.State.VendorId = command.VendorId;
        _state.State.VendorName = command.VendorName;
        _state.State.VendorType = command.VendorType;
        _state.State.CreatedAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version = 1;

        await _state.WriteStateAsync();

        var evt = new VendorMappingInitialized
        {
            OrganizationId = command.OrganizationId,
            VendorId = command.VendorId,
            VendorName = command.VendorName,
            VendorType = command.VendorType,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Initialized vendor mapping for {VendorName} ({VendorId}) in org {OrgId}",
            command.VendorName, command.VendorId, command.OrganizationId);

        return ToSnapshot();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Version > 0);

    public Task<MappingLookupResult> GetMappingAsync(string vendorDescription, string? vendorProductCode = null)
    {
        // First, try product code match if provided
        if (!string.IsNullOrEmpty(vendorProductCode) &&
            _state.State.ProductCodeMappings.TryGetValue(vendorProductCode.ToUpperInvariant(), out var codeMapping))
        {
            return Task.FromResult(new MappingLookupResult(true, codeMapping, MappingMatchType.ProductCode));
        }

        // Second, try exact description match
        var normalized = _fuzzyMatcher.Normalize(vendorDescription);
        if (_state.State.ExactMappings.TryGetValue(normalized, out var exactMapping))
        {
            return Task.FromResult(new MappingLookupResult(true, exactMapping, MappingMatchType.ExactDescription));
        }

        // Third, try fuzzy pattern match
        var patternMatches = _fuzzyMatcher.FindPatternMatches(
            vendorDescription,
            _state.State.LearnedPatterns,
            minConfidence: 0.85m, // High threshold for auto-matching
            maxResults: 1);

        if (patternMatches.Count > 0)
        {
            var bestMatch = patternMatches[0];
            var mapping = new VendorItemMapping
            {
                VendorDescription = vendorDescription,
                NormalizedDescription = normalized,
                IngredientId = bestMatch.Pattern.IngredientId,
                IngredientName = bestMatch.Pattern.IngredientName,
                IngredientSku = bestMatch.Pattern.IngredientSku,
                ProductId = bestMatch.Pattern.ProductId,
                SkuId = bestMatch.Pattern.SkuId,
                Source = MappingSource.Auto,
                Confidence = bestMatch.Score,
                CreatedAt = DateTime.UtcNow
            };

            return Task.FromResult(new MappingLookupResult(true, mapping, MappingMatchType.FuzzyPattern));
        }

        return Task.FromResult(new MappingLookupResult(false, null, MappingMatchType.None));
    }

    public Task<IReadOnlyList<MappingSuggestion>> GetSuggestionsAsync(
        string vendorDescription,
        IReadOnlyList<IngredientInfo>? candidateIngredients = null,
        int maxSuggestions = 5)
    {
        var suggestions = new List<MappingSuggestion>();

        // Get pattern-based suggestions
        var patternMatches = _fuzzyMatcher.FindPatternMatches(
            vendorDescription,
            _state.State.LearnedPatterns,
            minConfidence: 0.5m,
            maxResults: maxSuggestions);

        foreach (var match in patternMatches)
        {
            suggestions.Add(new MappingSuggestion(
                match.Pattern.IngredientId,
                match.Pattern.IngredientName,
                match.Pattern.IngredientSku,
                match.Score,
                match.MatchReason,
                MappingMatchType.FuzzyPattern,
                match.Pattern.ProductId,
                match.Pattern.SkuId));
        }

        // Get ingredient-based suggestions if candidates provided
        if (candidateIngredients != null && candidateIngredients.Count > 0)
        {
            var ingredientMatches = _fuzzyMatcher.FindIngredientMatches(
                vendorDescription,
                candidateIngredients,
                minConfidence: 0.3m,
                maxResults: maxSuggestions);

            // Merge, preferring pattern matches for same ingredient
            var existingIds = new HashSet<Guid>(suggestions.Select(s => s.IngredientId));
            foreach (var match in ingredientMatches)
            {
                if (!existingIds.Contains(match.IngredientId))
                {
                    suggestions.Add(match);
                }
            }
        }

        // Sort by confidence and limit
        var result = suggestions
            .OrderByDescending(s => s.Confidence)
            .Take(maxSuggestions)
            .ToList();

        return Task.FromResult<IReadOnlyList<MappingSuggestion>>(result);
    }

    public async Task LearnMappingAsync(LearnMappingCommand command)
    {
        EnsureInitialized();

        var normalized = _fuzzyMatcher.Normalize(command.VendorDescription);

        var mapping = new VendorItemMapping
        {
            VendorDescription = command.VendorDescription,
            NormalizedDescription = normalized,
            VendorProductCode = command.VendorProductCode,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            IngredientSku = command.IngredientSku,
            ProductId = command.ProductId,
            SkuId = command.SkuId,
            Source = command.Source,
            Confidence = command.Confidence,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.LearnedBy,
            UsageCount = 1,
            LastUsedAt = DateTime.UtcNow,
            ExpectedUnitPrice = command.UnitPrice,
            Unit = command.Unit
        };

        // Store exact mapping
        _state.State.ExactMappings[normalized] = mapping;

        // Store product code mapping if available
        if (!string.IsNullOrEmpty(command.VendorProductCode))
        {
            _state.State.ProductCodeMappings[command.VendorProductCode.ToUpperInvariant()] = mapping;
        }

        // Learn pattern for fuzzy matching
        await LearnPatternFromDescriptionAsync(
            command.VendorDescription,
            command.IngredientId,
            command.IngredientName,
            command.IngredientSku,
            command.ProductId,
            command.SkuId);

        // Update statistics
        _state.State.TotalMappingsCreated++;
        if (command.Source == MappingSource.Auto)
            _state.State.TotalAutoMappings++;
        else
            _state.State.TotalManualMappings++;
        _state.State.LastMappingAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        var evt = new ItemMappingLearned
        {
            OrganizationId = _state.State.OrganizationId,
            VendorId = _state.State.VendorId,
            VendorDescription = command.VendorDescription,
            VendorProductCode = command.VendorProductCode,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            IngredientSku = command.IngredientSku,
            MappingOrigin = command.Source,
            Confidence = command.Confidence,
            LearnedFromDocumentId = command.LearnedFromDocumentId,
            ProductId = command.ProductId,
            SkuId = command.SkuId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Learned mapping: '{Description}' -> {IngredientName} ({Source})",
            command.VendorDescription, command.IngredientName, command.Source);
    }

    public async Task<VendorItemMapping> SetMappingAsync(SetMappingCommand command)
    {
        // Initialize if needed
        if (_state.State.Version == 0)
        {
            // Extract org and vendor from grain key
            var keyParts = this.GetPrimaryKeyString().Split(':');
            if (keyParts.Length >= 4)
            {
                await InitializeAsync(new InitializeVendorMappingCommand(
                    Guid.Parse(keyParts[1]),
                    keyParts[3],
                    keyParts[3],
                    VendorType.Unknown));
            }
        }

        var normalized = _fuzzyMatcher.Normalize(command.VendorDescription);

        var mapping = new VendorItemMapping
        {
            VendorDescription = command.VendorDescription,
            NormalizedDescription = normalized,
            VendorProductCode = command.VendorProductCode,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            IngredientSku = command.IngredientSku,
            ProductId = command.ProductId,
            SkuId = command.SkuId,
            Source = MappingSource.Manual,
            Confidence = 1.0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = command.SetBy,
            ExpectedUnitPrice = command.ExpectedUnitPrice,
            Unit = command.Unit
        };

        _state.State.ExactMappings[normalized] = mapping;

        if (!string.IsNullOrEmpty(command.VendorProductCode))
        {
            _state.State.ProductCodeMappings[command.VendorProductCode.ToUpperInvariant()] = mapping;
        }

        // Learn pattern
        await LearnPatternFromDescriptionAsync(
            command.VendorDescription,
            command.IngredientId,
            command.IngredientName,
            command.IngredientSku,
            command.ProductId,
            command.SkuId);

        _state.State.TotalMappingsCreated++;
        _state.State.TotalManualMappings++;
        _state.State.LastMappingAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        var evt = new ItemMappingSet
        {
            OrganizationId = _state.State.OrganizationId,
            VendorId = _state.State.VendorId,
            VendorDescription = command.VendorDescription,
            VendorProductCode = command.VendorProductCode,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            IngredientSku = command.IngredientSku,
            SetBy = command.SetBy,
            ExpectedUnitPrice = command.ExpectedUnitPrice,
            ProductId = command.ProductId,
            SkuId = command.SkuId,
            OccurredAt = DateTime.UtcNow
        };
        await PublishEventAsync(evt);

        _logger.LogInformation(
            "Set mapping: '{Description}' -> {IngredientName} by {UserId}",
            command.VendorDescription, command.IngredientName, command.SetBy);

        return mapping;
    }

    public async Task DeleteMappingAsync(DeleteMappingCommand command)
    {
        EnsureInitialized();

        var normalized = _fuzzyMatcher.Normalize(command.VendorDescription);

        if (_state.State.ExactMappings.TryGetValue(normalized, out var mapping))
        {
            _state.State.ExactMappings.Remove(normalized);

            if (!string.IsNullOrEmpty(mapping.VendorProductCode))
            {
                _state.State.ProductCodeMappings.Remove(mapping.VendorProductCode.ToUpperInvariant());
            }

            // Remove learned patterns associated with this mapping
            var tokens = _fuzzyMatcher.Tokenize(command.VendorDescription);
            if (tokens.Count > 0)
            {
                _state.State.LearnedPatterns.RemoveAll(p =>
                    p.IngredientId == mapping.IngredientId &&
                    p.Tokens.Count == tokens.Count &&
                    p.Tokens.All(t => tokens.Contains(t)));
            }

            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;

            await _state.WriteStateAsync();

            var evt = new ItemMappingDeleted
            {
                OrganizationId = _state.State.OrganizationId,
                VendorId = _state.State.VendorId,
                VendorDescription = command.VendorDescription,
                DeletedBy = command.DeletedBy,
                OccurredAt = DateTime.UtcNow
            };
            await PublishEventAsync(evt);

            _logger.LogInformation(
                "Deleted mapping: '{Description}' by {UserId}",
                command.VendorDescription, command.DeletedBy);
        }
    }

    public async Task RecordUsageAsync(RecordMappingUsageCommand command)
    {
        var normalized = _fuzzyMatcher.Normalize(command.VendorDescription);

        if (_state.State.ExactMappings.TryGetValue(normalized, out var mapping))
        {
            // Update usage count
            _state.State.ExactMappings[normalized] = mapping with
            {
                UsageCount = mapping.UsageCount + 1,
                LastUsedAt = DateTime.UtcNow
            };

            _state.State.UpdatedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();

            var evt = new ItemMappingUsed
            {
                OrganizationId = _state.State.OrganizationId,
                VendorId = _state.State.VendorId,
                VendorDescription = command.VendorDescription,
                DocumentId = command.DocumentId,
                OccurredAt = DateTime.UtcNow
            };
            await PublishEventAsync(evt);
        }
    }

    public Task<IReadOnlyList<MappingSummary>> GetAllMappingsAsync()
    {
        var mappings = _state.State.ExactMappings.Values
            .OrderByDescending(m => m.UsageCount)
            .ThenByDescending(m => m.CreatedAt)
            .Select(m => new MappingSummary(
                m.VendorDescription,
                m.VendorProductCode,
                m.IngredientId,
                m.IngredientName,
                m.IngredientSku,
                m.UsageCount,
                m.CreatedAt,
                m.Source,
                m.ProductId,
                m.SkuId))
            .ToList();

        return Task.FromResult<IReadOnlyList<MappingSummary>>(mappings);
    }

    public Task<VendorMappingSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public async Task UpdateVendorInfoAsync(string? vendorName = null, VendorType? vendorType = null)
    {
        EnsureInitialized();

        if (vendorName != null)
            _state.State.VendorName = vendorName;
        if (vendorType != null)
            _state.State.VendorType = vendorType.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    private async Task LearnPatternFromDescriptionAsync(
        string description,
        Guid ingredientId,
        string ingredientName,
        string ingredientSku,
        Guid? productId = null,
        Guid? skuId = null)
    {
        var tokens = _fuzzyMatcher.Tokenize(description);
        if (tokens.Count == 0)
            return;

        // Find existing pattern with same tokens
        var existingPattern = _state.State.LearnedPatterns
            .FirstOrDefault(p =>
                p.IngredientId == ingredientId &&
                p.Tokens.Count == tokens.Count &&
                p.Tokens.All(t => tokens.Contains(t)));

        if (existingPattern != null)
        {
            // Reinforce existing pattern
            var index = _state.State.LearnedPatterns.IndexOf(existingPattern);
            _state.State.LearnedPatterns[index] = existingPattern with
            {
                Weight = existingPattern.Weight + 1,
                LastReinforcedAt = DateTime.UtcNow
            };

            var evt = new PatternLearned
            {
                OrganizationId = _state.State.OrganizationId,
                VendorId = _state.State.VendorId,
                Tokens = tokens,
                IngredientId = ingredientId,
                IngredientName = ingredientName,
                IsReinforcement = true,
                OccurredAt = DateTime.UtcNow
            };
            await PublishEventAsync(evt);
        }
        else
        {
            // Add new pattern
            var pattern = new LearnedPattern
            {
                Tokens = tokens,
                IngredientId = ingredientId,
                IngredientName = ingredientName,
                IngredientSku = ingredientSku,
                Weight = 1,
                LearnedAt = DateTime.UtcNow,
                ProductId = productId,
                SkuId = skuId
            };
            _state.State.LearnedPatterns.Add(pattern);

            var evt = new PatternLearned
            {
                OrganizationId = _state.State.OrganizationId,
                VendorId = _state.State.VendorId,
                Tokens = tokens,
                IngredientId = ingredientId,
                IngredientName = ingredientName,
                IsReinforcement = false,
                OccurredAt = DateTime.UtcNow
            };
            await PublishEventAsync(evt);
        }
    }

    private VendorMappingSnapshot ToSnapshot()
    {
        return new VendorMappingSnapshot(
            _state.State.OrganizationId,
            _state.State.VendorId,
            _state.State.VendorName,
            _state.State.VendorType,
            _state.State.ExactMappings.Count,
            _state.State.LearnedPatterns.Count,
            _state.State.LastMappingAt,
            _state.State.Version);
    }

    private void EnsureInitialized()
    {
        if (_state.State.Version == 0)
            throw new InvalidOperationException("Vendor mapping not initialized");
    }

    private async Task PublishEventAsync(DomainEvent evt)
    {
        if (_eventStream != null)
        {
            await _eventStream.OnNextAsync(evt);
        }
    }
}
