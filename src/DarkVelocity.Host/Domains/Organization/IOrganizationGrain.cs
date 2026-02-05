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
public record UpdateBrandingCommand(
    [property: Id(0)] string? LogoUrl = null,
    [property: Id(1)] string? FaviconUrl = null,
    [property: Id(2)] string? PrimaryColor = null,
    [property: Id(3)] string? SecondaryColor = null,
    [property: Id(4)] string? AccentColor = null,
    [property: Id(5)] string? CustomCss = null);

[GenerateSerializer]
public record InitiateCancellationCommand(
    [property: Id(0)] string? Reason = null,
    [property: Id(1)] bool Immediate = false,
    [property: Id(2)] Guid? InitiatedBy = null);

[GenerateSerializer]
public record ChangeSlugCommand(
    [property: Id(0)] string NewSlug,
    [property: Id(1)] Guid? ChangedBy = null);

[GenerateSerializer]
public record OrganizationCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Slug, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record OrganizationUpdatedResult([property: Id(0)] int Version, [property: Id(1)] DateTime UpdatedAt);
[GenerateSerializer]
public record CancellationResult([property: Id(0)] DateTime EffectiveDate, [property: Id(1)] DateTime DataRetentionEndDate);

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

    // Extended methods for Organization domain improvements
    Task UpdateBrandingAsync(UpdateBrandingCommand command);
    Task ConfigureCustomDomainAsync(string domain);
    Task VerifyCustomDomainAsync();
    Task SetFeatureFlagAsync(string featureName, bool enabled);
    Task<bool> GetFeatureFlagAsync(string featureName);
    Task<CancellationResult> InitiateCancellationAsync(InitiateCancellationCommand command);
    Task ReactivateFromCancellationAsync(Guid? reactivatedBy = null);
    Task ChangeSlugAsync(ChangeSlugCommand command);
    Task<int> GetVersionAsync();
}
