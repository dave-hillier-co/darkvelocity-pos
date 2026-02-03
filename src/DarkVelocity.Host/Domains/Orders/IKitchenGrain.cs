using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateKitchenTicketCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid OrderId,
    [property: Id(3)] string OrderNumber,
    [property: Id(4)] OrderType OrderType,
    [property: Id(5)] string? TableNumber = null,
    [property: Id(6)] int? GuestCount = null,
    [property: Id(7)] string? ServerName = null,
    [property: Id(8)] string? Notes = null,
    [property: Id(9)] TicketPriority Priority = TicketPriority.Normal,
    [property: Id(10)] int CourseNumber = 1);

[GenerateSerializer]
public record AddTicketItemCommand(
    [property: Id(0)] Guid OrderLineId,
    [property: Id(1)] Guid MenuItemId,
    [property: Id(2)] string Name,
    [property: Id(3)] int Quantity,
    [property: Id(4)] List<string>? Modifiers = null,
    [property: Id(5)] string? SpecialInstructions = null,
    [property: Id(6)] Guid? StationId = null,
    [property: Id(7)] string? StationName = null,
    [property: Id(8)] int? CourseNumber = null);

[GenerateSerializer]
public record StartItemCommand([property: Id(0)] Guid ItemId, [property: Id(1)] Guid? PreparedBy = null);
[GenerateSerializer]
public record CompleteItemCommand([property: Id(0)] Guid ItemId, [property: Id(1)] Guid PreparedBy);
[GenerateSerializer]
public record VoidItemCommand([property: Id(0)] Guid ItemId, [property: Id(1)] string Reason);

[GenerateSerializer]
public record KitchenTicketCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string TicketNumber, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record TicketTimings([property: Id(0)] TimeSpan? WaitTime, [property: Id(1)] TimeSpan? PrepTime, [property: Id(2)] DateTime? CompletedAt);

public interface IKitchenTicketGrain : IGrainWithStringKey
{
    Task<KitchenTicketCreatedResult> CreateAsync(CreateKitchenTicketCommand command);
    Task<KitchenTicketState> GetStateAsync();

    // Item management
    Task AddItemAsync(AddTicketItemCommand command);
    Task StartItemAsync(StartItemCommand command);
    Task CompleteItemAsync(CompleteItemCommand command);
    Task VoidItemAsync(VoidItemCommand command);

    // Ticket operations
    Task ReceiveAsync();
    Task StartAsync();
    Task BumpAsync(Guid bumpedBy);
    Task VoidAsync(string reason);

    // Priority
    Task SetPriorityAsync(TicketPriority priority);
    Task MarkRushAsync();
    Task MarkVipAsync();
    Task FireAllAsync();

    // Queries
    Task<bool> ExistsAsync();
    Task<TicketStatus> GetStatusAsync();
    Task<TicketTimings> GetTimingsAsync();
    Task<IReadOnlyList<TicketItem>> GetPendingItemsAsync();
}

[GenerateSerializer]
public record OpenStationCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Name,
    [property: Id(3)] StationType Type,
    [property: Id(4)] int DisplayOrder = 0);

[GenerateSerializer]
public record AssignItemsToStationCommand(
    [property: Id(0)] List<Guid>? MenuItemCategories = null,
    [property: Id(1)] List<Guid>? MenuItemIds = null);

public interface IKitchenStationGrain : IGrainWithStringKey
{
    Task OpenAsync(OpenStationCommand command);
    Task<KitchenStationState> GetStateAsync();

    Task AssignItemsAsync(AssignItemsToStationCommand command);
    Task SetPrinterAsync(Guid printerId);
    Task SetDisplayAsync(Guid displayId);

    Task ReceiveTicketAsync(Guid ticketId);
    Task CompleteTicketAsync(Guid ticketId);
    Task RemoveTicketAsync(Guid ticketId);

    Task PauseAsync();
    Task ResumeAsync();
    Task CloseAsync(Guid closedBy);

    Task<bool> IsOpenAsync();
    Task<int> GetActiveItemCountAsync();
    Task<IReadOnlyList<Guid>> GetCurrentTicketIdsAsync();
    Task<bool> ExistsAsync();
}
