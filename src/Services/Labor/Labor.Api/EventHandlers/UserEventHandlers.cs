using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Labor.Api.EventHandlers;

/// <summary>
/// Handles user deactivation events to update employee status.
/// When a user account is deactivated, the associated employee should be marked as inactive.
/// </summary>
public class UserDeactivatedHandler : IEventHandler<UserDeactivated>
{
    private readonly LaborDbContext _context;
    private readonly ILogger<UserDeactivatedHandler> _logger;

    public UserDeactivatedHandler(LaborDbContext context, ILogger<UserDeactivatedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(UserDeactivated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling UserDeactivated event for user {UserId}",
            @event.UserId);

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == @event.UserId, cancellationToken);

        if (employee == null)
        {
            _logger.LogDebug(
                "No employee found for user {UserId}, skipping status update",
                @event.UserId);
            return;
        }

        if (employee.Status == "inactive" || employee.Status == "terminated")
        {
            _logger.LogDebug(
                "Employee {EmployeeId} already has status {Status}, skipping update",
                employee.Id,
                employee.Status);
            return;
        }

        employee.Status = "inactive";
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated employee {EmployeeId} status to inactive due to user {UserId} deactivation",
            employee.Id,
            @event.UserId);
    }
}

/// <summary>
/// Handles user reactivation events to update employee status.
/// </summary>
public class UserReactivatedHandler : IEventHandler<UserReactivated>
{
    private readonly LaborDbContext _context;
    private readonly ILogger<UserReactivatedHandler> _logger;

    public UserReactivatedHandler(LaborDbContext context, ILogger<UserReactivatedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(UserReactivated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling UserReactivated event for user {UserId}",
            @event.UserId);

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == @event.UserId, cancellationToken);

        if (employee == null)
        {
            _logger.LogDebug(
                "No employee found for user {UserId}, skipping status update",
                @event.UserId);
            return;
        }

        // Don't reactivate terminated employees
        if (employee.Status == "terminated")
        {
            _logger.LogWarning(
                "Cannot reactivate terminated employee {EmployeeId}",
                employee.Id);
            return;
        }

        if (employee.Status == "active")
        {
            _logger.LogDebug(
                "Employee {EmployeeId} is already active, skipping update",
                employee.Id);
            return;
        }

        employee.Status = "active";
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated employee {EmployeeId} status to active due to user {UserId} reactivation",
            employee.Id,
            @event.UserId);
    }
}

/// <summary>
/// Handles user update events to sync employee information.
/// </summary>
public class UserUpdatedHandler : IEventHandler<UserUpdated>
{
    private readonly LaborDbContext _context;
    private readonly ILogger<UserUpdatedHandler> _logger;

    public UserUpdatedHandler(LaborDbContext context, ILogger<UserUpdatedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(UserUpdated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling UserUpdated event for user {UserId}, changed fields: {ChangedFields}",
            @event.UserId,
            string.Join(", ", @event.ChangedFields));

        // Only sync name and email changes
        var relevantChanges = @event.ChangedFields
            .Intersect(new[] { "FirstName", "LastName", "Email" })
            .ToList();

        if (relevantChanges.Count == 0)
        {
            _logger.LogDebug(
                "No relevant changes for employee sync in UserUpdated event for user {UserId}",
                @event.UserId);
            return;
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == @event.UserId, cancellationToken);

        if (employee == null)
        {
            _logger.LogDebug(
                "No employee found for user {UserId}, skipping info sync",
                @event.UserId);
            return;
        }

        var updated = false;

        if (relevantChanges.Contains("FirstName") && @event.FirstName != null && employee.FirstName != @event.FirstName)
        {
            employee.FirstName = @event.FirstName;
            updated = true;
        }

        if (relevantChanges.Contains("LastName") && @event.LastName != null && employee.LastName != @event.LastName)
        {
            employee.LastName = @event.LastName;
            updated = true;
        }

        if (relevantChanges.Contains("Email") && @event.Email != null && employee.Email != @event.Email)
        {
            employee.Email = @event.Email;
            updated = true;
        }

        if (updated)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Synced employee {EmployeeId} info from user {UserId} update",
                employee.Id,
                @event.UserId);
        }
    }
}

/// <summary>
/// Handles user location access granted events to add location to employee's allowed locations.
/// </summary>
public class UserLocationAccessGrantedHandler : IEventHandler<UserLocationAccessGranted>
{
    private readonly LaborDbContext _context;
    private readonly ILogger<UserLocationAccessGrantedHandler> _logger;

    public UserLocationAccessGrantedHandler(LaborDbContext context, ILogger<UserLocationAccessGrantedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(UserLocationAccessGranted @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling UserLocationAccessGranted event: adding location {LocationId} for user {UserId}",
            @event.LocationId,
            @event.UserId);

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == @event.UserId, cancellationToken);

        if (employee == null)
        {
            _logger.LogDebug(
                "No employee found for user {UserId}, skipping location sync",
                @event.UserId);
            return;
        }

        if (employee.AllowedLocationIds.Contains(@event.LocationId))
        {
            _logger.LogDebug(
                "Employee {EmployeeId} already has access to location {LocationId}",
                employee.Id,
                @event.LocationId);
            return;
        }

        employee.AllowedLocationIds.Add(@event.LocationId);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added location {LocationId} to employee {EmployeeId} allowed locations",
            @event.LocationId,
            employee.Id);
    }
}

/// <summary>
/// Handles user location access revoked events to remove location from employee's allowed locations.
/// </summary>
public class UserLocationAccessRevokedHandler : IEventHandler<UserLocationAccessRevoked>
{
    private readonly LaborDbContext _context;
    private readonly ILogger<UserLocationAccessRevokedHandler> _logger;

    public UserLocationAccessRevokedHandler(LaborDbContext context, ILogger<UserLocationAccessRevokedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(UserLocationAccessRevoked @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling UserLocationAccessRevoked event: removing location {LocationId} for user {UserId}",
            @event.LocationId,
            @event.UserId);

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == @event.UserId, cancellationToken);

        if (employee == null)
        {
            _logger.LogDebug(
                "No employee found for user {UserId}, skipping location sync",
                @event.UserId);
            return;
        }

        // Don't remove the default location
        if (employee.DefaultLocationId == @event.LocationId)
        {
            _logger.LogWarning(
                "Cannot remove default location {LocationId} from employee {EmployeeId}",
                @event.LocationId,
                employee.Id);
            return;
        }

        if (!employee.AllowedLocationIds.Contains(@event.LocationId))
        {
            _logger.LogDebug(
                "Employee {EmployeeId} does not have access to location {LocationId}",
                employee.Id,
                @event.LocationId);
            return;
        }

        employee.AllowedLocationIds.Remove(@event.LocationId);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Removed location {LocationId} from employee {EmployeeId} allowed locations",
            @event.LocationId,
            employee.Id);
    }
}

/// <summary>
/// Handles user login events for audit logging.
/// </summary>
public class UserLoggedInAuditHandler : IEventHandler<UserLoggedIn>
{
    private readonly ILogger<UserLoggedInAuditHandler> _logger;

    public UserLoggedInAuditHandler(ILogger<UserLoggedInAuditHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(UserLoggedIn @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "User {UserId} ({Username}) logged in at location {LocationId} via {LoginMethod} from {IpAddress}",
            @event.UserId,
            @event.Username,
            @event.LocationId,
            @event.LoginMethod,
            @event.IpAddress ?? "unknown");

        return Task.CompletedTask;
    }
}
