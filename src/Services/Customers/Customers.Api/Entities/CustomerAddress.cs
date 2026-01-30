using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class CustomerAddress : BaseEntity
{
    public Guid CustomerId { get; set; }
    public string Label { get; set; } = "home"; // home, work, other
    public string Street { get; set; } = string.Empty;
    public string? Street2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? DeliveryInstructions { get; set; }

    // Navigation property
    public Customer? Customer { get; set; }
}
