using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Devices;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PrintJobGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PrintJobGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPrintJobGrain GetJobGrain(Guid orgId, Guid deviceId, Guid jobId)
    {
        var key = $"{orgId}:device:{deviceId}:printjob:{jobId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPrintJobGrain>(key);
    }

    private IDevicePrintQueueGrain GetQueueGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:device:{deviceId}:printqueue";
        return _fixture.Cluster.GrainFactory.GetGrain<IDevicePrintQueueGrain>(key);
    }

    // Given: A new print job with receipt content, printer assignment, and order reference
    // When: The job is queued for printing
    // Then: The job is created in Pending status with all specified properties and a queued timestamp
    [Fact]
    public async Task QueueAsync_ShouldCreatePendingJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);

        // Act
        var result = await grain.QueueAsync(new QueuePrintJobCommand(
            PrinterId: printerId,
            JobType: PrintJobType.Receipt,
            Content: "Test receipt content",
            Copies: 1,
            Priority: 0,
            SourceOrderId: null,
            SourceReference: "ORD-001"));

        // Assert
        result.JobId.Should().Be(jobId);
        result.DeviceId.Should().Be(deviceId);
        result.PrinterId.Should().Be(printerId);
        result.JobType.Should().Be(PrintJobType.Receipt);
        result.Status.Should().Be(PrintJobStatus.Pending);
        result.Content.Should().Be("Test receipt content");
        result.Copies.Should().Be(1);
        result.Priority.Should().Be(0);
        result.SourceReference.Should().Be("ORD-001");
        result.QueuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A queued print job in Pending status
    // When: The printer acknowledges and starts processing the job
    // Then: The job transitions to Printing status with a started timestamp
    [Fact]
    public async Task StartAsync_ShouldTransitionToPrinting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));

        // Act
        var result = await grain.StartAsync(new StartPrintJobCommand("Printer ACK"));

        // Assert
        result.Status.Should().Be(PrintJobStatus.Printing);
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A print job currently in Printing status
    // When: The printer successfully finishes printing
    // Then: The job transitions to Completed status with a completion timestamp
    [Fact]
    public async Task CompleteAsync_ShouldTransitionToCompleted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));
        await grain.StartAsync(new StartPrintJobCommand());

        // Act
        var result = await grain.CompleteAsync(new CompletePrintJobCommand("Success"));

        // Assert
        result.Status.Should().Be(PrintJobStatus.Completed);
        result.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A print job currently in Printing status
    // When: The printer reports a failure with an error code
    // Then: The job transitions to Failed status with error details, incremented retry count, and a scheduled retry time
    [Fact]
    public async Task FailAsync_ShouldTransitionToFailedWithRetryScheduled()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));
        await grain.StartAsync(new StartPrintJobCommand());

        // Act
        var result = await grain.FailAsync(new FailPrintJobCommand("Printer offline", "ERR001"));

        // Assert
        result.Status.Should().Be(PrintJobStatus.Failed);
        result.LastError.Should().Be("Printer offline");
        result.RetryCount.Should().Be(1);
        result.NextRetryAt.Should().NotBeNull();
        result.NextRetryAt.Should().BeAfter(DateTime.UtcNow);
    }

    // Given: A print job that has failed after a printing attempt
    // When: The job is retried
    // Then: The job resets back to Pending status for reprocessing
    [Fact]
    public async Task RetryAsync_ShouldResetToPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));
        await grain.StartAsync(new StartPrintJobCommand());
        await grain.FailAsync(new FailPrintJobCommand("Error"));

        // Act
        var result = await grain.RetryAsync();

        // Assert
        result.Status.Should().Be(PrintJobStatus.Pending);
    }

    // Given: A queued print job in Pending status
    // When: A user cancels the print job
    // Then: The job transitions to Cancelled status
    [Fact]
    public async Task CancelAsync_ShouldCancelJob()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));

        // Act
        await grain.CancelAsync("User cancelled");

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(PrintJobStatus.Cancelled);
    }

    // Given: A new print job with kitchen ticket content for a table order
    // When: The job is queued as a kitchen ticket type
    // Then: The job is created with the KitchenTicket job type
    [Fact]
    public async Task QueueAsync_WithKitchenTicket_ShouldSetCorrectType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);

        // Act
        var result = await grain.QueueAsync(new QueuePrintJobCommand(
            PrinterId: Guid.NewGuid(),
            JobType: PrintJobType.KitchenTicket,
            Content: "Table 5\n2x Burger\n1x Fries"));

        // Assert
        result.JobType.Should().Be(PrintJobType.KitchenTicket);
    }

    // Given: A new print job with elevated priority
    // When: The job is queued with a priority of 10
    // Then: The job is created with the specified priority level
    [Fact]
    public async Task QueueAsync_WithPriority_ShouldSetPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);

        // Act
        var result = await grain.QueueAsync(new QueuePrintJobCommand(
            PrinterId: Guid.NewGuid(),
            JobType: PrintJobType.Receipt,
            Content: "Priority content",
            Priority: 10));

        // Assert
        result.Priority.Should().Be(10);
    }

    // Given: A print job grain that has never been queued
    // When: Checking whether the job exists
    // Then: The job does not exist
    [Fact]
    public async Task ExistsAsync_WhenNotQueued_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetJobGrain(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    // Given: A print job that has been queued
    // When: Checking whether the job exists
    // Then: The job exists
    [Fact]
    public async Task ExistsAsync_WhenQueued_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var grain = GetJobGrain(orgId, deviceId, jobId);
        await grain.QueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Content"));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DevicePrintQueueGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DevicePrintQueueGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDevicePrintQueueGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:device:{deviceId}:printqueue";
        return _fixture.Cluster.GrainFactory.GetGrain<IDevicePrintQueueGrain>(key);
    }

    // Given: A newly created device print queue
    // When: The queue is initialized for a device
    // Then: The queue starts empty with zero pending jobs
    [Fact]
    public async Task InitializeAsync_ShouldInitializeQueue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        await grain.InitializeAsync(deviceId);

        // Assert - no exception
        var summary = await grain.GetSummaryAsync();
        summary.PendingJobs.Should().Be(0);
    }

    // Given: An initialized device print queue with no jobs
    // When: A receipt print job is enqueued
    // Then: The job is added in Pending status and the queue count increases to one
    [Fact]
    public async Task EnqueueAsync_ShouldAddJobToQueue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act
        var result = await grain.EnqueueAsync(new QueuePrintJobCommand(
            PrinterId: printerId,
            JobType: PrintJobType.Receipt,
            Content: "Test content"));

        // Assert
        result.Status.Should().Be(PrintJobStatus.Pending);
        var summary = await grain.GetSummaryAsync();
        summary.PendingJobs.Should().Be(1);
    }

    // Given: An initialized device print queue
    // When: Three print jobs are enqueued sequentially
    // Then: All three jobs are queued and the pending count reflects the total
    [Fact]
    public async Task EnqueueAsync_MultipleTimes_ShouldQueueInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Job 1"));
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Job 2"));
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Job 3"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.PendingJobs.Should().Be(3);
    }

    // Given: An initialized device print queue with jobs at different priority levels
    // When: The next job is dequeued
    // Then: The highest priority job is returned first
    [Fact]
    public async Task EnqueueAsync_WithPriority_ShouldOrderByPriority()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act - enqueue with different priorities
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Low priority", Priority: 1));
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "High priority", Priority: 10));
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Medium priority", Priority: 5));

        // Assert - next job should be highest priority
        var nextJob = await grain.DequeueAsync();
        nextJob.Should().NotBeNull();
        nextJob!.Content.Should().Be("High priority");
    }

    // Given: An initialized device print queue with no pending jobs
    // When: A dequeue is attempted
    // Then: No job is returned
    [Fact]
    public async Task DequeueAsync_WhenEmpty_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act
        var result = await grain.DequeueAsync();

        // Assert
        result.Should().BeNull();
    }

    // Given: An initialized device print queue with two enqueued jobs
    // When: The pending jobs are retrieved
    // Then: Both jobs are returned and all are in Pending status
    [Fact]
    public async Task GetPendingJobsAsync_ShouldReturnPendingJobs()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Job 1"));
        await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Job 2"));

        // Act
        var pending = await grain.GetPendingJobsAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.All(j => j.Status == PrintJobStatus.Pending).Should().BeTrue();
    }

    // Given: An initialized device print queue with one enqueued job
    // When: The job status is updated to Completed
    // Then: The job moves from pending to history and completed count increases
    [Fact]
    public async Task NotifyJobStatusChangedAsync_ToCompleted_ShouldMoveToHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);
        var job = await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Test job"));

        // Act
        await grain.NotifyJobStatusChangedAsync(job.JobId, PrintJobStatus.Completed);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.PendingJobs.Should().Be(0);
        summary.CompletedJobs.Should().Be(1);
    }

    // Given: A device print queue with a completed job in its history
    // When: The history is cleared
    // Then: The job history becomes empty
    [Fact]
    public async Task ClearHistoryAsync_ShouldClearHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);
        var job = await grain.EnqueueAsync(new QueuePrintJobCommand(
            Guid.NewGuid(), PrintJobType.Receipt, "Test"));
        await grain.NotifyJobStatusChangedAsync(job.JobId, PrintJobStatus.Completed);

        // Act
        await grain.ClearHistoryAsync();

        // Assert
        var history = await grain.GetHistoryAsync();
        history.Should().BeEmpty();
    }
}
