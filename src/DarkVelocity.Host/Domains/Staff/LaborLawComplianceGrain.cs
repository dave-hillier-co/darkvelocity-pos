using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Labor Law Compliance Grain Interface
// ============================================================================

/// <summary>
/// Represents overtime calculation rules for a jurisdiction.
/// </summary>
[GenerateSerializer]
public record OvertimeRule(
    [property: Id(0)] decimal DailyThresholdHours,
    [property: Id(1)] decimal DailyDoubleThresholdHours,
    [property: Id(2)] decimal WeeklyThresholdHours,
    [property: Id(3)] decimal OvertimeMultiplier,
    [property: Id(4)] decimal DoubleOvertimeMultiplier,
    [property: Id(5)] bool SeventhConsecutiveDayRule,
    [property: Id(6)] decimal SeventhDayMultiplier);

/// <summary>
/// Represents break requirements for a jurisdiction.
/// </summary>
[GenerateSerializer]
public record BreakRequirement(
    [property: Id(0)] decimal MinimumShiftHoursForBreak,
    [property: Id(1)] int MinimumBreakMinutes,
    [property: Id(2)] bool IsPaidBreak,
    [property: Id(3)] decimal MaxHoursBeforeBreak,
    [property: Id(4)] string BreakType); // meal, rest

/// <summary>
/// Configuration for a jurisdiction's labor laws.
/// </summary>
[GenerateSerializer]
public record JurisdictionConfig(
    [property: Id(0)] string JurisdictionCode,
    [property: Id(1)] string JurisdictionName,
    [property: Id(2)] OvertimeRule OvertimeRule,
    [property: Id(3)] IReadOnlyList<BreakRequirement> BreakRequirements,
    [property: Id(4)] int MinimumWageHourly,
    [property: Id(5)] int MinimumTippedWageHourly);

[GenerateSerializer]
public record ConfigureJurisdictionCommand(
    [property: Id(0)] string JurisdictionCode,
    [property: Id(1)] string JurisdictionName,
    [property: Id(2)] OvertimeRule OvertimeRule,
    [property: Id(3)] IReadOnlyList<BreakRequirement> BreakRequirements,
    [property: Id(4)] int MinimumWageHourly = 0,
    [property: Id(5)] int MinimumTippedWageHourly = 0);

[GenerateSerializer]
public record TimeEntryForCalculation(
    [property: Id(0)] Guid TimeEntryId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] DateOnly Date,
    [property: Id(3)] decimal TotalHours,
    [property: Id(4)] decimal BreakMinutes);

[GenerateSerializer]
public record OvertimeCalculationResult(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] DateOnly PeriodStart,
    [property: Id(2)] DateOnly PeriodEnd,
    [property: Id(3)] decimal TotalHours,
    [property: Id(4)] decimal RegularHours,
    [property: Id(5)] decimal OvertimeHours,
    [property: Id(6)] decimal DoubleOvertimeHours,
    [property: Id(7)] decimal SeventhDayHours,
    [property: Id(8)] IReadOnlyList<DailyOvertimeBreakdown> DailyBreakdown);

[GenerateSerializer]
public record DailyOvertimeBreakdown(
    [property: Id(0)] DateOnly Date,
    [property: Id(1)] decimal TotalHours,
    [property: Id(2)] decimal RegularHours,
    [property: Id(3)] decimal OvertimeHours,
    [property: Id(4)] decimal DoubleOvertimeHours,
    [property: Id(5)] bool IsSeventhConsecutiveDay,
    [property: Id(6)] int ConsecutiveDayCount);

[GenerateSerializer]
public record BreakComplianceResult(
    [property: Id(0)] Guid TimeEntryId,
    [property: Id(1)] Guid EmployeeId,
    [property: Id(2)] DateOnly Date,
    [property: Id(3)] bool IsCompliant,
    [property: Id(4)] IReadOnlyList<BreakViolation> Violations);

