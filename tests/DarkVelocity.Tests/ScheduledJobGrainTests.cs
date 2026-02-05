using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.Streams;
using FluentAssertions;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ScheduledJobGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ScheduledJobGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetJobGrainKey(Guid orgId, Guid jobId) => $"{orgId}:jobs:{jobId}";
    private string GetRegistryGrainKey(Guid orgId) => $"{orgId}:job-registry";

    #region One-Time Job Tests

    [Fact]
    public async Task ScheduleAsync_OneTimeJob_ShouldCreateJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        var runAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        var job = await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Test One-Time Job",
            Description: "A test job that runs once",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "test-key",
            TargetMethodName: "ExecuteAsync",
            RunAt: runAt));

        // Assert
        job.Should().NotBeNull();
        job.JobId.Should().Be(jobId);
        job.Name.Should().Be("Test One-Time Job");
        job.TriggerType.Should().Be(JobTriggerType.OneTime);
        job.Status.Should().Be(JobStatus.Scheduled);
        job.NextRunAt.Should().BeCloseTo(runAt, TimeSpan.FromSeconds(1));
        job.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetJobAsync_ShouldReturnJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Test Job",
            Description: "Test",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddMinutes(5)));

        // Act
        var job = await grain.GetJobAsync();

        // Assert
        job.Should().NotBeNull();
        job!.Name.Should().Be("Test Job");
    }

    [Fact]
    public async Task GetJobAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        // Act
        var job = await grain.GetJobAsync();

        // Assert
        job.Should().BeNull();
    }

    #endregion

    #region Recurring Job Tests

    [Fact]
    public async Task ScheduleAsync_RecurringJob_ShouldCreateJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        var interval = TimeSpan.FromHours(1);

        // Act
        var job = await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Hourly Sync Job",
            Description: "Syncs data every hour",
            TargetGrainType: "ISyncGrain",
            TargetGrainKey: "sync-key",
            TargetMethodName: "SyncAsync",
            Interval: interval));

        // Assert
        job.Should().NotBeNull();
        job.TriggerType.Should().Be(JobTriggerType.Recurring);
        job.Interval.Should().Be(interval);
        job.NextRunAt.Should().NotBeNull();
        job.NextRunAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ScheduleAsync_RecurringJob_WithStartTime_ShouldUseStartTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        var startAt = DateTime.UtcNow.AddHours(2);

        // Act
        var job = await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Delayed Recurring Job",
            Description: "Starts in 2 hours",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1),
            StartAt: startAt));

        // Assert
        job.NextRunAt.Should().BeCloseTo(startAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Cron Job Tests

    [Fact]
    public async Task ScheduleAsync_CronJob_ShouldCreateJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        // Act
        var job = await grain.ScheduleAsync(new ScheduleCronJobCommand(
            Name: "Daily Backup Job",
            Description: "Runs backup at midnight",
            TargetGrainType: "IBackupGrain",
            TargetGrainKey: "backup-key",
            TargetMethodName: "BackupAsync",
            CronExpression: "0 0 * * *")); // Daily at midnight

        // Assert
        job.Should().NotBeNull();
        job.TriggerType.Should().Be(JobTriggerType.Cron);
        job.CronExpression.Should().Be("0 0 * * *");
        job.NextRunAt.Should().NotBeNull();
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task CancelAsync_ShouldCancelJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Job to Cancel",
            Description: "This job will be cancelled",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        // Act
        await grain.CancelAsync("No longer needed");

        // Assert
        var job = await grain.GetJobAsync();
        job.Should().NotBeNull();
        job!.Status.Should().Be(JobStatus.Cancelled);
        job.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CancelAsync_WhenNotExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        // Act & Assert
        var act = async () => await grain.CancelAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    #endregion

    #region Pause/Resume Tests

    [Fact]
    public async Task PauseAsync_ShouldPauseJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Job to Pause",
            Description: "This job will be paused",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1)));

        // Act
        await grain.PauseAsync();

        // Assert
        var job = await grain.GetJobAsync();
        job.Should().NotBeNull();
        job!.Status.Should().Be(JobStatus.Paused);
    }

    [Fact]
    public async Task ResumeAsync_ShouldResumeJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Job to Resume",
            Description: "This job will be paused then resumed",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1)));

        await grain.PauseAsync();

        // Act
        await grain.ResumeAsync();

        // Assert
        var job = await grain.GetJobAsync();
        job.Should().NotBeNull();
        job!.Status.Should().Be(JobStatus.Scheduled);
    }

    [Fact]
    public async Task ResumeAsync_WhenNotPaused_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Active Job",
            Description: "Not paused",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        // Act & Assert
        var act = async () => await grain.ResumeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not paused*");
    }

    #endregion

    #region Trigger Tests

    [Fact]
    public async Task TriggerAsync_ShouldExecuteImmediately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Manual Trigger Job",
            Description: "Will be triggered manually",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        // Act
        var execution = await grain.TriggerAsync();

        // Assert
        execution.Should().NotBeNull();
        execution.ExecutionId.Should().NotBeEmpty();
        execution.JobId.Should().Be(jobId);
        execution.Success.Should().BeTrue();
        execution.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task TriggerAsync_ShouldRecordExecution()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Trigger Test Job",
            Description: "Test",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        // Act
        await grain.TriggerAsync();
        var executions = await grain.GetExecutionsAsync();

        // Assert
        executions.Should().ContainSingle();
        executions[0].Success.Should().BeTrue();
        executions[0].CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region Update Schedule Tests

    [Fact]
    public async Task UpdateScheduleAsync_RecurringJob_ShouldUpdateInterval()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Update Interval Job",
            Description: "Test",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1)));

        var newInterval = TimeSpan.FromMinutes(30);

        // Act
        await grain.UpdateScheduleAsync(interval: newInterval);

        // Assert
        var job = await grain.GetJobAsync();
        job!.Interval.Should().Be(newInterval);
    }

    [Fact]
    public async Task UpdateScheduleAsync_CronJob_ShouldUpdateExpression()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleCronJobCommand(
            Name: "Update Cron Job",
            Description: "Test",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            CronExpression: "0 0 * * *"));

        // Act
        await grain.UpdateScheduleAsync(cronExpression: "0 * * * *"); // Hourly

        // Assert
        var job = await grain.GetJobAsync();
        job!.CronExpression.Should().Be("0 * * * *");
    }

    [Fact]
    public async Task UpdateScheduleAsync_OneTimeJob_WithInterval_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

        await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "One Time Job",
            Description: "Test",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        // Act & Assert
        var act = async () => await grain.UpdateScheduleAsync(interval: TimeSpan.FromHours(1));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*recurring jobs*");
    }

    #endregion

    #region Registry Tests

    [Fact]
    public async Task JobRegistry_ShouldTrackJobs()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _fixture.Cluster.GrainFactory.GetGrain<IJobRegistryGrain>(GetRegistryGrainKey(orgId));
        await registry.InitializeAsync(orgId);

        var jobId1 = Guid.NewGuid();
        var grain1 = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId1));

        var jobId2 = Guid.NewGuid();
        var grain2 = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId2));

        // Act
        await grain1.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Job 1",
            Description: "First job",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key1",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        await grain2.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Job 2",
            Description: "Second job",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key2",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1)));

        var jobs = await registry.GetJobsAsync();

        // Assert
        jobs.Should().HaveCount(2);
        jobs.Should().Contain(j => j.Name == "Job 1" && j.TriggerType == JobTriggerType.OneTime);
        jobs.Should().Contain(j => j.Name == "Job 2" && j.TriggerType == JobTriggerType.Recurring);
    }

    [Fact]
    public async Task JobRegistry_GetJobsAsync_ShouldFilterByStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var registry = _fixture.Cluster.GrainFactory.GetGrain<IJobRegistryGrain>(GetRegistryGrainKey(orgId));
        await registry.InitializeAsync(orgId);

        var jobId1 = Guid.NewGuid();
        var grain1 = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId1));

        var jobId2 = Guid.NewGuid();
        var grain2 = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId2));

        await grain1.ScheduleAsync(new ScheduleOneTimeJobCommand(
            Name: "Active Job",
            Description: "Will stay scheduled",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key1",
            TargetMethodName: "Execute",
            RunAt: DateTime.UtcNow.AddHours(1)));

        await grain2.ScheduleAsync(new ScheduleRecurringJobCommand(
            Name: "Paused Job",
            Description: "Will be paused",
            TargetGrainType: "ITestGrain",
            TargetGrainKey: "key2",
            TargetMethodName: "Execute",
            Interval: TimeSpan.FromHours(1)));

        await grain2.PauseAsync();

        // Act
        var scheduledJobs = await registry.GetJobsAsync(JobStatus.Scheduled);
        var pausedJobs = await registry.GetJobsAsync(JobStatus.Paused);

        // Assert
        scheduledJobs.Should().ContainSingle(j => j.Name == "Active Job");
        pausedJobs.Should().ContainSingle(j => j.Name == "Paused Job");
    }

    #endregion

    #region Stream Event Tests

    [Fact]
    public async Task ScheduleJob_ShouldPublishScheduledEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _fixture.Cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.ScheduledJobStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var registry = _fixture.Cluster.GrainFactory.GetGrain<IJobRegistryGrain>(GetRegistryGrainKey(orgId));
            await registry.InitializeAsync(orgId);

            var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

            // Act
            await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
                Name: "Event Test Job",
                Description: "Test",
                TargetGrainType: "ITestGrain",
                TargetGrainKey: "key",
                TargetMethodName: "Execute",
                RunAt: DateTime.UtcNow.AddHours(1)));

            // Wait for event propagation
            await Task.Delay(500);

            // Assert
            receivedEvents.Should().Contain(e => e is JobScheduledEvent);
            var scheduledEvent = receivedEvents.OfType<JobScheduledEvent>().First();
            scheduledEvent.JobId.Should().Be(jobId);
            scheduledEvent.JobName.Should().Be("Event Test Job");
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task TriggerJob_ShouldPublishStartedAndCompletedEvents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _fixture.Cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.ScheduledJobStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var registry = _fixture.Cluster.GrainFactory.GetGrain<IJobRegistryGrain>(GetRegistryGrainKey(orgId));
            await registry.InitializeAsync(orgId);

            var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

            await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
                Name: "Trigger Event Test",
                Description: "Test",
                TargetGrainType: "ITestGrain",
                TargetGrainKey: "key",
                TargetMethodName: "Execute",
                RunAt: DateTime.UtcNow.AddHours(1)));

            // Act
            await grain.TriggerAsync();

            // Wait for event propagation
            await Task.Delay(500);

            // Assert
            receivedEvents.Should().Contain(e => e is JobStartedEvent);
            receivedEvents.Should().Contain(e => e is JobCompletedEvent);

            var completedEvent = receivedEvents.OfType<JobCompletedEvent>().First();
            completedEvent.Success.Should().BeTrue();
            completedEvent.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task CancelJob_ShouldPublishCancelledEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _fixture.Cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.ScheduledJobStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var registry = _fixture.Cluster.GrainFactory.GetGrain<IJobRegistryGrain>(GetRegistryGrainKey(orgId));
            await registry.InitializeAsync(orgId);

            var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduledJobGrain>(GetJobGrainKey(orgId, jobId));

            await grain.ScheduleAsync(new ScheduleOneTimeJobCommand(
                Name: "Cancel Event Test",
                Description: "Test",
                TargetGrainType: "ITestGrain",
                TargetGrainKey: "key",
                TargetMethodName: "Execute",
                RunAt: DateTime.UtcNow.AddHours(1)));

            // Act
            await grain.CancelAsync("Testing cancellation");

            // Wait for event propagation
            await Task.Delay(500);

            // Assert
            receivedEvents.Should().Contain(e => e is JobCancelledEvent);
            var cancelledEvent = receivedEvents.OfType<JobCancelledEvent>().First();
            cancelledEvent.JobId.Should().Be(jobId);
            cancelledEvent.Reason.Should().Be("Testing cancellation");
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    #endregion
}
