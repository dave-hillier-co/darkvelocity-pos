using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to initialize the fiscal year.
/// </summary>
[GenerateSerializer]
public record InitializeFiscalYearCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] int Year,
    [property: Id(2)] Guid InitializedBy,
    [property: Id(3)] int FiscalYearStartMonth = 1,
    [property: Id(4)] PeriodFrequency Frequency = PeriodFrequency.Monthly);

/// <summary>
/// Command to open a period.
/// </summary>
[GenerateSerializer]
public record OpenPeriodCommand(
    [property: Id(0)] int PeriodNumber,
    [property: Id(1)] Guid OpenedBy,
    [property: Id(2)] string? Notes = null);

/// <summary>
/// Command to close a period.
/// </summary>
[GenerateSerializer]
public record ClosePeriodCommand2(
    [property: Id(0)] int PeriodNumber,
    [property: Id(1)] Guid ClosedBy,
    [property: Id(2)] string? Notes = null,
    [property: Id(3)] bool Force = false);

/// <summary>
/// Command to reopen a closed period.
/// </summary>
[GenerateSerializer]
public record ReopenPeriodCommand(
    [property: Id(0)] int PeriodNumber,
    [property: Id(1)] Guid ReopenedBy,
    [property: Id(2)] string Reason);

/// <summary>
/// Command to perform year-end closing.
/// </summary>
[GenerateSerializer]
public record YearEndCloseCommand(
    [property: Id(0)] Guid ClosedBy,
    [property: Id(1)] string RetainedEarningsAccountNumber,
    [property: Id(2)] string? Notes = null);

/// <summary>
/// Command to lock a period permanently.
/// </summary>
[GenerateSerializer]
public record LockPeriodCommand(
    [property: Id(0)] int PeriodNumber,
    [property: Id(1)] Guid LockedBy,
    [property: Id(2)] string? Reason = null);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Frequency of accounting periods.
/// </summary>
public enum PeriodFrequency
{
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>
/// Status of an accounting period.
/// </summary>
public enum PeriodStatus
{
    /// <summary>Period not yet started.</summary>
    NotStarted,
    /// <summary>Period is open for posting.</summary>
    Open,
    /// <summary>Period is closed but can be reopened.</summary>
    Closed,
    /// <summary>Period is permanently locked.</summary>
    Locked
}

/// <summary>
/// Represents an accounting period.
/// </summary>
[GenerateSerializer]
public record AccountingPeriod(
    [property: Id(0)] int PeriodNumber,
    [property: Id(1)] string Name,
    [property: Id(2)] DateOnly StartDate,
    [property: Id(3)] DateOnly EndDate,
    [property: Id(4)] PeriodStatus Status,
    [property: Id(5)] DateTime? OpenedAt,
    [property: Id(6)] Guid? OpenedBy,
    [property: Id(7)] DateTime? ClosedAt,
    [property: Id(8)] Guid? ClosedBy,
    [property: Id(9)] DateTime? LockedAt,
    [property: Id(10)] Guid? LockedBy,
    [property: Id(11)] bool IsYearEnd);

/// <summary>
/// Summary of the fiscal year.
/// </summary>
[GenerateSerializer]
public record FiscalYearSummary(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] int Year,
    [property: Id(2)] int FiscalYearStartMonth,
    [property: Id(3)] PeriodFrequency Frequency,
    [property: Id(4)] int TotalPeriods,
    [property: Id(5)] int OpenPeriods,
    [property: Id(6)] int ClosedPeriods,
    [property: Id(7)] int LockedPeriods,
    [property: Id(8)] bool IsYearClosed,
    [property: Id(9)] DateOnly FiscalYearStart,
    [property: Id(10)] DateOnly FiscalYearEnd);

// ============================================================================
// State
// ============================================================================

/// <summary>
/// State for the Accounting Period grain.
/// </summary>
[GenerateSerializer]
public sealed class AccountingPeriodState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public int Year { get; set; }
    [Id(2)] public int FiscalYearStartMonth { get; set; } = 1;
    [Id(3)] public PeriodFrequency Frequency { get; set; } = PeriodFrequency.Monthly;
    [Id(4)] public bool IsInitialized { get; set; }
    [Id(5)] public bool IsYearClosed { get; set; }
    [Id(6)] public Dictionary<int, PeriodState> Periods { get; set; } = [];
    [Id(7)] public DateTime CreatedAt { get; set; }
    [Id(8)] public Guid CreatedBy { get; set; }
    [Id(9)] public DateTime? YearClosedAt { get; set; }
    [Id(10)] public Guid? YearClosedBy { get; set; }
    [Id(11)] public int Version { get; set; }
}

