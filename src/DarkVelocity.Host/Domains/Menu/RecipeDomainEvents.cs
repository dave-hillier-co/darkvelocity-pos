namespace DarkVelocity.Host.Events;

// ============================================================================
// Recipe Versioning Events
// ============================================================================

/// <summary>
/// New recipe version published.
/// </summary>
public sealed record RecipeVersionPublished : DomainEvent
{
    public override string EventType => "menu.recipe.version_published";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
    public required IReadOnlyList<string> Allergens { get; init; }
    public required IReadOnlyList<string> DietaryFlags { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal PortionYield { get; init; }
    public required decimal CostPerPortion { get; init; }
    public required DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public required Guid PublishedBy { get; init; }
    public string? ChangeNotes { get; init; }
}

/// <summary>
/// Recipe ingredient definition.
/// </summary>
public sealed record RecipeIngredient
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string Sku { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal WastePercentage { get; init; }
    public required decimal EffectiveQuantity { get; init; }
    public required decimal StandardCost { get; init; }
    public required decimal LineCost { get; init; }
    public string? PrepInstructions { get; init; }
    public bool IsOptional { get; init; }
    public IReadOnlyList<Guid>? SubstitutionIds { get; init; }
}

/// <summary>
/// Recipe version retired/superseded.
/// </summary>
public sealed record RecipeVersionRetired : DomainEvent
{
    public override string EventType => "menu.recipe.version_retired";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required DateTime RetiredAt { get; init; }
    public Guid? SupersededByVersionId { get; init; }
    public required Guid RetiredBy { get; init; }
    public string? RetirementReason { get; init; }
}

/// <summary>
/// Recipe cost recalculated due to ingredient price changes.
/// </summary>
public sealed record RecipeCostUpdated : DomainEvent
{
    public override string EventType => "menu.recipe.cost_updated";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required decimal PreviousCost { get; init; }
    public required decimal NewCost { get; init; }
    public required decimal CostChange { get; init; }
    public required decimal CostChangePercent { get; init; }
    public required IReadOnlyList<IngredientCostChange> ChangedIngredients { get; init; }
    public required RecipeCostTrigger Trigger { get; init; }
}

public sealed record IngredientCostChange
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PreviousCost { get; init; }
    public required decimal NewCost { get; init; }
    public required decimal Variance { get; init; }
}

public enum RecipeCostTrigger
{
    IngredientPriceChange,
    NewDeliveryReceived,
    ManualRecalculation,
    ScheduledUpdate,
    WACRecalculation
}

/// <summary>
/// Recipe ingredient substituted.
/// </summary>
public sealed record RecipeIngredientSubstituted : DomainEvent
{
    public override string EventType => "menu.recipe.ingredient_substituted";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required Guid OriginalIngredientId { get; init; }
    public required string OriginalIngredientName { get; init; }
    public required Guid SubstituteIngredientId { get; init; }
    public required string SubstituteIngredientName { get; init; }
    public required decimal OriginalQuantity { get; init; }
    public required decimal SubstituteQuantity { get; init; }
    public required string Reason { get; init; }
    public required Guid SubstitutedBy { get; init; }
    public DateTime? ValidUntil { get; init; }
}

/// <summary>
/// Recipe yield adjusted based on actual production.
/// </summary>
public sealed record RecipeYieldAdjusted : DomainEvent
{
    public override string EventType => "menu.recipe.yield_adjusted";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required decimal PreviousYield { get; init; }
    public required decimal NewYield { get; init; }
    public required decimal AverageActualYield { get; init; }
    public required int SampleSize { get; init; }
    public required Guid AdjustedBy { get; init; }
}

/// <summary>
/// Recipe marked as unavailable (86'd).
/// </summary>
public sealed record RecipeUnavailable : DomainEvent
{
    public override string EventType => "menu.recipe.unavailable";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required UnavailableReason Reason { get; init; }
    public required IReadOnlyList<MissingIngredient>? MissingIngredients { get; init; }
    public required Guid MarkedBy { get; init; }
    public DateTime? EstimatedAvailableAt { get; init; }
}

public sealed record MissingIngredient
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal RequiredQuantity { get; init; }
    public required decimal AvailableQuantity { get; init; }
}

public enum UnavailableReason
{
    OutOfStock,
    EquipmentFailure,
    StaffShortage,
    QualityIssue,
    SeasonalEnd,
    SupplierIssue,
    Other
}

/// <summary>
/// Recipe availability restored.
/// </summary>
public sealed record RecipeAvailabilityRestored : DomainEvent
{
    public override string EventType => "menu.recipe.availability_restored";
    public override string AggregateType => "Recipe";
    public override Guid AggregateId => RecipeId;

    public required Guid RecipeId { get; init; }
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required Guid RestoredBy { get; init; }
}

// ============================================================================
// Menu Events
// ============================================================================

/// <summary>
/// Menu item price changed.
/// </summary>
public sealed record MenuItemPriceChanged : DomainEvent
{
    public override string EventType => "menu.item.price_changed";
    public override string AggregateType => "MenuItem";
    public override Guid AggregateId => MenuItemId;

