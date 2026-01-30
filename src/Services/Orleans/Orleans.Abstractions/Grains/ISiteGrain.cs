using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateSiteCommand(
    Guid OrganizationId,
    string Name,
    string Code,
    Address Address,
    string Timezone = "America/New_York",
    string Currency = "USD");

public record UpdateSiteCommand(
    string? Name = null,
    Address? Address = null,
    OperatingHours? OperatingHours = null,
    SiteSettings? Settings = null);

public record SiteCreatedResult(Guid Id, string Code, DateTime CreatedAt);
public record SiteUpdatedResult(int Version, DateTime UpdatedAt);

public interface ISiteGrain : IGrainWithStringKey
{
    Task<SiteCreatedResult> CreateAsync(CreateSiteCommand command);
    Task<SiteUpdatedResult> UpdateAsync(UpdateSiteCommand command);
    Task<SiteState> GetStateAsync();
    Task OpenAsync();
    Task CloseAsync();
    Task CloseTemporarilyAsync(string reason);
    Task SetActiveMenuAsync(Guid menuId);
    Task AddFloorAsync(Guid floorId);
    Task RemoveFloorAsync(Guid floorId);
    Task AddStationAsync(Guid stationId);
    Task RemoveStationAsync(Guid stationId);
    Task<bool> ExistsAsync();
    Task<bool> IsOpenAsync();
}
