using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class PayrollEndpoints
{
    public static WebApplication MapPayrollEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/payroll").WithTags("Payroll");

        // Create a payroll period
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreatePayrollPeriodRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, request.PeriodStart));
            var result = await grain.CreateAsync(new CreatePayrollPeriodCommand(
                siteId,
                request.PeriodStart.ToDateTime(TimeOnly.MinValue),
                request.PeriodEnd.ToDateTime(TimeOnly.MinValue)));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/payroll/{request.PeriodStart:yyyy-MM-dd}",
                Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{request.PeriodStart:yyyy-MM-dd}" },
                    ["calculate"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{request.PeriodStart:yyyy-MM-dd}/calculate" },
                    ["approve"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{request.PeriodStart:yyyy-MM-dd}/approve" },
                    ["process"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{request.PeriodStart:yyyy-MM-dd}/process" }
                }));
        });

        // Get a payroll period
        group.MapGet("/{periodStart}", async (
            Guid orgId,
            Guid siteId,
            DateOnly periodStart,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}" },
                    ["calculate"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}/calculate" },
                    ["approve"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}/approve" },
                    ["process"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}/process" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Payroll period not found"));
            }
        });

        // Calculate payroll
        group.MapPost("/{periodStart}/calculate", async (
            Guid orgId,
            Guid siteId,
            DateOnly periodStart,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
            try
            {
                await grain.CalculateAsync();
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Approve payroll
        group.MapPost("/{periodStart}/approve", async (
            Guid orgId,
            Guid siteId,
            DateOnly periodStart,
            [FromBody] ApprovePayrollRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
            try
            {
                await grain.ApproveAsync(request.ApprovedByUserId);
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Process payroll
        group.MapPost("/{periodStart}/process", async (
            Guid orgId,
            Guid siteId,
            DateOnly periodStart,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
            try
            {
                await grain.ProcessAsync();
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Get employee payroll for a period
        group.MapGet("/{periodStart}/employees/{employeeId}", async (
            Guid orgId,
            Guid siteId,
            DateOnly periodStart,
            Guid employeeId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPayrollPeriodGrain>(GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
            try
            {
                var entry = await grain.GetEmployeePayrollAsync(employeeId);
                return Results.Ok(Hal.Resource(entry, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}/employees/{employeeId}" },
                    ["payroll"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payroll/{periodStart:yyyy-MM-dd}" },
                    ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(Hal.Error("not_found", ex.Message));
            }
        });

        return app;
    }
}
