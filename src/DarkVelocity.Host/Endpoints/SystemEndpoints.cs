using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class SystemEndpoints
{
    public static WebApplication MapSystemEndpoints(this WebApplication app)
    {
        // Notification endpoints
        var notifications = app.MapGroup("/api/orgs/{orgId}/notifications")
            .WithTags("Notifications");

        notifications.MapPost("/", SendNotification)
            .WithName("SendNotification")
            .WithSummary("Send a notification (email, SMS, push, or Slack)");

        notifications.MapGet("/", GetNotifications)
            .WithName("GetNotifications")
            .WithSummary("Get recent notifications");

        notifications.MapGet("/{notificationId:guid}", GetNotification)
            .WithName("GetNotification")
            .WithSummary("Get a specific notification");

        notifications.MapPost("/{notificationId:guid}/retry", RetryNotification)
            .WithName("RetryNotification")
            .WithSummary("Retry a failed notification");

        // Notification channels
        notifications.MapGet("/channels", GetNotificationChannels)
            .WithName("GetNotificationChannels")
            .WithSummary("Get configured notification channels");

        notifications.MapPost("/channels", AddNotificationChannel)
            .WithName("AddNotificationChannel")
            .WithSummary("Add a notification channel");

        notifications.MapPut("/channels/{channelId:guid}", UpdateNotificationChannel)
            .WithName("UpdateNotificationChannel")
            .WithSummary("Update a notification channel");

        notifications.MapDelete("/channels/{channelId:guid}", RemoveNotificationChannel)
            .WithName("RemoveNotificationChannel")
            .WithSummary("Remove a notification channel");

        // Alert endpoints
        var alerts = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/alerts")
            .WithTags("Alerts");

        alerts.MapPost("/", CreateAlert)
            .WithName("CreateAlert")
            .WithSummary("Create a new alert");

        alerts.MapGet("/", GetAlerts)
            .WithName("GetAlerts")
            .WithSummary("Get alerts with optional filtering");

        alerts.MapGet("/active", GetActiveAlerts)
            .WithName("GetActiveAlerts")
            .WithSummary("Get active alerts");

        alerts.MapGet("/{alertId:guid}", GetAlert)
            .WithName("GetAlert")
            .WithSummary("Get a specific alert");

        alerts.MapPost("/{alertId:guid}/acknowledge", AcknowledgeAlert)
            .WithName("AcknowledgeAlert")
            .WithSummary("Acknowledge an alert");

        alerts.MapPost("/{alertId:guid}/resolve", ResolveAlert)
            .WithName("ResolveAlert")
            .WithSummary("Resolve an alert");

        alerts.MapPost("/{alertId:guid}/snooze", SnoozeAlert)
            .WithName("SnoozeAlert")
            .WithSummary("Snooze an alert");

        alerts.MapPost("/{alertId:guid}/dismiss", DismissAlert)
            .WithName("DismissAlert")
            .WithSummary("Dismiss an alert");

        // Alert rules
        alerts.MapGet("/rules", GetAlertRules)
            .WithName("GetAlertRules")
            .WithSummary("Get alert rules");

        alerts.MapPut("/rules", UpdateAlertRule)
            .WithName("UpdateAlertRule")
            .WithSummary("Update an alert rule");

        alerts.MapPost("/evaluate", EvaluateAlertRules)
            .WithName("EvaluateAlertRules")
            .WithSummary("Evaluate alert rules against provided metrics");

        // Scheduled jobs
        var jobs = app.MapGroup("/api/orgs/{orgId}/jobs")
            .WithTags("Scheduled Jobs");

        jobs.MapPost("/", ScheduleJob)
            .WithName("ScheduleJob")
            .WithSummary("Schedule a new job");

        jobs.MapGet("/", GetJobs)
            .WithName("GetJobs")
            .WithSummary("Get scheduled jobs");

        jobs.MapGet("/{jobId:guid}", GetJob)
            .WithName("GetJob")
            .WithSummary("Get a specific job");

        jobs.MapPost("/{jobId:guid}/trigger", TriggerJob)
            .WithName("TriggerJob")
            .WithSummary("Manually trigger a job");

        jobs.MapPost("/{jobId:guid}/pause", PauseJob)
            .WithName("PauseJob")
            .WithSummary("Pause a job");

        jobs.MapPost("/{jobId:guid}/resume", ResumeJob)
            .WithName("ResumeJob")
            .WithSummary("Resume a paused job");

        jobs.MapDelete("/{jobId:guid}", CancelJob)
            .WithName("CancelJob")
            .WithSummary("Cancel a job");

        jobs.MapGet("/{jobId:guid}/executions", GetJobExecutions)
            .WithName("GetJobExecutions")
            .WithSummary("Get job execution history");

        return app;
    }

    // ============================================================================
    // Notification Endpoints
    // ============================================================================

    private static async Task<IResult> SendNotification(
        Guid orgId,
        [FromBody] SendNotificationRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId);

        Notification notification = request.Type.ToLowerInvariant() switch
        {
            "email" => await grain.SendEmailAsync(new SendEmailCommand(
                To: request.Recipient,
                Subject: request.Subject ?? "Notification",
                Body: request.Body,
                IsHtml: request.IsHtml ?? true)),

            "sms" => await grain.SendSmsAsync(new SendSmsCommand(
                To: request.Recipient,
                Message: request.Body)),

            "push" => await grain.SendPushAsync(new SendPushCommand(
                DeviceToken: request.Recipient,
                Title: request.Subject ?? "Notification",
                Body: request.Body,
                Data: request.Data)),

            "slack" => await grain.SendSlackAsync(new SendSlackCommand(
                WebhookUrl: request.Recipient,
                Message: request.Body,
                Channel: request.Channel,
                Username: request.Username)),

            _ => throw new ArgumentException($"Unknown notification type: {request.Type}")
        };

        return Results.Created($"/api/orgs/{orgId}/notifications/{notification.NotificationId}",
            Hal.Resource(notification, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/notifications/{notification.NotificationId}" },
                ["retry"] = new { href = $"/api/orgs/{orgId}/notifications/{notification.NotificationId}/retry" }
            }));
    }

    private static async Task<IResult> GetNotifications(
        Guid orgId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            return Results.Ok(Array.Empty<Notification>());

        NotificationType? notificationType = type != null
            ? Enum.Parse<NotificationType>(type, ignoreCase: true)
            : null;

        NotificationStatus? notificationStatus = status != null
            ? Enum.Parse<NotificationStatus>(status, ignoreCase: true)
            : null;

        var notifications = await grain.GetNotificationsAsync(
            notificationType,
            notificationStatus,
            limit ?? 100);

        var items = notifications.Select(n => (object)Hal.Resource(n, new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}/notifications/{n.NotificationId}" },
            ["retry"] = new { href = $"/api/orgs/{orgId}/notifications/{n.NotificationId}/retry" }
        })).ToList();
        return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/notifications", items, items.Count));
    }

    private static async Task<IResult> GetNotification(
        Guid orgId,
        Guid notificationId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        var notification = await grain.GetNotificationAsync(notificationId);
        if (notification == null) return Results.NotFound();
        return Results.Ok(Hal.Resource(notification, new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}/notifications/{notificationId}" },
            ["retry"] = new { href = $"/api/orgs/{orgId}/notifications/{notificationId}/retry" }
        }));
    }

    private static async Task<IResult> RetryNotification(
        Guid orgId,
        Guid notificationId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        var notification = await grain.RetryAsync(notificationId);
        return Results.Ok(Hal.Resource(notification, new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}/notifications/{notificationId}" },
            ["retry"] = new { href = $"/api/orgs/{orgId}/notifications/{notificationId}/retry" }
        }));
    }

    private static async Task<IResult> GetNotificationChannels(
        Guid orgId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId);

        var channels = await grain.GetChannelsAsync();
        return Results.Ok(channels);
    }

    private static async Task<IResult> AddNotificationChannel(
        Guid orgId,
        [FromBody] NotificationChannelConfig channel,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId);

        var created = await grain.AddChannelAsync(channel);
        return Results.Created($"/api/orgs/{orgId}/notifications/channels/{created.ChannelId}", created);
    }

    private static async Task<IResult> UpdateNotificationChannel(
        Guid orgId,
        Guid channelId,
        [FromBody] NotificationChannelConfig channel,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.UpdateChannelAsync(channel with { ChannelId = channelId });
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveNotificationChannel(
        Guid orgId,
        Guid channelId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<INotificationGrain>($"{orgId}:notifications");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.RemoveChannelAsync(channelId);
        return Results.NoContent();
    }

    // ============================================================================
    // Alert Endpoints
    // ============================================================================

    private static async Task<IResult> CreateAlert(
        Guid orgId,
        Guid siteId,
        [FromBody] CreateAlertRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId, siteId);

        var alert = await grain.CreateAlertAsync(new CreateAlertCommand(
            Type: Enum.Parse<AlertType>(request.Type, ignoreCase: true),
            Severity: Enum.Parse<AlertSeverity>(request.Severity, ignoreCase: true),
            Title: request.Title,
            Message: request.Message,
            EntityId: request.EntityId,
            EntityType: request.EntityType,
            Metadata: request.Metadata));

        return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/alerts/{alert.AlertId}",
            Hal.Resource(alert, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/alerts/{alert.AlertId}" },
                ["acknowledge"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/alerts/{alert.AlertId}/acknowledge" },
                ["resolve"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/alerts/{alert.AlertId}/resolve" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
    }

    private static async Task<IResult> GetAlerts(
        Guid orgId,
        Guid siteId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.Ok(Array.Empty<Alert>());

        AlertType? alertType = type != null
            ? Enum.Parse<AlertType>(type, ignoreCase: true)
            : null;

        AlertStatus? alertStatus = status != null
            ? Enum.Parse<AlertStatus>(status, ignoreCase: true)
            : null;

        var alerts = await grain.GetAlertsAsync(alertStatus, alertType, limit ?? 100);
        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetActiveAlerts(
        Guid orgId,
        Guid siteId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.Ok(Array.Empty<Alert>());

        var alerts = await grain.GetActiveAlertsAsync();
        return Results.Ok(alerts);
    }

    private static async Task<IResult> GetAlert(
        Guid orgId,
        Guid siteId,
        Guid alertId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        var alert = await grain.GetAlertAsync(alertId);
        return alert != null ? Results.Ok(alert) : Results.NotFound();
    }

    private static async Task<IResult> AcknowledgeAlert(
        Guid orgId,
        Guid siteId,
        Guid alertId,
        [FromBody] AcknowledgeAlertRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.AcknowledgeAsync(new AcknowledgeAlertCommand(alertId, request.UserId));
        return Results.NoContent();
    }

    private static async Task<IResult> ResolveAlert(
        Guid orgId,
        Guid siteId,
        Guid alertId,
        [FromBody] ResolveAlertRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.ResolveAsync(new ResolveAlertCommand(alertId, request.UserId, request.Notes));
        return Results.NoContent();
    }

    private static async Task<IResult> SnoozeAlert(
        Guid orgId,
        Guid siteId,
        Guid alertId,
        [FromBody] SnoozeAlertRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.SnoozeAsync(new SnoozeAlertCommand(alertId, TimeSpan.FromMinutes(request.DurationMinutes), request.UserId));
        return Results.NoContent();
    }

    private static async Task<IResult> DismissAlert(
        Guid orgId,
        Guid siteId,
        Guid alertId,
        [FromBody] DismissAlertRequest request,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.DismissAsync(new DismissAlertCommand(alertId, request.UserId, request.Reason));
        return Results.NoContent();
    }

    private static async Task<IResult> GetAlertRules(
        Guid orgId,
        Guid siteId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId, siteId);

        var rules = await grain.GetRulesAsync();
        return Results.Ok(rules);
    }

    private static async Task<IResult> UpdateAlertRule(
        Guid orgId,
        Guid siteId,
        [FromBody] AlertRule rule,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId, siteId);

        await grain.UpdateRuleAsync(rule);
        return Results.NoContent();
    }

    private static async Task<IResult> EvaluateAlertRules(
        Guid orgId,
        Guid siteId,
        [FromBody] MetricsSnapshot metrics,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IAlertGrain>($"{orgId}:{siteId}:alerts");

        if (!await grain.ExistsAsync())
            await grain.InitializeAsync(orgId, siteId);

        var triggeredAlerts = await grain.EvaluateRulesAsync(metrics);
        return Results.Ok(new { triggeredAlerts, count = triggeredAlerts.Count });
    }

    // ============================================================================
    // Scheduled Job Endpoints
    // ============================================================================

    private static async Task<IResult> ScheduleJob(
        Guid orgId,
        [FromBody] ScheduleJobRequest request,
        IGrainFactory grainFactory)
    {
        var jobId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        // Initialize registry if needed
        var registry = grainFactory.GetGrain<IJobRegistryGrain>($"{orgId}:job-registry");
        if (!await registry.ExistsAsync())
            await registry.InitializeAsync(orgId);

        ScheduledJob job = request.TriggerType.ToLowerInvariant() switch
        {
            "onetime" => await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
                Name: request.Name,
                Description: request.Description ?? "",
                TargetGrainType: request.TargetGrainType,
                TargetGrainKey: request.TargetGrainKey,
                TargetMethodName: request.TargetMethodName,
                RunAt: request.RunAt ?? DateTime.UtcNow.AddMinutes(1),
                Parameters: request.Parameters,
                MaxRetries: request.MaxRetries ?? 3)),

            "recurring" => await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
                Name: request.Name,
                Description: request.Description ?? "",
                TargetGrainType: request.TargetGrainType,
                TargetGrainKey: request.TargetGrainKey,
                TargetMethodName: request.TargetMethodName,
                Interval: request.Interval ?? TimeSpan.FromHours(1),
                StartAt: request.RunAt,
                Parameters: request.Parameters,
                MaxRetries: request.MaxRetries ?? 3)),

            "cron" => await grain.ScheduleAsync(new ScheduleCronJobCommand(
                Name: request.Name,
                Description: request.Description ?? "",
                TargetGrainType: request.TargetGrainType,
                TargetGrainKey: request.TargetGrainKey,
                TargetMethodName: request.TargetMethodName,
                CronExpression: request.CronExpression ?? "0 * * * *",
                Parameters: request.Parameters,
                MaxRetries: request.MaxRetries ?? 3)),

            _ => throw new ArgumentException($"Unknown trigger type: {request.TriggerType}")
        };

        return Results.Created($"/api/orgs/{orgId}/jobs/{job.JobId}",
            Hal.Resource(job, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/jobs/{job.JobId}" },
                ["trigger"] = new { href = $"/api/orgs/{orgId}/jobs/{job.JobId}/trigger" },
                ["pause"] = new { href = $"/api/orgs/{orgId}/jobs/{job.JobId}/pause" },
                ["executions"] = new { href = $"/api/orgs/{orgId}/jobs/{job.JobId}/executions" }
            }));
    }

    private static async Task<IResult> GetJobs(
        Guid orgId,
        [FromQuery] string? status,
        IGrainFactory grainFactory)
    {
        var registry = grainFactory.GetGrain<IJobRegistryGrain>($"{orgId}:job-registry");

        if (!await registry.ExistsAsync())
            return Results.Ok(Array.Empty<JobRegistryEntry>());

        JobStatus? jobStatus = status != null
            ? Enum.Parse<JobStatus>(status, ignoreCase: true)
            : null;

        var jobs = await registry.GetJobsAsync(jobStatus);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetJob(
        Guid orgId,
        Guid jobId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        var job = await grain.GetJobAsync();
        return job != null ? Results.Ok(job) : Results.NotFound();
    }

    private static async Task<IResult> TriggerJob(
        Guid orgId,
        Guid jobId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        var execution = await grain.TriggerAsync();
        return Results.Ok(execution);
    }

    private static async Task<IResult> PauseJob(
        Guid orgId,
        Guid jobId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.PauseAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> ResumeJob(
        Guid orgId,
        Guid jobId,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.ResumeAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> CancelJob(
        Guid orgId,
        Guid jobId,
        [FromQuery] string? reason,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        await grain.CancelAsync(reason);
        return Results.NoContent();
    }

    private static async Task<IResult> GetJobExecutions(
        Guid orgId,
        Guid jobId,
        [FromQuery] int? limit,
        IGrainFactory grainFactory)
    {
        var grain = grainFactory.GetGrain<IScheduledJobGrain>($"{orgId}:jobs:{jobId}");

        if (!await grain.ExistsAsync())
            return Results.NotFound();

        var executions = await grain.GetExecutionsAsync(limit ?? 20);
        return Results.Ok(executions);
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record SendNotificationRequest(
    string Type,
    string Recipient,
    string Body,
    string? Subject = null,
    bool? IsHtml = null,
    Dictionary<string, string>? Data = null,
    string? Channel = null,
    string? Username = null);

public record CreateAlertRequest(
    string Type,
    string Severity,
    string Title,
    string Message,
    Guid? EntityId = null,
    string? EntityType = null,
    Dictionary<string, string>? Metadata = null);

public record AcknowledgeAlertRequest(Guid UserId);
public record ResolveAlertRequest(Guid UserId, string? Notes = null);
public record SnoozeAlertRequest(Guid UserId, int DurationMinutes);
public record DismissAlertRequest(Guid UserId, string? Reason = null);

public record ScheduleJobRequest(
    string Name,
    string TriggerType,
    string TargetGrainType,
    string TargetGrainKey,
    string TargetMethodName,
    string? Description = null,
    DateTime? RunAt = null,
    TimeSpan? Interval = null,
    string? CronExpression = null,
    Dictionary<string, string>? Parameters = null,
    int? MaxRetries = null);