    public required Guid MenuItemId { get; init; }
    public required string ItemName { get; init; }
    public required decimal PreviousPrice { get; init; }
    public required decimal NewPrice { get; init; }
    public required decimal PriceChange { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal PreviousMarginPercent { get; init; }
    public required decimal NewMarginPercent { get; init; }
    public required DateTime EffectiveFrom { get; init; }
    public required Guid ChangedBy { get; init; }
    public string? ChangeReason { get; init; }
}

/// <summary>
/// Menu item margin alert triggered.
/// </summary>
public sealed record MenuItemMarginAlert : DomainEvent
{
    public override string EventType => "menu.item.margin_alert";
    public override string AggregateType => "MenuItem";
    public override Guid AggregateId => MenuItemId;

    public required Guid AlertId { get; init; }
    public required Guid MenuItemId { get; init; }
    public required string ItemName { get; init; }
    public required string Category { get; init; }
    public required decimal SellingPrice { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal CurrentMargin { get; init; }
    public required decimal TargetMargin { get; init; }
    public required decimal MarginVariance { get; init; }
    public required MarginAlertType AlertType { get; init; }
}

public enum MarginAlertType
{
    BelowTarget,
    SignificantDrop,
    NegativeMargin,
    CostSpike
}

// ============================================================================
// Batch Prep Production Events
// ============================================================================

/// <summary>
/// Batch prep production started. Ingredients will be consumed.
/// </summary>
public sealed record BatchPrepProductionStarted : DomainEvent
{
    public override string EventType => "recipe.batch_prep.production_started";
    public override string AggregateType => "BatchPrepProduction";
    public override Guid AggregateId => ProductionId;

    public required Guid ProductionId { get; init; }
    public required Guid RecipeId { get; init; }
    public required string RecipeDocumentId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required int RecipeVersion { get; init; }
    public required string RecipeName { get; init; }
    public required decimal BatchMultiplier { get; init; }
    public required decimal ExpectedYieldQuantity { get; init; }
    public required string YieldUnit { get; init; }
    public required decimal ExpectedOutputQuantity { get; init; }
    public required string OutputUnit { get; init; }
    public required Guid OutputInventoryItemId { get; init; }
    public required string OutputInventoryItemName { get; init; }
    public required IReadOnlyList<ProductionIngredient> Ingredients { get; init; }
    public required decimal EstimatedCost { get; init; }
    public required Guid StartedBy { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Ingredient to be consumed in batch prep production.
/// </summary>
public sealed record ProductionIngredient
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal RequiredQuantity { get; init; }
    public required string Unit { get; init; }
    public required decimal EstimatedCost { get; init; }
}

/// <summary>
/// Batch prep production completed. Output stock created with shelf life.
/// </summary>
public sealed record BatchPrepProductionCompleted : DomainEvent
{
    public override string EventType => "recipe.batch_prep.production_completed";
    public override string AggregateType => "BatchPrepProduction";
    public override Guid AggregateId => ProductionId;

    public required Guid ProductionId { get; init; }
    public required Guid RecipeId { get; init; }
    public required string RecipeDocumentId { get; init; }
    public required Guid RecipeVersionId { get; init; }
    public required string RecipeName { get; init; }

    // What was actually produced
    public required decimal ActualYieldQuantity { get; init; }
    public required string YieldUnit { get; init; }
    public required decimal ActualOutputQuantity { get; init; }
    public required string OutputUnit { get; init; }

    // Output batch created in inventory
    public required Guid OutputBatchId { get; init; }
    public required string OutputBatchNumber { get; init; }
    public required Guid OutputInventoryItemId { get; init; }
    public required string OutputInventoryItemName { get; init; }
    public required DateTime ExpiryDate { get; init; }
    public required int ShelfLifeHours { get; init; }

    // Costing
    public required IReadOnlyList<ProductionIngredientConsumed> IngredientsConsumed { get; init; }
    public required decimal TotalIngredientCost { get; init; }
    public required decimal CostPerOutputUnit { get; init; }

    // Yield variance
    public required decimal ExpectedYieldQuantity { get; init; }
    public required decimal YieldVariance { get; init; }
    public required decimal YieldVariancePercent { get; init; }

    public required Guid CompletedBy { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Ingredient actually consumed during batch prep production.
/// </summary>
public sealed record ProductionIngredientConsumed
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public required decimal ActualQuantity { get; init; }
    public required string Unit { get; init; }
    public required decimal ActualCost { get; init; }
    public required IReadOnlyList<ProductionBatchConsumption> BatchBreakdown { get; init; }
}

/// <summary>
/// Batch breakdown for ingredient consumption during production.
/// </summary>
public sealed record ProductionBatchConsumption
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
}

/// <summary>
/// Batch prep production aborted before completion.
/// </summary>
public sealed record BatchPrepProductionAborted : DomainEvent
{
    public override string EventType => "recipe.batch_prep.production_aborted";
    public override string AggregateType => "BatchPrepProduction";
    public override Guid AggregateId => ProductionId;

    public required Guid ProductionId { get; init; }
    public required Guid RecipeId { get; init; }
    public required string RecipeDocumentId { get; init; }
    public required string RecipeName { get; init; }
    public required BatchPrepAbortReason Reason { get; init; }
    public required string ReasonDetails { get; init; }

    // Ingredients already consumed (if any)
    public IReadOnlyList<ProductionIngredientConsumed>? IngredientsConsumed { get; init; }
    public decimal? WastedCost { get; init; }

    public required Guid AbortedBy { get; init; }
}

/// <summary>
/// Reasons for aborting batch prep production.
/// </summary>
public enum BatchPrepAbortReason
{
    InsufficientIngredients,
    EquipmentFailure,
    QualityFailure,
    StaffError,
    ContaminationRisk,
    Other
}
