using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        MapStationsEndpoints(app);
        MapDeviceAuthEndpoints(app);
        MapDeviceManagementEndpoints(app);
        MapPrintJobEndpoints(app);
        MapSyncQueueEndpoints(app);
        MapDeviceHealthEndpoints(app);

        return app;
    }

    private static void MapStationsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/stations").WithTags("Stations");

        group.MapGet("/{orgId}/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var stations = new[]
            {
                new { id = Guid.NewGuid(), name = "Grill Station", siteId, orderTypes = new[] { "hot", "grill" } },
                new { id = Guid.NewGuid(), name = "Cold Station", siteId, orderTypes = new[] { "cold", "salad" } },
                new { id = Guid.NewGuid(), name = "Expeditor", siteId, orderTypes = new[] { "all" } },
                new { id = Guid.NewGuid(), name = "Bar", siteId, orderTypes = new[] { "drinks", "bar" } },
            };
            return Results.Ok(new { items = stations });
        });

        group.MapPost("/{orgId}/{siteId}/select", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SelectStationRequest request,
            IGrainFactory grainFactory) =>
        {
            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, request.DeviceId));
            if (!await deviceGrain.ExistsAsync())
                return Results.NotFound(new { error = "device_not_found" });

            return Results.Ok(new
            {
                message = "Station selected",
                deviceId = request.DeviceId,
                stationId = request.StationId,
                stationName = request.StationName
            });
        });
    }

    private static void MapDeviceAuthEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/device").WithTags("DeviceAuth");

        group.MapPost("/code", async (
            [FromBody] DeviceCodeApiRequest request,
            IGrainFactory grainFactory,
            HttpContext httpContext) =>
        {
            var userCode = GrainKeys.GenerateUserCode();
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(userCode);

            var response = await grain.InitiateAsync(new DeviceCodeRequest(
                request.ClientId,
                request.Scope ?? "device",
                request.DeviceFingerprint,
                httpContext.Connection.RemoteIpAddress?.ToString()
            ));

            return Results.Ok(response);
        });

        group.MapPost("/token", async (
            [FromBody] DeviceTokenApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            var status = await grain.GetStatusAsync();

            return status switch
            {
                DeviceAuthStatus.Pending => Results.BadRequest(new { error = "authorization_pending", error_description = "The authorization request is still pending" }),
                DeviceAuthStatus.Expired => Results.BadRequest(new { error = "expired_token", error_description = "The device code has expired" }),
                DeviceAuthStatus.Denied => Results.BadRequest(new { error = "access_denied", error_description = "The authorization request was denied" }),
                DeviceAuthStatus.Authorized => Results.Ok(await grain.GetTokenAsync(request.DeviceCode)),
                _ => Results.BadRequest(new { error = "invalid_request" })
            };
        });

        group.MapPost("/authorize", async (
            [FromBody] AuthorizeDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());

            await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
                request.AuthorizedBy,
                request.OrganizationId,
                request.SiteId,
                request.DeviceName,
                request.AppType
            ));

            return Results.Ok(new { message = "Device authorized successfully" });
        });

        group.MapPost("/deny", async (
            [FromBody] DenyDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            await grain.DenyAsync(request.Reason ?? "User denied authorization");
            return Results.Ok(new { message = "Device authorization denied" });
        });
    }

    private static void MapDeviceManagementEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices");

        group.MapGet("/{orgId}/{deviceId}", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });

        group.MapPost("/{orgId}/{deviceId}/heartbeat", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] DeviceHeartbeatRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RecordHeartbeatAsync(request.AppVersion);
            return Results.Ok();
        });

        group.MapPost("/{orgId}/{deviceId}/suspend", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] SuspendDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.SuspendAsync(request.Reason);
            return Results.Ok(new { message = "Device suspended" });
        });

        group.MapPost("/{orgId}/{deviceId}/revoke", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] RevokeDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RevokeAsync(request.Reason);
            return Results.Ok(new { message = "Device revoked" });
        });
    }

    // ============================================================================
    // Print Job Endpoints
    // ============================================================================

    private static void MapPrintJobEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("PrintJobs");

        // Queue a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] QueuePrintJobApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IDevicePrintQueueGrain>(
                $"{orgId}:device:{deviceId}:printqueue");

            // Initialize queue if needed
            await queueGrain.InitializeAsync(deviceId);

            var command = new QueuePrintJobCommand(
                PrinterId: request.PrinterId,
                JobType: request.JobType,
                Content: request.Content,
                Copies: request.Copies ?? 1,
                Priority: request.Priority ?? 0,
                SourceOrderId: request.SourceOrderId,
                SourceReference: request.SourceReference);

            var snapshot = await queueGrain.EnqueueAsync(command);
            return Results.Created($"/api/devices/{orgId}/{deviceId}/print-jobs/{snapshot.JobId}", snapshot);
        });

        // List print jobs
        group.MapGet("/{orgId}/{deviceId}/print-jobs", async (
            Guid orgId,
            Guid deviceId,
            [FromQuery] bool? includeHistory,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IDevicePrintQueueGrain>(
                $"{orgId}:device:{deviceId}:printqueue");

            var summary = await queueGrain.GetSummaryAsync();

            if (includeHistory == true)
            {
                var history = await queueGrain.GetHistoryAsync(50);
                return Results.Ok(new
                {
                    summary.PendingJobs,
                    summary.PrintingJobs,
                    summary.CompletedJobs,
                    summary.FailedJobs,
                    ActiveJobs = summary.ActiveJobs,
                    History = history
                });
            }

            return Results.Ok(summary);
        });

        // Get specific print job
        group.MapGet("/{orgId}/{deviceId}/print-jobs/{jobId}", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });

        // Start a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/start", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.StartAsync(new StartPrintJobCommand());
            return Results.Ok(snapshot);
        });

        // Complete a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/complete", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromBody] CompletePrintJobApiRequest? request,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.CompleteAsync(new CompletePrintJobCommand(request?.PrinterResponse));
            return Results.Ok(snapshot);
        });

        // Fail a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/fail", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromBody] FailPrintJobApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.FailAsync(new FailPrintJobCommand(request.ErrorMessage, request.ErrorCode));
            return Results.Ok(snapshot);
        });

        // Retry a failed print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/retry", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.RetryAsync();
            return Results.Ok(snapshot);
        });

        // Cancel a print job
        group.MapDelete("/{orgId}/{deviceId}/print-jobs/{jobId}", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromQuery] string? reason,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            await jobGrain.CancelAsync(reason);
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Sync Queue Endpoints
    // ============================================================================

    private static void MapSyncQueueEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("OfflineSync");

        // Queue an offline operation
        group.MapPost("/{orgId}/{deviceId}/sync-queue", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] QueueOfflineOperationApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            // Initialize queue if needed
            await queueGrain.InitializeAsync(deviceId);

            var command = new QueueOfflineOperationCommand(
                OperationType: request.OperationType,
                EntityType: request.EntityType,
                EntityId: request.EntityId,
                PayloadJson: request.PayloadJson,
                ClientTimestamp: request.ClientTimestamp,
                ClientSequence: request.ClientSequence,
                UserId: request.UserId,
                IdempotencyKey: request.IdempotencyKey);

            var snapshot = await queueGrain.QueueOperationAsync(command);
            return Results.Created($"/api/devices/{orgId}/{deviceId}/sync-queue/{snapshot.OperationId}", snapshot);
        });

        // Get sync queue summary
        group.MapGet("/{orgId}/{deviceId}/sync-queue", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var summary = await queueGrain.GetSummaryAsync();
            return Results.Ok(summary);
        });

        // Process (sync) the queue
        group.MapPost("/{orgId}/{deviceId}/sync", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var result = await queueGrain.ProcessQueueAsync();
            return Results.Ok(result);
        });

        // Get conflicted operations
        group.MapGet("/{orgId}/{deviceId}/sync-queue/conflicts", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var conflicts = await queueGrain.GetConflictedOperationsAsync();
            return Results.Ok(conflicts);
        });

        // Resolve a conflict
        group.MapPost("/{orgId}/{deviceId}/sync-queue/{operationId}/resolve", async (
            Guid orgId,
            Guid deviceId,
            Guid operationId,
            [FromBody] ResolveConflictApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var command = new ResolveConflictCommand(
                OperationId: operationId,
                Strategy: request.Strategy,
                ResolvedPayloadJson: request.ResolvedPayloadJson,
                ResolvedBy: request.ResolvedBy);

            var snapshot = await queueGrain.ResolveConflictAsync(command);
            return Results.Ok(snapshot);
        });

        // Clear synced operations from history
        group.MapDelete("/{orgId}/{deviceId}/sync-queue/synced", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            await queueGrain.ClearSyncedOperationsAsync();
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Device Health Endpoints
    // ============================================================================

    private static void MapDeviceHealthEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("DeviceHealth");

        // Get device health for a specific device
        group.MapGet("/{orgId}/{siteId}/health/{deviceId}", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var health = await statusGrain.GetDeviceHealthAsync(deviceId);
            if (health == null)
                return Results.NotFound();

            return Results.Ok(health);
        });

        // Get health summary for all devices at a site
        group.MapGet("/{orgId}/{siteId}/health", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var summary = await statusGrain.GetHealthSummaryAsync();
            return Results.Ok(summary);
        });

        // Record device heartbeat with health metrics
        group.MapPost("/{orgId}/{siteId}/health/{deviceId}/heartbeat", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            [FromBody] DeviceHealthHeartbeatRequest? request,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            UpdateDeviceHealthCommand? healthUpdate = null;
            if (request != null)
            {
                healthUpdate = new UpdateDeviceHealthCommand(
                    DeviceId: deviceId,
                    SignalStrength: request.SignalStrength,
                    LatencyMs: request.LatencyMs,
                    PrinterStatus: request.PrinterStatus,
                    PaperLevel: request.PaperLevel,
                    PendingPrintJobs: request.PendingPrintJobs);
            }

            await statusGrain.RecordHeartbeatAsync(deviceId, healthUpdate);
            return Results.Ok(new { message = "Heartbeat recorded" });
        });

        // Perform health check (mark stale devices as offline)
        group.MapPost("/{orgId}/{siteId}/health/check", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? offlineThresholdMinutes,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var threshold = TimeSpan.FromMinutes(offlineThresholdMinutes ?? 5);
            var staleDevices = await statusGrain.PerformHealthCheckAsync(threshold);

            return Results.Ok(new
            {
                staleDevicesCount = staleDevices.Count,
                staleDevices
            });
        });

        // Update printer health status
        group.MapPost("/{orgId}/{siteId}/health/{printerId}/printer-status", async (
            Guid orgId,
            Guid siteId,
            Guid printerId,
            [FromBody] UpdatePrinterHealthRequest request,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            await statusGrain.UpdatePrinterHealthAsync(printerId, request.Status, request.PaperLevel);
            return Results.Ok(new { message = "Printer health updated" });
        });
    }
}

// ============================================================================
// API Request/Response Types
// ============================================================================

public record QueuePrintJobApiRequest(
    Guid PrinterId,
    PrintJobType JobType,
    string Content,
    int? Copies = 1,
    int? Priority = 0,
    Guid? SourceOrderId = null,
    string? SourceReference = null);

public record CompletePrintJobApiRequest(string? PrinterResponse = null);

public record FailPrintJobApiRequest(string ErrorMessage, string? ErrorCode = null);

public record QueueOfflineOperationApiRequest(
    OfflineOperationType OperationType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTime ClientTimestamp,
    long ClientSequence,
    Guid? UserId = null,
    string? IdempotencyKey = null);

public record ResolveConflictApiRequest(
    ConflictResolutionStrategy Strategy,
    string? ResolvedPayloadJson = null,
    Guid? ResolvedBy = null);

public record DeviceHealthHeartbeatRequest(
    int? SignalStrength = null,
    int? LatencyMs = null,
    PrinterHealthStatus? PrinterStatus = null,
    int? PaperLevel = null,
    int? PendingPrintJobs = null);

public record UpdatePrinterHealthRequest(
    PrinterHealthStatus Status,
    int? PaperLevel = null);
