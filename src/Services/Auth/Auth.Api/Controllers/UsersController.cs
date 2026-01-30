using DarkVelocity.Auth.Api.Data;
using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Auth.Api.Entities;
using DarkVelocity.Auth.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Contracts.Hal;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Auth.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/users")]
public class UsersController : ControllerBase
{
    private readonly AuthDbContext _context;
    private readonly IAuthService _authService;
    private readonly IEventBus _eventBus;

    public UsersController(AuthDbContext context, IAuthService authService, IEventBus eventBus)
    {
        _context = context;
        _authService = authService;
        _eventBus = eventBus;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<UserDto>>> GetUsers(Guid locationId)
    {
        var users = await _context.PosUsers
            .Include(u => u.UserGroup)
            .Include(u => u.LocationAccess)
            .Where(u => u.HomeLocationId == locationId ||
                        u.LocationAccess.Any(la => la.LocationId == locationId))
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                UserGroupName = u.UserGroup!.Name,
                HomeLocationId = u.HomeLocationId,
                IsActive = u.IsActive
            })
            .ToListAsync();

        foreach (var user in users)
        {
            user.AddSelfLink($"/api/locations/{locationId}/users/{user.Id}");
        }

        return Ok(HalCollection<UserDto>.Create(
            users,
            $"/api/locations/{locationId}/users",
            users.Count
        ));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid locationId, Guid userId)
    {
        var user = await _context.PosUsers
            .Include(u => u.UserGroup)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserGroupName = user.UserGroup!.Name,
            HomeLocationId = user.HomeLocationId,
            IsActive = user.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/users/{user.Id}");
        dto.AddLink("location", $"/api/locations/{user.HomeLocationId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(Guid locationId, [FromBody] CreateUserRequest request)
    {
        var existingUser = await _context.PosUsers
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingUser != null)
            return Conflict(new { message = "Username already exists" });

        var userGroup = await _context.UserGroups.FindAsync(request.UserGroupId);
        if (userGroup == null)
            return BadRequest(new { message = "Invalid user group" });

        var location = await _context.Locations.FindAsync(request.HomeLocationId);
        if (location == null)
            return BadRequest(new { message = "Invalid location" });

        var user = new PosUser
        {
            Username = request.Username,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PinHash = _authService.HashPin(request.Pin),
            UserGroupId = request.UserGroupId,
            HomeLocationId = request.HomeLocationId,
            QrCodeToken = Guid.NewGuid().ToString("N")
        };

        _context.PosUsers.Add(user);

        // Add access to home location
        _context.UserLocationAccess.Add(new UserLocationAccess
        {
            UserId = user.Id,
            LocationId = request.HomeLocationId
        });

        await _context.SaveChangesAsync();

        // Publish UserCreated event
        await _eventBus.PublishAsync(new UserCreated(
            UserId: user.Id,
            TenantId: location.TenantId,
            HomeLocationId: user.HomeLocationId,
            Username: user.Username,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            UserGroupName: userGroup.Name
        ));

        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserGroupName = userGroup.Name,
            HomeLocationId = user.HomeLocationId,
            IsActive = user.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/users/{user.Id}");

        return CreatedAtAction(nameof(GetUser), new { locationId, userId = user.Id }, dto);
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid locationId,
        Guid userId,
        [FromBody] UpdateUserRequest request)
    {
        var user = await _context.PosUsers
            .Include(u => u.UserGroup)
            .Include(u => u.HomeLocation)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        var changedFields = new List<string>();
        var oldUserGroupId = user.UserGroupId;
        var oldUserGroupName = user.UserGroup?.Name ?? "";
        var wasActive = user.IsActive;

        if (request.FirstName != null && user.FirstName != request.FirstName)
        {
            user.FirstName = request.FirstName;
            changedFields.Add("FirstName");
        }

        if (request.LastName != null && user.LastName != request.LastName)
        {
            user.LastName = request.LastName;
            changedFields.Add("LastName");
        }

        if (request.Email != null && user.Email != request.Email)
        {
            user.Email = request.Email;
            changedFields.Add("Email");
        }

        if (request.Pin != null)
        {
            user.PinHash = _authService.HashPin(request.Pin);
            changedFields.Add("Pin");
        }

        if (request.UserGroupId.HasValue && user.UserGroupId != request.UserGroupId.Value)
        {
            var userGroup = await _context.UserGroups.FindAsync(request.UserGroupId.Value);
            if (userGroup == null)
                return BadRequest(new { message = "Invalid user group" });
            user.UserGroupId = request.UserGroupId.Value;
            changedFields.Add("UserGroup");
        }

        if (request.IsActive.HasValue && user.IsActive != request.IsActive.Value)
        {
            user.IsActive = request.IsActive.Value;
            changedFields.Add("IsActive");
        }

        await _context.SaveChangesAsync();

        // Reload user group
        await _context.Entry(user).Reference(u => u.UserGroup).LoadAsync();

        var tenantId = user.HomeLocation?.TenantId ?? Guid.Empty;

        // Publish events based on changes
        if (changedFields.Count > 0)
        {
            await _eventBus.PublishAsync(new UserUpdated(
                UserId: user.Id,
                TenantId: tenantId,
                ChangedFields: changedFields,
                FirstName: user.FirstName,
                LastName: user.LastName,
                Email: user.Email,
                UserGroupName: user.UserGroup?.Name
            ));
        }

        // Publish specific events for status changes
        if (changedFields.Contains("IsActive"))
        {
            if (user.IsActive && !wasActive)
            {
                await _eventBus.PublishAsync(new UserReactivated(
                    UserId: user.Id,
                    TenantId: tenantId,
                    HomeLocationId: user.HomeLocationId
                ));
            }
            else if (!user.IsActive && wasActive)
            {
                await _eventBus.PublishAsync(new UserDeactivated(
                    UserId: user.Id,
                    TenantId: tenantId,
                    HomeLocationId: user.HomeLocationId
                ));
            }
        }

        // Publish event for user group change
        if (changedFields.Contains("UserGroup"))
        {
            await _eventBus.PublishAsync(new UserGroupChanged(
                UserId: user.Id,
                TenantId: tenantId,
                OldUserGroupId: oldUserGroupId,
                OldUserGroupName: oldUserGroupName,
                NewUserGroupId: user.UserGroupId,
                NewUserGroupName: user.UserGroup?.Name ?? ""
            ));
        }

        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserGroupName = user.UserGroup!.Name,
            HomeLocationId = user.HomeLocationId,
            IsActive = user.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/users/{user.Id}");

        return Ok(dto);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid locationId, Guid userId)
    {
        var user = await _context.PosUsers
            .Include(u => u.HomeLocation)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        user.IsActive = false;
        await _context.SaveChangesAsync();

        // Publish UserDeactivated event
        await _eventBus.PublishAsync(new UserDeactivated(
            UserId: user.Id,
            TenantId: user.HomeLocation?.TenantId ?? Guid.Empty,
            HomeLocationId: user.HomeLocationId
        ));

        return NoContent();
    }
}
