using DarkVelocity.Orleans.Grains;
using DarkVelocity.Orleans.Grains.Grains;
using DarkVelocity.Orleans.Grains.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Orleans.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles employee events for HR/payroll integration.
///
/// This subscriber enables:
/// - Payroll system updates when employees clock in/out
/// - HR notifications for employee status changes and terminations
/// - Time tracking and labor cost projections
/// - Badge/access system integration
/// </summary>
[ImplicitStreamSubscription(StreamConstants.EmployeeStreamNamespace)]
public class EmployeeEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<EmployeeEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    // Account codes for labor cost tracking
    private const string LaborExpenseAccountCode = "5100";
    private const string AccruedPayrollAccountCode = "2200";

    public EmployeeEventSubscriberGrain(ILogger<EmployeeEventSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.EmployeeStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "EmployeeEventSubscriber activated for organization {OrgId}",
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
                case EmployeeCreatedEvent createdEvent:
                    await HandleEmployeeCreatedAsync(createdEvent);
                    break;

                case EmployeeClockedInEvent clockedInEvent:
                    await HandleEmployeeClockedInAsync(clockedInEvent);
                    break;

                case EmployeeClockedOutEvent clockedOutEvent:
                    await HandleEmployeeClockedOutAsync(clockedOutEvent);
                    break;

                case EmployeeTerminatedEvent terminatedEvent:
                    await HandleEmployeeTerminatedAsync(terminatedEvent);
                    break;

                case EmployeeStatusChangedEvent statusEvent:
                    await HandleEmployeeStatusChangedAsync(statusEvent);
                    break;

                case EmployeeUpdatedEvent updatedEvent:
                    _logger.LogDebug(
                        "Employee {EmployeeId} updated: {ChangedFields}",
                        updatedEvent.EmployeeId,
                        string.Join(", ", updatedEvent.ChangedFields));
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling employee event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Employee event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in employee event stream");
        return Task.CompletedTask;
    }

    private Task HandleEmployeeCreatedAsync(EmployeeCreatedEvent evt)
    {
        _logger.LogInformation(
            "New employee created: {FirstName} {LastName} (#{EmployeeNumber}) - {EmploymentType}",
            evt.FirstName,
            evt.LastName,
            evt.EmployeeNumber,
            evt.EmploymentType);

        // In a real implementation, you might:
        // 1. Create payroll record in external HR system
        // 2. Provision access badges
        // 3. Set up training schedules
        // 4. Initialize benefits enrollment

        return Task.CompletedTask;
    }

    private async Task HandleEmployeeClockedInAsync(EmployeeClockedInEvent evt)
    {
        _logger.LogInformation(
            "Employee {EmployeeId} clocked in at site {SiteId} at {ClockInTime}",
            evt.EmployeeId,
            evt.SiteId,
            evt.ClockInTime);

        // In a real implementation, you might:
        // 1. Update real-time labor dashboard
        // 2. Check for overtime thresholds
        // 3. Verify scheduled shift compliance
        // 4. Update floor management display

        // Publish to alert stream if overtime approaching (example integration)
        // This would check against configured thresholds
        await Task.CompletedTask;
    }

    private async Task HandleEmployeeClockedOutAsync(EmployeeClockedOutEvent evt)
    {
        _logger.LogInformation(
            "Employee {EmployeeId} clocked out at site {SiteId}. Total hours: {TotalHours:F2}",
            evt.EmployeeId,
            evt.SiteId,
            evt.TotalHours);

        // Create accrued payroll entry for the worked hours
        // In production, you would look up the employee's hourly rate
        var estimatedHourlyRate = 15.00m; // Default rate, would be looked up
        var laborCost = evt.TotalHours * estimatedHourlyRate;

        if (laborCost > 0)
        {
            var journalEntryId = Guid.NewGuid();

            // Debit Labor Expense (increase expense)
            var laborExpenseGrain = GrainFactory.GetGrain<IAccountGrain>(
                GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, LaborExpenseAccountCode)));

            await laborExpenseGrain.PostDebitAsync(new PostDebitCommand(
                Amount: laborCost,
                Description: $"Labor cost - Employee {evt.EmployeeId} worked {evt.TotalHours:F2} hours",
                PerformedBy: Guid.Empty,
                ReferenceType: "TimeEntry",
                ReferenceId: evt.EmployeeId,
                AccountingJournalEntryId: journalEntryId));

            // Credit Accrued Payroll (increase liability)
            var accruedPayrollGrain = GrainFactory.GetGrain<IAccountGrain>(
                GrainKeys.Account(evt.OrganizationId, GetAccountId(evt.OrganizationId, AccruedPayrollAccountCode)));

            await accruedPayrollGrain.PostCreditAsync(new PostCreditCommand(
                Amount: laborCost,
                Description: $"Accrued payroll - Employee {evt.EmployeeId}",
                PerformedBy: Guid.Empty,
                ReferenceType: "TimeEntry",
                ReferenceId: evt.EmployeeId,
                AccountingJournalEntryId: journalEntryId));

            _logger.LogInformation(
                "Created labor cost journal entry {JournalEntryId} for {Amount:C}",
                journalEntryId,
                laborCost);
        }
    }

    private Task HandleEmployeeTerminatedAsync(EmployeeTerminatedEvent evt)
    {
        _logger.LogWarning(
            "Employee {EmployeeId} terminated on {TerminationDate}. Reason: {Reason}",
            evt.EmployeeId,
            evt.TerminationDate,
            evt.Reason ?? "Not specified");

        // In a real implementation, you might:
        // 1. Trigger final paycheck calculation
        // 2. Revoke access badges immediately
        // 3. Schedule exit interview
        // 4. Update HR compliance records
        // 5. Calculate accrued PTO payout
        // 6. Notify relevant managers

        return Task.CompletedTask;
    }

    private Task HandleEmployeeStatusChangedAsync(EmployeeStatusChangedEvent evt)
    {
        _logger.LogInformation(
            "Employee {EmployeeId} status changed from {OldStatus} to {NewStatus}",
            evt.EmployeeId,
            evt.OldStatus,
            evt.NewStatus);

        // In a real implementation, you might:
        // 1. Update access permissions based on new status
        // 2. Trigger compliance notifications
        // 3. Update scheduling eligibility
        // 4. Adjust benefits enrollment

        return Task.CompletedTask;
    }

    private static Guid GetAccountId(Guid orgId, string accountCode)
    {
        var input = $"{orgId}:{accountCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
