using DarkVelocity.Host.PaymentProcessors;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class IdempotencyKeyGrainTests
{
    private readonly TestClusterFixture _fixture;

    public IdempotencyKeyGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IIdempotencyKeyGrain GetIdempotencyGrain(Guid orgId)
        => _fixture.Cluster.GrainFactory.GetGrain<IIdempotencyKeyGrain>($"{orgId}:idempotency");

    // =========================================================================
    // Key Generation Tests
    // =========================================================================

    [Fact]
    public async Task GenerateKeyAsync_ShouldReturnUniqueKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();

        // Act
        var key1 = await grain.GenerateKeyAsync("authorize", entityId);
        var key2 = await grain.GenerateKeyAsync("authorize", entityId);

        // Assert
        key1.Should().NotBeNullOrEmpty();
        key2.Should().NotBeNullOrEmpty();
        key1.Should().NotBe(key2); // Each call generates a unique key
        key1.Should().StartWith("idem_authorize_");
    }

    [Fact]
    public async Task GenerateKeyAsync_WithCustomTtl_ShouldSetExpiry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();

        // Act
        var key = await grain.GenerateKeyAsync("capture", entityId, TimeSpan.FromMinutes(30));
        var status = await grain.GetKeyStatusAsync(key);

        // Assert
        status.Should().NotBeNull();
        status!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    // =========================================================================
    // Key Check Tests
    // =========================================================================

    [Fact]
    public async Task CheckKeyAsync_ForExistingUnusedKey_ShouldReturnCorrectState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("refund", entityId);

        // Act
        var result = await grain.CheckKeyAsync(key);

        // Assert
        result.Exists.Should().BeTrue();
        result.AlreadyUsed.Should().BeFalse();
        result.PreviousSuccess.Should().BeNull();
    }

    [Fact]
    public async Task CheckKeyAsync_ForNonExistentKey_ShouldReturnNotExists()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);

        // Act
        var result = await grain.CheckKeyAsync("nonexistent_key_12345");

        // Assert
        result.Exists.Should().BeFalse();
        result.AlreadyUsed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckKeyAsync_ForUsedKey_ShouldReturnUsedState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("void", entityId);

        // Mark as used
        await grain.MarkKeyUsedAsync(key, successful: true, resultHash: "abc123");

        // Act
        var result = await grain.CheckKeyAsync(key);

        // Assert
        result.Exists.Should().BeTrue();
        result.AlreadyUsed.Should().BeTrue();
        result.PreviousSuccess.Should().BeTrue();
        result.PreviousResultHash.Should().Be("abc123");
    }

    // =========================================================================
    // Mark Key Used Tests
    // =========================================================================

    [Fact]
    public async Task MarkKeyUsedAsync_WithSuccess_ShouldUpdateState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("authorize", entityId);

        // Act
        await grain.MarkKeyUsedAsync(key, successful: true, resultHash: "result_hash_xyz");

        // Assert
        var status = await grain.GetKeyStatusAsync(key);
        status.Should().NotBeNull();
        status!.Used.Should().BeTrue();
        status.UsedAt.Should().NotBeNull();
        status.Successful.Should().BeTrue();
        status.ResultHash.Should().Be("result_hash_xyz");
    }

    [Fact]
    public async Task MarkKeyUsedAsync_WithFailure_ShouldUpdateState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("capture", entityId);

        // Act
        await grain.MarkKeyUsedAsync(key, successful: false);

        // Assert
        var status = await grain.GetKeyStatusAsync(key);
        status.Should().NotBeNull();
        status!.Used.Should().BeTrue();
        status.Successful.Should().BeFalse();
    }

    [Fact]
    public async Task MarkKeyUsedAsync_ForNonExistentKey_ShouldCreateAndMarkUsed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var newKey = $"idem_test_{Guid.NewGuid():N}";

        // Act
        await grain.MarkKeyUsedAsync(newKey, successful: true);

        // Assert
        var status = await grain.GetKeyStatusAsync(newKey);
        status.Should().NotBeNull();
        status!.Used.Should().BeTrue();
    }

    // =========================================================================
    // TryAcquire Tests
    // =========================================================================

    [Fact]
    public async Task TryAcquireAsync_ForNewKey_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = $"idem_acquire_{Guid.NewGuid():N}";

        // Act
        var result = await grain.TryAcquireAsync(key, "authorize", entityId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_ForUsedSuccessfulKey_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("authorize", entityId);

        // Mark as successfully used
        await grain.MarkKeyUsedAsync(key, successful: true);

        // Act
        var result = await grain.TryAcquireAsync(key, "authorize", entityId);

        // Assert
        result.Should().BeFalse(); // Should not allow re-execution of successful operation
    }

    [Fact]
    public async Task TryAcquireAsync_ForUsedFailedKey_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("authorize", entityId);

        // Mark as failed
        await grain.MarkKeyUsedAsync(key, successful: false);

        // Act
        var result = await grain.TryAcquireAsync(key, "authorize", entityId);

        // Assert
        result.Should().BeTrue(); // Should allow retry of failed operation
    }

    // =========================================================================
    // Cleanup Tests
    // =========================================================================

    [Fact]
    public async Task CleanupExpiredKeysAsync_ShouldRemoveExpiredKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();

        // Generate a key with very short TTL (1 millisecond - already expired)
        var key = await grain.GenerateKeyAsync("test", entityId, TimeSpan.FromMilliseconds(1));

        // Wait for expiry
        await Task.Delay(10);

        // Act
        var removedCount = await grain.CleanupExpiredKeysAsync();

        // Assert
        removedCount.Should().BeGreaterThanOrEqualTo(1);

        var status = await grain.GetKeyStatusAsync(key);
        status.Should().BeNull(); // Key should be removed
    }

    // =========================================================================
    // Key Status Tests
    // =========================================================================

    [Fact]
    public async Task GetKeyStatusAsync_ForExistingKey_ShouldReturnFullStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);
        var entityId = Guid.NewGuid();
        var key = await grain.GenerateKeyAsync("refund", entityId, TimeSpan.FromHours(2));

        // Act
        var status = await grain.GetKeyStatusAsync(key);

        // Assert
        status.Should().NotBeNull();
        status!.Key.Should().Be(key);
        status.Operation.Should().Be("refund");
        status.RelatedEntityId.Should().Be(entityId);
        status.Used.Should().BeFalse();
        status.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetKeyStatusAsync_ForNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetIdempotencyGrain(orgId);

        // Act
        var status = await grain.GetKeyStatusAsync("nonexistent_key");

        // Assert
        status.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class PaymentProcessorRetryHelperTests
{
    // =========================================================================
    // Retry Delay Tests
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void GetRetryDelay_ShouldReturnIncreasingDelays(int attemptNumber)
    {
        // Act
        var delay = PaymentProcessorRetryHelper.GetRetryDelay(attemptNumber);

        // Assert
        delay.Should().BeGreaterThan(TimeSpan.Zero);

        // Base delays: 1s, 2s, 4s, 8s, 16s
        var expectedBase = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
        var tolerance = expectedBase.TotalMilliseconds * 0.25 + 100; // 25% jitter + small buffer

        delay.TotalMilliseconds.Should().BeGreaterThan(expectedBase.TotalMilliseconds * 0.75);
        delay.TotalMilliseconds.Should().BeLessThan(expectedBase.TotalMilliseconds + tolerance);
    }

    [Fact]
    public void GetRetryDelay_WithNegativeAttempt_ShouldTreatAsZero()
    {
        // Act
        var delay = PaymentProcessorRetryHelper.GetRetryDelay(-5);

        // Assert
        delay.TotalSeconds.Should().BeGreaterThan(0);
        delay.TotalSeconds.Should().BeLessThan(2); // Should be around 1 second with jitter
    }

    [Fact]
    public void GetRetryDelay_WithHighAttemptNumber_ShouldCapAtMaxDelay()
    {
        // Act
        var delay = PaymentProcessorRetryHelper.GetRetryDelay(100);

        // Assert
        delay.TotalSeconds.Should().BeLessThan(25); // Max is 16s + 25% jitter
    }

    // =========================================================================
    // Should Retry Tests
    // =========================================================================

    [Fact]
    public void ShouldRetry_WithinMaxRetries_ShouldReturnTrue()
    {
        // Act
        var result = PaymentProcessorRetryHelper.ShouldRetry(2, "processing_error");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_AtMaxRetries_ShouldReturnFalse()
    {
        // Act
        var result = PaymentProcessorRetryHelper.ShouldRetry(5, "processing_error");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithTerminalError_ShouldReturnFalse()
    {
        // Act
        var result = PaymentProcessorRetryHelper.ShouldRetry(1, "card_declined");

        // Assert
        result.Should().BeFalse();
    }

    // =========================================================================
    // Terminal Error Tests
    // =========================================================================

    [Theory]
    [InlineData("card_declined", true)]
    [InlineData("insufficient_funds", true)]
    [InlineData("expired_card", true)]
    [InlineData("incorrect_cvc", true)]
    [InlineData("fraudulent", true)]
    [InlineData("Refused", true)]
    [InlineData("Blocked Card", true)]
    public void IsTerminalError_WithTerminalErrors_ShouldReturnTrue(string errorCode, bool expected)
    {
        // Act
        var result = PaymentProcessorRetryHelper.IsTerminalError(errorCode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("processing_error", false)]
    [InlineData("rate_limit", false)]
    [InlineData("api_connection_error", false)]
    [InlineData("timeout", false)]
    [InlineData("Acquirer Error", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsTerminalError_WithRetryableErrors_ShouldReturnFalse(string? errorCode, bool expected)
    {
        // Act
        var result = PaymentProcessorRetryHelper.IsTerminalError(errorCode);

        // Assert
        result.Should().Be(expected);
    }

    // =========================================================================
    // Retryable Error Tests
    // =========================================================================

    [Theory]
    [InlineData("processing_error", true)]
    [InlineData("rate_limit", true)]
    [InlineData("api_connection_error", true)]
    [InlineData("timeout", true)]
    [InlineData("Acquirer Error", true)]
    [InlineData("Issuer Unavailable", true)]
    public void IsRetryableError_WithRetryableErrors_ShouldReturnTrue(string errorCode, bool expected)
    {
        // Act
        var result = PaymentProcessorRetryHelper.IsRetryableError(errorCode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("card_declined", false)]
    [InlineData("insufficient_funds", false)]
    [InlineData("idempotency_error", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsRetryableError_WithNonRetryableErrors_ShouldReturnFalse(string? errorCode, bool expected)
    {
        // Act
        var result = PaymentProcessorRetryHelper.IsRetryableError(errorCode);

        // Assert
        result.Should().Be(expected);
    }

    // =========================================================================
    // Circuit Breaker Tests
    // =========================================================================

    [Fact]
    public void CircuitBreaker_InitialState_ShouldBeClosed()
    {
        // Arrange
        var processorKey = $"test_processor_{Guid.NewGuid()}";

        // Act
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(processorKey);

        // Assert
        isOpen.Should().BeFalse();
    }

    [Fact]
    public void CircuitBreaker_AfterMultipleFailures_ShouldOpen()
    {
        // Arrange
        var processorKey = $"test_processor_{Guid.NewGuid()}";

        // Act - Record 5+ failures to open circuit
        for (int i = 0; i < 6; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(processorKey, TimeSpan.FromMinutes(1));
        }

        // Assert
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(processorKey);
        isOpen.Should().BeTrue();

        // Cleanup
        PaymentProcessorRetryHelper.ResetCircuit(processorKey);
    }

    [Fact]
    public void CircuitBreaker_AfterSuccess_ShouldResetFailureCount()
    {
        // Arrange
        var processorKey = $"test_processor_{Guid.NewGuid()}";

        // Record a few failures (but not enough to open)
        PaymentProcessorRetryHelper.RecordFailure(processorKey);
        PaymentProcessorRetryHelper.RecordFailure(processorKey);

        // Act - Record success
        PaymentProcessorRetryHelper.RecordSuccess(processorKey);

        // Assert - More failures shouldn't immediately open circuit
        PaymentProcessorRetryHelper.RecordFailure(processorKey);
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(processorKey);
        isOpen.Should().BeFalse();

        // Cleanup
        PaymentProcessorRetryHelper.ResetCircuit(processorKey);
    }

    [Fact]
    public void CircuitBreaker_GetState_ShouldReturnCurrentState()
    {
        // Arrange
        var processorKey = $"test_processor_{Guid.NewGuid()}";

        // Record some failures
        PaymentProcessorRetryHelper.RecordFailure(processorKey);
        PaymentProcessorRetryHelper.RecordFailure(processorKey);

        // Act
        var state = PaymentProcessorRetryHelper.GetCircuitState(processorKey);

        // Assert
        state.Should().NotBeNull();
        state!.FailureCount.Should().Be(2);
        state.State.Should().Be(CircuitState.Closed);
        state.LastFailureTime.Should().NotBeNull();

        // Cleanup
        PaymentProcessorRetryHelper.ResetCircuit(processorKey);
    }

    [Fact]
    public void CircuitBreaker_Reset_ShouldClearState()
    {
        // Arrange
        var processorKey = $"test_processor_{Guid.NewGuid()}";
        PaymentProcessorRetryHelper.RecordFailure(processorKey);

        // Act
        PaymentProcessorRetryHelper.ResetCircuit(processorKey);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(processorKey);
        state.Should().BeNull();
    }

    // =========================================================================
    // Next Retry Time Tests
    // =========================================================================

    [Fact]
    public void GetNextRetryTime_ShouldReturnFutureTime()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var nextRetry = PaymentProcessorRetryHelper.GetNextRetryTime(0);

        // Assert
        nextRetry.Should().BeAfter(now);
        nextRetry.Should().BeCloseTo(now.AddSeconds(1), TimeSpan.FromSeconds(1)); // ~1 second with jitter
    }
}

[Trait("Category", "Unit")]
public class IdempotencyKeyExtensionsTests
{
    [Fact]
    public void ComputeResultHash_ShouldReturnConsistentHash()
    {
        // Arrange
        var result = new { id = 123, status = "success" };

        // Act
        var hash1 = IdempotencyKeyExtensions.ComputeResultHash(result);
        var hash2 = IdempotencyKeyExtensions.ComputeResultHash(result);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(16);
    }

    [Fact]
    public void ComputeResultHash_DifferentObjects_ShouldReturnDifferentHashes()
    {
        // Arrange
        var result1 = new { id = 123 };
        var result2 = new { id = 456 };

        // Act
        var hash1 = IdempotencyKeyExtensions.ComputeResultHash(result1);
        var hash2 = IdempotencyKeyExtensions.ComputeResultHash(result2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeResultHash_NullObject_ShouldReturnNullString()
    {
        // Act
        var hash = IdempotencyKeyExtensions.ComputeResultHash<object?>(null);

        // Assert
        hash.Should().Be("null");
    }

    [Fact]
    public void IdempotencyKeyGrainKey_ShouldReturnCorrectFormat()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Act
        var key = IdempotencyKeyExtensions.IdempotencyKeyGrainKey(orgId);

        // Assert
        key.Should().Be($"{orgId}:idempotency");
    }
}
