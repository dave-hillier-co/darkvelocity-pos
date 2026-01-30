using DarkVelocity.Auth.Api.Data;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Auth.Api.EventHandlers;

/// <summary>
/// Handles employee termination events to deactivate the associated user account.
/// When an employee is terminated, their POS user account should be deactivated.
/// </summary>
public class EmployeeTerminatedHandler : IEventHandler<EmployeeTerminated>
{
    private readonly AuthDbContext _context;
    private readonly ILogger<EmployeeTerminatedHandler> _logger;

    public EmployeeTerminatedHandler(AuthDbContext context, ILogger<EmployeeTerminatedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(EmployeeTerminated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling EmployeeTerminated event for employee {EmployeeId}, user {UserId}",
            @event.EmployeeId,
            @event.UserId);

        var user = await _context.PosUsers.FindAsync(new object[] { @event.UserId }, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning(
                "User {UserId} not found when handling EmployeeTerminated event for employee {EmployeeId}",
                @event.UserId,
                @event.EmployeeId);
            return;
        }

        if (!user.IsActive)
        {
            _logger.LogDebug(
                "User {UserId} is already inactive, skipping deactivation",
                @event.UserId);
            return;
        }

        user.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deactivated user {UserId} due to employee {EmployeeId} termination",
            @event.UserId,
            @event.EmployeeId);
    }
}

/// <summary>
/// Handles employee status change events to sync user account status.
/// </summary>
public class EmployeeStatusChangedHandler : IEventHandler<EmployeeStatusChanged>
{
    private readonly AuthDbContext _context;
    private readonly ILogger<EmployeeStatusChangedHandler> _logger;

    public EmployeeStatusChangedHandler(AuthDbContext context, ILogger<EmployeeStatusChangedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(EmployeeStatusChanged @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling EmployeeStatusChanged event for employee user {UserId}: {OldStatus} -> {NewStatus}",
            @event.UserId,
            @event.OldStatus,
            @event.NewStatus);

        var user = await _context.PosUsers.FindAsync(new object[] { @event.UserId }, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning(
                "User {UserId} not found when handling EmployeeStatusChanged event",
                @event.UserId);
            return;
        }

        // Sync user active status based on employee status
        var shouldBeActive = @event.NewStatus != "terminated" && @event.NewStatus != "inactive";

        if (user.IsActive != shouldBeActive)
        {
            user.IsActive = shouldBeActive;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated user {UserId} active status to {IsActive} based on employee status {EmployeeStatus}",
                @event.UserId,
                shouldBeActive,
                @event.NewStatus);
        }
    }
}

/// <summary>
/// Handles employee location changes to sync user location access.
/// </summary>
public class EmployeeLocationAddedHandler : IEventHandler<EmployeeLocationAdded>
{
    private readonly AuthDbContext _context;
    private readonly ILogger<EmployeeLocationAddedHandler> _logger;

    public EmployeeLocationAddedHandler(AuthDbContext context, ILogger<EmployeeLocationAddedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(EmployeeLocationAdded @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling EmployeeLocationAdded event: granting user {UserId} access to location {LocationId}",
            @event.UserId,
            @event.LocationId);

        // Check if user already has access
        var existingAccess = await _context.UserLocationAccess
            .FirstOrDefaultAsync(
                ula => ula.UserId == @event.UserId && ula.LocationId == @event.LocationId,
                cancellationToken);

        if (existingAccess != null)
        {
            _logger.LogDebug(
                "User {UserId} already has access to location {LocationId}",
                @event.UserId,
                @event.LocationId);
            return;
        }

        _context.UserLocationAccess.Add(new Entities.UserLocationAccess
        {
            UserId = @event.UserId,
            LocationId = @event.LocationId
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Granted user {UserId} access to location {LocationId}",
            @event.UserId,
            @event.LocationId);
    }
}

/// <summary>
/// Handles employee location removal to revoke user location access.
/// </summary>
public class EmployeeLocationRemovedHandler : IEventHandler<EmployeeLocationRemoved>
{
    private readonly AuthDbContext _context;
    private readonly ILogger<EmployeeLocationRemovedHandler> _logger;

    public EmployeeLocationRemovedHandler(AuthDbContext context, ILogger<EmployeeLocationRemovedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(EmployeeLocationRemoved @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling EmployeeLocationRemoved event: revoking user {UserId} access to location {LocationId}",
            @event.UserId,
            @event.LocationId);

        var access = await _context.UserLocationAccess
            .FirstOrDefaultAsync(
                ula => ula.UserId == @event.UserId && ula.LocationId == @event.LocationId,
                cancellationToken);

        if (access == null)
        {
            _logger.LogDebug(
                "User {UserId} does not have access to location {LocationId}, nothing to revoke",
                @event.UserId,
                @event.LocationId);
            return;
        }

        // Don't revoke access to home location
        var user = await _context.PosUsers.FindAsync(new object[] { @event.UserId }, cancellationToken);
        if (user != null && user.HomeLocationId == @event.LocationId)
        {
            _logger.LogWarning(
                "Cannot revoke access to home location {LocationId} for user {UserId}",
                @event.LocationId,
                @event.UserId);
            return;
        }

        _context.UserLocationAccess.Remove(access);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Revoked user {UserId} access to location {LocationId}",
            @event.UserId,
            @event.LocationId);
    }
}
