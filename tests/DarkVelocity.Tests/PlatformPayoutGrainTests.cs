using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PlatformPayoutGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PlatformPayoutGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPlatformPayoutGrain GetPayoutGrain(Guid orgId, Guid payoutId)
        => _fixture.Cluster.GrainFactory.GetGrain<IPlatformPayoutGrain>(GrainKeys.PlatformPayout(orgId, payoutId));

    private PayoutReceived CreatePayoutReceived(
        Guid? deliveryPlatformId = null,
        Guid? locationId = null,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        decimal grossAmount = 1000m,
        decimal platformFees = 150m,
        decimal netAmount = 850m,
        string currency = "EUR",
        string? payoutReference = null)
    {
        return new PayoutReceived(
            DeliveryPlatformId: deliveryPlatformId ?? Guid.NewGuid(),
            LocationId: locationId ?? Guid.NewGuid(),
            PeriodStart: periodStart ?? DateTime.UtcNow.AddDays(-7),
            PeriodEnd: periodEnd ?? DateTime.UtcNow,
            GrossAmount: grossAmount,
            PlatformFees: platformFees,
            NetAmount: netAmount,
            Currency: currency,
            PayoutReference: payoutReference);
    }

    // ============================================================================
    // ReceiveAsync Tests
    // ============================================================================

    [Fact]
    public async Task ReceiveAsync_ShouldCreatePayout()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived();

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PayoutId.Should().Be(payoutId);
        result.DeliveryPlatformId.Should().Be(payoutReceived.DeliveryPlatformId);
        result.LocationId.Should().Be(payoutReceived.LocationId);
        result.Status.Should().Be(PayoutStatus.Pending);
        result.GrossAmount.Should().Be(payoutReceived.GrossAmount);
        result.PlatformFees.Should().Be(payoutReceived.PlatformFees);
        result.NetAmount.Should().Be(payoutReceived.NetAmount);
        result.Currency.Should().Be(payoutReceived.Currency);
        result.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReceiveAsync_ShouldStorePayoutPeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var periodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc);
        var payoutReceived = CreatePayoutReceived(periodStart: periodStart, periodEnd: periodEnd);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PeriodStart.Should().Be(periodStart);
        result.PeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldStorePayoutReference()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(payoutReference: "UBER-PAY-2024-001");

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PayoutReference.Should().Be("UBER-PAY-2024-001");
    }

    [Fact]
    public async Task ReceiveAsync_WithNullPayoutReference_ShouldStoreNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(payoutReference: null);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PayoutReference.Should().BeNull();
    }

    [Fact]
    public async Task ReceiveAsync_ShouldThrowIfPayoutAlreadyExists()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived();

        await grain.ReceiveAsync(payoutReceived);

        // Act & Assert
        var action = () => grain.ReceiveAsync(payoutReceived);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payout already exists");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldHandleDifferentCurrencies()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(currency: "GBP");

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.Currency.Should().Be("GBP");
    }

    // ============================================================================
    // Fee Calculation and Amount Tests
    // ============================================================================

    [Fact]
    public async Task ReceiveAsync_ShouldTrackFees_TypicalScenario()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(
            grossAmount: 5000m,
            platformFees: 750m,  // 15% platform fee
            netAmount: 4250m);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.GrossAmount.Should().Be(5000m);
        result.PlatformFees.Should().Be(750m);
        result.NetAmount.Should().Be(4250m);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldHandleZeroPlatformFees()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(
            grossAmount: 1000m,
            platformFees: 0m,
            netAmount: 1000m);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.GrossAmount.Should().Be(1000m);
        result.PlatformFees.Should().Be(0m);
        result.NetAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldHandleSmallAmounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(
            grossAmount: 10.50m,
            platformFees: 1.58m,
            netAmount: 8.92m);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.GrossAmount.Should().Be(10.50m);
        result.PlatformFees.Should().Be(1.58m);
        result.NetAmount.Should().Be(8.92m);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldHandleLargeAmounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(
            grossAmount: 1_000_000m,
            platformFees: 150_000m,
            netAmount: 850_000m);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.GrossAmount.Should().Be(1_000_000m);
        result.PlatformFees.Should().Be(150_000m);
        result.NetAmount.Should().Be(850_000m);
    }

    // ============================================================================
    // Status Transition Tests
    // ============================================================================

    [Fact]
    public async Task SetProcessingAsync_ShouldTransitionFromPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());

        // Act
        await grain.SetProcessingAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Processing);
    }

    [Fact]
    public async Task CompleteAsync_ShouldTransitionFromProcessing()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());
        await grain.SetProcessingAsync();
        var processedAt = DateTime.UtcNow;

        // Act
        await grain.CompleteAsync(processedAt);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Completed);
        snapshot.ProcessedAt.Should().BeCloseTo(processedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CompleteAsync_ShouldTransitionDirectlyFromPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());
        var processedAt = DateTime.UtcNow;

        // Act
        await grain.CompleteAsync(processedAt);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Completed);
        snapshot.ProcessedAt.Should().BeCloseTo(processedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FailAsync_ShouldTransitionFromProcessing()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());
        await grain.SetProcessingAsync();

        // Act
        await grain.FailAsync("Bank rejected transfer");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
    }

    [Fact]
    public async Task FailAsync_ShouldTransitionDirectlyFromPending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());

        // Act
        await grain.FailAsync("Invalid account details");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
    }

    [Fact]
    public async Task FullLifecycle_PendingToProcessingToCompleted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var deliveryPlatformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var payoutReceived = CreatePayoutReceived(
            deliveryPlatformId: deliveryPlatformId,
            locationId: locationId,
            grossAmount: 2500m,
            platformFees: 375m,
            netAmount: 2125m,
            currency: "USD",
            payoutReference: "DRO-2024-Q1-001");

        // Act - Step 1: Receive payout
        var initial = await grain.ReceiveAsync(payoutReceived);
        initial.Status.Should().Be(PayoutStatus.Pending);

        // Act - Step 2: Start processing
        await grain.SetProcessingAsync();
        var processing = await grain.GetSnapshotAsync();
        processing.Status.Should().Be(PayoutStatus.Processing);

        // Act - Step 3: Complete
        var processedAt = DateTime.UtcNow;
        await grain.CompleteAsync(processedAt);
        var completed = await grain.GetSnapshotAsync();

        // Assert final state
        completed.Status.Should().Be(PayoutStatus.Completed);
        completed.ProcessedAt.Should().BeCloseTo(processedAt, TimeSpan.FromSeconds(1));
        completed.GrossAmount.Should().Be(2500m);
        completed.PlatformFees.Should().Be(375m);
        completed.NetAmount.Should().Be(2125m);
        completed.PayoutReference.Should().Be("DRO-2024-Q1-001");
    }

    [Fact]
    public async Task FullLifecycle_PendingToProcessingToFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());

        // Act - Step 1: Start processing
        await grain.SetProcessingAsync();
        var processing = await grain.GetSnapshotAsync();
        processing.Status.Should().Be(PayoutStatus.Processing);

        // Act - Step 2: Fail
        await grain.FailAsync("Insufficient funds in platform account");
        var failed = await grain.GetSnapshotAsync();

        // Assert final state
        failed.Status.Should().Be(PayoutStatus.Failed);
        failed.ProcessedAt.Should().BeNull();
    }

    // ============================================================================
    // GetSnapshotAsync Tests
    // ============================================================================

    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var deliveryPlatformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var periodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        await grain.ReceiveAsync(CreatePayoutReceived(
            deliveryPlatformId: deliveryPlatformId,
            locationId: locationId,
            periodStart: periodStart,
            periodEnd: periodEnd,
            grossAmount: 3000m,
            platformFees: 450m,
            netAmount: 2550m,
            currency: "EUR",
            payoutReference: "REF-123"));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.PayoutId.Should().Be(payoutId);
        snapshot.DeliveryPlatformId.Should().Be(deliveryPlatformId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.PeriodStart.Should().Be(periodStart);
        snapshot.PeriodEnd.Should().Be(periodEnd);
        snapshot.GrossAmount.Should().Be(3000m);
        snapshot.PlatformFees.Should().Be(450m);
        snapshot.NetAmount.Should().Be(2550m);
        snapshot.Currency.Should().Be("EUR");
        snapshot.Status.Should().Be(PayoutStatus.Pending);
        snapshot.PayoutReference.Should().Be("REF-123");
        snapshot.ProcessedAt.Should().BeNull();
    }

    // ============================================================================
    // Operations on Uninitialized Grain Tests
    // ============================================================================

    [Fact]
    public async Task SetProcessingAsync_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        // Act & Assert
        var action = () => grain.SetProcessingAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");
    }

    [Fact]
    public async Task CompleteAsync_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        // Act & Assert
        var action = () => grain.CompleteAsync(DateTime.UtcNow);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");
    }

    [Fact]
    public async Task FailAsync_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        // Act & Assert
        var action = () => grain.FailAsync("Some reason");
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");
    }

    [Fact]
    public async Task GetSnapshotAsync_OnUninitializedGrain_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        // Act & Assert
        var action = () => grain.GetSnapshotAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Platform payout grain not initialized");
    }

    // ============================================================================
    // Multiple Payouts Tests (Isolation)
    // ============================================================================

    [Fact]
    public async Task MultiplePayouts_ShouldBeIsolated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId1 = Guid.NewGuid();
        var payoutId2 = Guid.NewGuid();
        var grain1 = GetPayoutGrain(orgId, payoutId1);
        var grain2 = GetPayoutGrain(orgId, payoutId2);

        await grain1.ReceiveAsync(CreatePayoutReceived(grossAmount: 1000m));
        await grain2.ReceiveAsync(CreatePayoutReceived(grossAmount: 2000m));

        // Act
        await grain1.CompleteAsync(DateTime.UtcNow);

        // Assert - grain1 should be completed
        var snapshot1 = await grain1.GetSnapshotAsync();
        snapshot1.Status.Should().Be(PayoutStatus.Completed);
        snapshot1.GrossAmount.Should().Be(1000m);

        // Assert - grain2 should still be pending
        var snapshot2 = await grain2.GetSnapshotAsync();
        snapshot2.Status.Should().Be(PayoutStatus.Pending);
        snapshot2.GrossAmount.Should().Be(2000m);
    }

    [Fact]
    public async Task PayoutsAcrossOrganizations_ShouldBeIsolated()
    {
        // Arrange
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();
        var payoutId = Guid.NewGuid(); // Same payout ID in different orgs
        var grain1 = GetPayoutGrain(org1, payoutId);
        var grain2 = GetPayoutGrain(org2, payoutId);

        await grain1.ReceiveAsync(CreatePayoutReceived(grossAmount: 500m, currency: "EUR"));
        await grain2.ReceiveAsync(CreatePayoutReceived(grossAmount: 800m, currency: "GBP"));

        // Act & Assert - Both should have their own independent state
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();

        snapshot1.GrossAmount.Should().Be(500m);
        snapshot1.Currency.Should().Be("EUR");

        snapshot2.GrossAmount.Should().Be(800m);
        snapshot2.Currency.Should().Be("GBP");
    }

    // ============================================================================
    // Edge Cases and Special Scenarios
    // ============================================================================

    [Fact]
    public async Task ReceiveAsync_WithSamePeriodDates_ShouldWork()
    {
        // Arrange - Some platforms may report single-day payouts
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var sameDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var payoutReceived = CreatePayoutReceived(
            periodStart: sameDate,
            periodEnd: sameDate);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PeriodStart.Should().Be(sameDate);
        result.PeriodEnd.Should().Be(sameDate);
    }

    [Fact]
    public async Task ReceiveAsync_WithEmptyPayoutReference_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(payoutReference: "");

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PayoutReference.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteAsync_ShouldSetProcessedAtCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());
        var specificTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        await grain.CompleteAsync(specificTime);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ProcessedAt.Should().Be(specificTime);
    }

    [Fact]
    public async Task FailAsync_ShouldNotSetProcessedAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());

        // Act
        await grain.FailAsync("Transfer failed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
        snapshot.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetProcessingAsync_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        await grain.ReceiveAsync(CreatePayoutReceived());

        // Act - Call SetProcessingAsync multiple times
        await grain.SetProcessingAsync();
        await grain.SetProcessingAsync();

        // Assert - Should still be in Processing status without error
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Processing);
    }

    [Fact]
    public async Task ReceiveAsync_WithHighPrecisionDecimals_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var payoutReceived = CreatePayoutReceived(
            grossAmount: 1234.5678m,
            platformFees: 185.18m,
            netAmount: 1049.3878m);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.GrossAmount.Should().Be(1234.5678m);
        result.PlatformFees.Should().Be(185.18m);
        result.NetAmount.Should().Be(1049.3878m);
    }

    [Fact]
    public async Task ReceiveAsync_WithLongPayoutReference_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);
        var longReference = "UBER-EATS-PAYOUT-2024-Q1-WEEK-15-RESTAURANT-12345-BATCH-67890-TRANSFER-ABC123XYZ";
        var payoutReceived = CreatePayoutReceived(payoutReference: longReference);

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PayoutReference.Should().Be(longReference);
    }

    // ============================================================================
    // Platform Integration Scenarios
    // ============================================================================

    [Fact]
    public async Task Scenario_UberEatsWeeklyPayout()
    {
        // Arrange - Simulate a typical UberEats weekly payout
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var uberPlatformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        var weekStart = new DateTime(2024, 1, 8, 0, 0, 0, DateTimeKind.Utc);
        var weekEnd = new DateTime(2024, 1, 14, 23, 59, 59, DateTimeKind.Utc);

        var payoutReceived = CreatePayoutReceived(
            deliveryPlatformId: uberPlatformId,
            locationId: locationId,
            periodStart: weekStart,
            periodEnd: weekEnd,
            grossAmount: 4500m,
            platformFees: 1350m,  // 30% platform fee
            netAmount: 3150m,
            currency: "USD",
            payoutReference: "UBER-W02-2024-LOC123");

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);
        await grain.SetProcessingAsync();
        await grain.CompleteAsync(DateTime.UtcNow.AddDays(2)); // Typical 2-day settlement

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Completed);
        snapshot.GrossAmount.Should().Be(4500m);
        snapshot.PlatformFees.Should().Be(1350m);
        snapshot.NetAmount.Should().Be(3150m);
        snapshot.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Scenario_DeliverooMonthlyPayout()
    {
        // Arrange - Simulate a Deliveroo monthly payout
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var deliverooPlatformId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        var monthStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var payoutReceived = CreatePayoutReceived(
            deliveryPlatformId: deliverooPlatformId,
            locationId: locationId,
            periodStart: monthStart,
            periodEnd: monthEnd,
            grossAmount: 15000m,
            platformFees: 3000m,  // 20% platform fee
            netAmount: 12000m,
            currency: "GBP",
            payoutReference: "DLV-JAN-2024");

        // Act
        var result = await grain.ReceiveAsync(payoutReceived);

        // Assert
        result.PeriodStart.Should().Be(monthStart);
        result.PeriodEnd.Should().Be(monthEnd);
        result.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task Scenario_FailedPayoutDueToBankIssue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();
        var grain = GetPayoutGrain(orgId, payoutId);

        await grain.ReceiveAsync(CreatePayoutReceived(
            grossAmount: 2000m,
            platformFees: 400m,
            netAmount: 1600m));

        // Act - Simulate bank rejection
        await grain.SetProcessingAsync();
        await grain.FailAsync("Bank account validation failed - invalid IBAN");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(PayoutStatus.Failed);
        snapshot.ProcessedAt.Should().BeNull();
        snapshot.GrossAmount.Should().Be(2000m); // Original amounts preserved
        snapshot.NetAmount.Should().Be(1600m);
    }
}
