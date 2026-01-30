using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class CustomerLoyalty : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid ProgramId { get; set; }

    public int CurrentPoints { get; set; }
    public int LifetimePoints { get; set; }

    public Guid? CurrentTierId { get; set; }
    public int TierQualifyingPoints { get; set; } // points counting toward next tier
    public DateTime? TierExpiresAt { get; set; }

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public LoyaltyProgram? Program { get; set; }
    public LoyaltyTier? CurrentTier { get; set; }
    public ICollection<PointsTransaction> Transactions { get; set; } = new List<PointsTransaction>();
}