/// <summary>
/// State for a single accounting period.
/// </summary>
[GenerateSerializer]
public sealed class PeriodState
{
    [Id(0)] public int PeriodNumber { get; set; }
    [Id(1)] public string Name { get; set; } = "";
    [Id(2)] public DateOnly StartDate { get; set; }
    [Id(3)] public DateOnly EndDate { get; set; }
    [Id(4)] public PeriodStatus Status { get; set; } = PeriodStatus.NotStarted;
    [Id(5)] public DateTime? OpenedAt { get; set; }
    [Id(6)] public Guid? OpenedBy { get; set; }
    [Id(7)] public DateTime? ClosedAt { get; set; }
    [Id(8)] public Guid? ClosedBy { get; set; }
    [Id(9)] public DateTime? LockedAt { get; set; }
    [Id(10)] public Guid? LockedBy { get; set; }
    [Id(11)] public string? Notes { get; set; }
    [Id(12)] public bool IsYearEnd { get; set; }
}

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain interface for managing accounting periods (fiscal years).
/// Key format: {orgId}:accountingperiod:{year}
/// </summary>
public interface IAccountingPeriodGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the fiscal year with periods.
    /// </summary>
    Task InitializeAsync(InitializeFiscalYearCommand command);

    /// <summary>
    /// Checks if the fiscal year is initialized.
    /// </summary>
    Task<bool> IsInitializedAsync();

    /// <summary>
    /// Opens a period for posting.
    /// </summary>
    Task<AccountingPeriod> OpenPeriodAsync(OpenPeriodCommand command);

    /// <summary>
    /// Closes a period.
    /// </summary>
    Task<AccountingPeriod> ClosePeriodAsync(ClosePeriodCommand2 command);

    /// <summary>
    /// Reopens a closed period.
    /// </summary>
    Task<AccountingPeriod> ReopenPeriodAsync(ReopenPeriodCommand command);

    /// <summary>
    /// Permanently locks a period.
    /// </summary>
    Task<AccountingPeriod> LockPeriodAsync(LockPeriodCommand command);

    /// <summary>
    /// Performs year-end closing.
    /// </summary>
    Task<FiscalYearSummary> YearEndCloseAsync(YearEndCloseCommand command);

    /// <summary>
    /// Gets a specific period.
    /// </summary>
    Task<AccountingPeriod?> GetPeriodAsync(int periodNumber);

    /// <summary>
    /// Gets all periods.
    /// </summary>
    Task<IReadOnlyList<AccountingPeriod>> GetAllPeriodsAsync();

    /// <summary>
    /// Gets the current open period.
    /// </summary>
    Task<AccountingPeriod?> GetCurrentOpenPeriodAsync();

    /// <summary>
    /// Gets the fiscal year summary.
    /// </summary>
    Task<FiscalYearSummary> GetSummaryAsync();

    /// <summary>
    /// Checks if a date can be posted to.
    /// </summary>
    Task<bool> CanPostToDateAsync(DateOnly date);

    /// <summary>
    /// Gets the period containing a specific date.
    /// </summary>
    Task<AccountingPeriod?> GetPeriodForDateAsync(DateOnly date);
}

// ============================================================================
// Grain Implementation
// ============================================================================

/// <summary>
/// Orleans grain for managing accounting periods.
/// </summary>
public class AccountingPeriodGrain : Grain, IAccountingPeriodGrain
{
    private readonly IPersistentState<AccountingPeriodState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AccountingPeriodGrain> _logger;

