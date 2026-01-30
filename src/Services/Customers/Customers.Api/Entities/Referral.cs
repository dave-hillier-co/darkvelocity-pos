using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class Referral : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ReferrerCustomerId { get; set; }
    public Guid? ReferredCustomerId { get; set; }

    public string ReferralCode { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, completed, expired

    public int ReferrerBonus { get; set; } // points awarded to referrer
    public int RefereeBonus { get; set; } // points awarded to new customer

    public DateTime? CompletedAt { get; set; }
    public Guid? FirstOrderId { get; set; }

    // Navigation properties
    public Customer? ReferrerCustomer { get; set; }
    public Customer? ReferredCustomer { get; set; }
}
