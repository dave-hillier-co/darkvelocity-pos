using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class AccountingPeriodDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public PeriodType PeriodType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PeriodStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int JournalEntryCount { get; set; }
}

// Request DTOs

public record CreateAccountingPeriodRequest(
    PeriodType PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes = null);

public record CloseAccountingPeriodRequest(
    string? Notes = null);

public record LockAccountingPeriodRequest(
    string? Notes = null);