    public AccountingPeriodGrain(
        [PersistentState("accounting-period", "OrleansStorage")]
        IPersistentState<AccountingPeriodState> state,
        IGrainFactory grainFactory,
        ILogger<AccountingPeriodGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(InitializeFiscalYearCommand command)
    {
        if (_state.State.IsInitialized)
            throw new InvalidOperationException($"Fiscal year {command.Year} already initialized");

        var now = DateTime.UtcNow;
        _state.State.OrganizationId = command.OrganizationId;
        _state.State.Year = command.Year;
        _state.State.FiscalYearStartMonth = command.FiscalYearStartMonth;
        _state.State.Frequency = command.Frequency;
        _state.State.IsInitialized = true;
        _state.State.CreatedAt = now;
        _state.State.CreatedBy = command.InitializedBy;
        _state.State.Version = 1;

        // Create periods based on frequency
        CreatePeriods(command.Year, command.FiscalYearStartMonth, command.Frequency);

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Fiscal year {Year} initialized for organization {OrgId} with {PeriodCount} {Frequency} periods",
            command.Year,
            command.OrganizationId,
            _state.State.Periods.Count,
            command.Frequency);
    }

    private void CreatePeriods(int year, int startMonth, PeriodFrequency frequency)
    {
        var periods = frequency switch
        {
            PeriodFrequency.Monthly => CreateMonthlyPeriods(year, startMonth),
            PeriodFrequency.Quarterly => CreateQuarterlyPeriods(year, startMonth),
            PeriodFrequency.Yearly => CreateYearlyPeriod(year, startMonth),
            _ => throw new ArgumentException($"Unknown frequency: {frequency}")
        };

        foreach (var period in periods)
        {
            _state.State.Periods[period.PeriodNumber] = period;
        }
    }

    private static List<PeriodState> CreateMonthlyPeriods(int year, int startMonth)
    {
        var periods = new List<PeriodState>();

        for (int i = 0; i < 12; i++)
        {
            var month = ((startMonth - 1 + i) % 12) + 1;
            var periodYear = startMonth > 1 && month < startMonth ? year + 1 : year;
            var startDate = new DateOnly(periodYear, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            periods.Add(new PeriodState
            {
                PeriodNumber = i + 1,
                Name = $"Period {i + 1} ({startDate:MMM yyyy})",
                StartDate = startDate,
                EndDate = endDate,
                IsYearEnd = i == 11
            });
        }

        return periods;
    }

    private static List<PeriodState> CreateQuarterlyPeriods(int year, int startMonth)
    {
        var periods = new List<PeriodState>();

        for (int i = 0; i < 4; i++)
        {
            var quarterStartMonth = ((startMonth - 1 + (i * 3)) % 12) + 1;
            var periodYear = startMonth > 1 && quarterStartMonth < startMonth ? year + 1 : year;
            var startDate = new DateOnly(periodYear, quarterStartMonth, 1);
            var endDate = startDate.AddMonths(3).AddDays(-1);

            periods.Add(new PeriodState
            {
                PeriodNumber = i + 1,
                Name = $"Q{i + 1} {periodYear}",
                StartDate = startDate,
                EndDate = endDate,
                IsYearEnd = i == 3
            });
        }

        return periods;
    }

    private static List<PeriodState> CreateYearlyPeriod(int year, int startMonth)
    {
        var startDate = new DateOnly(year, startMonth, 1);
        var endDate = startDate.AddYears(1).AddDays(-1);

        return
        [
            new PeriodState
            {
                PeriodNumber = 1,
                Name = $"FY {year}",
                StartDate = startDate,
                EndDate = endDate,
                IsYearEnd = true
            }
        ];
    }

    public Task<bool> IsInitializedAsync()
    {
        return Task.FromResult(_state.State.IsInitialized);
    }

    public async Task<AccountingPeriod> OpenPeriodAsync(OpenPeriodCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Periods.TryGetValue(command.PeriodNumber, out var period))
            throw new InvalidOperationException($"Period {command.PeriodNumber} not found");

        if (period.Status == PeriodStatus.Open)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is already open");

        if (period.Status == PeriodStatus.Locked)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is locked and cannot be opened");

        // Ensure previous period is not NotStarted (sequential opening)
        if (command.PeriodNumber > 1)
        {
            var prevPeriod = _state.State.Periods[command.PeriodNumber - 1];
            if (prevPeriod.Status == PeriodStatus.NotStarted)
                throw new InvalidOperationException(
                    $"Cannot open period {command.PeriodNumber} before opening period {command.PeriodNumber - 1}");
        }

        var now = DateTime.UtcNow;
        period.Status = PeriodStatus.Open;
        period.OpenedAt = now;
        period.OpenedBy = command.OpenedBy;
        period.Notes = command.Notes;
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Period {PeriodNumber} ({Name}) opened by {UserId}",
            command.PeriodNumber,
            period.Name,
            command.OpenedBy);

