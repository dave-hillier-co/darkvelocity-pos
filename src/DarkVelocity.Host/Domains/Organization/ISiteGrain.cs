using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateSiteCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string Code,
    [property: Id(3)] Address Address,
    [property: Id(4)] string Timezone = "America/New_York",
    [property: Id(5)] string Currency = "USD");

[GenerateSerializer]
public record UpdateSiteCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] Address? Address = null,
    [property: Id(2)] OperatingHours? OperatingHours = null,
    [property: Id(3)] SiteSettings? Settings = null);

[GenerateSerializer]
public record SiteCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Code, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record SiteUpdatedResult([property: Id(0)] int Version, [property: Id(1)] DateTime UpdatedAt);

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
