using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class ReconciliationDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public ReconciliationType ReconciliationType { get; set; }
    public DateOnly Date { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public ReconciliationStatus Status { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Request DTOs

public record CreateReconciliationRequest(
    ReconciliationType ReconciliationType,
    DateOnly Date,
    decimal ExpectedAmount,
    decimal ActualAmount,
    string Currency = "EUR",
    string? ExternalReference = null);

public record UpdateReconciliationRequest(
    decimal? ActualAmount = null,
    string? ExternalReference = null);

public record ResolveReconciliationRequest(
    string ResolutionNotes);
