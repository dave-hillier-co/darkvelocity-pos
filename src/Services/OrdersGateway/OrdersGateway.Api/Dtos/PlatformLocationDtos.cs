using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Response DTO for a platform-location mapping.
/// </summary>
public class PlatformLocationDto : HalResource
{
    public Guid Id { get; set; }
    public Guid DeliveryPlatformId { get; set; }
    public Guid LocationId { get; set; }
    public string PlatformStoreId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? MenuMappingId { get; set; }
    public OperatingHoursOverride? OperatingHoursOverride { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to map a location to a delivery platform.
/// </summary>
public record MapLocationRequest(
    Guid LocationId,
    string PlatformStoreId,
    Guid? MenuMappingId = null,
    OperatingHoursOverride? OperatingHoursOverride = null);

/// <summary>
/// Request to update a platform-location mapping.
/// </summary>
public record UpdatePlatformLocationRequest(
    string? PlatformStoreId = null,
    bool? IsActive = null,
    Guid? MenuMappingId = null,
    OperatingHoursOverride? OperatingHoursOverride = null);

/// <summary>
/// Operating hours override for a specific day.
/// </summary>
public class OperatingHoursOverride
{
    public DayHours? Monday { get; set; }
    public DayHours? Tuesday { get; set; }
    public DayHours? Wednesday { get; set; }
    public DayHours? Thursday { get; set; }
    public DayHours? Friday { get; set; }
    public DayHours? Saturday { get; set; }
    public DayHours? Sunday { get; set; }
}

/// <summary>
/// Operating hours for a single day.
/// </summary>
public class DayHours
{
    public bool IsClosed { get; set; }
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
}
