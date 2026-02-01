using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class EmployeeGrain : Grain, IEmployeeGrain
{
    private readonly IPersistentState<EmployeeState> _state;
    private IAsyncStream<IStreamEvent>? _employeeStream;

    public EmployeeGrain(
        [PersistentState("employee", "OrleansStorage")]
        IPersistentState<EmployeeState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetEmployeeStream()
    {
        if (_employeeStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.EmployeeStreamNamespace, _state.State.OrganizationId.ToString());
            _employeeStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _employeeStream!;
    }

    public async Task<EmployeeCreatedResult> CreateAsync(CreateEmployeeCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Employee already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, employeeId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new EmployeeState
        {
            Id = employeeId,
            OrganizationId = command.OrganizationId,
            UserId = command.UserId,
            EmployeeNumber = command.EmployeeNumber,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email,
            EmploymentType = command.EmploymentType,
            HireDate = command.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DefaultSiteId = command.DefaultSiteId,
            AllowedSiteIds = [command.DefaultSiteId],
            Status = EmployeeStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Publish employee created event
        if (GetEmployeeStream() != null)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeCreatedEvent(
                employeeId,
                command.UserId,
                command.DefaultSiteId,
                command.EmployeeNumber,
                command.FirstName,
                command.LastName,
                command.Email,
                command.EmploymentType,
                _state.State.HireDate)
            {
                OrganizationId = command.OrganizationId
            });
        }

        return new EmployeeCreatedResult(employeeId, command.EmployeeNumber, _state.State.CreatedAt);
    }

    public async Task<EmployeeUpdatedResult> UpdateAsync(UpdateEmployeeCommand command)
    {
        EnsureExists();

        var changedFields = new List<string>();

        if (command.FirstName != null && _state.State.FirstName != command.FirstName)
        {
            _state.State.FirstName = command.FirstName;
            changedFields.Add(nameof(command.FirstName));
        }

        if (command.LastName != null && _state.State.LastName != command.LastName)
        {
            _state.State.LastName = command.LastName;
            changedFields.Add(nameof(command.LastName));
        }

        if (command.Email != null && _state.State.Email != command.Email)
        {
            _state.State.Email = command.Email;
            changedFields.Add(nameof(command.Email));
        }

        if (command.HourlyRate != null)
        {
            _state.State.HourlyRate = command.HourlyRate;
            changedFields.Add(nameof(command.HourlyRate));
        }

        if (command.SalaryAmount != null)
        {
            _state.State.SalaryAmount = command.SalaryAmount;
            changedFields.Add(nameof(command.SalaryAmount));
        }

        if (command.PayFrequency != null)
        {
            _state.State.PayFrequency = command.PayFrequency;
            changedFields.Add(nameof(command.PayFrequency));
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish employee updated event
        if (_employeeStream != null && changedFields.Count > 0)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeUpdatedEvent(
                _state.State.Id,
                _state.State.UserId,
                changedFields)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return new EmployeeUpdatedResult(_state.State.Version, _state.State.UpdatedAt.Value);
    }

    public Task<EmployeeState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task AssignRoleAsync(AssignRoleCommand command)
    {
        EnsureExists();

        var existingRole = _state.State.RoleAssignments.FirstOrDefault(r => r.RoleId == command.RoleId);
        if (existingRole != null)
        {
            existingRole.RoleName = command.RoleName;
            existingRole.Department = command.Department;
            existingRole.IsPrimary = command.IsPrimary;
            existingRole.HourlyRateOverride = command.HourlyRateOverride;
        }
        else
        {
            // If this is the primary role, demote other primary roles
            if (command.IsPrimary)
            {
                foreach (var role in _state.State.RoleAssignments.Where(r => r.IsPrimary))
                {
                    role.IsPrimary = false;
                }
            }

            _state.State.RoleAssignments.Add(new EmployeeRoleAssignment
            {
                RoleId = command.RoleId,
                RoleName = command.RoleName,
                Department = command.Department,
                IsPrimary = command.IsPrimary,
                HourlyRateOverride = command.HourlyRateOverride,
                AssignedAt = DateTime.UtcNow
            });
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveRoleAsync(Guid roleId)
    {
        EnsureExists();

        var role = _state.State.RoleAssignments.FirstOrDefault(r => r.RoleId == roleId);
        if (role != null)
        {
            _state.State.RoleAssignments.Remove(role);

            // If removed role was primary, promote another role
            if (role.IsPrimary && _state.State.RoleAssignments.Count > 0)
            {
                _state.State.RoleAssignments[0].IsPrimary = true;
            }

            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;

            await _state.WriteStateAsync();
        }
    }

    public async Task GrantSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        if (!_state.State.AllowedSiteIds.Contains(siteId))
        {
            _state.State.AllowedSiteIds.Add(siteId);
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;

            await _state.WriteStateAsync();
        }
    }

    public async Task RevokeSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        // Cannot revoke default site
        if (siteId == _state.State.DefaultSiteId)
            throw new InvalidOperationException("Cannot revoke access to default site");

        if (_state.State.AllowedSiteIds.Remove(siteId))
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;

            await _state.WriteStateAsync();
        }
    }

    public async Task ActivateAsync()
    {
        EnsureExists();

        if (_state.State.Status == EmployeeStatus.Terminated)
            throw new InvalidOperationException("Cannot reactivate terminated employee");

        var oldStatus = _state.State.Status;
        _state.State.Status = EmployeeStatus.Active;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish status change event
        if (_employeeStream != null && oldStatus != EmployeeStatus.Active)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeStatusChangedEvent(
                _state.State.Id,
                _state.State.UserId,
                oldStatus,
                EmployeeStatus.Active)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        var oldStatus = _state.State.Status;
        _state.State.Status = EmployeeStatus.Inactive;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish status change event
        if (_employeeStream != null && oldStatus != EmployeeStatus.Inactive)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeStatusChangedEvent(
                _state.State.Id,
                _state.State.UserId,
                oldStatus,
                EmployeeStatus.Inactive)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task SetOnLeaveAsync()
    {
        EnsureExists();

        var oldStatus = _state.State.Status;
        _state.State.Status = EmployeeStatus.OnLeave;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        if (_employeeStream != null && oldStatus != EmployeeStatus.OnLeave)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeStatusChangedEvent(
                _state.State.Id,
                _state.State.UserId,
                oldStatus,
                EmployeeStatus.OnLeave)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task TerminateAsync(DateOnly terminationDate, string? reason = null)
    {
        EnsureExists();

        var oldStatus = _state.State.Status;
        _state.State.Status = EmployeeStatus.Terminated;
        _state.State.TerminationDate = terminationDate;
        _state.State.TerminationReason = reason;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        // Clock out if currently clocked in
        if (_state.State.CurrentTimeEntry != null)
        {
            var entry = _state.State.CurrentTimeEntry;
            entry.ClockOut = DateTime.UtcNow;
            entry.TotalHours = (decimal)(entry.ClockOut.Value - entry.ClockIn).TotalHours;
            _state.State.RecentTimeEntries.Insert(0, entry);
            _state.State.CurrentTimeEntry = null;
        }

        await _state.WriteStateAsync();

        // Publish termination event
        if (GetEmployeeStream() != null)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeTerminatedEvent(
                _state.State.Id,
                _state.State.UserId,
                terminationDate,
                reason)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task<ClockInResult> ClockInAsync(ClockInCommand command)
    {
        EnsureExists();

        if (_state.State.Status != EmployeeStatus.Active)
            throw new InvalidOperationException("Only active employees can clock in");

        if (_state.State.CurrentTimeEntry != null)
            throw new InvalidOperationException("Employee is already clocked in");

        if (!_state.State.AllowedSiteIds.Contains(command.SiteId))
            throw new InvalidOperationException("Employee does not have access to this site");

        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            SiteId = command.SiteId,
            ShiftId = command.ShiftId,
            ClockIn = DateTime.UtcNow
        };

        _state.State.CurrentTimeEntry = entry;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish clock in event
        if (GetEmployeeStream() != null)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeClockedInEvent(
                _state.State.Id,
                _state.State.UserId,
                command.SiteId,
                entry.ClockIn,
                command.ShiftId)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return new ClockInResult(entry.Id, entry.ClockIn);
    }

    public async Task<ClockOutResult> ClockOutAsync(ClockOutCommand command)
    {
        EnsureExists();

        if (_state.State.CurrentTimeEntry == null)
            throw new InvalidOperationException("Employee is not clocked in");

        var entry = _state.State.CurrentTimeEntry;
        entry.ClockOut = DateTime.UtcNow;
        entry.TotalHours = (decimal)(entry.ClockOut.Value - entry.ClockIn).TotalHours;
        entry.Notes = command.Notes;

        // Move to recent entries (keep last 50)
        _state.State.RecentTimeEntries.Insert(0, entry);
        if (_state.State.RecentTimeEntries.Count > 50)
        {
            _state.State.RecentTimeEntries.RemoveAt(_state.State.RecentTimeEntries.Count - 1);
        }

        _state.State.CurrentTimeEntry = null;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish clock out event
        if (GetEmployeeStream() != null)
        {
            await GetEmployeeStream().OnNextAsync(new EmployeeClockedOutEvent(
                _state.State.Id,
                _state.State.UserId,
                entry.SiteId,
                entry.ClockOut.Value,
                entry.TotalHours.Value)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return new ClockOutResult(entry.Id, entry.ClockOut.Value, entry.TotalHours.Value);
    }

    public Task<bool> IsClockedInAsync()
    {
        return Task.FromResult(_state.State.CurrentTimeEntry != null);
    }

    public async Task SyncFromUserAsync(string? firstName, string? lastName, UserStatus userStatus)
    {
        if (_state.State.Id == Guid.Empty)
            return; // Employee doesn't exist yet

        var updated = false;

        if (firstName != null && _state.State.FirstName != firstName)
        {
            _state.State.FirstName = firstName;
            updated = true;
        }

        if (lastName != null && _state.State.LastName != lastName)
        {
            _state.State.LastName = lastName;
            updated = true;
        }

        // Sync status (but don't override terminated)
        if (_state.State.Status != EmployeeStatus.Terminated)
        {
            var newStatus = userStatus switch
            {
                UserStatus.Active => EmployeeStatus.Active,
                UserStatus.Inactive => EmployeeStatus.Inactive,
                UserStatus.Locked => EmployeeStatus.Inactive,
                _ => _state.State.Status
            };

            if (_state.State.Status != newStatus)
            {
                _state.State.Status = newStatus;
                updated = true;
            }
        }

        if (updated)
        {
            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Employee does not exist");
    }
}