        return ToPeriod(period);
    }

    public async Task<AccountingPeriod> ClosePeriodAsync(ClosePeriodCommand2 command)
    {
        EnsureInitialized();

        if (!_state.State.Periods.TryGetValue(command.PeriodNumber, out var period))
            throw new InvalidOperationException($"Period {command.PeriodNumber} not found");

        if (period.Status == PeriodStatus.Closed)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is already closed");

        if (period.Status == PeriodStatus.Locked)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is locked");

        if (period.Status == PeriodStatus.NotStarted && !command.Force)
            throw new InvalidOperationException(
                $"Period {command.PeriodNumber} was never opened. Use Force=true to close anyway.");

        var now = DateTime.UtcNow;
        period.Status = PeriodStatus.Closed;
        period.ClosedAt = now;
        period.ClosedBy = command.ClosedBy;
        if (command.Notes != null)
            period.Notes = (period.Notes ?? "") + $"\nClosed: {command.Notes}";

        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Period {PeriodNumber} ({Name}) closed by {UserId}",
            command.PeriodNumber,
            period.Name,
            command.ClosedBy);

        return ToPeriod(period);
    }

    public async Task<AccountingPeriod> ReopenPeriodAsync(ReopenPeriodCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Periods.TryGetValue(command.PeriodNumber, out var period))
            throw new InvalidOperationException($"Period {command.PeriodNumber} not found");

        if (period.Status != PeriodStatus.Closed)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is not closed");

        // Check no later periods are locked
        var laterLockedPeriod = _state.State.Periods.Values
            .FirstOrDefault(p => p.PeriodNumber > command.PeriodNumber && p.Status == PeriodStatus.Locked);

        if (laterLockedPeriod != null)
            throw new InvalidOperationException(
                $"Cannot reopen period {command.PeriodNumber} because period {laterLockedPeriod.PeriodNumber} is locked");

        var now = DateTime.UtcNow;
        period.Status = PeriodStatus.Open;
        period.ClosedAt = null;
        period.ClosedBy = null;
        period.Notes = (period.Notes ?? "") + $"\nReopened {now:yyyy-MM-dd}: {command.Reason}";
        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Period {PeriodNumber} ({Name}) reopened by {UserId}. Reason: {Reason}",
            command.PeriodNumber,
            period.Name,
            command.ReopenedBy,
            command.Reason);

        return ToPeriod(period);
    }

    public async Task<AccountingPeriod> LockPeriodAsync(LockPeriodCommand command)
    {
        EnsureInitialized();

        if (!_state.State.Periods.TryGetValue(command.PeriodNumber, out var period))
            throw new InvalidOperationException($"Period {command.PeriodNumber} not found");

        if (period.Status == PeriodStatus.Locked)
            throw new InvalidOperationException($"Period {command.PeriodNumber} is already locked");

        if (period.Status == PeriodStatus.Open || period.Status == PeriodStatus.NotStarted)
            throw new InvalidOperationException(
                $"Period {command.PeriodNumber} must be closed before it can be locked");

        // Ensure all previous periods are at least closed
        var openPrevPeriod = _state.State.Periods.Values
            .FirstOrDefault(p => p.PeriodNumber < command.PeriodNumber &&
                                 (p.Status == PeriodStatus.Open || p.Status == PeriodStatus.NotStarted));

        if (openPrevPeriod != null)
            throw new InvalidOperationException(
                $"Cannot lock period {command.PeriodNumber} before closing period {openPrevPeriod.PeriodNumber}");

        var now = DateTime.UtcNow;
        period.Status = PeriodStatus.Locked;
        period.LockedAt = now;
        period.LockedBy = command.LockedBy;
        if (command.Reason != null)
            period.Notes = (period.Notes ?? "") + $"\nLocked: {command.Reason}";

        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Period {PeriodNumber} ({Name}) permanently locked by {UserId}",
            command.PeriodNumber,
            period.Name,
            command.LockedBy);

        return ToPeriod(period);
    }

    public async Task<FiscalYearSummary> YearEndCloseAsync(YearEndCloseCommand command)
    {
        EnsureInitialized();

        if (_state.State.IsYearClosed)
            throw new InvalidOperationException($"Fiscal year {_state.State.Year} is already closed");

        // Ensure all periods are at least closed
        var openPeriod = _state.State.Periods.Values.FirstOrDefault(p =>
            p.Status == PeriodStatus.Open || p.Status == PeriodStatus.NotStarted);

        if (openPeriod != null)
            throw new InvalidOperationException(
                $"Cannot perform year-end close. Period {openPeriod.PeriodNumber} ({openPeriod.Name}) is not closed.");

        // Create closing journal entry to transfer income/expense to retained earnings
        var journalEntryId = Guid.NewGuid();
        var journalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(_state.State.OrganizationId, journalEntryId));

        // Note: In a real implementation, you would:
        // 1. Get all revenue and expense account balances
        // 2. Create closing entries to zero them out
        // 3. Transfer net income to retained earnings

        var now = DateTime.UtcNow;
        _state.State.IsYearClosed = true;
        _state.State.YearClosedAt = now;
        _state.State.YearClosedBy = command.ClosedBy;

        // Lock all periods
        foreach (var period in _state.State.Periods.Values)
        {
            if (period.Status != PeriodStatus.Locked)
            {
                period.Status = PeriodStatus.Locked;
                period.LockedAt = now;
                period.LockedBy = command.ClosedBy;
            }
        }

        _state.State.Version++;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Fiscal year {Year} closed by {UserId}",
            _state.State.Year,
            command.ClosedBy);

        return await GetSummaryAsync();
    }

    public Task<AccountingPeriod?> GetPeriodAsync(int periodNumber)
    {
        if (!_state.State.Periods.TryGetValue(periodNumber, out var period))
            return Task.FromResult<AccountingPeriod?>(null);

        return Task.FromResult<AccountingPeriod?>(ToPeriod(period));
    }

    public Task<IReadOnlyList<AccountingPeriod>> GetAllPeriodsAsync()
    {
        var periods = _state.State.Periods.Values
            .OrderBy(p => p.PeriodNumber)
            .Select(ToPeriod)
            .ToList();

        return Task.FromResult<IReadOnlyList<AccountingPeriod>>(periods);
    }

    public Task<AccountingPeriod?> GetCurrentOpenPeriodAsync()
    {
        var openPeriod = _state.State.Periods.Values
            .Where(p => p.Status == PeriodStatus.Open)
            .OrderBy(p => p.PeriodNumber)
            .FirstOrDefault();

        return Task.FromResult(openPeriod != null ? ToPeriod(openPeriod) : null);
    }

    public Task<FiscalYearSummary> GetSummaryAsync()
    {
        var periods = _state.State.Periods.Values.ToList();
        var fiscalYearStart = periods.Min(p => p.StartDate);
        var fiscalYearEnd = periods.Max(p => p.EndDate);

        return Task.FromResult(new FiscalYearSummary(
            _state.State.OrganizationId,
            _state.State.Year,
            _state.State.FiscalYearStartMonth,
            _state.State.Frequency,
            periods.Count,
            periods.Count(p => p.Status == PeriodStatus.Open),
            periods.Count(p => p.Status == PeriodStatus.Closed),
            periods.Count(p => p.Status == PeriodStatus.Locked),
            _state.State.IsYearClosed,
            fiscalYearStart,
            fiscalYearEnd));
    }

    public Task<bool> CanPostToDateAsync(DateOnly date)
    {
        if (!_state.State.IsInitialized)
            return Task.FromResult(false);

        if (_state.State.IsYearClosed)
            return Task.FromResult(false);

        var period = _state.State.Periods.Values.FirstOrDefault(p =>
            date >= p.StartDate && date <= p.EndDate);

        if (period == null)
            return Task.FromResult(false);

        return Task.FromResult(period.Status == PeriodStatus.Open);
    }

    public Task<AccountingPeriod?> GetPeriodForDateAsync(DateOnly date)
    {
        var period = _state.State.Periods.Values.FirstOrDefault(p =>
            date >= p.StartDate && date <= p.EndDate);

        return Task.FromResult(period != null ? ToPeriod(period) : null);
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
            throw new InvalidOperationException("Fiscal year not initialized");
    }

    private static AccountingPeriod ToPeriod(PeriodState state)
    {
        return new AccountingPeriod(
            state.PeriodNumber,
            state.Name,
            state.StartDate,
            state.EndDate,
            state.Status,
            state.OpenedAt,
            state.OpenedBy,
            state.ClosedAt,
            state.ClosedBy,
            state.LockedAt,
            state.LockedBy,
            state.IsYearEnd);
    }
}
