using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class EmployeeGrain : JournaledGrain<EmployeeState, IEmployeeEvent>, IEmployeeGrain
{
    private Lazy<IAsyncStream<IStreamEvent>>? _employeeStream;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _employeeStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.EmployeeStreamNamespace, State.OrganizationId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });

        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(EmployeeState state, IEmployeeEvent @event)
    {
        switch (@event)
        {
            case EmployeeCreated e:
                state.Id = e.EmployeeId;
                state.OrganizationId = e.OrganizationId;
                state.UserId = e.UserId;
                state.EmployeeNumber = e.EmployeeNumber;
                state.FirstName = e.FirstName;
                state.LastName = e.LastName;
                state.Email = e.Email;
                state.EmploymentType = e.EmploymentType;
                state.HireDate = e.HireDate;
                state.DefaultSiteId = e.DefaultSiteId;
                state.AllowedSiteIds = [e.DefaultSiteId];
                state.Status = EmployeeStatus.Active;
                state.CreatedAt = e.OccurredAt;
                break;

            case EmployeeProfileUpdated e:
                if (e.FirstName != null) state.FirstName = e.FirstName;
                if (e.LastName != null) state.LastName = e.LastName;
                if (e.Email != null) state.Email = e.Email;
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeStatusChanged e:
                state.Status = e.NewStatus;
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeRoleAssigned e:
                // Add role assignment - handled differently in grain logic
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeRoleRevoked e:
                // Remove role assignment - handled differently in grain logic
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeSiteAssigned e:
                if (!state.AllowedSiteIds.Contains(e.SiteId))
                {
                    state.AllowedSiteIds.Add(e.SiteId);
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeSiteRemoved e:
                state.AllowedSiteIds.Remove(e.SiteId);
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeClockedIn e:
                state.CurrentTimeEntry = new TimeEntry
                {
                    Id = Guid.NewGuid(),
                    SiteId = e.SiteId,
                    ShiftId = e.ShiftId,
                    ClockIn = e.OccurredAt
                };
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeClockedOut e:
                if (state.CurrentTimeEntry != null)
                {
                    var entry = state.CurrentTimeEntry;
                    entry.ClockOut = e.OccurredAt;
                    entry.TotalHours = e.TotalHours;
                    state.RecentTimeEntries.Insert(0, entry);
                    if (state.RecentTimeEntries.Count > 50)
                    {
                        state.RecentTimeEntries.RemoveAt(state.RecentTimeEntries.Count - 1);
                    }
                    state.CurrentTimeEntry = null;
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeePayRateChanged e:
                if (e.RateType == "hourly")
                {
                    state.HourlyRate = e.NewRate;
                }
                else if (e.RateType == "salary")
                {
                    state.SalaryAmount = e.NewRate;
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeTerminated e:
                state.Status = EmployeeStatus.Terminated;
                state.TerminationDate = e.TerminationDate;
                state.TerminationReason = e.Reason;
                // Clock out if currently clocked in
                if (state.CurrentTimeEntry != null)
                {
                    var entry = state.CurrentTimeEntry;
                    entry.ClockOut = e.OccurredAt;
                    entry.TotalHours = (decimal)(entry.ClockOut.Value - entry.ClockIn).TotalHours;
                    state.RecentTimeEntries.Insert(0, entry);
                    state.CurrentTimeEntry = null;
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case EmployeeRehired e:
                state.Status = EmployeeStatus.Active;
                state.HireDate = e.RehireDate;
                state.DefaultSiteId = e.DefaultSiteId;
                state.TerminationDate = null;
                state.TerminationReason = null;
                if (!state.AllowedSiteIds.Contains(e.DefaultSiteId))
                {
                    state.AllowedSiteIds.Add(e.DefaultSiteId);
                }
                state.UpdatedAt = e.OccurredAt;
                break;
        }
    }

    public async Task<EmployeeCreatedResult> CreateAsync(CreateEmployeeCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Employee already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, employeeId) = GrainKeys.ParseOrgEntity(key);

        RaiseEvent(new EmployeeCreated
        {
            EmployeeId = employeeId,
            OrganizationId = command.OrganizationId,
            UserId = command.UserId,
            DefaultSiteId = command.DefaultSiteId,
            EmployeeNumber = command.EmployeeNumber,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email,
            Phone = null,
            EmploymentType = command.EmploymentType,
            HireDate = command.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish employee created event
        if (State.OrganizationId != Guid.Empty)
        {
            await _employeeStream!.Value.OnNextAsync(new EmployeeCreatedEvent(
                employeeId,
                command.UserId,
                command.DefaultSiteId,
                command.EmployeeNumber,
                command.FirstName,
                command.LastName,
                command.Email,
                command.EmploymentType,
                State.HireDate)
            {
                OrganizationId = command.OrganizationId
            });
        }

        return new EmployeeCreatedResult(employeeId, command.EmployeeNumber, State.CreatedAt);
    }

    public async Task<EmployeeUpdatedResult> UpdateAsync(UpdateEmployeeCommand command)
    {
        EnsureExists();

        var changedFields = new List<string>();
        string? newFirstName = null;
        string? newLastName = null;
        string? newEmail = null;

        if (command.FirstName != null && State.FirstName != command.FirstName)
        {
            newFirstName = command.FirstName;
            changedFields.Add(nameof(command.FirstName));
        }

        if (command.LastName != null && State.LastName != command.LastName)
        {
            newLastName = command.LastName;
            changedFields.Add(nameof(command.LastName));
        }

        if (command.Email != null && State.Email != command.Email)
        {
            newEmail = command.Email;
            changedFields.Add(nameof(command.Email));
        }

        if (newFirstName != null || newLastName != null || newEmail != null)
        {
            RaiseEvent(new EmployeeProfileUpdated
            {
                EmployeeId = State.Id,
                FirstName = newFirstName,
                LastName = newLastName,
                Email = newEmail,
                UpdatedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
        }

        // Handle pay rate changes
        if (command.HourlyRate != null && State.HourlyRate != command.HourlyRate)
        {
            RaiseEvent(new EmployeePayRateChanged
            {
                EmployeeId = State.Id,
                OldRate = State.HourlyRate ?? 0,
                NewRate = command.HourlyRate.Value,
                RateType = "hourly",
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ChangedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
            changedFields.Add(nameof(command.HourlyRate));
        }

        if (command.SalaryAmount != null && State.SalaryAmount != command.SalaryAmount)
        {
            RaiseEvent(new EmployeePayRateChanged
            {
                EmployeeId = State.Id,
                OldRate = State.SalaryAmount ?? 0,
                NewRate = command.SalaryAmount.Value,
                RateType = "salary",
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ChangedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
            changedFields.Add(nameof(command.SalaryAmount));
        }

        if (command.PayFrequency != null)
        {
            State.PayFrequency = command.PayFrequency;
            changedFields.Add(nameof(command.PayFrequency));
        }

        await ConfirmEvents();

        // Publish employee updated event
        if (changedFields.Count > 0 && State.OrganizationId != Guid.Empty)
        {
            await _employeeStream!.Value.OnNextAsync(new EmployeeUpdatedEvent(
                State.Id,
                State.UserId,
                changedFields)
            {
                OrganizationId = State.OrganizationId
            });
        }

        return new EmployeeUpdatedResult(Version, State.UpdatedAt ?? DateTime.UtcNow);
    }

    public Task<EmployeeState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task AssignRoleAsync(AssignRoleCommand command)
    {
        EnsureExists();

        var existingRole = State.RoleAssignments.FirstOrDefault(r => r.RoleId == command.RoleId);
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
                foreach (var role in State.RoleAssignments.Where(r => r.IsPrimary))
                {
                    role.IsPrimary = false;
                }
            }

            State.RoleAssignments.Add(new EmployeeRoleAssignment
            {
                RoleId = command.RoleId,
                RoleName = command.RoleName,
                Department = command.Department,
                IsPrimary = command.IsPrimary,
                HourlyRateOverride = command.HourlyRateOverride,
                AssignedAt = DateTime.UtcNow
            });
        }

        RaiseEvent(new EmployeeRoleAssigned
        {
            EmployeeId = State.Id,
            RoleName = command.RoleName,
            SiteId = null,
            AssignedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RemoveRoleAsync(Guid roleId)
    {
        EnsureExists();

        var role = State.RoleAssignments.FirstOrDefault(r => r.RoleId == roleId);
        if (role != null)
        {
            State.RoleAssignments.Remove(role);

            // If removed role was primary, promote another role
            if (role.IsPrimary && State.RoleAssignments.Count > 0)
            {
                State.RoleAssignments[0].IsPrimary = true;
            }

            RaiseEvent(new EmployeeRoleRevoked
            {
                EmployeeId = State.Id,
                RoleName = role.RoleName,
                SiteId = null,
                RevokedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task GrantSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        if (!State.AllowedSiteIds.Contains(siteId))
        {
            RaiseEvent(new EmployeeSiteAssigned
            {
                EmployeeId = State.Id,
                SiteId = siteId,
                AssignedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task RevokeSiteAccessAsync(Guid siteId)
    {
        EnsureExists();

        // Cannot revoke default site
        if (siteId == State.DefaultSiteId)
            throw new InvalidOperationException("Cannot revoke access to default site");

        if (State.AllowedSiteIds.Contains(siteId))
        {
            RaiseEvent(new EmployeeSiteRemoved
            {
                EmployeeId = State.Id,
                SiteId = siteId,
                RemovedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();
        }
    }

    public async Task ActivateAsync()
    {
        EnsureExists();

        if (State.Status == EmployeeStatus.Terminated)
            throw new InvalidOperationException("Cannot reactivate terminated employee");

        var oldStatus = State.Status;
        if (oldStatus != EmployeeStatus.Active)
        {
            RaiseEvent(new EmployeeStatusChanged
            {
                EmployeeId = State.Id,
                OldStatus = oldStatus,
                NewStatus = EmployeeStatus.Active,
                Reason = null,
                ChangedBy = null,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            // Publish status change event
            if (State.OrganizationId != Guid.Empty)
            {
                await _employeeStream!.Value.OnNextAsync(new EmployeeStatusChangedEvent(
                    State.Id,
                    State.UserId,
                    oldStatus,
                    EmployeeStatus.Active)
                {
                    OrganizationId = State.OrganizationId
                });
            }
        }
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        var oldStatus = State.Status;
        if (oldStatus != EmployeeStatus.Inactive)
        {
            RaiseEvent(new EmployeeStatusChanged
            {
                EmployeeId = State.Id,
                OldStatus = oldStatus,
                NewStatus = EmployeeStatus.Inactive,
                Reason = null,
                ChangedBy = null,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            // Publish status change event
            if (State.OrganizationId != Guid.Empty)
            {
                await _employeeStream!.Value.OnNextAsync(new EmployeeStatusChangedEvent(
                    State.Id,
                    State.UserId,
                    oldStatus,
                    EmployeeStatus.Inactive)
                {
                    OrganizationId = State.OrganizationId
                });
            }
        }
    }

    public async Task SetOnLeaveAsync()
    {
        EnsureExists();

        var oldStatus = State.Status;
        if (oldStatus != EmployeeStatus.OnLeave)
        {
            RaiseEvent(new EmployeeStatusChanged
            {
                EmployeeId = State.Id,
                OldStatus = oldStatus,
                NewStatus = EmployeeStatus.OnLeave,
                Reason = null,
                ChangedBy = null,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            if (State.OrganizationId != Guid.Empty)
            {
                await _employeeStream!.Value.OnNextAsync(new EmployeeStatusChangedEvent(
                    State.Id,
                    State.UserId,
                    oldStatus,
                    EmployeeStatus.OnLeave)
                {
                    OrganizationId = State.OrganizationId
                });
            }
        }
    }

    public async Task TerminateAsync(DateOnly terminationDate, string? reason = null)
    {
        EnsureExists();

        RaiseEvent(new EmployeeTerminated
        {
            EmployeeId = State.Id,
            TerminationDate = terminationDate,
            Reason = reason,
            TerminatedBy = Guid.Empty,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish termination event
        if (State.OrganizationId != Guid.Empty)
        {
            await _employeeStream!.Value.OnNextAsync(new EmployeeTerminatedEvent(
                State.Id,
                State.UserId,
                terminationDate,
                reason)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task<ClockInResult> ClockInAsync(ClockInCommand command)
    {
        EnsureExists();

        if (State.Status != EmployeeStatus.Active)
            throw new InvalidOperationException("Only active employees can clock in");

        if (State.CurrentTimeEntry != null)
            throw new InvalidOperationException("Employee is already clocked in");

        if (!State.AllowedSiteIds.Contains(command.SiteId))
            throw new InvalidOperationException("Employee does not have access to this site");

        RaiseEvent(new EmployeeClockedIn
        {
            EmployeeId = State.Id,
            SiteId = command.SiteId,
            ShiftId = command.ShiftId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish clock in event
        if (State.OrganizationId != Guid.Empty)
        {
            await _employeeStream!.Value.OnNextAsync(new EmployeeClockedInEvent(
                State.Id,
                State.UserId,
                command.SiteId,
                State.CurrentTimeEntry!.ClockIn,
                command.ShiftId)
            {
                OrganizationId = State.OrganizationId
            });
        }

        return new ClockInResult(State.CurrentTimeEntry!.Id, State.CurrentTimeEntry.ClockIn);
    }

    public async Task<ClockOutResult> ClockOutAsync(ClockOutCommand command)
    {
        EnsureExists();

        if (State.CurrentTimeEntry == null)
            throw new InvalidOperationException("Employee is not clocked in");

        var entry = State.CurrentTimeEntry;
        var clockOutTime = DateTime.UtcNow;
        var totalHours = (decimal)(clockOutTime - entry.ClockIn).TotalHours;

        // Set notes before the event (not captured in journaled event)
        entry.Notes = command.Notes;

        RaiseEvent(new EmployeeClockedOut
        {
            EmployeeId = State.Id,
            SiteId = entry.SiteId,
            TotalHours = totalHours,
            OccurredAt = clockOutTime
        });
        await ConfirmEvents();

        // Publish clock out event
        if (State.OrganizationId != Guid.Empty)
        {
            await _employeeStream!.Value.OnNextAsync(new EmployeeClockedOutEvent(
                State.Id,
                State.UserId,
                entry.SiteId,
                clockOutTime,
                totalHours)
            {
                OrganizationId = State.OrganizationId
            });
        }

        // Get the entry that was just moved to recent entries
        var completedEntry = State.RecentTimeEntries.FirstOrDefault();
        return new ClockOutResult(
            completedEntry?.Id ?? Guid.Empty,
            clockOutTime,
            totalHours);
    }

    public Task<bool> IsClockedInAsync()
    {
        return Task.FromResult(State.CurrentTimeEntry != null);
    }

    public async Task SyncFromUserAsync(string? firstName, string? lastName, UserStatus userStatus)
    {
        if (State.Id == Guid.Empty)
            return; // Employee doesn't exist yet

        var hasProfileChanges = false;
        string? newFirstName = null;
        string? newLastName = null;

        if (firstName != null && State.FirstName != firstName)
        {
            newFirstName = firstName;
            hasProfileChanges = true;
        }

        if (lastName != null && State.LastName != lastName)
        {
            newLastName = lastName;
            hasProfileChanges = true;
        }

        if (hasProfileChanges)
        {
            RaiseEvent(new EmployeeProfileUpdated
            {
                EmployeeId = State.Id,
                FirstName = newFirstName,
                LastName = newLastName,
                Email = null,
                UpdatedBy = Guid.Empty,
                OccurredAt = DateTime.UtcNow
            });
        }

        // Sync status (but don't override terminated)
        if (State.Status != EmployeeStatus.Terminated)
        {
            var newStatus = userStatus switch
            {
                UserStatus.Active => EmployeeStatus.Active,
                UserStatus.Inactive => EmployeeStatus.Inactive,
                UserStatus.Locked => EmployeeStatus.Inactive,
                _ => State.Status
            };

            if (State.Status != newStatus)
            {
                RaiseEvent(new EmployeeStatusChanged
                {
                    EmployeeId = State.Id,
                    OldStatus = State.Status,
                    NewStatus = newStatus,
                    Reason = "Synced from user status",
                    ChangedBy = null,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.Id != Guid.Empty);
    }

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Employee does not exist");
    }
}
