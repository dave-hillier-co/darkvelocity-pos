using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class PaymentGatewayEndpoints
{
    public static WebApplication MapPaymentGatewayEndpoints(this WebApplication app)
    {
        MapPaymentIntentEndpoints(app);
        MapPaymentMethodEndpoints(app);
        return app;
    }

    private static void MapPaymentIntentEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/v1/payment_intents").WithTags("PaymentIntents");

        // POST /v1/payment_intents - Create a PaymentIntent
        group.MapPost("/", async (
            [FromBody] CreatePaymentIntentApiRequest request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var paymentIntentId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{paymentIntentId}");

            var result = await grain.CreateAsync(new CreatePaymentIntentCommand(
                accountId,
                request.Amount,
                request.Currency ?? "usd",
                request.PaymentMethodTypes,
                request.Description,
                request.StatementDescriptor,
                request.CaptureMethod == "manual" ? CaptureMethod.Manual : CaptureMethod.Automatic,
                request.Customer,
                request.PaymentMethod,
                request.Metadata));

            return Results.Created($"/v1/payment_intents/{paymentIntentId}",
                ToPaymentIntentResponse(result));
        });

        // GET /v1/payment_intents/{id} - Retrieve a PaymentIntent
        group.MapGet("/{id}", async (
            Guid id,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_intent" } });

            var result = await grain.GetSnapshotAsync();
            return Results.Ok(ToPaymentIntentResponse(result));
        });

        // POST /v1/payment_intents/{id} - Update a PaymentIntent
        group.MapPost("/{id}", async (
            Guid id,
            [FromBody] UpdatePaymentIntentApiRequest request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_intent" } });

            var result = await grain.UpdateAsync(new UpdatePaymentIntentCommand(
                request.Amount,
                request.Description,
                request.Customer,
                request.Metadata));

            return Results.Ok(ToPaymentIntentResponse(result));
        });

        // POST /v1/payment_intents/{id}/confirm - Confirm a PaymentIntent
        group.MapPost("/{id}/confirm", async (
            Guid id,
            [FromBody] ConfirmPaymentIntentApiRequest? request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_intent" } });

            var result = await grain.ConfirmAsync(new ConfirmPaymentIntentCommand(
                request?.PaymentMethod,
                request?.ReturnUrl,
                request?.OffSession ?? false));

            return Results.Ok(ToPaymentIntentResponse(result));
        });

        // POST /v1/payment_intents/{id}/capture - Capture a PaymentIntent
        group.MapPost("/{id}/capture", async (
            Guid id,
            [FromBody] CapturePaymentIntentApiRequest? request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_intent" } });

            var result = await grain.CaptureAsync(request?.AmountToCapture);
            return Results.Ok(ToPaymentIntentResponse(result));
        });

        // POST /v1/payment_intents/{id}/cancel - Cancel a PaymentIntent
        group.MapPost("/{id}/cancel", async (
            Guid id,
            [FromBody] CancelPaymentIntentApiRequest? request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentIntentGrain>($"{accountId}:pi:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_intent" } });

            var result = await grain.CancelAsync(request?.CancellationReason);
            return Results.Ok(ToPaymentIntentResponse(result));
        });
    }

    private static void MapPaymentMethodEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/v1/payment_methods").WithTags("PaymentMethods");

        // POST /v1/payment_methods - Create a PaymentMethod
        group.MapPost("/", async (
            [FromBody] CreatePaymentMethodApiRequest request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var paymentMethodId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{paymentMethodId}");

            var type = request.Type?.ToLowerInvariant() switch
            {
                "card" => PaymentMethodType.Card,
                "us_bank_account" => PaymentMethodType.BankAccount,
                _ => PaymentMethodType.Card
            };

            CardDetails? cardDetails = null;
            if (request.Card != null)
            {
                cardDetails = new CardDetails(
                    request.Card.Number,
                    request.Card.ExpMonth,
                    request.Card.ExpYear,
                    request.Card.Cvc);
            }

            BankAccountDetails? bankDetails = null;
            if (request.UsBankAccount != null)
            {
                bankDetails = new BankAccountDetails(
                    "US",
                    "usd",
                    request.UsBankAccount.AccountHolderName,
                    request.UsBankAccount.AccountHolderType,
                    request.UsBankAccount.RoutingNumber,
                    request.UsBankAccount.AccountNumber);
            }

            BillingDetails? billingDetails = null;
            if (request.BillingDetails != null)
            {
                PaymentMethodAddress? address = null;
                if (request.BillingDetails.Address != null)
                {
                    address = new PaymentMethodAddress(
                        request.BillingDetails.Address.Line1,
                        request.BillingDetails.Address.Line2,
                        request.BillingDetails.Address.City,
                        request.BillingDetails.Address.State,
                        request.BillingDetails.Address.PostalCode,
                        request.BillingDetails.Address.Country);
                }

                billingDetails = new BillingDetails(
                    request.BillingDetails.Name,
                    request.BillingDetails.Email,
                    request.BillingDetails.Phone,
                    address);
            }

            var result = await grain.CreateAsync(new CreatePaymentMethodCommand(
                accountId,
                type,
                cardDetails,
                bankDetails,
                billingDetails,
                request.Metadata));

            return Results.Created($"/v1/payment_methods/{paymentMethodId}",
                ToPaymentMethodResponse(result));
        });

        // GET /v1/payment_methods/{id} - Retrieve a PaymentMethod
        group.MapGet("/{id}", async (
            Guid id,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_method" } });

            var result = await grain.GetSnapshotAsync();
            return Results.Ok(ToPaymentMethodResponse(result));
        });

        // POST /v1/payment_methods/{id} - Update a PaymentMethod
        group.MapPost("/{id}", async (
            Guid id,
            [FromBody] UpdatePaymentMethodApiRequest request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_method" } });

            BillingDetails? billingDetails = null;
            if (request.BillingDetails != null)
            {
                PaymentMethodAddress? address = null;
                if (request.BillingDetails.Address != null)
                {
                    address = new PaymentMethodAddress(
                        request.BillingDetails.Address.Line1,
                        request.BillingDetails.Address.Line2,
                        request.BillingDetails.Address.City,
                        request.BillingDetails.Address.State,
                        request.BillingDetails.Address.PostalCode,
                        request.BillingDetails.Address.Country);
                }

                billingDetails = new BillingDetails(
                    request.BillingDetails.Name,
                    request.BillingDetails.Email,
                    request.BillingDetails.Phone,
                    address);
            }

            var result = await grain.UpdateAsync(
                billingDetails,
                request.Card?.ExpMonth,
                request.Card?.ExpYear,
                request.Metadata);

            return Results.Ok(ToPaymentMethodResponse(result));
        });

        // POST /v1/payment_methods/{id}/attach - Attach to customer
        group.MapPost("/{id}/attach", async (
            Guid id,
            [FromBody] AttachPaymentMethodApiRequest request,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_method" } });

            var result = await grain.AttachToCustomerAsync(request.Customer);
            return Results.Ok(ToPaymentMethodResponse(result));
        });

        // POST /v1/payment_methods/{id}/detach - Detach from customer
        group.MapPost("/{id}/detach", async (
            Guid id,
            HttpContext context,
            IGrainFactory grainFactory) =>
        {
            var accountId = GetAccountIdFromContext(context);
            if (accountId == Guid.Empty)
                return Results.Unauthorized();

            var grain = grainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{id}");
            if (!await grain.ExistsAsync())
                return Results.NotFound(new { error = new { type = "invalid_request_error", message = "No such payment_method" } });

            var result = await grain.DetachFromCustomerAsync();
            return Results.Ok(ToPaymentMethodResponse(result));
        });
    }

    // ============================================================================
    // Helper Functions
    // ============================================================================

    private static Guid GetAccountIdFromContext(HttpContext context)
    {
        var accountIdClaim = context.User.FindFirst("account_id")?.Value;
        if (string.IsNullOrEmpty(accountIdClaim))
            return Guid.Empty;

        // Account ID in API key is alphanumeric, try to parse as GUID or generate deterministic GUID
        if (Guid.TryParse(accountIdClaim, out var accountId))
            return accountId;

        // Generate deterministic GUID from account ID string
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(accountIdClaim));
        return new Guid(bytes);
    }

    private static object ToPaymentIntentResponse(PaymentIntentSnapshot snapshot)
    {
        return new
        {
            id = $"pi_{snapshot.Id:N}",
            @object = "payment_intent",
            amount = snapshot.Amount,
            amount_capturable = snapshot.AmountCapturable,
            amount_received = snapshot.AmountReceived,
            currency = snapshot.Currency,
            status = snapshot.Status.ToString().ToLowerInvariant() switch
            {
                "requirespaymentmethod" => "requires_payment_method",
                "requiresconfirmation" => "requires_confirmation",
                "requiresaction" => "requires_action",
                "requirescapture" => "requires_capture",
                _ => snapshot.Status.ToString().ToLowerInvariant()
            },
            client_secret = snapshot.ClientSecret,
            payment_method = snapshot.PaymentMethodId,
            customer = snapshot.CustomerId,
            description = snapshot.Description,
            statement_descriptor = snapshot.StatementDescriptor,
            capture_method = snapshot.CaptureMethod == CaptureMethod.Automatic ? "automatic" : "manual",
            last_payment_error = snapshot.LastPaymentError != null ? new { message = snapshot.LastPaymentError } : null,
            next_action = snapshot.NextAction != null ? new
            {
                type = snapshot.NextAction.Type,
                redirect_to_url = snapshot.NextAction.RedirectUrl != null ? new { url = snapshot.NextAction.RedirectUrl } : null
            } : null,
            metadata = snapshot.Metadata ?? new Dictionary<string, string>(),
            created = new DateTimeOffset(snapshot.CreatedAt).ToUnixTimeSeconds(),
            canceled_at = snapshot.CanceledAt.HasValue ? new DateTimeOffset(snapshot.CanceledAt.Value).ToUnixTimeSeconds() : (long?)null,
            livemode = false
        };
    }

    private static object ToPaymentMethodResponse(PaymentMethodSnapshot snapshot)
    {
        return new
        {
            id = $"pm_{snapshot.Id:N}",
            @object = "payment_method",
            type = snapshot.Type.ToString().ToLowerInvariant(),
            customer = snapshot.CustomerId,
            card = snapshot.Card != null ? new
            {
                brand = snapshot.Card.Brand,
                last4 = snapshot.Card.Last4,
                exp_month = snapshot.Card.ExpMonth,
                exp_year = snapshot.Card.ExpYear,
                fingerprint = snapshot.Card.Fingerprint,
                funding = snapshot.Card.Funding,
                country = snapshot.Card.Country
            } : null,
            us_bank_account = snapshot.BankAccount != null ? new
            {
                account_holder_type = snapshot.BankAccount.AccountHolderType,
                account_type = "checking",
                bank_name = snapshot.BankAccount.BankName,
                last4 = snapshot.BankAccount.Last4,
                routing_number = snapshot.BankAccount.RoutingNumber
            } : null,
            billing_details = snapshot.BillingDetails != null ? new
            {
                name = snapshot.BillingDetails.Name,
                email = snapshot.BillingDetails.Email,
                phone = snapshot.BillingDetails.Phone,
                address = snapshot.BillingDetails.Address != null ? new
                {
                    line1 = snapshot.BillingDetails.Address.Line1,
                    line2 = snapshot.BillingDetails.Address.Line2,
                    city = snapshot.BillingDetails.Address.City,
                    state = snapshot.BillingDetails.Address.State,
                    postal_code = snapshot.BillingDetails.Address.PostalCode,
                    country = snapshot.BillingDetails.Address.Country
                } : null
            } : null,
            metadata = snapshot.Metadata ?? new Dictionary<string, string>(),
            created = new DateTimeOffset(snapshot.CreatedAt).ToUnixTimeSeconds(),
            livemode = snapshot.Livemode
        };
    }
}
