using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Table Request DTOs
// ============================================================================

public record CreateTableRequest(
    string Number,
    int MinCapacity = 1,
    int MaxCapacity = 4,
    string? Name = null,
    TableShape Shape = TableShape.Square,
    Guid? FloorPlanId = null);

public record UpdateTableRequest(
    string? Number = null,
    string? Name = null,
    int? MinCapacity = null,
    int? MaxCapacity = null,
    TableShape? Shape = null,
    TablePosition? Position = null,
    bool? IsCombinable = null,
    int? SortOrder = null,
    Guid? SectionId = null);

public record SeatTableRequest(
    Guid? BookingId,
    Guid? OrderId,
    string? GuestName,
    int GuestCount,
    Guid? ServerId = null);

public record SetTableStatusRequest(TableStatus Status);

// ============================================================================
// Floor Plan Request DTOs
// ============================================================================

public record CreateFloorPlanRequest(
    string Name,
    bool IsDefault = false,
    int Width = 800,
    int Height = 600);

public record UpdateFloorPlanRequest(
    string? Name = null,
    int? Width = null,
    int? Height = null,
    string? BackgroundImageUrl = null,
    bool? IsActive = null);

public record AddTableToFloorPlanRequest(Guid TableId);

// ============================================================================
// Waitlist Request DTOs
// ============================================================================

public record AddToWaitlistRequest(
    GuestInfo Guest,
    int PartySize,
    TimeSpan QuotedWait,
    string? TablePreferences = null,
    NotificationMethod NotificationMethod = NotificationMethod.Sms);

public record SeatWaitlistEntryRequest(Guid TableId);
