using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.PaymentProcessors;

public static class PaymentProcessorEndpoints
{
    public static WebApplication MapPaymentProcessorEndpoints(this WebApplication app)
    {
        MapProcessorPaymentEndpoints(app);
        MapTerminalEndpoints(app);
        MapWebhookEndpoints(app);
        return app;
    }

    // ========================================================================
    // Processor Payment Operations
    // ========================================================================

    private static void MapProcessorPaymentEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/processors/{processorType}")
            .WithTags("PaymentProcessors");

        // POST /processors/{processorType}/payments - Create payment
        group.MapPost("/payments", async (
            Guid orgId,
            Guid siteId,
            string processorType,
            [FromBody] CreateProcessorPaymentRequest request,
            IGrainFactory grainFactory) =>
        {
            var paymentIntentId = request.PaymentIntentId ?? Guid.NewGuid();
            var processorGrain = GetProcessorGrain(grainFactory, orgId, processorType, paymentIntentId);

            if (processorGrain == null)
            {
                return Results.BadRequest(new { error = "Invalid processor type. Supported: stripe, adyen, mock" });
            }

            var authRequest = new ProcessorAuthRequest(
                paymentIntentId,
                request.Amount,
                request.Currency ?? "usd",
                request.PaymentMethodToken,
                request.CaptureAutomatically ?? true,
                request.StatementDescriptor,
                request.Metadata);

            var result = await processorGrain.AuthorizeAsync(authRequest);

            return Results.Ok(ToProcessorPaymentResponse(paymentIntentId, processorType, result));
        });

        // POST /processors/{processorType}/payments/{paymentId}/capture - Capture payment
        group.MapPost("/payments/{paymentId}/capture", async (
            Guid orgId,
            Guid siteId,
            string processorType,
            Guid paymentId,
            [FromBody] CaptureProcessorPaymentRequest? request,
            IGrainFactory grainFactory) =>
        {
            var processorGrain = GetProcessorGrain(grainFactory, orgId, processorType, paymentId);

            if (processorGrain == null)
            {
                return Results.BadRequest(new { error = "Invalid processor type" });
            }

            // Get the transaction ID from state
            var state = await processorGrain.GetStateAsync();
            if (string.IsNullOrEmpty(state.TransactionId))
            {
                return Results.NotFound(new { error = "Payment not found" });
            }

            var result = await processorGrain.CaptureAsync(state.TransactionId, request?.Amount);

            return Results.Ok(new
            {
                success = result.Success,
                capture_id = result.CaptureId,
                captured_amount = result.CapturedAmount,
                error = result.Success ? null : new { code = result.ErrorCode, message = result.ErrorMessage }
            });
        });

        // POST /processors/{processorType}/payments/{paymentId}/refund - Refund payment
        group.MapPost("/payments/{paymentId}/refund", async (
            Guid orgId,
            Guid siteId,
            string processorType,
            Guid paymentId,
            [FromBody] RefundProcessorPaymentRequest request,
            IGrainFactory grainFactory) =>
        {
            var processorGrain = GetProcessorGrain(grainFactory, orgId, processorType, paymentId);

            if (processorGrain == null)
            {
                return Results.BadRequest(new { error = "Invalid processor type" });
            }

            var state = await processorGrain.GetStateAsync();
            if (string.IsNullOrEmpty(state.TransactionId))
            {
                return Results.NotFound(new { error = "Payment not found" });
            }

            var result = await processorGrain.RefundAsync(state.TransactionId, request.Amount, request.Reason);

            return Results.Ok(new
            {
                success = result.Success,
                refund_id = result.RefundId,
                refunded_amount = result.RefundedAmount,
                error = result.Success ? null : new { code = result.ErrorCode, message = result.ErrorMessage }
            });
        });

