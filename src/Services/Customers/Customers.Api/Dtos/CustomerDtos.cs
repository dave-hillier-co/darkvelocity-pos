using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Customers.Api.Dtos;

// Customer DTOs
public class CustomerDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? ExternalId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool MarketingOptIn { get; set; }
    public bool SmsOptIn { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? DefaultLocationId { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public int TotalVisits { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CustomerAddressDto> Addresses { get; set; } = new();
}

public class CustomerSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FullName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public int TotalVisits { get; set; }
    public decimal TotalSpend { get; set; }
    public DateTime? LastVisitAt { get; set; }
}

public class CreateCustomerRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool MarketingOptIn { get; set; }
    public bool SmsOptIn { get; set; }
    public List<string>? Tags { get; set; }
    public string? Notes { get; set; }
    public string Source { get; set; } = "pos";
    public Guid? DefaultLocationId { get; set; }
    public string? ExternalId { get; set; }
}

public class UpdateCustomerRequest
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool? MarketingOptIn { get; set; }
    public bool? SmsOptIn { get; set; }
    public List<string>? Tags { get; set; }
    public string? Notes { get; set; }
    public Guid? DefaultLocationId { get; set; }
}

public class CustomerLookupRequest
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class MergeCustomersRequest
{
    public Guid PrimaryCustomerId { get; set; }
    public Guid SecondaryCustomerId { get; set; }
}

// CustomerAddress DTOs
public class CustomerAddressDto : HalResource
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string? Street2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? DeliveryInstructions { get; set; }
}

public class CreateAddressRequest
{
    public string Label { get; set; } = "home";
    public string Street { get; set; } = string.Empty;
    public string? Street2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? DeliveryInstructions { get; set; }
}

public class UpdateAddressRequest
{
    public string? Label { get; set; }
    public string? Street { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool? IsDefault { get; set; }
    public string? DeliveryInstructions { get; set; }
}
