namespace DarkVelocity.Host.Events;

// ============================================================================
// Domain Event Interfaces for Event Sourcing
// ============================================================================

/// <summary>
/// Base interface for Recipe Document domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IRecipeDocumentEvent
{
    string DocumentId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base interface for Recipe Category Document domain events.
/// </summary>
public interface IRecipeCategoryDocumentEvent
{
    string DocumentId { get; }
    DateTimeOffset OccurredAt { get; }
}

// ============================================================================
// Recipe Document Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A recipe document was initialized.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDocumentInitialized(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] string? Description,
    [property: Id(5)] decimal PortionYield,
    [property: Id(6)] string YieldUnit,
    [property: Id(7)] List<RecipeIngredientData>? Ingredients,
    [property: Id(8)] List<string>? AllergenTags,
    [property: Id(9)] List<string>? DietaryTags,
    [property: Id(10)] string? PrepInstructions,
    [property: Id(11)] int PrepTimeMinutes,
    [property: Id(12)] int CookTimeMinutes,
    [property: Id(13)] string? ImageUrl,
    [property: Id(14)] Guid? CategoryId,
    [property: Id(15)] string Locale,
    [property: Id(16)] Guid? CreatedBy,
    [property: Id(17)] bool PublishImmediately
) : IRecipeDocumentEvent;

/// <summary>
/// Data transfer object for recipe ingredients in events.
/// </summary>
[GenerateSerializer]
public sealed record RecipeIngredientData(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string Unit,
    [property: Id(4)] decimal WastePercentage,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] string? PrepInstructions,
    [property: Id(7)] bool IsOptional,
    [property: Id(8)] int DisplayOrder,
    [property: Id(9)] List<Guid>? SubstitutionIds
);

