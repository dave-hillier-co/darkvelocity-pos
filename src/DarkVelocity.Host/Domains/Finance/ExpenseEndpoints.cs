using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ExpenseEndpoints
{
    public static WebApplication MapExpenseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/expenses")
            .WithTags("Expenses");

        // Create a new expense
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateExpenseRequest request,
            IGrainFactory grainFactory) =>
        {
            var expenseId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
                orgId,
                siteId,
                expenseId,
                request.Category,
                request.Description,
                request.Amount,
                request.ExpenseDate,
                request.RecordedBy,
                request.Currency ?? "USD",
                request.CustomCategory,
                request.VendorId,
                request.VendorName,
                request.PaymentMethod,
                request.ReferenceNumber,
                request.TaxAmount,
                request.IsTaxDeductible ?? false,
                request.Notes,
                request.Tags));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/expenses/{expenseId}",
                Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Get expense by ID
        group.MapGet("/{expenseId}", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // List expenses (with filtering)
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] ExpenseCategory? category,
            [FromQuery] ExpenseStatus? status,
            [FromQuery] string? vendor,
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            IGrainFactory grainFactory) =>
        {
            var indexGrain = grainFactory.GetGrain<IExpenseIndexGrain>(
                GrainKeys.Site(orgId, siteId));

            var result = await indexGrain.QueryAsync(new ExpenseQuery(
                from,
                to,
                category,
                status,
                vendor,
                minAmount,
                maxAmount,
                null,
                skip ?? 0,
                take ?? 50));

            return Results.Ok(new
            {
                _links = new
                {
                    self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/expenses" }
                },
                totalCount = result.TotalCount,
                totalAmount = result.TotalAmount,
                expenses = result.Expenses
            });
        });

        // Update expense
        group.MapPatch("/{expenseId}", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromBody] UpdateExpenseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.UpdateAsync(new UpdateExpenseCommand(
                request.UpdatedBy,
                request.Category,
                request.CustomCategory,
                request.Description,
                request.Amount,
                request.ExpenseDate,
                request.VendorId,
                request.VendorName,
                request.PaymentMethod,
                request.ReferenceNumber,
                request.TaxAmount,
                request.IsTaxDeductible,
                request.Notes,
                request.Tags));

            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Approve expense
        group.MapPost("/{expenseId}/approve", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromBody] ApproveExpenseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.ApproveAsync(new ApproveExpenseCommand(
                request.ApprovedBy,
                request.Notes));

            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Reject expense
        group.MapPost("/{expenseId}/reject", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromBody] RejectExpenseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.RejectAsync(new RejectExpenseCommand(
                request.RejectedBy,
                request.Reason));

            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Mark expense as paid
        group.MapPost("/{expenseId}/pay", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromBody] MarkExpensePaidRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.MarkPaidAsync(new MarkExpensePaidCommand(
                request.PaidBy,
                request.PaymentDate,
                request.ReferenceNumber,
                request.PaymentMethod));

            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Void expense
        group.MapDelete("/{expenseId}", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromQuery] Guid voidedBy,
            [FromQuery] string reason,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            await grain.VoidAsync(new VoidExpenseCommand(voidedBy, reason));
            return Results.NoContent();
        });

        // Attach document
        group.MapPost("/{expenseId}/document", async (
            Guid orgId,
            Guid siteId,
            Guid expenseId,
            [FromBody] AttachDocumentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExpenseGrain>(
                GrainKeys.Expense(orgId, siteId, expenseId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Expense not found"));

            var snapshot = await grain.AttachDocumentAsync(new AttachDocumentCommand(
                request.DocumentUrl,
                request.Filename,
                request.AttachedBy));

            return Results.Ok(Hal.Resource(snapshot, BuildExpenseLinks(orgId, siteId, expenseId)));
        });

        // Get category totals
        group.MapGet("/summary/by-category", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            IGrainFactory grainFactory) =>
        {
            var indexGrain = grainFactory.GetGrain<IExpenseIndexGrain>(
                GrainKeys.Site(orgId, siteId));

            var totals = await indexGrain.GetCategoryTotalsAsync(from, to);
            var grandTotal = await indexGrain.GetTotalAsync(from, to);

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/expenses/summary/by-category" } },
                fromDate = from,
                toDate = to,
                grandTotal,
                categories = totals
            });
        });

        // Get expense categories (enum values)
        app.MapGet("/api/expense-categories", () =>
        {
            var categories = Enum.GetValues<ExpenseCategory>()
                .Select(c => new { value = c, name = c.ToString() })
                .ToList();

            return Results.Ok(new { categories });
        }).WithTags("Expenses");

        return app;
    }

    private static Dictionary<string, object> BuildExpenseLinks(Guid orgId, Guid siteId, Guid expenseId)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/expenses/{expenseId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["approve"] = new { href = $"{basePath}/approve" },
            ["reject"] = new { href = $"{basePath}/reject" },
            ["pay"] = new { href = $"{basePath}/pay" },
            ["document"] = new { href = $"{basePath}/document" },
            ["collection"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/expenses" }
        };
    }
}
