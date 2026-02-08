using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// Table Management Commands
[GenerateSerializer]
public record CreateTableCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Number,
    [property: Id(3)] int MinCapacity = 1,
    [property: Id(4)] int MaxCapacity = 4,
    [property: Id(5)] string? Name = null,
    [property: Id(6)] TableShape Shape = TableShape.Square,
    [property: Id(7)] Guid? FloorPlanId = null);

[GenerateSerializer]
public record UpdateTableCommand(
    [property: Id(0)] string? Number = null,
    [property: Id(1)] string? Name = null,
    [property: Id(2)] int? MinCapacity = null,
    [property: Id(3)] int? MaxCapacity = null,
    [property: Id(4)] TableShape? Shape = null,
    [property: Id(5)] TablePosition? Position = null,
    [property: Id(6)] bool? IsCombinable = null,
    [property: Id(7)] int? SortOrder = null,
    [property: Id(8)] Guid? SectionId = null);

[GenerateSerializer]
public record SeatTableCommand(
    [property: Id(0)] Guid? BookingId,
    [property: Id(1)] Guid? OrderId,
    [property: Id(2)] string? GuestName,
    [property: Id(3)] int GuestCount,
    [property: Id(4)] Guid? ServerId = null);

[GenerateSerializer]
public record TableCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Number, [property: Id(2)] DateTime CreatedAt);

public interface ITableGrain : IGrainWithStringKey
{
    Task<TableCreatedResult> CreateAsync(CreateTableCommand command);
    Task<TableState> GetStateAsync();
    Task UpdateAsync(UpdateTableCommand command);
    Task DeleteAsync();

    // Status management
    Task SetStatusAsync(TableStatus status);
    Task SeatAsync(SeatTableCommand command);
    Task ClearAsync();
    Task MarkDirtyAsync();
    Task MarkCleanAsync();
    Task BlockAsync(string? reason = null);
    Task UnblockAsync();

    // Table combinations
    Task CombineWithAsync(Guid otherTableId);
    Task UncombineAsync();

    // Tags
    Task AddTagAsync(string tag);
    Task RemoveTagAsync(string tag);

    // Position (for floor plan)
    Task SetPositionAsync(TablePosition position);
    Task SetFloorPlanAsync(Guid floorPlanId);
    Task SetSectionAsync(Guid? sectionId);

    // Queries
    Task<bool> ExistsAsync();
    Task<TableStatus> GetStatusAsync();
    Task<bool> IsAvailableAsync();
    Task<TableOccupancy?> GetOccupancyAsync();
}

// Floor Plan Commands
[GenerateSerializer]
public record CreateFloorPlanCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Name,
    [property: Id(3)] bool IsDefault = false,
    [property: Id(4)] int Width = 800,
    [property: Id(5)] int Height = 600);

[GenerateSerializer]
public record UpdateFloorPlanCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] int? Width = null,
    [property: Id(2)] int? Height = null,
    [property: Id(3)] string? BackgroundImageUrl = null,
    [property: Id(4)] bool? IsActive = null);

[GenerateSerializer]
public record FloorPlanCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Name, [property: Id(2)] DateTime CreatedAt);

public interface IFloorPlanGrain : IGrainWithStringKey
{
    Task<FloorPlanCreatedResult> CreateAsync(CreateFloorPlanCommand command);
    Task<FloorPlanState> GetStateAsync();
    Task UpdateAsync(UpdateFloorPlanCommand command);
    Task DeleteAsync();

    // Table management
    Task AddTableAsync(Guid tableId);
    Task RemoveTableAsync(Guid tableId);
    Task<IReadOnlyList<Guid>> GetTableIdsAsync();

    // Sections
    Task AddSectionAsync(string name, string? color = null);
    Task RemoveSectionAsync(Guid sectionId);
    Task UpdateSectionAsync(Guid sectionId, string? name = null, string? color = null, int? sortOrder = null);
    Task AssignTableToSectionAsync(Guid tableId, Guid sectionId);
    Task UnassignTableFromSectionAsync(Guid tableId);

    // Status
    Task SetDefaultAsync();
    Task ActivateAsync();
    Task DeactivateAsync();

    // Queries
    Task<bool> ExistsAsync();
    Task<bool> IsActiveAsync();
}

// Booking Availability Commands
[GenerateSerializer]
public record GetAvailabilityQuery(
    [property: Id(0)] DateOnly Date,
    [property: Id(1)] int PartySize,
    [property: Id(2)] TimeOnly? PreferredTime = null);

[GenerateSerializer]
public record UpdateBookingSettingsCommand(
    [property: Id(0)] TimeOnly? DefaultOpenTime = null,
    [property: Id(1)] TimeOnly? DefaultCloseTime = null,
    [property: Id(2)] TimeSpan? DefaultDuration = null,
    [property: Id(3)] TimeSpan? SlotInterval = null,
    [property: Id(4)] int? MaxPartySizeOnline = null,
    [property: Id(5)] int? MaxBookingsPerSlot = null,
    [property: Id(6)] int? AdvanceBookingDays = null,
    [property: Id(7)] bool? RequireDeposit = null,
    [property: Id(8)] decimal? DepositAmount = null);

public interface IBookingSettingsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);
    Task<BookingSettingsState> GetStateAsync();
    Task UpdateAsync(UpdateBookingSettingsCommand command);

    // Availability queries
    Task<IReadOnlyList<AvailabilitySlot>> GetAvailabilityAsync(GetAvailabilityQuery query);
    Task<bool> IsSlotAvailableAsync(DateOnly date, TimeOnly time, int partySize);

    // Blocked dates
    Task BlockDateAsync(DateOnly date);
    Task UnblockDateAsync(DateOnly date);
    Task<bool> IsDateBlockedAsync(DateOnly date);

    Task<bool> ExistsAsync();
}
