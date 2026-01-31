namespace DarkVelocity.Shared.Infrastructure.Events;

public sealed record StockBatchCreated(
    Guid BatchId,
    Guid IngredientId,
    Guid LocationId,
    Guid DeliveryId,
    decimal Quantity,
    decimal UnitCost,
    DateTime? ExpiryDate
) : IntegrationEvent
{
    public override string EventType => "inventory.batch.created";
}

public sealed record StockBatchConsumed(
    Guid BatchId,
    Guid IngredientId,
    decimal QuantityConsumed,
    decimal RemainingQuantity,
    Guid? RelatedOrderId
) : IntegrationEvent
{
    public override string EventType => "inventory.batch.consumed";
}

public sealed record StockBatchExhausted(
    Guid BatchId,
    Guid IngredientId
) : IntegrationEvent
{
    public override string EventType => "inventory.batch.exhausted";
}

public sealed record StockConsumedForSale(
    Guid OrderId,
    Guid LocationId,
    List<IngredientConsumption> Consumptions,
    decimal TotalCOGS
) : IntegrationEvent
{
    public override string EventType => "inventory.stock.consumed_for_sale";
}

public sealed record IngredientConsumption(
    Guid IngredientId,
    string IngredientName,
    decimal QuantityConsumed,
    decimal TotalCost,
    List<BatchConsumption> BatchConsumptions
);

public sealed record BatchConsumption(
    Guid BatchId,
    decimal Quantity,
    decimal UnitCost,
    decimal Cost,
    decimal RemainingQuantity
);

public sealed record RecipeCostRecalculated(
    Guid RecipeId,
    Guid MenuItemId,
    decimal NewCost,
    decimal PreviousCost
) : IntegrationEvent
{
    public override string EventType => "inventory.recipe.cost_recalculated";
}
