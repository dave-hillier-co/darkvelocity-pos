using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class JournalEntryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public DateTime PostedAt { get; set; }
    public JournalEntrySourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string Currency { get; set; } = string.Empty;
    public JournalEntryStatus Status { get; set; }
    public Guid? ReversedByEntryId { get; set; }
    public Guid? ReversesEntryId { get; set; }
    public Guid? FiscalTransactionId { get; set; }
    public Guid? AccountingPeriodId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto : HalResource
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? TaxCode { get; set; }
    public decimal? TaxAmount { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Description { get; set; }
    public int LineNumber { get; set; }
}

// Request DTOs

public record CreateJournalEntryRequest(
    DateOnly EntryDate,
    string Description,
    string Currency,
    List<CreateJournalEntryLineRequest> Lines,
    JournalEntrySourceType SourceType = JournalEntrySourceType.Manual,
    Guid? SourceId = null,
    Guid? FiscalTransactionId = null);

public record CreateJournalEntryLineRequest(
    string AccountCode,
    decimal DebitAmount,
    decimal CreditAmount,
    string? TaxCode = null,
    decimal? TaxAmount = null,
    Guid? CostCenterId = null,
    string? Description = null);

public record ReverseJournalEntryRequest(
    string Reason);
