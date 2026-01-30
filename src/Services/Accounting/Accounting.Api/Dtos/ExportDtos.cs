using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class ExportResultDto : HalResource
{
    public Guid Id { get; set; }
    public string ExportType { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ExportStatus Status { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public int RecordCount { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ExportStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

// Request DTOs

public record ExportDatevRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? LocationId = null,
    string Format = "csv",
    bool IncludeHeaders = true);

public record ExportCsvRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? LocationId = null,
    List<string>? AccountCodes = null,
    bool IncludeHeaders = true);

public record ExportSageRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? LocationId = null);

public record ExportXeroRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? LocationId = null);

public record ExportQuickBooksRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? LocationId = null);