[GenerateSerializer]
public record BreakViolation(
    [property: Id(0)] string ViolationType,
    [property: Id(1)] string Description,
    [property: Id(2)] BreakRequirement RequiredBreak,
    [property: Id(3)] decimal? ActualBreakMinutes);

[GenerateSerializer]
public record LaborLawComplianceSnapshot(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] string DefaultJurisdictionCode,
    [property: Id(2)] IReadOnlyList<JurisdictionConfig> Jurisdictions);

/// <summary>
/// Grain for labor law compliance configuration and overtime calculation.
/// Key: "org:{orgId}:laborlaw"
/// </summary>
public interface ILaborLawComplianceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configures labor law rules for a jurisdiction.
    /// </summary>
    Task ConfigureJurisdictionAsync(ConfigureJurisdictionCommand command);

    /// <summary>
    /// Sets the default jurisdiction for the organization.
    /// </summary>
    Task SetDefaultJurisdictionAsync(string jurisdictionCode);

    /// <summary>
    /// Gets the configuration for a specific jurisdiction.
    /// </summary>
    Task<JurisdictionConfig?> GetJurisdictionConfigAsync(string jurisdictionCode);

    /// <summary>
    /// Calculates overtime for a set of time entries.
    /// </summary>
    Task<OvertimeCalculationResult> CalculateOvertimeAsync(
        Guid employeeId,
        string jurisdictionCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<TimeEntryForCalculation> timeEntries);

    /// <summary>
    /// Checks break compliance for a time entry.
    /// </summary>
    Task<BreakComplianceResult> CheckBreakComplianceAsync(
        string jurisdictionCode,
        TimeEntryForCalculation timeEntry,
        IReadOnlyList<BreakRecord> breaks);

    /// <summary>
    /// Gets the full compliance snapshot.
    /// </summary>
    Task<LaborLawComplianceSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Initializes with default US jurisdiction configurations.
    /// </summary>
    Task InitializeDefaultsAsync();
}

[GenerateSerializer]
public record BreakRecord(
    [property: Id(0)] TimeSpan StartTime,
    [property: Id(1)] TimeSpan? EndTime,
    [property: Id(2)] bool IsPaid,
    [property: Id(3)] string BreakType);

// ============================================================================
// Labor Law Compliance State
// ============================================================================

