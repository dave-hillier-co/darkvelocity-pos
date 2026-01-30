using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Auth.Api.Entities;

public class Location : BaseEntity
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Timezone { get; set; }
    public required string CurrencyCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PosUser> Users { get; set; } = new List<PosUser>();
    public ICollection<UserLocationAccess> UserLocationAccess { get; set; } = new List<UserLocationAccess>();
}
