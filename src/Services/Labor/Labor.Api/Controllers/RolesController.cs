using DarkVelocity.Labor.Api.Data;
using DarkVelocity.Labor.Api.Dtos;
using DarkVelocity.Labor.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Labor.Api.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly LaborDbContext _context;

    public RolesController(LaborDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List all roles.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<RoleSummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? department = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.Roles.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        if (!string.IsNullOrEmpty(department))
            query = query.Where(r => r.Department == department);

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        var total = await query.CountAsync();

        var roles = await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = roles.Select(r => new RoleSummaryDto
        {
            Id = r.Id,
            Name = r.Name,
            Department = r.Department,
            Color = r.Color,
            DefaultHourlyRate = r.DefaultHourlyRate,
            IsActive = r.IsActive
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/roles/{dto.Id}");

        return Ok(HalCollection<RoleSummaryDto>.Create(dtos, "/api/roles", total));
    }

    /// <summary>
    /// Get role by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleDto>> GetById(Guid id)
    {
        var role = await _context.Roles
            .Include(r => r.EmployeeRoles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return NotFound();

        var dto = MapToDto(role);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Create a new role.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(
        [FromQuery] Guid tenantId,
        [FromBody] CreateRoleRequest request)
    {
        // Check for duplicate name
        var existingName = await _context.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == request.Name);
        if (existingName)
            return BadRequest(new { message = "Role name already exists" });

        var role = new Role
        {
            TenantId = tenantId,
            Name = request.Name,
            Department = request.Department,
            DefaultHourlyRate = request.DefaultHourlyRate,
            Color = request.Color,
            SortOrder = request.SortOrder,
            RequiredCertifications = request.RequiredCertifications ?? new List<string>()
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var dto = MapToDto(role);
        AddLinks(dto);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, dto);
    }

    /// <summary>
    /// Update a role.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Update(
        Guid id,
        [FromBody] UpdateRoleRequest request)
    {
        var role = await _context.Roles
            .Include(r => r.EmployeeRoles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return NotFound();

        if (request.Name != null) role.Name = request.Name;
        if (request.Department != null) role.Department = request.Department;
        if (request.DefaultHourlyRate.HasValue) role.DefaultHourlyRate = request.DefaultHourlyRate;
        if (request.Color != null) role.Color = request.Color;
        if (request.SortOrder.HasValue) role.SortOrder = request.SortOrder.Value;
        if (request.RequiredCertifications != null) role.RequiredCertifications = request.RequiredCertifications;
        if (request.IsActive.HasValue) role.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = MapToDto(role);
        AddLinks(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Get employees assigned to a role.
    /// </summary>
    [HttpGet("{id:guid}/employees")]
    public async Task<ActionResult<HalCollection<EmployeeSummaryDto>>> GetEmployees(
        Guid id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null)
            return NotFound();

        var query = _context.Employees
            .Include(e => e.DefaultRole)
            .Where(e => e.DefaultRoleId == id || e.EmployeeRoles.Any(er => er.RoleId == id));

        var total = await query.CountAsync();

        var employees = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = employees.Select(e => new EmployeeSummaryDto
        {
            Id = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FirstName = e.FirstName,
            LastName = e.LastName,
            Email = e.Email,
            EmploymentType = e.EmploymentType,
            Status = e.Status,
            DefaultRoleName = e.DefaultRole?.Name,
            DefaultLocationId = e.DefaultLocationId
        }).ToList();

        foreach (var dto in dtos)
            dto.AddSelfLink($"/api/employees/{dto.Id}");

        return Ok(HalCollection<EmployeeSummaryDto>.Create(dtos, $"/api/roles/{id}/employees", total));
    }

    private static RoleDto MapToDto(Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Name = role.Name,
            Department = role.Department,
            DefaultHourlyRate = role.DefaultHourlyRate,
            Color = role.Color,
            SortOrder = role.SortOrder,
            RequiredCertifications = role.RequiredCertifications,
            IsActive = role.IsActive,
            EmployeeCount = role.EmployeeRoles?.Count ?? 0,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }

    private static void AddLinks(RoleDto dto)
    {
        dto.AddSelfLink($"/api/roles/{dto.Id}");
        dto.AddLink("employees", $"/api/roles/{dto.Id}/employees");
    }
}