[GenerateSerializer]
public sealed class LaborLawComplianceState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public string DefaultJurisdictionCode { get; set; } = "US-FEDERAL";
    [Id(2)] public List<JurisdictionConfigState> Jurisdictions { get; set; } = [];
    [Id(3)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class JurisdictionConfigState
{
    [Id(0)] public string JurisdictionCode { get; set; } = string.Empty;
    [Id(1)] public string JurisdictionName { get; set; } = string.Empty;
    [Id(2)] public decimal DailyOvertimeThreshold { get; set; }
    [Id(3)] public decimal DailyDoubleOvertimeThreshold { get; set; }
    [Id(4)] public decimal WeeklyOvertimeThreshold { get; set; }
    [Id(5)] public decimal OvertimeMultiplier { get; set; } = 1.5m;
    [Id(6)] public decimal DoubleOvertimeMultiplier { get; set; } = 2.0m;
    [Id(7)] public bool SeventhConsecutiveDayRule { get; set; }
    [Id(8)] public decimal SeventhDayMultiplier { get; set; } = 1.5m;
    [Id(9)] public List<BreakRequirementState> BreakRequirements { get; set; } = [];
    [Id(10)] public int MinimumWageHourly { get; set; }
    [Id(11)] public int MinimumTippedWageHourly { get; set; }
}

[GenerateSerializer]
public sealed class BreakRequirementState
{
    [Id(0)] public decimal MinimumShiftHoursForBreak { get; set; }
    [Id(1)] public int MinimumBreakMinutes { get; set; }
    [Id(2)] public bool IsPaidBreak { get; set; }
    [Id(3)] public decimal MaxHoursBeforeBreak { get; set; }
    [Id(4)] public string BreakType { get; set; } = "meal";
}

// ============================================================================
// Labor Law Compliance Grain Implementation
// ============================================================================

/// <summary>
/// Grain for managing labor law compliance rules and overtime calculations.
/// </summary>
public class LaborLawComplianceGrain : Grain, ILaborLawComplianceGrain
{
    private readonly IPersistentState<LaborLawComplianceState> _state;

    public LaborLawComplianceGrain(
        [PersistentState("laborLawCompliance", "OrleansStorage")]
        IPersistentState<LaborLawComplianceState> state)
    {
        _state = state;
    }

    public async Task ConfigureJurisdictionAsync(ConfigureJurisdictionCommand command)
    {
        EnsureInitialized();

        var existing = _state.State.Jurisdictions
            .FirstOrDefault(j => j.JurisdictionCode == command.JurisdictionCode);

        if (existing != null)
        {
            // Update existing
            existing.JurisdictionName = command.JurisdictionName;
            existing.DailyOvertimeThreshold = command.OvertimeRule.DailyThresholdHours;
            existing.DailyDoubleOvertimeThreshold = command.OvertimeRule.DailyDoubleThresholdHours;
            existing.WeeklyOvertimeThreshold = command.OvertimeRule.WeeklyThresholdHours;
            existing.OvertimeMultiplier = command.OvertimeRule.OvertimeMultiplier;
            existing.DoubleOvertimeMultiplier = command.OvertimeRule.DoubleOvertimeMultiplier;
            existing.SeventhConsecutiveDayRule = command.OvertimeRule.SeventhConsecutiveDayRule;
            existing.SeventhDayMultiplier = command.OvertimeRule.SeventhDayMultiplier;
            existing.MinimumWageHourly = command.MinimumWageHourly;
            existing.MinimumTippedWageHourly = command.MinimumTippedWageHourly;

            existing.BreakRequirements.Clear();
            foreach (var br in command.BreakRequirements)
            {
                existing.BreakRequirements.Add(new BreakRequirementState
                {
                    MinimumShiftHoursForBreak = br.MinimumShiftHoursForBreak,
                    MinimumBreakMinutes = br.MinimumBreakMinutes,
                    IsPaidBreak = br.IsPaidBreak,
                    MaxHoursBeforeBreak = br.MaxHoursBeforeBreak,
                    BreakType = br.BreakType
                });
            }
        }
        else
        {
            // Add new
            var config = new JurisdictionConfigState
            {
                JurisdictionCode = command.JurisdictionCode,
                JurisdictionName = command.JurisdictionName,
                DailyOvertimeThreshold = command.OvertimeRule.DailyThresholdHours,
                DailyDoubleOvertimeThreshold = command.OvertimeRule.DailyDoubleThresholdHours,
                WeeklyOvertimeThreshold = command.OvertimeRule.WeeklyThresholdHours,
                OvertimeMultiplier = command.OvertimeRule.OvertimeMultiplier,
                DoubleOvertimeMultiplier = command.OvertimeRule.DoubleOvertimeMultiplier,
                SeventhConsecutiveDayRule = command.OvertimeRule.SeventhConsecutiveDayRule,
                SeventhDayMultiplier = command.OvertimeRule.SeventhDayMultiplier,
                MinimumWageHourly = command.MinimumWageHourly,
                MinimumTippedWageHourly = command.MinimumTippedWageHourly
            };

            foreach (var br in command.BreakRequirements)
            {
                config.BreakRequirements.Add(new BreakRequirementState
                {
                    MinimumShiftHoursForBreak = br.MinimumShiftHoursForBreak,
                    MinimumBreakMinutes = br.MinimumBreakMinutes,
                    IsPaidBreak = br.IsPaidBreak,
                    MaxHoursBeforeBreak = br.MaxHoursBeforeBreak,
                    BreakType = br.BreakType
                });
            }

            _state.State.Jurisdictions.Add(config);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetDefaultJurisdictionAsync(string jurisdictionCode)
    {
        EnsureInitialized();

        if (!_state.State.Jurisdictions.Any(j => j.JurisdictionCode == jurisdictionCode))
            throw new InvalidOperationException($"Jurisdiction {jurisdictionCode} not configured");

        _state.State.DefaultJurisdictionCode = jurisdictionCode;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<JurisdictionConfig?> GetJurisdictionConfigAsync(string jurisdictionCode)
    {
        var config = _state.State.Jurisdictions
            .FirstOrDefault(j => j.JurisdictionCode == jurisdictionCode);

        if (config == null)
            return Task.FromResult<JurisdictionConfig?>(null);

        return Task.FromResult<JurisdictionConfig?>(new JurisdictionConfig(
            config.JurisdictionCode,
            config.JurisdictionName,
            new OvertimeRule(
                config.DailyOvertimeThreshold,
                config.DailyDoubleOvertimeThreshold,
                config.WeeklyOvertimeThreshold,
                config.OvertimeMultiplier,
                config.DoubleOvertimeMultiplier,
                config.SeventhConsecutiveDayRule,
                config.SeventhDayMultiplier),
            config.BreakRequirements.Select(br => new BreakRequirement(
                br.MinimumShiftHoursForBreak,
                br.MinimumBreakMinutes,
                br.IsPaidBreak,
                br.MaxHoursBeforeBreak,
                br.BreakType)).ToList(),
            config.MinimumWageHourly,
            config.MinimumTippedWageHourly));
    }

    public Task<OvertimeCalculationResult> CalculateOvertimeAsync(
        Guid employeeId,
        string jurisdictionCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<TimeEntryForCalculation> timeEntries)
    {
        var config = _state.State.Jurisdictions
            .FirstOrDefault(j => j.JurisdictionCode == jurisdictionCode)
            ?? _state.State.Jurisdictions.FirstOrDefault(j => j.JurisdictionCode == _state.State.DefaultJurisdictionCode)
            ?? throw new InvalidOperationException("No jurisdiction configured");

        var dailyBreakdowns = new List<DailyOvertimeBreakdown>();
        var entriesByDate = timeEntries
            .Where(e => e.EmployeeId == employeeId)
            .GroupBy(e => e.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.TotalHours));

        decimal totalRegular = 0;
        decimal totalOvertime = 0;
        decimal totalDoubleOvertime = 0;
        decimal totalSeventhDay = 0;
        decimal weeklyHoursAccum = 0;

        // Track consecutive days worked
        int consecutiveDays = 0;
        DateOnly? lastWorkedDate = null;

        for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
        {
            var dailyHours = entriesByDate.GetValueOrDefault(date, 0);

            // Track consecutive days
            if (dailyHours > 0)
            {
                if (lastWorkedDate == null || date == lastWorkedDate.Value.AddDays(1))
                {
                    consecutiveDays++;
                }
                else
                {
                    consecutiveDays = 1;
                }
                lastWorkedDate = date;
            }

            var isSeventhDay = config.SeventhConsecutiveDayRule && consecutiveDays >= 7;

            // Calculate daily overtime
            decimal dailyRegular = 0;
            decimal dailyOvertime = 0;
            decimal dailyDoubleOvertime = 0;

            if (config.DailyOvertimeThreshold > 0)
            {
                // Daily overtime calculation (e.g., California)
                if (dailyHours <= config.DailyOvertimeThreshold)
                {
                    dailyRegular = dailyHours;
                }
                else if (config.DailyDoubleOvertimeThreshold > 0 && dailyHours > config.DailyDoubleOvertimeThreshold)
                {
                    dailyRegular = config.DailyOvertimeThreshold;
                    dailyOvertime = config.DailyDoubleOvertimeThreshold - config.DailyOvertimeThreshold;
                    dailyDoubleOvertime = dailyHours - config.DailyDoubleOvertimeThreshold;
                }
                else
                {
                    dailyRegular = config.DailyOvertimeThreshold;
                    dailyOvertime = dailyHours - config.DailyOvertimeThreshold;
                }
            }
            else
            {
                // No daily overtime threshold, use weekly only
                dailyRegular = dailyHours;
            }

            // Apply seventh consecutive day rules
            if (isSeventhDay)
            {
                // First 8 hours at 1.5x, over 8 at 2x (California-style)
                if (config.DailyOvertimeThreshold > 0)
                {
                    var seventhDayOT = Math.Min(dailyRegular, config.DailyOvertimeThreshold);
                    dailyRegular -= seventhDayOT;
                    totalSeventhDay += seventhDayOT;
                }
            }

            // Track weekly hours for weekly overtime
            weeklyHoursAccum += dailyRegular;

            // Apply weekly overtime threshold if applicable
            if (config.WeeklyOvertimeThreshold > 0 && !isSeventhDay)
            {
                if (weeklyHoursAccum > config.WeeklyOvertimeThreshold)
                {
                    var weeklyOT = weeklyHoursAccum - config.WeeklyOvertimeThreshold;
                    if (weeklyOT > dailyRegular)
                    {
                        weeklyOT = dailyRegular;
                    }
                    dailyRegular -= weeklyOT;
                    dailyOvertime += weeklyOT;
                }
            }

            totalRegular += dailyRegular;
            totalOvertime += dailyOvertime;
            totalDoubleOvertime += dailyDoubleOvertime;

            if (dailyHours > 0)
            {
                dailyBreakdowns.Add(new DailyOvertimeBreakdown(
                    date,
                    dailyHours,
                    dailyRegular,
                    dailyOvertime,
                    dailyDoubleOvertime,
                    isSeventhDay,
                    consecutiveDays));
            }

            // Reset weekly accumulator on week boundary (Sunday)
            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                weeklyHoursAccum = 0;
            }
        }

        return Task.FromResult(new OvertimeCalculationResult(
            employeeId,
            periodStart,
            periodEnd,
            timeEntries.Where(e => e.EmployeeId == employeeId).Sum(e => e.TotalHours),
            totalRegular,
            totalOvertime,
            totalDoubleOvertime,
            totalSeventhDay,
            dailyBreakdowns));
    }

    public Task<BreakComplianceResult> CheckBreakComplianceAsync(
        string jurisdictionCode,
        TimeEntryForCalculation timeEntry,
        IReadOnlyList<BreakRecord> breaks)
    {
        var config = _state.State.Jurisdictions
            .FirstOrDefault(j => j.JurisdictionCode == jurisdictionCode)
            ?? _state.State.Jurisdictions.FirstOrDefault(j => j.JurisdictionCode == _state.State.DefaultJurisdictionCode);

        var violations = new List<BreakViolation>();

        if (config == null)
        {
            return Task.FromResult(new BreakComplianceResult(
                timeEntry.TimeEntryId,
                timeEntry.EmployeeId,
                timeEntry.Date,
                true,
                violations));
        }

        foreach (var requirement in config.BreakRequirements)
        {
            if (timeEntry.TotalHours < requirement.MinimumShiftHoursForBreak)
                continue;

            // Find breaks matching the requirement type
            var matchingBreaks = breaks
                .Where(b => b.BreakType == requirement.BreakType || string.IsNullOrEmpty(requirement.BreakType))
                .ToList();

            var totalBreakMinutes = matchingBreaks
                .Where(b => b.EndTime.HasValue)
                .Sum(b => (b.EndTime!.Value - b.StartTime).TotalMinutes);

            if (totalBreakMinutes < requirement.MinimumBreakMinutes)
            {
                violations.Add(new BreakViolation(
                    "INSUFFICIENT_BREAK",
                    $"Required {requirement.MinimumBreakMinutes} minute {requirement.BreakType} break for shifts over {requirement.MinimumShiftHoursForBreak} hours",
                    new BreakRequirement(
                        requirement.MinimumShiftHoursForBreak,
                        requirement.MinimumBreakMinutes,
                        requirement.IsPaidBreak,
                        requirement.MaxHoursBeforeBreak,
                        requirement.BreakType),
                    (decimal)totalBreakMinutes));
            }

            // Check if break was taken within required time
            if (requirement.MaxHoursBeforeBreak > 0 && matchingBreaks.Count == 0)
            {
                violations.Add(new BreakViolation(
                    "LATE_BREAK",
                    $"Break must be taken within {requirement.MaxHoursBeforeBreak} hours of starting work",
                    new BreakRequirement(
                        requirement.MinimumShiftHoursForBreak,
                        requirement.MinimumBreakMinutes,
                        requirement.IsPaidBreak,
                        requirement.MaxHoursBeforeBreak,
                        requirement.BreakType),
                    null));
            }
        }

        return Task.FromResult(new BreakComplianceResult(
            timeEntry.TimeEntryId,
            timeEntry.EmployeeId,
            timeEntry.Date,
            violations.Count == 0,
            violations));
    }

    public Task<LaborLawComplianceSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(new LaborLawComplianceSnapshot(
            _state.State.OrgId,
            _state.State.DefaultJurisdictionCode,
            _state.State.Jurisdictions.Select(j => new JurisdictionConfig(
                j.JurisdictionCode,
                j.JurisdictionName,
                new OvertimeRule(
                    j.DailyOvertimeThreshold,
                    j.DailyDoubleOvertimeThreshold,
                    j.WeeklyOvertimeThreshold,
                    j.OvertimeMultiplier,
                    j.DoubleOvertimeMultiplier,
                    j.SeventhConsecutiveDayRule,
                    j.SeventhDayMultiplier),
                j.BreakRequirements.Select(br => new BreakRequirement(
                    br.MinimumShiftHoursForBreak,
                    br.MinimumBreakMinutes,
                    br.IsPaidBreak,
                    br.MaxHoursBeforeBreak,
                    br.BreakType)).ToList(),
                j.MinimumWageHourly,
                j.MinimumTippedWageHourly)).ToList()));
    }

    public async Task InitializeDefaultsAsync()
    {
        if (_state.State.OrgId == Guid.Empty)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            _state.State.OrgId = Guid.Parse(parts[1]);
        }

        // US Federal (default)
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "US-FEDERAL",
            "United States Federal",
            new OvertimeRule(
                DailyThresholdHours: 0, // No daily OT at federal level
                DailyDoubleThresholdHours: 0,
                WeeklyThresholdHours: 40,
                OvertimeMultiplier: 1.5m,
                DoubleOvertimeMultiplier: 2.0m,
                SeventhConsecutiveDayRule: false,
                SeventhDayMultiplier: 1.5m),
            new List<BreakRequirement>(), // Federal has no break requirements
            MinimumWageHourly: 725, // $7.25 in cents
            MinimumTippedWageHourly: 213)); // $2.13 in cents

        // California - strictest labor laws
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "US-CA",
            "California",
            new OvertimeRule(
                DailyThresholdHours: 8,
                DailyDoubleThresholdHours: 12,
                WeeklyThresholdHours: 40,
                OvertimeMultiplier: 1.5m,
                DoubleOvertimeMultiplier: 2.0m,
                SeventhConsecutiveDayRule: true,
                SeventhDayMultiplier: 1.5m),
            new List<BreakRequirement>
            {
                new(MinimumShiftHoursForBreak: 5, MinimumBreakMinutes: 30, IsPaidBreak: false, MaxHoursBeforeBreak: 5, BreakType: "meal"),
                new(MinimumShiftHoursForBreak: 10, MinimumBreakMinutes: 30, IsPaidBreak: false, MaxHoursBeforeBreak: 0, BreakType: "meal"),
                new(MinimumShiftHoursForBreak: 3.5m, MinimumBreakMinutes: 10, IsPaidBreak: true, MaxHoursBeforeBreak: 4, BreakType: "rest")
            },
            MinimumWageHourly: 1600,
            MinimumTippedWageHourly: 1600));

        // New York
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "US-NY",
            "New York",
            new OvertimeRule(
                DailyThresholdHours: 0,
                DailyDoubleThresholdHours: 0,
                WeeklyThresholdHours: 40,
                OvertimeMultiplier: 1.5m,
                DoubleOvertimeMultiplier: 2.0m,
                SeventhConsecutiveDayRule: false,
                SeventhDayMultiplier: 1.0m),
            new List<BreakRequirement>
            {
                new(MinimumShiftHoursForBreak: 6, MinimumBreakMinutes: 30, IsPaidBreak: false, MaxHoursBeforeBreak: 0, BreakType: "meal")
            },
            MinimumWageHourly: 1500,
            MinimumTippedWageHourly: 1000));

        // Texas
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "US-TX",
            "Texas",
            new OvertimeRule(
                DailyThresholdHours: 0,
                DailyDoubleThresholdHours: 0,
                WeeklyThresholdHours: 40,
                OvertimeMultiplier: 1.5m,
                DoubleOvertimeMultiplier: 2.0m,
                SeventhConsecutiveDayRule: false,
                SeventhDayMultiplier: 1.0m),
            new List<BreakRequirement>(), // Texas has no break requirements
            MinimumWageHourly: 725,
            MinimumTippedWageHourly: 213));

        // Colorado
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "US-CO",
            "Colorado",
            new OvertimeRule(
                DailyThresholdHours: 12,
                DailyDoubleThresholdHours: 0,
                WeeklyThresholdHours: 40,
                OvertimeMultiplier: 1.5m,
                DoubleOvertimeMultiplier: 2.0m,
                SeventhConsecutiveDayRule: false,
                SeventhDayMultiplier: 1.0m),
            new List<BreakRequirement>
            {
                new(MinimumShiftHoursForBreak: 5, MinimumBreakMinutes: 30, IsPaidBreak: false, MaxHoursBeforeBreak: 5, BreakType: "meal"),
                new(MinimumShiftHoursForBreak: 4, MinimumBreakMinutes: 10, IsPaidBreak: true, MaxHoursBeforeBreak: 4, BreakType: "rest")
            },
            MinimumWageHourly: 1442,
            MinimumTippedWageHourly: 1139));

        // United Kingdom
        await ConfigureJurisdictionAsync(new ConfigureJurisdictionCommand(
            "UK",
            "United Kingdom",
            new OvertimeRule(
                DailyThresholdHours: 0,
                DailyDoubleThresholdHours: 0,
                WeeklyThresholdHours: 48, // Working Time Regulations
                OvertimeMultiplier: 1.0m, // UK doesn't mandate OT pay
                DoubleOvertimeMultiplier: 1.0m,
                SeventhConsecutiveDayRule: false,
                SeventhDayMultiplier: 1.0m),
            new List<BreakRequirement>
            {
                new(MinimumShiftHoursForBreak: 6, MinimumBreakMinutes: 20, IsPaidBreak: false, MaxHoursBeforeBreak: 6, BreakType: "rest")
            },
            MinimumWageHourly: 1044, // GBP pence
            MinimumTippedWageHourly: 1044));
    }

    private void EnsureInitialized()
    {
        if (_state.State.OrgId == Guid.Empty)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            _state.State.OrgId = Guid.Parse(parts[1]);
        }
    }
}