/// <summary>
/// Domain event: A recipe draft version was created.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDraftVersionCreated(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] Guid? CreatedBy,
    [property: Id(4)] string? ChangeNote,
    [property: Id(5)] string? Name,
    [property: Id(6)] string? Description,
    [property: Id(7)] decimal? PortionYield,
    [property: Id(8)] string? YieldUnit,
    [property: Id(9)] List<RecipeIngredientData>? Ingredients,
    [property: Id(10)] List<string>? AllergenTags,
    [property: Id(11)] List<string>? DietaryTags,
    [property: Id(12)] string? PrepInstructions,
    [property: Id(13)] int? PrepTimeMinutes,
    [property: Id(14)] int? CookTimeMinutes,
    [property: Id(15)] string? ImageUrl,
    [property: Id(16)] Guid? CategoryId
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe draft was published.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDraftWasPublished(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int PublishedVersion,
    [property: Id(3)] int? PreviousPublishedVersion,
    [property: Id(4)] Guid? PublishedBy,
    [property: Id(5)] string? Note
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe draft was discarded.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDraftWasDiscarded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int DiscardedVersion
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe reverted to a previous version.
/// </summary>
[GenerateSerializer]
public sealed record RecipeRevertedToVersion(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int FromVersion,
    [property: Id(3)] int ToVersion,
    [property: Id(4)] int NewVersionNumber,
    [property: Id(5)] Guid? RevertedBy,
    [property: Id(6)] string? Reason
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe translation was added.
/// </summary>
[GenerateSerializer]
public sealed record RecipeTranslationAdded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string Locale,
    [property: Id(3)] string Name,
    [property: Id(4)] string? Description
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe translation was removed.
/// </summary>
[GenerateSerializer]
public sealed record RecipeTranslationRemoved(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string Locale
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe change was scheduled.
/// </summary>
[GenerateSerializer]
public sealed record RecipeChangeWasScheduled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId,
    [property: Id(3)] int VersionToActivate,
    [property: Id(4)] DateTimeOffset ActivateAt,
    [property: Id(5)] DateTimeOffset? DeactivateAt,
    [property: Id(6)] string? Name
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: A recipe schedule was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record RecipeScheduleWasCancelled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe was archived.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDocumentWasArchived(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record RecipeDocumentWasRestored(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe cost was recalculated.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCostWasRecalculated(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] decimal PreviousCost,
    [property: Id(4)] decimal NewCost,
    [property: Id(5)] Dictionary<Guid, decimal>? UpdatedIngredientPrices
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe was linked to a menu item.
/// </summary>
[GenerateSerializer]
public sealed record RecipeLinkedToMenu(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string MenuItemDocumentId
) : IRecipeDocumentEvent;

/// <summary>
/// Domain event: Recipe was unlinked from a menu item.
/// </summary>
[GenerateSerializer]
public sealed record RecipeUnlinkedFromMenu(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string MenuItemDocumentId
) : IRecipeDocumentEvent;

// ============================================================================
// Recipe Category Document Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A recipe category document was initialized.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDocumentInitialized(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DateTimeOffset OccurredAt,
    [property: Id(3)] string Name,
    [property: Id(4)] int DisplayOrder,
    [property: Id(5)] string? Description,
    [property: Id(6)] string? Color,
    [property: Id(7)] string? IconUrl,
    [property: Id(8)] string Locale,
    [property: Id(9)] Guid? CreatedBy,
    [property: Id(10)] bool PublishImmediately
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe category draft version was created.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDraftVersionCreated(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int VersionNumber,
    [property: Id(3)] Guid? CreatedBy,
    [property: Id(4)] string? ChangeNote,
    [property: Id(5)] string? Name,
    [property: Id(6)] int? DisplayOrder,
    [property: Id(7)] string? Description,
    [property: Id(8)] string? Color,
    [property: Id(9)] string? IconUrl,
    [property: Id(10)] List<string>? RecipeDocumentIds
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe category draft was published.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDraftWasPublished(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int PublishedVersion,
    [property: Id(3)] int? PreviousPublishedVersion,
    [property: Id(4)] Guid? PublishedBy,
    [property: Id(5)] string? Note
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe category draft was discarded.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDraftWasDiscarded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int DiscardedVersion
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: Recipe category reverted to a previous version.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryRevertedToVersion(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] int FromVersion,
    [property: Id(3)] int ToVersion,
    [property: Id(4)] int NewVersionNumber,
    [property: Id(5)] Guid? RevertedBy,
    [property: Id(6)] string? Reason
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe was added to the category.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryRecipeAdded(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string RecipeDocumentId
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe was removed from the category.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryRecipeRemoved(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string RecipeDocumentId
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: Recipes were reordered in the category.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryRecipesReordered(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] List<string> RecipeDocumentIds
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe category change was scheduled.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryChangeWasScheduled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId,
    [property: Id(3)] int VersionToActivate,
    [property: Id(4)] DateTimeOffset ActivateAt,
    [property: Id(5)] DateTimeOffset? DeactivateAt,
    [property: Id(6)] string? Name
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: A recipe category schedule was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryScheduleWasCancelled(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string ScheduleId
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: Recipe category was archived.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDocumentWasArchived(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ArchivedBy,
    [property: Id(3)] string? Reason
) : IRecipeCategoryDocumentEvent;

/// <summary>
/// Domain event: Recipe category was restored from archive.
/// </summary>
[GenerateSerializer]
public sealed record RecipeCategoryDocumentWasRestored(
    [property: Id(0)] string DocumentId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? RestoredBy
) : IRecipeCategoryDocumentEvent;

// ============================================================================
// Integration Events (for external notifications, NOT event sourcing)
// ============================================================================

// ============================================================================
// Recipe Document Integration Events
// ============================================================================

/// <summary>
/// A new recipe document was created.
/// </summary>
public sealed record RecipeDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    decimal CostPerPortion,
    Guid? CategoryId
) : IntegrationEvent
{
    public override string EventType => "recipe.document.created";
}

/// <summary>
/// A new draft version was created for a recipe document.
/// </summary>
public sealed record RecipeDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "recipe.draft.created";
}

/// <summary>
/// A recipe draft was published to become the live version.
/// </summary>
public sealed record RecipeDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy,
    decimal CostPerPortion
) : IntegrationEvent
{
    public override string EventType => "recipe.draft.published";
}

/// <summary>
/// A recipe was reverted to a previous version.
/// </summary>
public sealed record RecipeDocumentReverted(
    string DocumentId,
    Guid OrgId,
    int FromVersion,
    int ToVersion,
    Guid? RevertedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.version.reverted";
}

/// <summary>
/// A scheduled change was created for a recipe.
/// </summary>
public sealed record RecipeChangeScheduled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    int Version,
    DateTimeOffset ActivateAt,
    DateTimeOffset? DeactivateAt
) : IntegrationEvent
{
    public override string EventType => "recipe.change.scheduled";
}

/// <summary>
/// A scheduled change was cancelled for a recipe.
/// </summary>
public sealed record RecipeScheduleCancelled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    Guid? CancelledBy
) : IntegrationEvent
{
    public override string EventType => "recipe.schedule.cancelled";
}

/// <summary>
/// A recipe document was archived.
/// </summary>
public sealed record RecipeDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.document.archived";
}

/// <summary>
/// A recipe document was restored from archive.
/// </summary>
public sealed record RecipeDocumentRestored(
    string DocumentId,
    Guid OrgId,
    Guid? RestoredBy
) : IntegrationEvent
{
    public override string EventType => "recipe.document.restored";
}

/// <summary>
/// A recipe's cost was recalculated.
/// </summary>
public sealed record RecipeCostRecalculated(
    string DocumentId,
    Guid OrgId,
    int Version,
    decimal PreviousCost,
    decimal NewCost,
    decimal CostChange,
    decimal CostChangePercent
) : IntegrationEvent
{
    public override string EventType => "recipe.cost.recalculated";
}

/// <summary>
/// A recipe was linked to a menu item.
/// </summary>
public sealed record RecipeLinkedToMenuItem(
    string RecipeDocumentId,
    string MenuItemDocumentId,
    Guid OrgId
) : IntegrationEvent
{
    public override string EventType => "recipe.menu_item.linked";
}

/// <summary>
/// A recipe was unlinked from a menu item.
/// </summary>
public sealed record RecipeUnlinkedFromMenuItem(
    string RecipeDocumentId,
    string MenuItemDocumentId,
    Guid OrgId
) : IntegrationEvent
{
    public override string EventType => "recipe.menu_item.unlinked";
}

// ============================================================================
// Recipe Category Document Events
// ============================================================================

/// <summary>
/// A new recipe category document was created.
/// </summary>
public sealed record RecipeCategoryDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    int DisplayOrder
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.created";
}

/// <summary>
/// A new draft version was created for a recipe category document.
/// </summary>
public sealed record RecipeCategoryDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "recipe.category.draft.created";
}

/// <summary>
/// A recipe category draft was published to become the live version.
/// </summary>
public sealed record RecipeCategoryDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy
) : IntegrationEvent
{
    public override string EventType => "recipe.category.draft.published";
}

/// <summary>
/// A recipe category document was archived.
/// </summary>
public sealed record RecipeCategoryDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.archived";
}

/// <summary>
/// A recipe category document was restored from archive.
/// </summary>
public sealed record RecipeCategoryDocumentRestored(
    string DocumentId,
    Guid OrgId,
    Guid? RestoredBy
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.restored";
}
