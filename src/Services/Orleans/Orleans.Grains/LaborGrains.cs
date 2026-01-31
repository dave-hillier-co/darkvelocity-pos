using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

// ============================================================================
// Employee Grain
// ============================================================================

/// <summary>
/// Grain for employee management.
/// Manages employee profile, roles, and employment status.
/// </summary>
public class EmployeeGrain : Grain, IEmployeeGrain
{
    private readonly IPersistentState<EmployeeState> _state;

    public EmployeeGrain(
        [PersistentState("employee", "OrleansStorage")]
        IPersistentState<EmployeeState> state)
    {
        _state = state;
    }

    public async Task<EmployeeSnapshot> CreateAsync(CreateEmployeeCommand command)
    {
        if (_state.State.EmployeeId != Guid.Empty)
            throw new InvalidOperationException("Employee already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var employeeId = Guid.Parse(parts[2]);

        _state.State = new EmployeeState
        {
            OrgId = orgId,
            EmployeeId = employeeId,
            UserId = command.UserId,
            LocationId = command.LocationId,
            EmployeeNumber = command.EmployeeNumber,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email,
            Phone = command.Phone,
            DateOfBirth = command.DateOfBirth,
            HireDate = command.HireDate,
            Status = EmploymentStatus.Active,
            EmploymentType = command.EmploymentType,
            HourlyRate = command.HourlyRate,
            SalaryAmount = command.SalaryAmount,
            OvertimeRate = command.OvertimeRate,
            MaxHoursPerWeek = command.MaxHoursPerWeek,
            MinHoursPerWeek = command.MinHoursPerWeek,
            DefaultRoleId = command.DefaultRoleId,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<EmployeeSnapshot> UpdateAsync(UpdateEmployeeCommand command)
    {
        EnsureInitialized();

        if (command.FirstName != null) _state.State.FirstName = command.FirstName;
        if (command.LastName != null) _state.State.LastName = command.LastName;
        if (command.Email != null) _state.State.Email = command.Email;
        if (command.Phone != null) _state.State.Phone = command.Phone;
        if (command.Status.HasValue) _state.State.Status = command.Status.Value;
        if (command.EmploymentType.HasValue) _state.State.EmploymentType = command.EmploymentType.Value;
        if (command.HourlyRate.HasValue) _state.State.HourlyRate = command.HourlyRate.Value;
        if (command.SalaryAmount.HasValue) _state.State.SalaryAmount = command.SalaryAmount.Value;
        if (command.OvertimeRate.HasValue) _state.State.OvertimeRate = command.OvertimeRate.Value;
        if (command.MaxHoursPerWeek.HasValue) _state.State.MaxHoursPerWeek = command.MaxHoursPerWeek.Value;
        if (command.MinHoursPerWeek.HasValue) _state.State.MinHoursPerWeek = command.MinHoursPerWeek.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task TerminateAsync(TerminateEmployeeCommand command)
    {
        EnsureInitialized();

        _state.State.Status = EmploymentStatus.Terminated;
        _state.State.TerminationDate = command.TerminationDate;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AssignRoleAsync(AssignRoleCommand command)
    {
        EnsureInitialized();

        var existingRole = _state.State.Roles.FirstOrDefault(r => r.RoleId == command.RoleId);
        if (existingRole != null)
        {
            existingRole.HourlyRateOverride = command.HourlyRateOverride;
            existingRole.IsPrimary = command.IsPrimary;
        }
        else
        {
            _state.State.Roles.Add(new EmployeeRoleState
            {
                RoleId = command.RoleId,
                HourlyRateOverride = command.HourlyRateOverride,
                IsPrimary = command.IsPrimary,
                CertifiedAt = DateTime.UtcNow
            });
        }

        if (command.IsPrimary)
        {
            foreach (var role in _state.State.Roles.Where(r => r.RoleId != command.RoleId))
                role.IsPrimary = false;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveRoleAsync(Guid roleId)
    {
        EnsureInitialized();

        _state.State.Roles.RemoveAll(r => r.RoleId == roleId);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<EmployeeSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.Status == EmploymentStatus.Active);
    }

    public Task<decimal> GetEffectiveHourlyRateAsync(Guid roleId)
    {
        EnsureInitialized();

        var role = _state.State.Roles.FirstOrDefault(r => r.RoleId == roleId);
        var rate = role?.HourlyRateOverride ?? _state.State.HourlyRate;

        return Task.FromResult(rate);
    }

    private EmployeeSnapshot CreateSnapshot()
    {
        return new EmployeeSnapshot(
            EmployeeId: _state.State.EmployeeId,
            UserId: _state.State.UserId,
            LocationId: _state.State.LocationId,
            EmployeeNumber: _state.State.EmployeeNumber,
            FirstName: _state.State.FirstName,
            LastName: _state.State.LastName,
            Email: _state.State.Email,
            Phone: _state.State.Phone,
            DateOfBirth: _state.State.DateOfBirth,
            HireDate: _state.State.HireDate,
            TerminationDate: _state.State.TerminationDate,
            Status: _state.State.Status,
            EmploymentType: _state.State.EmploymentType,
            HourlyRate: _state.State.HourlyRate,
            SalaryAmount: _state.State.SalaryAmount,
            OvertimeRate: _state.State.OvertimeRate,
            MaxHoursPerWeek: _state.State.MaxHoursPerWeek,
            MinHoursPerWeek: _state.State.MinHoursPerWeek,
            DefaultRoleId: _state.State.DefaultRoleId,
            Roles: _state.State.Roles.Select(r => new EmployeeRoleAssignment(
                RoleId: r.RoleId,
                RoleName: r.RoleName,
                HourlyRateOverride: r.HourlyRateOverride,
                IsPrimary: r.IsPrimary,
                CertifiedAt: r.CertifiedAt)).ToList());
    }

    private void EnsureInitialized()
    {
        if (_state.State.EmployeeId == Guid.Empty)
            throw new InvalidOperationException("Employee grain not initialized");
    }
}

// ============================================================================
// Role Grain
// ============================================================================

/// <summary>
/// Grain for role management.
/// Manages role definitions and permissions.
/// </summary>
public class RoleGrain : Grain, IRoleGrain
{
    private readonly IPersistentState<RoleState> _state;

    public RoleGrain(
        [PersistentState("role", "OrleansStorage")]
        IPersistentState<RoleState> state)
    {
        _state = state;
    }

    public async Task<RoleSnapshot> CreateAsync(CreateRoleCommand command)
    {
        if (_state.State.RoleId != Guid.Empty)
            throw new InvalidOperationException("Role already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var roleId = Guid.Parse(parts[2]);

        _state.State = new RoleState
        {
            OrgId = orgId,
            RoleId = roleId,
            Name = command.Name,
            Department = command.Department,
            DefaultHourlyRate = command.DefaultHourlyRate,
            Color = command.Color,
            SortOrder = command.SortOrder,
            RequiredCertifications = command.RequiredCertifications.ToList(),
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<RoleSnapshot> UpdateAsync(UpdateRoleCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Department.HasValue) _state.State.Department = command.Department.Value;
        if (command.DefaultHourlyRate.HasValue) _state.State.DefaultHourlyRate = command.DefaultHourlyRate.Value;
        if (command.Color != null) _state.State.Color = command.Color;
        if (command.SortOrder.HasValue) _state.State.SortOrder = command.SortOrder.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<RoleSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    private RoleSnapshot CreateSnapshot()
    {
        return new RoleSnapshot(
            RoleId: _state.State.RoleId,
            Name: _state.State.Name,
            Department: _state.State.Department,
            DefaultHourlyRate: _state.State.DefaultHourlyRate,
            Color: _state.State.Color,
            SortOrder: _state.State.SortOrder,
            RequiredCertifications: _state.State.RequiredCertifications,
            IsActive: _state.State.IsActive);
    }

    private void EnsureInitialized()
    {
        if (_state.State.RoleId == Guid.Empty)
            throw new InvalidOperationException("Role grain not initialized");
    }
}

// ============================================================================
// Schedule Grain
// ============================================================================

/// <summary>
/// Grain for schedule management.
/// Manages weekly schedules and shifts.
/// </summary>
public class ScheduleGrain : Grain, IScheduleGrain
{
    private readonly IPersistentState<ScheduleState> _state;

    public ScheduleGrain(
        [PersistentState("schedule", "OrleansStorage")]
        IPersistentState<ScheduleState> state)
    {
        _state = state;
    }

    public async Task<ScheduleSnapshot> CreateAsync(CreateScheduleCommand command)
    {
        if (_state.State.ScheduleId != Guid.Empty)
            throw new InvalidOperationException("Schedule already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new ScheduleState
        {
            OrgId = orgId,
            ScheduleId = Guid.NewGuid(),
            LocationId = command.LocationId,
            WeekStartDate = command.WeekStartDate,
            Status = ScheduleStatus.Draft,
            Notes = command.Notes,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<ScheduleSnapshot> PublishAsync(PublishScheduleCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == ScheduleStatus.Locked)
            throw new InvalidOperationException("Cannot publish a locked schedule");

        _state.State.Status = ScheduleStatus.Published;
        _state.State.PublishedAt = DateTime.UtcNow;
        _state.State.PublishedByUserId = command.PublishedByUserId;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task LockAsync()
    {
        EnsureInitialized();

        _state.State.Status = ScheduleStatus.Locked;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddShiftAsync(AddShiftCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == ScheduleStatus.Locked)
            throw new InvalidOperationException("Cannot modify a locked schedule");

        var scheduledHours = (decimal)(command.EndTime - command.StartTime).TotalHours - command.BreakMinutes / 60m;
        var laborCost = scheduledHours * command.HourlyRate;

        var shift = new ShiftState
        {
            ShiftId = command.ShiftId,
            EmployeeId = command.EmployeeId,
            RoleId = command.RoleId,
            Date = command.Date,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            BreakMinutes = command.BreakMinutes,
            ScheduledHours = scheduledHours,
            HourlyRate = command.HourlyRate,
            LaborCost = laborCost,
            Status = ShiftStatus.Scheduled,
            Notes = command.Notes
        };

        _state.State.Shifts.Add(shift);
        RecalculateTotals();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UpdateShiftAsync(UpdateShiftCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == ScheduleStatus.Locked)
            throw new InvalidOperationException("Cannot modify a locked schedule");

        var shift = _state.State.Shifts.FirstOrDefault(s => s.ShiftId == command.ShiftId)
            ?? throw new InvalidOperationException("Shift not found");

        if (command.StartTime.HasValue) shift.StartTime = command.StartTime.Value;
        if (command.EndTime.HasValue) shift.EndTime = command.EndTime.Value;
        if (command.BreakMinutes.HasValue) shift.BreakMinutes = command.BreakMinutes.Value;
        if (command.EmployeeId.HasValue) shift.EmployeeId = command.EmployeeId.Value;
        if (command.RoleId.HasValue) shift.RoleId = command.RoleId.Value;
        if (command.Notes != null) shift.Notes = command.Notes;

        shift.ScheduledHours = (decimal)(shift.EndTime - shift.StartTime).TotalHours - shift.BreakMinutes / 60m;
        shift.LaborCost = shift.ScheduledHours * shift.HourlyRate;

        RecalculateTotals();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveShiftAsync(Guid shiftId)
    {
        EnsureInitialized();

        if (_state.State.Status == ScheduleStatus.Locked)
            throw new InvalidOperationException("Cannot modify a locked schedule");

        _state.State.Shifts.RemoveAll(s => s.ShiftId == shiftId);
        RecalculateTotals();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<ScheduleSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<IReadOnlyList<ShiftSnapshot>> GetShiftsForEmployeeAsync(Guid employeeId)
    {
        EnsureInitialized();

        var shifts = _state.State.Shifts
            .Where(s => s.EmployeeId == employeeId)
            .Select(CreateShiftSnapshot)
            .ToList();

        return Task.FromResult<IReadOnlyList<ShiftSnapshot>>(shifts);
    }

    public Task<IReadOnlyList<ShiftSnapshot>> GetShiftsForDateAsync(DateTime date)
    {
        EnsureInitialized();

        var shifts = _state.State.Shifts
            .Where(s => s.Date.Date == date.Date)
            .Select(CreateShiftSnapshot)
            .ToList();

        return Task.FromResult<IReadOnlyList<ShiftSnapshot>>(shifts);
    }

    public Task<decimal> GetTotalLaborCostAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.TotalLaborCost);
    }

    private void RecalculateTotals()
    {
        _state.State.TotalScheduledHours = _state.State.Shifts.Sum(s => s.ScheduledHours);
        _state.State.TotalLaborCost = _state.State.Shifts.Sum(s => s.LaborCost);
    }

    private ScheduleSnapshot CreateSnapshot()
    {
        return new ScheduleSnapshot(
            ScheduleId: _state.State.ScheduleId,
            LocationId: _state.State.LocationId,
            WeekStartDate: _state.State.WeekStartDate,
            Status: _state.State.Status,
            PublishedAt: _state.State.PublishedAt,
            PublishedByUserId: _state.State.PublishedByUserId,
            TotalScheduledHours: _state.State.TotalScheduledHours,
            TotalLaborCost: _state.State.TotalLaborCost,
            Shifts: _state.State.Shifts.Select(CreateShiftSnapshot).ToList(),
            Notes: _state.State.Notes);
    }

    private static ShiftSnapshot CreateShiftSnapshot(ShiftState s)
    {
        return new ShiftSnapshot(
            ShiftId: s.ShiftId,
            EmployeeId: s.EmployeeId,
            RoleId: s.RoleId,
            Date: s.Date,
            StartTime: s.StartTime,
            EndTime: s.EndTime,
            BreakMinutes: s.BreakMinutes,
            ScheduledHours: s.ScheduledHours,
            HourlyRate: s.HourlyRate,
            LaborCost: s.LaborCost,
            Status: s.Status,
            IsOvertime: s.IsOvertime,
            Notes: s.Notes);
    }

    private void EnsureInitialized()
    {
        if (_state.State.ScheduleId == Guid.Empty)
            throw new InvalidOperationException("Schedule grain not initialized");
    }
}

// ============================================================================
// Time Entry Grain
// ============================================================================

/// <summary>
/// Grain for time entry management.
/// Manages clock in/out and time tracking.
/// </summary>
public class TimeEntryGrain : Grain, ITimeEntryGrain
{
    private readonly IPersistentState<TimeEntryState> _state;

    public TimeEntryGrain(
        [PersistentState("timeEntry", "OrleansStorage")]
        IPersistentState<TimeEntryState> state)
    {
        _state = state;
    }

    public async Task<TimeEntrySnapshot> ClockInAsync(ClockInCommand command)
    {
        if (_state.State.TimeEntryId != Guid.Empty)
            throw new InvalidOperationException("Time entry already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var timeEntryId = Guid.Parse(parts[2]);

        _state.State = new TimeEntryState
        {
            OrgId = orgId,
            TimeEntryId = timeEntryId,
            EmployeeId = command.EmployeeId,
            LocationId = command.LocationId,
            RoleId = command.RoleId,
            ShiftId = command.ShiftId,
            ClockInAt = DateTime.UtcNow,
            ClockInMethod = command.Method,
            Status = TimeEntryStatus.Active,
            Notes = command.Notes,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<TimeEntrySnapshot> ClockOutAsync(ClockOutCommand command)
    {
        EnsureInitialized();

        if (_state.State.ClockOutAt.HasValue)
            throw new InvalidOperationException("Already clocked out");

        _state.State.ClockOutAt = DateTime.UtcNow;
        _state.State.ClockOutMethod = command.Method;
        _state.State.Status = TimeEntryStatus.Completed;

        if (command.Notes != null)
            _state.State.Notes = (_state.State.Notes ?? "") + "\n" + command.Notes;

        CalculateHours();
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddBreakAsync(AddBreakCommand command)
    {
        EnsureInitialized();

        _state.State.Breaks.Add(new BreakState
        {
            BreakStart = command.BreakStart,
            BreakEnd = command.BreakEnd,
            IsPaid = command.IsPaid
        });

        if (!command.IsPaid && command.BreakEnd.HasValue)
        {
            var breakDuration = (int)(command.BreakEnd.Value - command.BreakStart).TotalMinutes;
            _state.State.BreakMinutes += breakDuration;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<TimeEntrySnapshot> AdjustAsync(AdjustTimeEntryCommand command)
    {
        EnsureInitialized();

        if (command.ClockInAt.HasValue) _state.State.ClockInAt = command.ClockInAt.Value;
        if (command.ClockOutAt.HasValue) _state.State.ClockOutAt = command.ClockOutAt.Value;
        if (command.BreakMinutes.HasValue) _state.State.BreakMinutes = command.BreakMinutes.Value;

        _state.State.AdjustedByUserId = command.AdjustedByUserId;
        _state.State.AdjustmentReason = command.Reason;
        _state.State.Status = TimeEntryStatus.Adjusted;

        CalculateHours();
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<TimeEntrySnapshot> ApproveAsync(ApproveTimeEntryCommand command)
    {
        EnsureInitialized();

        _state.State.ApprovedByUserId = command.ApprovedByUserId;
        _state.State.ApprovedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<TimeEntrySnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.Status == TimeEntryStatus.Active);
    }

    private void CalculateHours()
    {
        if (!_state.State.ClockOutAt.HasValue) return;

        var totalMinutes = (_state.State.ClockOutAt.Value - _state.State.ClockInAt).TotalMinutes;
        var actualMinutes = totalMinutes - _state.State.BreakMinutes;
        _state.State.ActualHours = (decimal)actualMinutes / 60m;

        // Simplified overtime calculation (over 8 hours)
        var regularHours = Math.Min(_state.State.ActualHours.Value, 8m);
        var overtimeHours = Math.Max(0, _state.State.ActualHours.Value - 8m);

        _state.State.RegularHours = regularHours;
        _state.State.OvertimeHours = overtimeHours;

        _state.State.GrossPay = regularHours * _state.State.HourlyRate +
                               overtimeHours * _state.State.OvertimeRate;
    }

    private TimeEntrySnapshot CreateSnapshot()
    {
        return new TimeEntrySnapshot(
            TimeEntryId: _state.State.TimeEntryId,
            EmployeeId: _state.State.EmployeeId,
            LocationId: _state.State.LocationId,
            RoleId: _state.State.RoleId,
            ShiftId: _state.State.ShiftId,
            ClockInAt: _state.State.ClockInAt,
            ClockOutAt: _state.State.ClockOutAt,
            ClockInMethod: _state.State.ClockInMethod,
            ClockOutMethod: _state.State.ClockOutMethod,
            BreakMinutes: _state.State.BreakMinutes,
            ActualHours: _state.State.ActualHours,
            RegularHours: _state.State.RegularHours,
            OvertimeHours: _state.State.OvertimeHours,
            HourlyRate: _state.State.HourlyRate,
            OvertimeRate: _state.State.OvertimeRate,
            GrossPay: _state.State.GrossPay,
            Status: _state.State.Status,
            AdjustedByUserId: _state.State.AdjustedByUserId,
            AdjustmentReason: _state.State.AdjustmentReason,
            ApprovedByUserId: _state.State.ApprovedByUserId,
            ApprovedAt: _state.State.ApprovedAt,
            Notes: _state.State.Notes);
    }

    private void EnsureInitialized()
    {
        if (_state.State.TimeEntryId == Guid.Empty)
            throw new InvalidOperationException("Time entry grain not initialized");
    }
}

// ============================================================================
// Tip Pool Grain
// ============================================================================

/// <summary>
/// Grain for tip pool management.
/// Manages tip collection and distribution.
/// </summary>
public class TipPoolGrain : Grain, ITipPoolGrain
{
    private readonly IPersistentState<TipPoolState> _state;

    public TipPoolGrain(
        [PersistentState("tipPool", "OrleansStorage")]
        IPersistentState<TipPoolState> state)
    {
        _state = state;
    }

    public async Task<TipPoolSnapshot> CreateAsync(CreateTipPoolCommand command)
    {
        if (_state.State.TipPoolId != Guid.Empty)
            throw new InvalidOperationException("Tip pool already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new TipPoolState
        {
            OrgId = orgId,
            TipPoolId = Guid.NewGuid(),
            LocationId = command.LocationId,
            BusinessDate = command.BusinessDate,
            Name = command.Name,
            Method = command.Method,
            EligibleRoleIds = command.EligibleRoleIds.ToList(),
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddTipsAsync(AddTipsCommand command)
    {
        EnsureInitialized();

        if (_state.State.IsDistributed)
            throw new InvalidOperationException("Cannot add tips to a distributed pool");

        _state.State.TotalTips += command.Amount;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddParticipantAsync(Guid employeeId, decimal hoursWorked, decimal points)
    {
        EnsureInitialized();

        if (_state.State.IsDistributed)
            throw new InvalidOperationException("Cannot add participants to a distributed pool");

        var existing = _state.State.Participants.FirstOrDefault(p => p.EmployeeId == employeeId);
        if (existing != null)
        {
            existing.HoursWorked += hoursWorked;
            existing.Points += points;
        }
        else
        {
            _state.State.Participants.Add(new TipParticipantState
            {
                EmployeeId = employeeId,
                HoursWorked = hoursWorked,
                Points = points
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<TipPoolSnapshot> DistributeAsync(DistributeTipsCommand command)
    {
        EnsureInitialized();

        if (_state.State.IsDistributed)
            throw new InvalidOperationException("Tips already distributed");

        if (_state.State.TotalTips <= 0 || _state.State.Participants.Count == 0)
            throw new InvalidOperationException("No tips or participants to distribute");

        _state.State.Distributions.Clear();

        switch (_state.State.Method)
        {
            case TipPoolMethod.Equal:
                DistributeEqually();
                break;
            case TipPoolMethod.ByHoursWorked:
                DistributeByHours();
                break;
            case TipPoolMethod.ByPoints:
                DistributeByPoints();
                break;
        }

        _state.State.IsDistributed = true;
        _state.State.DistributedAt = DateTime.UtcNow;
        _state.State.DistributedByUserId = command.DistributedByUserId;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<TipPoolSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    private void DistributeEqually()
    {
        var perPerson = _state.State.TotalTips / _state.State.Participants.Count;

        foreach (var p in _state.State.Participants)
        {
            _state.State.Distributions.Add(new TipDistributionState
            {
                EmployeeId = p.EmployeeId,
                EmployeeName = p.EmployeeName,
                RoleId = p.RoleId,
                HoursWorked = p.HoursWorked,
                Points = p.Points,
                TipAmount = perPerson
            });
        }
    }

    private void DistributeByHours()
    {
        var totalHours = _state.State.Participants.Sum(p => p.HoursWorked);
        if (totalHours <= 0) return;

        foreach (var p in _state.State.Participants)
        {
            var share = p.HoursWorked / totalHours;
            _state.State.Distributions.Add(new TipDistributionState
            {
                EmployeeId = p.EmployeeId,
                EmployeeName = p.EmployeeName,
                RoleId = p.RoleId,
                HoursWorked = p.HoursWorked,
                Points = p.Points,
                TipAmount = _state.State.TotalTips * share
            });
        }
    }

    private void DistributeByPoints()
    {
        var totalPoints = _state.State.Participants.Sum(p => p.Points);
        if (totalPoints <= 0) return;

        foreach (var p in _state.State.Participants)
        {
            var share = p.Points / totalPoints;
            _state.State.Distributions.Add(new TipDistributionState
            {
                EmployeeId = p.EmployeeId,
                EmployeeName = p.EmployeeName,
                RoleId = p.RoleId,
                HoursWorked = p.HoursWorked,
                Points = p.Points,
                TipAmount = _state.State.TotalTips * share
            });
        }
    }

    private TipPoolSnapshot CreateSnapshot()
    {
        return new TipPoolSnapshot(
            TipPoolId: _state.State.TipPoolId,
            LocationId: _state.State.LocationId,
            BusinessDate: _state.State.BusinessDate,
            Name: _state.State.Name,
            Method: _state.State.Method,
            TotalTips: _state.State.TotalTips,
            IsDistributed: _state.State.IsDistributed,
            DistributedAt: _state.State.DistributedAt,
            DistributedByUserId: _state.State.DistributedByUserId,
            Distributions: _state.State.Distributions.Select(d => new TipDistribution(
                EmployeeId: d.EmployeeId,
                EmployeeName: d.EmployeeName,
                RoleId: d.RoleId,
                HoursWorked: d.HoursWorked,
                Points: d.Points,
                TipAmount: d.TipAmount)).ToList());
    }

    private void EnsureInitialized()
    {
        if (_state.State.TipPoolId == Guid.Empty)
            throw new InvalidOperationException("Tip pool grain not initialized");
    }
}

// ============================================================================
// Payroll Period Grain
// ============================================================================

/// <summary>
/// Grain for payroll period management.
/// Manages payroll calculations and processing.
/// </summary>
public class PayrollPeriodGrain : Grain, IPayrollPeriodGrain
{
    private readonly IPersistentState<PayrollPeriodState> _state;

    public PayrollPeriodGrain(
        [PersistentState("payrollPeriod", "OrleansStorage")]
        IPersistentState<PayrollPeriodState> state)
    {
        _state = state;
    }

    public async Task<PayrollPeriodSnapshot> CreateAsync(CreatePayrollPeriodCommand command)
    {
        if (_state.State.PayrollPeriodId != Guid.Empty)
            throw new InvalidOperationException("Payroll period already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new PayrollPeriodState
        {
            OrgId = orgId,
            PayrollPeriodId = Guid.NewGuid(),
            LocationId = command.LocationId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            Status = PayrollStatus.Open,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task CalculateAsync()
    {
        EnsureInitialized();

        if (_state.State.Status != PayrollStatus.Open)
            throw new InvalidOperationException("Payroll period is not open");

        _state.State.Status = PayrollStatus.Calculating;

        // Totals would be calculated from time entries
        _state.State.TotalRegularHours = _state.State.Entries.Sum(e => e.RegularHours);
        _state.State.TotalOvertimeHours = _state.State.Entries.Sum(e => e.OvertimeHours);
        _state.State.TotalRegularPay = _state.State.Entries.Sum(e => e.RegularPay);
        _state.State.TotalOvertimePay = _state.State.Entries.Sum(e => e.OvertimePay);
        _state.State.TotalTips = _state.State.Entries.Sum(e => e.TipsReceived);
        _state.State.TotalGrossPay = _state.State.Entries.Sum(e => e.GrossPay);
        _state.State.TotalDeductions = _state.State.Entries.Sum(e => e.Deductions);
        _state.State.TotalNetPay = _state.State.Entries.Sum(e => e.NetPay);

        _state.State.Status = PayrollStatus.PendingApproval;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(Guid approvedByUserId)
    {
        EnsureInitialized();

        if (_state.State.Status != PayrollStatus.PendingApproval)
            throw new InvalidOperationException("Payroll period is not pending approval");

        _state.State.Status = PayrollStatus.Approved;
        _state.State.ApprovedByUserId = approvedByUserId;
        _state.State.ApprovedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ProcessAsync()
    {
        EnsureInitialized();

        if (_state.State.Status != PayrollStatus.Approved)
            throw new InvalidOperationException("Payroll period is not approved");

        _state.State.Status = PayrollStatus.Processed;
        _state.State.ProcessedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<PayrollPeriodSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<PayrollEntrySnapshot> GetEmployeePayrollAsync(Guid employeeId)
    {
        EnsureInitialized();

        var entry = _state.State.Entries.FirstOrDefault(e => e.EmployeeId == employeeId)
            ?? throw new InvalidOperationException("Employee not found in payroll");

        return Task.FromResult(new PayrollEntrySnapshot(
            EmployeeId: entry.EmployeeId,
            EmployeeName: entry.EmployeeName,
            RegularHours: entry.RegularHours,
            OvertimeHours: entry.OvertimeHours,
            RegularPay: entry.RegularPay,
            OvertimePay: entry.OvertimePay,
            TipsReceived: entry.TipsReceived,
            GrossPay: entry.GrossPay,
            Deductions: entry.Deductions,
            NetPay: entry.NetPay));
    }

    private PayrollPeriodSnapshot CreateSnapshot()
    {
        return new PayrollPeriodSnapshot(
            PayrollPeriodId: _state.State.PayrollPeriodId,
            LocationId: _state.State.LocationId,
            PeriodStart: _state.State.PeriodStart,
            PeriodEnd: _state.State.PeriodEnd,
            Status: _state.State.Status,
            TotalRegularHours: _state.State.TotalRegularHours,
            TotalOvertimeHours: _state.State.TotalOvertimeHours,
            TotalRegularPay: _state.State.TotalRegularPay,
            TotalOvertimePay: _state.State.TotalOvertimePay,
            TotalTips: _state.State.TotalTips,
            TotalGrossPay: _state.State.TotalGrossPay,
            TotalDeductions: _state.State.TotalDeductions,
            TotalNetPay: _state.State.TotalNetPay,
            Entries: _state.State.Entries.Select(e => new PayrollEntrySnapshot(
                EmployeeId: e.EmployeeId,
                EmployeeName: e.EmployeeName,
                RegularHours: e.RegularHours,
                OvertimeHours: e.OvertimeHours,
                RegularPay: e.RegularPay,
                OvertimePay: e.OvertimePay,
                TipsReceived: e.TipsReceived,
                GrossPay: e.GrossPay,
                Deductions: e.Deductions,
                NetPay: e.NetPay)).ToList());
    }

    private void EnsureInitialized()
    {
        if (_state.State.PayrollPeriodId == Guid.Empty)
            throw new InvalidOperationException("Payroll period grain not initialized");
    }
}
