using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateOrganizationCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Slug,
    [property: Id(2)] OrganizationSettings? Settings = null);

[GenerateSerializer]
public record UpdateOrganizationCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] OrganizationSettings? Settings = null);

[GenerateSerializer]
public record OrganizationCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Slug, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record OrganizationUpdatedResult([property: Id(0)] int Version, [property: Id(1)] DateTime UpdatedAt);

public interface IOrganizationGrain : IGrainWithStringKey
{
    Task<OrganizationCreatedResult> CreateAsync(CreateOrganizationCommand command);
    Task<OrganizationUpdatedResult> UpdateAsync(UpdateOrganizationCommand command);
    Task<OrganizationState> GetStateAsync();
    Task SuspendAsync(string reason);
    Task ReactivateAsync();
    Task<Guid> AddSiteAsync(Guid siteId);
    Task RemoveSiteAsync(Guid siteId);
    Task<IReadOnlyList<Guid>> GetSiteIdsAsync();
    Task<bool> ExistsAsync();
}
