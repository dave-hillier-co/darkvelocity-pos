using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateOrgRequest(string Name, string Slug, OrganizationSettings? Settings = null);
public record UpdateOrgRequest(string? Name = null, OrganizationSettings? Settings = null);
public record SuspendOrgRequest(string Reason);

public record CreateSiteRequest(
    string Name,
    string Code,
    Address Address,
    string Timezone = "America/New_York",
    string Currency = "USD");

public record UpdateSiteRequest(
    string? Name = null,
    Address? Address = null,
    OperatingHours? OperatingHours = null,
    SiteSettings? Settings = null);
