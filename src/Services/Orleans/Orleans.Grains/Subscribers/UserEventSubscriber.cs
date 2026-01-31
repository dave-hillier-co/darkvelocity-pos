using DarkVelocity.Orleans.Grains;
using DarkVelocity.Orleans.Grains.Grains;
using DarkVelocity.Orleans.Grains.State;
using DarkVelocity.Orleans.Grains.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Orleans.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that syncs user events to employee grains.
/// This keeps User and Employee projections in sync within the Orleans cluster.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.UserStreamNamespace)]
public class UserEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<UserEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public UserEventSubscriberGrain(ILogger<UserEventSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.UserStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "UserEventSubscriber activated for organization {OrgId}",
            this.GetPrimaryKeyString());

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task OnNextAsync(IStreamEvent item, StreamSequenceToken? token = null)
    {
        try
        {
            switch (item)
            {
                case UserStatusChangedEvent statusEvent:
                    await HandleUserStatusChangedAsync(statusEvent);
                    break;

                case UserUpdatedEvent updatedEvent:
                    await HandleUserUpdatedAsync(updatedEvent);
                    break;

                case UserSiteAccessGrantedEvent siteGrantedEvent:
                    await HandleSiteAccessGrantedAsync(siteGrantedEvent);
                    break;

                case UserSiteAccessRevokedEvent siteRevokedEvent:
                    await HandleSiteAccessRevokedAsync(siteRevokedEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("User event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in user event stream");
        return Task.CompletedTask;
    }

    private async Task HandleUserStatusChangedAsync(UserStatusChangedEvent evt)
    {
        _logger.LogInformation(
            "User {UserId} status changed from {OldStatus} to {NewStatus}",
            evt.UserId,
            evt.OldStatus,
            evt.NewStatus);

        // Find the employee grain for this user and sync the status
        // In a real implementation, you'd have a mapping from UserId to EmployeeId
        // For now, we'll use the userId as a lookup key
        var employeeKey = GrainKeys.EmployeeByUser(evt.OrganizationId, evt.UserId);
        var employeeLookupGrain = GrainFactory.GetGrain<IEmployeeLookupGrain>(employeeKey);

        var employeeId = await employeeLookupGrain.GetEmployeeIdAsync();
        if (employeeId.HasValue)
        {
            var employeeGrain = GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(evt.OrganizationId, employeeId.Value));

            await employeeGrain.SyncFromUserAsync(null, null, evt.NewStatus);

            _logger.LogInformation(
                "Synced status change to employee {EmployeeId}",
                employeeId);
        }
    }

    private async Task HandleUserUpdatedAsync(UserUpdatedEvent evt)
    {
        if (!evt.ChangedFields.Contains("FirstName") && !evt.ChangedFields.Contains("LastName"))
            return;

        _logger.LogInformation(
            "User {UserId} updated: {ChangedFields}",
            evt.UserId,
            string.Join(", ", evt.ChangedFields));

        var employeeKey = GrainKeys.EmployeeByUser(evt.OrganizationId, evt.UserId);
        var employeeLookupGrain = GrainFactory.GetGrain<IEmployeeLookupGrain>(employeeKey);

        var employeeId = await employeeLookupGrain.GetEmployeeIdAsync();
        if (employeeId.HasValue)
        {
            var employeeGrain = GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(evt.OrganizationId, employeeId.Value));

            await employeeGrain.SyncFromUserAsync(evt.FirstName, evt.LastName, UserStatus.Active);

            _logger.LogInformation(
                "Synced name change to employee {EmployeeId}",
                employeeId);
        }
    }

    private async Task HandleSiteAccessGrantedAsync(UserSiteAccessGrantedEvent evt)
    {
        _logger.LogInformation(
            "User {UserId} granted access to site {SiteId}",
            evt.UserId,
            evt.SiteId);

        var employeeKey = GrainKeys.EmployeeByUser(evt.OrganizationId, evt.UserId);
        var employeeLookupGrain = GrainFactory.GetGrain<IEmployeeLookupGrain>(employeeKey);

        var employeeId = await employeeLookupGrain.GetEmployeeIdAsync();
        if (employeeId.HasValue)
        {
            var employeeGrain = GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(evt.OrganizationId, employeeId.Value));

            await employeeGrain.GrantSiteAccessAsync(evt.SiteId);

            _logger.LogInformation(
                "Granted site {SiteId} access to employee {EmployeeId}",
                evt.SiteId,
                employeeId);
        }
    }

    private async Task HandleSiteAccessRevokedAsync(UserSiteAccessRevokedEvent evt)
    {
        _logger.LogInformation(
            "User {UserId} revoked access from site {SiteId}",
            evt.UserId,
            evt.SiteId);

        var employeeKey = GrainKeys.EmployeeByUser(evt.OrganizationId, evt.UserId);
        var employeeLookupGrain = GrainFactory.GetGrain<IEmployeeLookupGrain>(employeeKey);

        var employeeId = await employeeLookupGrain.GetEmployeeIdAsync();
        if (employeeId.HasValue)
        {
            var employeeGrain = GrainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(evt.OrganizationId, employeeId.Value));

            try
            {
                await employeeGrain.RevokeSiteAccessAsync(evt.SiteId);
                _logger.LogInformation(
                    "Revoked site {SiteId} access from employee {EmployeeId}",
                    evt.SiteId,
                    employeeId);
            }
            catch (InvalidOperationException ex)
            {
                // Cannot revoke default site - this is expected
                _logger.LogDebug(ex, "Could not revoke site access: {Message}", ex.Message);
            }
        }
    }
}

/// <summary>
/// Lookup grain to map UserId to EmployeeId.
/// Key format: "orgId:employeebyuser:userId"
/// </summary>
public interface IEmployeeLookupGrain : IGrainWithStringKey
{
    Task<Guid?> GetEmployeeIdAsync();
    Task SetEmployeeIdAsync(Guid employeeId);
}

public class EmployeeLookupGrain : Grain, IEmployeeLookupGrain
{
    private Guid? _employeeId;

    public Task<Guid?> GetEmployeeIdAsync() => Task.FromResult(_employeeId);

    public Task SetEmployeeIdAsync(Guid employeeId)
    {
        _employeeId = employeeId;
        return Task.CompletedTask;
    }
}
