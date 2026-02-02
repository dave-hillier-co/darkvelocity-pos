using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class PaymentEndpoints
{
    public static WebApplication MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/payments").WithTags("Payments");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] InitiatePaymentRequest request,
            IGrainFactory grainFactory) =>
        {
            var paymentId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            var result = await grain.InitiateAsync(new InitiatePaymentCommand(
                orgId, siteId, request.OrderId, request.Method, request.Amount, request.CashierId, request.CustomerId, request.DrawerId));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}", Hal.Resource(new
            {
                id = result.Id,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" },
                ["complete-cash"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-cash" },
                ["complete-card"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-card" }
            }));
        });

        group.MapGet("/{paymentId}", async (Guid orgId, Guid siteId, Guid paymentId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Payment not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
            }));
        });

        group.MapPost("/{paymentId}/complete-cash", async (
            Guid orgId, Guid siteId, Guid paymentId,
            [FromBody] CompleteCashRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Payment not found"));

            var result = await grain.CompleteCashAsync(new CompleteCashPaymentCommand(request.AmountTendered, request.TipAmount));

            var state = await grain.GetStateAsync();
            var orderGrain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, state.OrderId));
            await orderGrain.RecordPaymentAsync(paymentId, result.TotalAmount, request.TipAmount, "cash");

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
            }));
        });

        group.MapPost("/{paymentId}/complete-card", async (
            Guid orgId, Guid siteId, Guid paymentId,
            [FromBody] CompleteCardRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Payment not found"));

            var result = await grain.CompleteCardAsync(new ProcessCardPaymentCommand(
                request.GatewayReference, request.AuthorizationCode, request.CardInfo, request.GatewayName, request.TipAmount));

            var state = await grain.GetStateAsync();
            var orderGrain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, state.OrderId));
            await orderGrain.RecordPaymentAsync(paymentId, result.TotalAmount, request.TipAmount, "card");

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
            }));
        });

        group.MapPost("/{paymentId}/void", async (
            Guid orgId, Guid siteId, Guid paymentId,
            [FromBody] VoidPaymentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Payment not found"));

            await grain.VoidAsync(new Grains.VoidPaymentCommand(request.VoidedBy, request.Reason));
            return Results.Ok(new { message = "Payment voided" });
        });

        group.MapPost("/{paymentId}/refund", async (
            Guid orgId, Guid siteId, Guid paymentId,
            [FromBody] RefundPaymentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Payment not found"));

            var result = await grain.RefundAsync(new RefundPaymentCommand(request.Amount, request.Reason, request.IssuedBy));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
            }));
        });

        return app;
    }
}
