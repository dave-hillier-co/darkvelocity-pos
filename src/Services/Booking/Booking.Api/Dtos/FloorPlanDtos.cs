using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Booking.Api.Dtos;

public class FloorPlanDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int GridWidth { get; set; }
    public int GridHeight { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int DefaultTurnTimeMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TableCount { get; set; }
    public List<TableDto> Tables { get; set; } = new();
}

public class TableDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid FloorPlanId { get; set; }
    public string? FloorPlanName { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int MinCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public string Shape { get; set; } = string.Empty;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Rotation { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCombinationAllowed { get; set; }
    public bool IsActive { get; set; }
    public int AssignmentPriority { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TableCombinationDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid FloorPlanId { get; set; }
    public string? FloorPlanName { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CombinedCapacity { get; set; }
    public int MinPartySize { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TableDto> Tables { get; set; } = new();
}

// Request DTOs

public record CreateFloorPlanRequest(
    string Name,
    string? Description = null,
    int GridWidth = 20,
    int GridHeight = 15,
    string? BackgroundImageUrl = null,
    int SortOrder = 0,
    int DefaultTurnTimeMinutes = 90);

public record UpdateFloorPlanRequest(
    string? Name = null,
    string? Description = null,
    int? GridWidth = null,
    int? GridHeight = null,
    string? BackgroundImageUrl = null,
    int? SortOrder = null,
    bool? IsActive = null,
    int? DefaultTurnTimeMinutes = null);

public record CreateTableRequest(
    Guid FloorPlanId,
    string TableNumber,
    string? Name = null,
    int MinCapacity = 1,
    int MaxCapacity = 4,
    string Shape = "rectangle",
    int PositionX = 0,
    int PositionY = 0,
    int Width = 2,
    int Height = 1,
    int Rotation = 0,
    bool IsCombinationAllowed = true,
    int AssignmentPriority = 100,
    string? Notes = null);

public record UpdateTableRequest(
    string? TableNumber = null,
    string? Name = null,
    int? MinCapacity = null,
    int? MaxCapacity = null,
    string? Shape = null,
    int? PositionX = null,
    int? PositionY = null,
    int? Width = null,
    int? Height = null,
    int? Rotation = null,
    string? Status = null,
    bool? IsCombinationAllowed = null,
    bool? IsActive = null,
    int? AssignmentPriority = null,
    string? Notes = null);

public record UpdateTableStatusRequest(
    string Status);

public record CreateTableCombinationRequest(
    Guid FloorPlanId,
    string Name,
    List<Guid> TableIds,
    int CombinedCapacity,
    int MinPartySize,
    string? Notes = null);

public record UpdateTableCombinationRequest(
    string? Name = null,
    int? CombinedCapacity = null,
    int? MinPartySize = null,
    bool? IsActive = null,
    string? Notes = null);

public record AddTableToCombinationRequest(
    Guid TableId,
    int Position);

public record RemoveTableFromCombinationRequest(
    Guid TableId);
