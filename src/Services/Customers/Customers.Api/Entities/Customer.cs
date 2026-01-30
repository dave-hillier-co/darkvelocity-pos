using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class Customer : BaseSoftDeleteEntity
{
    public Guid TenantId { get; set; }
    public string? ExternalId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string PreferredLanguage { get; set; } = "en";

    public bool MarketingOptIn { get; set; }
    public bool SmsOptIn { get; set; }

    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public string Source { get; set; } = "pos"; // pos, online, import, reservation

    public Guid? DefaultLocationId { get; set; }

    public DateTime? LastVisitAt { get; set; }
    public int TotalVisits { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal AverageOrderValue { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation properties
    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    public ICollection<CustomerLoyalty> LoyaltyMemberships { get; set; } = new List<CustomerLoyalty>();
    public ICollection<CustomerReward> Rewards { get; set; } = new List<CustomerReward>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
