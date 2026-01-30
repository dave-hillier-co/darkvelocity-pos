using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateKitchenTicketCommand(
    Guid OrganizationId,
    Guid SiteId,
    Guid OrderId,
    string OrderNumber,
    OrderType OrderType,
    string? TableNumber = null,
    int? GuestCount = null,
    string? ServerName = null,
    string? Notes = null,
    TicketPriority Priority = TicketPriority.Normal,
    int CourseNumber = 1);

public record AddTicketItemCommand(
    Guid OrderLineId,
    Guid MenuItemId,
    string Name,
    int Quantity,
    List<string>? Modifiers = null,
    string? SpecialInstructions = null,
    Guid? StationId = null,
    string? StationName = null,
    int? CourseNumber = null);

public record StartItemCommand(Guid ItemId, Guid? PreparedBy = null);
public record CompleteItemCommand(Guid ItemId, Guid PreparedBy);
public record VoidItemCommand(Guid ItemId, string Reason);

public record KitchenTicketCreatedResult(Guid Id, string TicketNumber, DateTime CreatedAt);
public record TicketTimings(TimeSpan? WaitTime, TimeSpan? PrepTime, DateTime? CompletedAt);

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

public record OpenStationCommand(
    Guid OrganizationId,
    Guid SiteId,
    string Name,
    StationType Type,
    int DisplayOrder = 0);

public record AssignItemsToStationCommand(
    List<Guid>? MenuItemCategories = null,
    List<Guid>? MenuItemIds = null);

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