        // POST /processors/{processorType}/payments/{paymentId}/void - Void payment
        group.MapPost("/payments/{paymentId}/void", async (
            Guid orgId,
            Guid siteId,
            string processorType,
            Guid paymentId,
            [FromBody] VoidProcessorPaymentRequest? request,
            IGrainFactory grainFactory) =>
        {
            var processorGrain = GetProcessorGrain(grainFactory, orgId, processorType, paymentId);

            if (processorGrain == null)
            {
                return Results.BadRequest(new { error = "Invalid processor type" });
            }

            var state = await processorGrain.GetStateAsync();
            if (string.IsNullOrEmpty(state.TransactionId))
            {
                return Results.NotFound(new { error = "Payment not found" });
            }

            var result = await processorGrain.VoidAsync(state.TransactionId, request?.Reason);

            return Results.Ok(new
            {
                success = result.Success,
                void_id = result.VoidId,
                error = result.Success ? null : new { code = result.ErrorCode, message = result.ErrorMessage }
            });
        });

        // GET /processors/{processorType}/payments/{paymentId} - Get payment status
        group.MapGet("/payments/{paymentId}", async (
            Guid orgId,
            Guid siteId,
            string processorType,
            Guid paymentId,
            IGrainFactory grainFactory) =>
        {
            var processorGrain = GetProcessorGrain(grainFactory, orgId, processorType, paymentId);

            if (processorGrain == null)
            {
                return Results.BadRequest(new { error = "Invalid processor type" });
            }

            var state = await processorGrain.GetStateAsync();

            if (state.PaymentIntentId == Guid.Empty)
            {
                return Results.NotFound(new { error = "Payment not found" });
            }

            return Results.Ok(ToProcessorPaymentStatusResponse(state));
        });
    }

    // ========================================================================
    // Terminal Operations
    // ========================================================================

    private static void MapTerminalEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/terminals")
            .WithTags("PaymentTerminals");

        // POST /terminals/{terminalId}/pair - Pair a terminal
        group.MapPost("/{terminalId}/pair", async (
            Guid orgId,
            Guid siteId,
            Guid terminalId,
            [FromBody] PairTerminalRequest request,
            IGrainFactory grainFactory) =>
        {
            // First, register the terminal in the TerminalGrain
            var terminalGrain = grainFactory.GetGrain<ITerminalGrain>(
                GrainKeys.Terminal(orgId, terminalId));

            var exists = await terminalGrain.ExistsAsync();
            if (!exists)
            {
                await terminalGrain.RegisterAsync(new RegisterTerminalCommand(
                    siteId,
                    request.Label ?? "Terminal",
                    request.DeviceType,
                    request.SerialNumber,
                    request.Metadata));
            }

            // Then pair with the processor
            if (request.ProcessorType?.ToLowerInvariant() == "stripe")
            {
                var stripeClient = app.Services.GetRequiredService<IStripeClient>();

                try
                {
                    var result = await stripeClient.CreateTerminalReaderAsync(new StripeTerminalReaderCreateRequest(
                        RegistrationCode: request.RegistrationCode ?? "",
                        Label: request.Label ?? "Terminal",
                        LocationId: request.LocationId,
                        Metadata: request.Metadata));

                    if (result.Success)
                    {
                        return Results.Ok(new
                        {
                            success = true,
                            terminal_id = terminalId,
                            processor_terminal_id = result.ReaderId,
                            status = result.Status,
                            device_type = result.DeviceType,
                            serial_number = result.SerialNumber
                        });
                    }
                    else
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = new { code = result.ErrorCode, message = result.ErrorMessage }
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = new { code = "pairing_error", message = ex.Message }
                    });
                }
            }
            else if (request.ProcessorType?.ToLowerInvariant() == "adyen")
            {
                var adyenClient = app.Services.GetRequiredService<IAdyenClient>();

                try
                {
                    var result = await adyenClient.RegisterTerminalAsync(new AdyenTerminalRegisterRequest(
                        TerminalId: request.RegistrationCode ?? terminalId.ToString(),
                        StoreId: request.LocationId ?? siteId.ToString(),
                        MerchantAccount: request.MerchantAccount ?? ""));

                    if (result.Success)
                    {
                        return Results.Ok(new
                        {
                            success = true,
                            terminal_id = terminalId,
                            processor_terminal_id = result.TerminalId,
                            status = result.Status
                        });
                    }
                    else
                    {
                        return Results.BadRequest(new
                        {
                            success = false,
                            error = new { code = result.ErrorCode, message = result.ErrorMessage }
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        error = new { code = "pairing_error", message = ex.Message }
                    });
                }
            }
            else
            {
                // Mock pairing
                return Results.Ok(new
                {
                    success = true,
                    terminal_id = terminalId,
                    processor_terminal_id = $"mock_{terminalId:N}",
                    status = "paired"
                });
            }
        });

        // POST /terminals/{terminalId}/connection-token - Get connection token (Stripe Terminal)
        group.MapPost("/{terminalId}/connection-token", async (
            Guid orgId,
            Guid siteId,
            Guid terminalId,
            [FromBody] ConnectionTokenRequest? request,
            IGrainFactory grainFactory,
            IStripeClient stripeClient) =>
        {
            try
            {
                var result = await stripeClient.CreateConnectionTokenAsync(request?.LocationId);

                if (result.Success)
                {
                    return Results.Ok(new
                    {
                        secret = result.Secret,
                        object_type = "terminal.connection_token"
                    });
                }
                else
                {
                    return Results.BadRequest(new
                    {
                        error = new { code = result.ErrorCode, message = result.ErrorMessage }
                    });
                }
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new
                {
                    error = new { code = "connection_token_error", message = ex.Message }
                });
            }
        });

        // GET /terminals/{terminalId} - Get terminal status
        group.MapGet("/{terminalId}", async (
            Guid orgId,
            Guid siteId,
            Guid terminalId,
            IGrainFactory grainFactory) =>
        {
            var terminalGrain = grainFactory.GetGrain<ITerminalGrain>(
                GrainKeys.Terminal(orgId, terminalId));

            if (!await terminalGrain.ExistsAsync())
            {
                return Results.NotFound(new { error = "Terminal not found" });
            }

            var snapshot = await terminalGrain.GetSnapshotAsync();
            var isOnline = await terminalGrain.IsOnlineAsync();

            return Results.Ok(new
            {
                terminal_id = snapshot.TerminalId,
                label = snapshot.Label,
                device_type = snapshot.DeviceType,
                serial_number = snapshot.SerialNumber,
                status = snapshot.Status.ToString().ToLowerInvariant(),
                is_online = isOnline,
                last_seen_at = snapshot.LastSeenAt,
                software_version = snapshot.SoftwareVersion
            });
        });
    }

    // ========================================================================
    // Webhook Endpoints
    // ========================================================================

    private static void MapWebhookEndpoints(WebApplication app)
    {
        var webhookGroup = app.MapGroup("/api/webhooks/processors")
            .WithTags("ProcessorWebhooks");

        // POST /webhooks/processors/stripe - Handle Stripe webhook
        webhookGroup.MapPost("/stripe", async (
            HttpContext context,
            IGrainFactory grainFactory,
            IStripeClient stripeClient) =>
        {
            var payload = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var signature = context.Request.Headers["Stripe-Signature"].FirstOrDefault();
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";

            // Verify signature in production
            if (!string.IsNullOrEmpty(webhookSecret) && !string.IsNullOrEmpty(signature))
            {
                if (!stripeClient.VerifyWebhookSignature(payload, signature, webhookSecret))
                {
                    return Results.BadRequest(new { error = "Invalid signature" });
                }
            }

            // Parse the webhook event
            try
            {
                var eventData = System.Text.Json.JsonDocument.Parse(payload);
                var eventType = eventData.RootElement.GetProperty("type").GetString();
                var eventId = eventData.RootElement.GetProperty("id").GetString();

                // Extract payment intent ID if present
                if (eventData.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("object", out var obj) &&
                    obj.TryGetProperty("id", out var piId))
                {
                    var paymentIntentId = piId.GetString();

                    // TODO: Route to appropriate grain based on metadata
                    // For now, just acknowledge
                    return Results.Ok(new { received = true, event_id = eventId });
                }

                return Results.Ok(new { received = true, event_id = eventId });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Invalid webhook payload", message = ex.Message });
            }
        });

        // POST /webhooks/processors/adyen - Handle Adyen notification
        webhookGroup.MapPost("/adyen", async (
            HttpContext context,
            IGrainFactory grainFactory,
            IAdyenClient adyenClient) =>
        {
            var payload = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var hmacSignature = context.Request.Headers["X-Adyen-Hmac-Signature"].FirstOrDefault();
            var hmacKey = Environment.GetEnvironmentVariable("ADYEN_HMAC_KEY") ?? "";

            // Verify HMAC in production
            if (!string.IsNullOrEmpty(hmacKey) && !string.IsNullOrEmpty(hmacSignature))
            {
                if (!adyenClient.VerifyHmacSignature(payload, hmacSignature, hmacKey))
                {
                    return Results.BadRequest(new { error = "Invalid HMAC signature" });
                }
            }

            // Parse notification
            try
            {
                var notification = System.Text.Json.JsonDocument.Parse(payload);

                // Adyen sends notifications in an array
                if (notification.RootElement.TryGetProperty("notificationItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("NotificationRequestItem", out var notificationItem))
                        {
                            var eventCode = notificationItem.GetProperty("eventCode").GetString();
                            var pspReference = notificationItem.GetProperty("pspReference").GetString();

                            // TODO: Route to appropriate grain based on merchantReference
                        }
                    }
                }

                // Adyen expects "[accepted]" response
                return Results.Ok("[accepted]");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Invalid notification", message = ex.Message });
            }
        });
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static IProcessorPaymentGrain? GetProcessorGrain(
        IGrainFactory grainFactory,
        Guid orgId,
        string processorType,
        Guid paymentIntentId)
    {
        var key = $"{orgId}:{processorType.ToLowerInvariant()}:{paymentIntentId}";

        return processorType.ToLowerInvariant() switch
        {
            "stripe" => grainFactory.GetGrain<IStripeProcessorGrain>(key),
            "adyen" => grainFactory.GetGrain<IAdyenProcessorGrain>(key),
            "mock" => grainFactory.GetGrain<IMockProcessorGrain>(key),
            _ => null
        };
    }

    private static object ToProcessorPaymentResponse(
        Guid paymentIntentId,
        string processorType,
        ProcessorAuthResult result)
    {
        return new
        {
            payment_intent_id = paymentIntentId,
            processor = processorType,
            success = result.Success,
            transaction_id = result.TransactionId,
            authorization_code = result.AuthorizationCode,
            network_transaction_id = result.NetworkTransactionId,
            decline_code = result.DeclineCode,
            decline_message = result.DeclineMessage,
            next_action = result.RequiredAction != null ? new
            {
                type = result.RequiredAction.Type,
                redirect_url = result.RequiredAction.RedirectUrl,
                data = result.RequiredAction.Data
            } : null
        };
    }

    private static object ToProcessorPaymentStatusResponse(ProcessorPaymentState state)
    {
        return new
        {
            payment_intent_id = state.PaymentIntentId,
            processor = state.ProcessorName,
            transaction_id = state.TransactionId,
            authorization_code = state.AuthorizationCode,
            status = state.Status,
            authorized_amount = state.AuthorizedAmount,
            captured_amount = state.CapturedAmount,
            refunded_amount = state.RefundedAmount,
            retry_count = state.RetryCount,
            last_attempt_at = state.LastAttemptAt,
            last_error = state.LastError,
            events = state.Events.Select(e => new
            {
                timestamp = e.Timestamp,
                event_type = e.EventType,
                external_event_id = e.ExternalEventId,
                data = e.Data
            })
        };
    }
}

// ========================================================================
// Request/Response DTOs
// ========================================================================

public record CreateProcessorPaymentRequest(
    Guid? PaymentIntentId,
    long Amount,
    string? Currency,
    string PaymentMethodToken,
    bool? CaptureAutomatically,
    string? StatementDescriptor,
    Dictionary<string, string>? Metadata);

public record CaptureProcessorPaymentRequest(
    long? Amount);

public record RefundProcessorPaymentRequest(
    long Amount,
    string? Reason);

public record VoidProcessorPaymentRequest(
    string? Reason);

public record PairTerminalRequest(
    string? ProcessorType,
    string? RegistrationCode,
    string? Label,
    string? DeviceType,
    string? SerialNumber,
    string? LocationId,
    string? MerchantAccount,
    Dictionary<string, string>? Metadata);

public record ConnectionTokenRequest(
    string? LocationId);
