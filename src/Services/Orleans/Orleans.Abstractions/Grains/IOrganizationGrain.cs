using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateOrganizationCommand(
    string Name,
    string Slug,
    OrganizationSettings? Settings = null);

public record UpdateOrganizationCommand(
    string? Name = null,
    OrganizationSettings? Settings = null);

public record OrganizationCreatedResult(Guid Id, string Slug, DateTime CreatedAt);
public record OrganizationUpdatedResult(int Version, DateTime UpdatedAt);

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
