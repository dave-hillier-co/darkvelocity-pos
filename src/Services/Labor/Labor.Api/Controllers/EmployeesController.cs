using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Contracts.Hal;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly LaborDbContext _context;
    private readonly IEventBus _eventBus;

    public EmployeesController(LaborDbContext context, IEventBus eventBus)
    {
        _context = context;
        _eventBus = eventBus;
    }

    /// <summary>
    /// List all employees with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<EmployeeSummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? employmentType = null,
        [FromQuery] Guid? roleId = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.Employees
            .Include(e => e.DefaultRole)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(e => e.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(e => e.LocationId == locationId.Value || e.AllowedLocationIds.Contains(locationId.Value));

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);

        if (!string.IsNullOrEmpty(employmentType))
            query = query.Where(e => e.EmploymentType == employmentType);

        if (roleId.HasValue)
            query = query.Where(e => e.DefaultRoleId == roleId.Value || e.EmployeeRoles.Any(er => er.RoleId == roleId.Value));

        if (!string.IsNullOrEmpty(search))
            query = query.Where(e =>
                e.FirstName.Contains(search) ||
                e.LastName.Contains(search) ||
                e.Email.Contains(search) ||
                e.EmployeeNumber.Contains(search));

        var total = await query.CountAsync();

        var employees = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = employees.Select(e => MapToSummaryDto(e)).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/employees/{dto.Id}");

        return Ok(HalCollection<EmployeeSummaryDto>.Create(dtos, "/api/employees", total));
    }

    /// <summary>
    /// Get employee by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id)
    {
        var employee = await _context.Employees
            .Include(e => e.DefaultRole)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
            return NotFound();

        var dto = MapToDto(employee);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Create a new employee.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid locationId,
        [FromBody] CreateEmployeeRequest request)
    {
        // Check for duplicate employee number
        var existingNumber = await _context.Employees
            .AnyAsync(e => e.TenantId == tenantId && e.EmployeeNumber == request.EmployeeNumber);
        if (existingNumber)
            return BadRequest(new { message = "Employee number already exists" });

        // Check for duplicate email
        var existingEmail = await _context.Employees
            .AnyAsync(e => e.TenantId == tenantId && e.Email == request.Email);
        if (existingEmail)
            return BadRequest(new { message = "Email already exists" });

        // Get the role for the event
        var role = await _context.Roles.FindAsync(request.DefaultRoleId);

        var employee = new Employee
        {
            TenantId = tenantId,
            LocationId = locationId,
            UserId = request.UserId,
            EmployeeNumber = request.EmployeeNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            HireDate = request.HireDate,
            EmploymentType = request.EmploymentType,
            DefaultLocationId = request.DefaultLocationId ?? locationId,
            AllowedLocationIds = request.AllowedLocationIds ?? new List<Guid> { locationId },
            DefaultRoleId = request.DefaultRoleId,
            HourlyRate = request.HourlyRate,
            SalaryAmount = request.SalaryAmount,
            PayFrequency = request.PayFrequency,
            OvertimeRate = request.OvertimeRate,
            MaxHoursPerWeek = request.MaxHoursPerWeek,
            MinHoursPerWeek = request.MinHoursPerWeek
        };

        _context.Employees.Add(employee);

        // Add default role as primary employee role
        var employeeRole = new EmployeeRole
        {
            EmployeeId = employee.Id,
            RoleId = request.DefaultRoleId,
            IsPrimary = true
        };
        _context.EmployeeRoles.Add(employeeRole);

        await _context.SaveChangesAsync();

        // Publish EmployeeCreated event
        await _eventBus.PublishAsync(new EmployeeCreated(
            EmployeeId: employee.Id,
            TenantId: tenantId,
            UserId: employee.UserId,
            LocationId: locationId,
            EmployeeNumber: employee.EmployeeNumber,
            FirstName: employee.FirstName,
            LastName: employee.LastName,
            Email: employee.Email,
            EmploymentType: employee.EmploymentType,
            DefaultRoleId: employee.DefaultRoleId,
            DefaultRoleName: role?.Name ?? "",
            HireDate: employee.HireDate
        ));

        // Reload with navigation properties
        employee = await _context.Employees
            .Include(e => e.DefaultRole)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstAsync(e => e.Id == employee.Id);

        var dto = MapToDto(employee);
        AddLinks(dto);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, dto);
    }

    /// <summary>
    /// Update an employee.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request)
    {
        var employee = await _context.Employees
            .Include(e => e.DefaultRole)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
            return NotFound();

        var changedFields = new List<string>();
        var oldStatus = employee.Status;
        var oldDefaultRoleId = employee.DefaultRoleId;
        var oldDefaultRoleName = employee.DefaultRole?.Name ?? "";
        var oldHourlyRate = employee.HourlyRate;
        var oldSalaryAmount = employee.SalaryAmount;
        var oldPayFrequency = employee.PayFrequency;

        if (request.FirstName != null && employee.FirstName != request.FirstName)
        {
            employee.FirstName = request.FirstName;
            changedFields.Add("FirstName");
        }
        if (request.LastName != null && employee.LastName != request.LastName)
        {
            employee.LastName = request.LastName;
            changedFields.Add("LastName");
        }
        if (request.Email != null && employee.Email != request.Email)
        {
            employee.Email = request.Email;
            changedFields.Add("Email");
        }
        if (request.Phone != null && employee.Phone != request.Phone)
        {
            employee.Phone = request.Phone;
            changedFields.Add("Phone");
        }
        if (request.DateOfBirth.HasValue && employee.DateOfBirth != request.DateOfBirth)
        {
            employee.DateOfBirth = request.DateOfBirth;
            changedFields.Add("DateOfBirth");
        }
        if (request.EmploymentType != null && employee.EmploymentType != request.EmploymentType)
        {
            employee.EmploymentType = request.EmploymentType;
            changedFields.Add("EmploymentType");
        }
        if (request.Status != null && employee.Status != request.Status)
        {
            employee.Status = request.Status;
            changedFields.Add("Status");
        }
        if (request.DefaultLocationId.HasValue && employee.DefaultLocationId != request.DefaultLocationId)
        {
            employee.DefaultLocationId = request.DefaultLocationId.Value;
            changedFields.Add("DefaultLocationId");
        }
        if (request.AllowedLocationIds != null)
        {
            employee.AllowedLocationIds = request.AllowedLocationIds;
            changedFields.Add("AllowedLocationIds");
        }
        if (request.DefaultRoleId.HasValue && employee.DefaultRoleId != request.DefaultRoleId)
        {
            employee.DefaultRoleId = request.DefaultRoleId.Value;
            changedFields.Add("DefaultRoleId");
        }
        if (request.HourlyRate.HasValue && employee.HourlyRate != request.HourlyRate)
        {
            employee.HourlyRate = request.HourlyRate;
            changedFields.Add("HourlyRate");
        }
        if (request.SalaryAmount.HasValue && employee.SalaryAmount != request.SalaryAmount)
        {
            employee.SalaryAmount = request.SalaryAmount;
            changedFields.Add("SalaryAmount");
        }
        if (request.PayFrequency != null && employee.PayFrequency != request.PayFrequency)
        {
            employee.PayFrequency = request.PayFrequency;
            changedFields.Add("PayFrequency");
        }
        if (request.OvertimeRate.HasValue && employee.OvertimeRate != request.OvertimeRate)
        {
            employee.OvertimeRate = request.OvertimeRate.Value;
            changedFields.Add("OvertimeRate");
        }
        if (request.MaxHoursPerWeek.HasValue && employee.MaxHoursPerWeek != request.MaxHoursPerWeek)
        {
            employee.MaxHoursPerWeek = request.MaxHoursPerWeek;
            changedFields.Add("MaxHoursPerWeek");
        }
        if (request.MinHoursPerWeek.HasValue && employee.MinHoursPerWeek != request.MinHoursPerWeek)
        {
            employee.MinHoursPerWeek = request.MinHoursPerWeek;
            changedFields.Add("MinHoursPerWeek");
        }

        await _context.SaveChangesAsync();

        // Publish events based on changes
        if (changedFields.Count > 0)
        {
            await _eventBus.PublishAsync(new EmployeeUpdated(
                EmployeeId: employee.Id,
                TenantId: employee.TenantId,
                UserId: employee.UserId,
                ChangedFields: changedFields
            ));
        }

        // Publish status change event
        if (changedFields.Contains("Status") && oldStatus != employee.Status)
        {
            await _eventBus.PublishAsync(new EmployeeStatusChanged(
                EmployeeId: employee.Id,
                TenantId: employee.TenantId,
                UserId: employee.UserId,
                OldStatus: oldStatus,
                NewStatus: employee.Status
            ));
        }

        // Publish default role change event
        if (changedFields.Contains("DefaultRoleId"))
        {
            // Reload to get new role name
            await _context.Entry(employee).Reference(e => e.DefaultRole).LoadAsync();

            await _eventBus.PublishAsync(new EmployeeDefaultRoleChanged(
                EmployeeId: employee.Id,
                TenantId: employee.TenantId,
                UserId: employee.UserId,
                OldRoleId: oldDefaultRoleId,
                OldRoleName: oldDefaultRoleName,
                NewRoleId: employee.DefaultRoleId,
                NewRoleName: employee.DefaultRole?.Name ?? ""
            ));
        }

        // Publish compensation change event
        if (changedFields.Contains("HourlyRate") || changedFields.Contains("SalaryAmount") || changedFields.Contains("PayFrequency"))
        {
            await _eventBus.PublishAsync(new EmployeeCompensationChanged(
                EmployeeId: employee.Id,
                TenantId: employee.TenantId,
                UserId: employee.UserId,
                OldHourlyRate: oldHourlyRate,
                NewHourlyRate: employee.HourlyRate,
                OldSalaryAmount: oldSalaryAmount,
                NewSalaryAmount: employee.SalaryAmount,
                OldPayFrequency: oldPayFrequency,
                NewPayFrequency: employee.PayFrequency
            ));
        }

        var dto = MapToDto(employee);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Terminate an employee.
    /// </summary>
    [HttpPost("{id:guid}/terminate")]
    public async Task<ActionResult<EmployeeDto>> Terminate(
        Guid id,
        [FromBody] TerminateEmployeeRequest request)
    {
        var employee = await _context.Employees
            .Include(e => e.DefaultRole)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
            return NotFound();

        if (employee.Status == "terminated")
            return BadRequest(new { message = "Employee is already terminated" });

        employee.Status = "terminated";
        employee.TerminationDate = request.TerminationDate;

        await _context.SaveChangesAsync();

        // Publish EmployeeTerminated event
        await _eventBus.PublishAsync(new EmployeeTerminated(
            EmployeeId: employee.Id,
            TenantId: employee.TenantId,
            UserId: employee.UserId,
            LocationId: employee.LocationId,
            TerminationDate: request.TerminationDate,
            Reason: request.Reason
        ));

        var dto = MapToDto(employee);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Get employee's assigned roles.
    /// </summary>
    [HttpGet("{id:guid}/roles")]
    public async Task<ActionResult<HalCollection<EmployeeRoleDto>>> GetRoles(Guid id)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
            return NotFound();

        var employeeRoles = await _context.EmployeeRoles
            .Include(er => er.Role)
            .Where(er => er.EmployeeId == id)
            .ToListAsync();

        var dtos = employeeRoles.Select(er => new EmployeeRoleDto
        {
            Id = er.Id,
            EmployeeId = er.EmployeeId,
            RoleId = er.RoleId,
            RoleName = er.Role?.Name ?? string.Empty,
            Department = er.Role?.Department ?? string.Empty,
            HourlyRateOverride = er.HourlyRateOverride,
            EffectiveHourlyRate = er.HourlyRateOverride ?? er.Role?.DefaultHourlyRate,
            IsPrimary = er.IsPrimary,
            CertifiedAt = er.CertifiedAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/employees/{id}/roles/{dto.RoleId}");
            dto.AddLink("role", $"/api/roles/{dto.RoleId}");
        }

        return Ok(HalCollection<EmployeeRoleDto>.Create(dtos, $"/api/employees/{id}/roles", dtos.Count));
    }

    /// <summary>
    /// Assign a role to an employee.
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    public async Task<ActionResult<EmployeeRoleDto>> AssignRole(
        Guid id,
        [FromBody] AssignRoleRequest request)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null)
            return NotFound();

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
        if (role == null)
            return BadRequest(new { message = "Role not found" });

        var existingRole = await _context.EmployeeRoles
            .FirstOrDefaultAsync(er => er.EmployeeId == id && er.RoleId == request.RoleId);

        if (existingRole != null)
            return BadRequest(new { message = "Employee already has this role assigned" });

        // If setting as primary, clear existing primary
        if (request.IsPrimary)
        {
            var currentPrimary = await _context.EmployeeRoles
                .FirstOrDefaultAsync(er => er.EmployeeId == id && er.IsPrimary);
            if (currentPrimary != null)
                currentPrimary.IsPrimary = false;
        }

        var employeeRole = new EmployeeRole
        {
            EmployeeId = id,
            RoleId = request.RoleId,
            HourlyRateOverride = request.HourlyRateOverride,
            IsPrimary = request.IsPrimary
        };

        _context.EmployeeRoles.Add(employeeRole);
        await _context.SaveChangesAsync();

        // Publish EmployeeRoleAssigned event
        await _eventBus.PublishAsync(new EmployeeRoleAssigned(
            EmployeeId: employee.Id,
            TenantId: employee.TenantId,
            UserId: employee.UserId,
            RoleId: request.RoleId,
            RoleName: role.Name,
            Department: role.Department,
            IsPrimary: request.IsPrimary,
            HourlyRateOverride: request.HourlyRateOverride
        ));

        var dto = new EmployeeRoleDto
        {
            Id = employeeRole.Id,
            EmployeeId = id,
            RoleId = request.RoleId,
            RoleName = role.Name,
            Department = role.Department,
            HourlyRateOverride = request.HourlyRateOverride,
            EffectiveHourlyRate = request.HourlyRateOverride ?? role.DefaultHourlyRate,
            IsPrimary = request.IsPrimary
        };

        dto.AddSelfLink($"/api/employees/{id}/roles/{request.RoleId}");
        dto.AddLink("role", $"/api/roles/{request.RoleId}");

        return CreatedAtAction(nameof(GetRoles), new { id }, dto);
    }

    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            TenantId = employee.TenantId,
            UserId = employee.UserId,
            LocationId = employee.LocationId,
            EmployeeNumber = employee.EmployeeNumber,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            Phone = employee.Phone,
            DateOfBirth = employee.DateOfBirth,
            HireDate = employee.HireDate,
            TerminationDate = employee.TerminationDate,
            EmploymentType = employee.EmploymentType,
            Status = employee.Status,
            DefaultLocationId = employee.DefaultLocationId,
            AllowedLocationIds = employee.AllowedLocationIds,
            DefaultRoleId = employee.DefaultRoleId,
            DefaultRoleName = employee.DefaultRole?.Name,
            HourlyRate = employee.HourlyRate,
            SalaryAmount = employee.SalaryAmount,
            PayFrequency = employee.PayFrequency,
            OvertimeRate = employee.OvertimeRate,
            MaxHoursPerWeek = employee.MaxHoursPerWeek,
            MinHoursPerWeek = employee.MinHoursPerWeek,
            Roles = employee.EmployeeRoles.Select(er => new EmployeeRoleDto
            {
                Id = er.Id,
                EmployeeId = er.EmployeeId,
                RoleId = er.RoleId,
                RoleName = er.Role?.Name ?? string.Empty,
                Department = er.Role?.Department ?? string.Empty,
                HourlyRateOverride = er.HourlyRateOverride,
                EffectiveHourlyRate = er.HourlyRateOverride ?? er.Role?.DefaultHourlyRate,
                IsPrimary = er.IsPrimary,
                CertifiedAt = er.CertifiedAt
            }).ToList(),
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }

    private static EmployeeSummaryDto MapToSummaryDto(Employee employee)
    {
        return new EmployeeSummaryDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            EmploymentType = employee.EmploymentType,
            Status = employee.Status,
            DefaultRoleName = employee.DefaultRole?.Name,
            DefaultLocationId = employee.DefaultLocationId
        };
    }

    private static void AddLinks(EmployeeDto dto)
    {
        dto.AddSelfLink($"/api/employees/{dto.Id}");
        dto.AddLink("roles", $"/api/employees/{dto.Id}/roles");
        dto.AddLink("availability", $"/api/employees/{dto.Id}/availability");
        dto.AddLink("time-entries", $"/api/employees/{dto.Id}/time-entries");
        dto.AddLink("time-off", $"/api/employees/{dto.Id}/time-off");
        dto.AddLink("payroll", $"/api/employees/{dto.Id}/payroll");
        dto.AddLink("tips", $"/api/employees/{dto.Id}/tips");

        if (dto.Status == "active")
        {
            dto.AddLink("terminate", $"/api/employees/{dto.Id}/terminate");
        }
    }
}
