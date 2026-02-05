using DarkVelocity.Host.PaymentProcessors;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for PaymentProcessorRetryHelper covering circuit breaker behavior,
/// retry logic with exponential backoff, and error classification.
/// </summary>
[Trait("Category", "Unit")]
public class PaymentProcessorRetryHelperTests : IDisposable
{
    private readonly string _testProcessorKey;

    public PaymentProcessorRetryHelperTests()
    {
        // Use unique processor key per test to avoid cross-test pollution
        _testProcessorKey = $"test_processor_{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        // Clean up circuit breaker state after each test
        PaymentProcessorRetryHelper.ResetCircuit(_testProcessorKey);
    }

    // =========================================================================
    // Circuit Breaker Tests - Closed State
    // =========================================================================

    [Fact]
    public void CircuitBreaker_InitialState_ShouldBeClosed()
    {
        // Arrange - fresh processor key
        var processorKey = $"fresh_processor_{Guid.NewGuid():N}";

        // Act
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(processorKey);
        var state = PaymentProcessorRetryHelper.GetCircuitState(processorKey);

        // Assert
        isOpen.Should().BeFalse("circuit should start closed");
        state.Should().BeNull("no state should exist for new processor");
    }

    [Fact]
    public void CircuitBreaker_AfterOneFailure_ShouldRemainClosed()
    {
        // Arrange & Act
        PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);

        // Assert
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey);
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);

        isOpen.Should().BeFalse("circuit should remain closed after single failure");
        state.Should().NotBeNull();
        state!.FailureCount.Should().Be(1);
        state.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void CircuitBreaker_AfterFourFailures_ShouldRemainClosed()
    {
        // Arrange & Act
        for (int i = 0; i < 4; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }

        // Assert
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey);
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);

        isOpen.Should().BeFalse("circuit should remain closed with 4 failures");
        state!.FailureCount.Should().Be(4);
        state.State.Should().Be(CircuitState.Closed);
    }

    // =========================================================================
    // Circuit Breaker Tests - Open State
    // =========================================================================

    [Fact]
    public void CircuitBreaker_AfterFiveFailures_ShouldOpen()
    {
        // Arrange & Act
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }

        // Assert
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey);
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);

        isOpen.Should().BeTrue("circuit should open after 5 failures");
        state!.State.Should().Be(CircuitState.Open);
        state.ResetTime.Should().BeAfter(DateTime.UtcNow, "reset time should be in the future");
    }

    [Fact]
    public void CircuitBreaker_WhenOpen_ShouldRejectOperations()
    {
        // Arrange - Force circuit open
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }

        // Act
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey);

        // Assert
        isOpen.Should().BeTrue("open circuit should reject operations");
    }

    [Fact]
    public void CircuitBreaker_OpenWithCustomDuration_ShouldRespectDuration()
    {
        // Arrange
        var customDuration = TimeSpan.FromMinutes(2);

        // Act - Record failures with custom open duration on last one
        for (int i = 0; i < 4; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }
        PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey, customDuration);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        var expectedResetTime = DateTime.UtcNow.Add(customDuration);

        state!.State.Should().Be(CircuitState.Open);
        state.ResetTime.Should().BeCloseTo(expectedResetTime, precision: TimeSpan.FromSeconds(2));
    }

    // =========================================================================
    // Circuit Breaker Tests - Half-Open State
    // =========================================================================

    [Fact]
    public void CircuitBreaker_AfterResetTime_ShouldTransitionToHalfOpen()
    {
        // Arrange - Force circuit open with very short duration
        var shortDuration = TimeSpan.FromMilliseconds(1);
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey, shortDuration);
        }

        // Wait for reset time to pass
        Thread.Sleep(10);

        // Act - Check circuit (should transition to half-open)
        var isOpen = PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey);

        // Assert
        isOpen.Should().BeFalse("circuit should allow one request in half-open state");
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        state!.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void CircuitBreaker_HalfOpen_SuccessClosesCiruit()
    {
        // Arrange - Force circuit to half-open
        var shortDuration = TimeSpan.FromMilliseconds(1);
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey, shortDuration);
        }
        Thread.Sleep(10);
        PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey); // Trigger half-open

        // Act
        PaymentProcessorRetryHelper.RecordSuccess(_testProcessorKey);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        state!.State.Should().Be(CircuitState.Closed);
        state.FailureCount.Should().Be(0, "failure count should reset on success");
    }

    [Fact]
    public void CircuitBreaker_HalfOpen_FailureReopensCircuit()
    {
        // Arrange - Force circuit to half-open
        var shortDuration = TimeSpan.FromMilliseconds(1);
        for (int i = 0; i < 5; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey, shortDuration);
        }
        Thread.Sleep(10);
        PaymentProcessorRetryHelper.IsCircuitOpen(_testProcessorKey); // Trigger half-open

        // Act
        PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        state!.State.Should().Be(CircuitState.Open);
    }

    // =========================================================================
    // Circuit Breaker Tests - Recovery
    // =========================================================================

    [Fact]
    public void CircuitBreaker_Closed_SuccessResetsFailureCount()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }

        // Act
        PaymentProcessorRetryHelper.RecordSuccess(_testProcessorKey);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        state!.FailureCount.Should().Be(0, "success should reset failure count in closed state");
        state.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void CircuitBreaker_Reset_ClearsAllState()
    {
        // Arrange - Create some state
        for (int i = 0; i < 3; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(_testProcessorKey);
        }

        // Act
        PaymentProcessorRetryHelper.ResetCircuit(_testProcessorKey);

        // Assert
        var state = PaymentProcessorRetryHelper.GetCircuitState(_testProcessorKey);
        state.Should().BeNull("reset should clear all state");
    }

    // =========================================================================
    // Retry Logic Tests - Exponential Backoff
    // =========================================================================

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 2.0)]
    [InlineData(2, 4.0)]
    [InlineData(3, 8.0)]
    [InlineData(4, 16.0)]
    public void GetRetryDelay_ShouldFollowExponentialBackoff(int attempt, double expectedSeconds)
    {
        // Act
        var delay = PaymentProcessorRetryHelper.GetRetryDelay(attempt);

        // Assert - Allow for jitter (+/- 25%)
        var expectedDelay = TimeSpan.FromSeconds(expectedSeconds);
        var minDelay = expectedDelay * 0.75;
        var maxDelay = expectedDelay * 1.25;

        delay.Should().BeGreaterThanOrEqualTo(minDelay);
        delay.Should().BeLessThanOrEqualTo(maxDelay);
    }

    [Fact]
    public void GetRetryDelay_BeyondMaxAttempts_ShouldCapAtMaxDelay()
    {
        // Act
        var delayAt5 = PaymentProcessorRetryHelper.GetRetryDelay(5);
        var delayAt10 = PaymentProcessorRetryHelper.GetRetryDelay(10);

        // Assert - Both should be capped at 16 seconds (the max)
        var maxDelay = TimeSpan.FromSeconds(16) * 1.25; // Including jitter

        delayAt5.Should().BeLessThanOrEqualTo(maxDelay);
        delayAt10.Should().BeLessThanOrEqualTo(maxDelay);
    }

    [Fact]
    public void GetRetryDelay_NegativeAttempt_ShouldTreatAsZero()
    {
        // Act
        var delay = PaymentProcessorRetryHelper.GetRetryDelay(-1);

        // Assert - Should be ~1 second (first backoff)
        var expectedDelay = TimeSpan.FromSeconds(1);
        delay.Should().BeCloseTo(expectedDelay, precision: TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void GetRetryDelay_MultipleCallsSameAttempt_ShouldHaveJitter()
    {
        // Act - Make multiple calls
        var delays = Enumerable.Range(0, 20)
            .Select(_ => PaymentProcessorRetryHelper.GetRetryDelay(2))
            .ToList();

        // Assert - Not all delays should be identical (jitter should vary them)
        var distinctDelays = delays.Select(d => d.TotalMilliseconds).Distinct().Count();
        distinctDelays.Should().BeGreaterThan(1, "jitter should create variation");
    }

    // =========================================================================
    // Retry Logic Tests - Should Retry Decision
    // =========================================================================

    [Fact]
    public void ShouldRetry_FirstAttempt_ShouldReturnTrue()
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(0, null);

        // Assert
        shouldRetry.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ShouldRetry_UnderMaxRetries_ShouldReturnTrue(int attempt)
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(attempt, null);

        // Assert
        shouldRetry.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_AtMaxRetries_ShouldReturnFalse()
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(5, null);

        // Assert
        shouldRetry.Should().BeFalse("max retries reached");
    }

    [Fact]
    public void ShouldRetry_BeyondMaxRetries_ShouldReturnFalse()
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(10, null);

        // Assert
        shouldRetry.Should().BeFalse();
    }

    // =========================================================================
    // Error Classification Tests - Terminal Errors (Non-Retryable)
    // =========================================================================

    [Theory]
    [InlineData("card_declined")]
    [InlineData("insufficient_funds")]
    [InlineData("expired_card")]
    [InlineData("incorrect_cvc")]
    [InlineData("incorrect_number")]
    [InlineData("invalid_card_type")]
    [InlineData("stolen_card")]
    [InlineData("fraudulent")]
    [InlineData("card_not_supported")]
    [InlineData("currency_not_supported")]
    [InlineData("duplicate_transaction")]
    [InlineData("invalid_amount")]
    [InlineData("invalid_cvc")]
    [InlineData("invalid_expiry_month")]
    [InlineData("invalid_expiry_year")]
    [InlineData("invalid_number")]
    [InlineData("postal_code_invalid")]
    public void IsTerminalError_StripeTerminalErrors_ShouldReturnTrue(string errorCode)
    {
        // Act
        var isTerminal = PaymentProcessorRetryHelper.IsTerminalError(errorCode);

        // Assert
        isTerminal.Should().BeTrue($"{errorCode} should be terminal");
    }

    [Theory]
    [InlineData("Refused")]
    [InlineData("Not enough balance")]
    [InlineData("Blocked Card")]
    [InlineData("Expired Card")]
    [InlineData("Invalid Card Number")]
    [InlineData("Invalid Pin")]
    [InlineData("Pin tries exceeded")]
    [InlineData("Fraud")]
    [InlineData("Shopper Cancelled")]
    [InlineData("CVC Declined")]
    [InlineData("Restricted Card")]
    [InlineData("Revocation Of Auth")]
    [InlineData("Declined Non Generic")]
    public void IsTerminalError_AdyenTerminalErrors_ShouldReturnTrue(string errorCode)
    {
        // Act
        var isTerminal = PaymentProcessorRetryHelper.IsTerminalError(errorCode);

        // Assert
        isTerminal.Should().BeTrue($"{errorCode} should be terminal");
    }

    [Fact]
    public void ShouldRetry_TerminalError_ShouldReturnFalse()
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(0, "card_declined");

        // Assert
        shouldRetry.Should().BeFalse("terminal errors should not be retried");
    }

    // =========================================================================
    // Error Classification Tests - Retryable Errors
    // =========================================================================

    [Theory]
    [InlineData("processing_error")]
    [InlineData("rate_limit")]
    [InlineData("api_connection_error")]
    [InlineData("api_error")]
    [InlineData("timeout")]
    [InlineData("lock_timeout")]
    [InlineData("Acquirer Error")]
    [InlineData("Issuer Unavailable")]
    public void IsRetryableError_TransientErrors_ShouldReturnTrue(string errorCode)
    {
        // Act
        var isRetryable = PaymentProcessorRetryHelper.IsRetryableError(errorCode);

        // Assert
        isRetryable.Should().BeTrue($"{errorCode} should be retryable");
    }

    [Theory]
    [InlineData("processing_error")]
    [InlineData("rate_limit")]
    [InlineData("api_connection_error")]
    [InlineData("api_error")]
    [InlineData("timeout")]
    [InlineData("Acquirer Error")]
    [InlineData("Issuer Unavailable")]
    public void IsTerminalError_RetryableErrors_ShouldReturnFalse(string errorCode)
    {
        // Act
        var isTerminal = PaymentProcessorRetryHelper.IsTerminalError(errorCode);

        // Assert
        isTerminal.Should().BeFalse($"{errorCode} should NOT be terminal");
    }

    [Fact]
    public void ShouldRetry_RetryableError_ShouldReturnTrue()
    {
        // Act
        var shouldRetry = PaymentProcessorRetryHelper.ShouldRetry(0, "processing_error");

        // Assert
        shouldRetry.Should().BeTrue("retryable errors should be retried");
    }

    // =========================================================================
    // Error Classification Tests - Special Cases
    // =========================================================================

    [Fact]
    public void IsTerminalError_AuthenticationRequired_ShouldReturnFalse()
    {
        // authentication_required means 3DS is needed, which can be completed
        var isTerminal = PaymentProcessorRetryHelper.IsTerminalError("authentication_required");

        isTerminal.Should().BeFalse("authentication_required can be resolved with 3DS");
    }

    [Fact]
    public void IsRetryableError_IdempotencyError_ShouldReturnFalse()
    {
        // Idempotency errors should not be retried with same key
        var isRetryable = PaymentProcessorRetryHelper.IsRetryableError("idempotency_error");

        isRetryable.Should().BeFalse("idempotency errors require new key, not retry");
    }

    [Fact]
    public void IsTerminalError_NullOrEmpty_ShouldReturnFalse()
    {
        // Act & Assert
        PaymentProcessorRetryHelper.IsTerminalError(null).Should().BeFalse();
        PaymentProcessorRetryHelper.IsTerminalError("").Should().BeFalse();
    }

    [Fact]
    public void IsRetryableError_NullOrEmpty_ShouldReturnFalse()
    {
        // Act & Assert
        PaymentProcessorRetryHelper.IsRetryableError(null).Should().BeFalse();
        PaymentProcessorRetryHelper.IsRetryableError("").Should().BeFalse();
    }

    [Fact]
    public void IsTerminalError_UnknownError_ShouldReturnFalse()
    {
        // Unknown errors default to allowing retry
        var isTerminal = PaymentProcessorRetryHelper.IsTerminalError("unknown_error_xyz");

        isTerminal.Should().BeFalse("unknown errors should allow retry attempt");
    }

    // =========================================================================
    // GetNextRetryTime Tests
    // =========================================================================

    [Fact]
    public void GetNextRetryTime_ShouldReturnFutureTime()
    {
        // Arrange
        var beforeCall = DateTime.UtcNow;

        // Act
        var nextRetryTime = PaymentProcessorRetryHelper.GetNextRetryTime(0);

        // Assert
        nextRetryTime.Should().BeAfter(beforeCall);
    }

    [Fact]
    public void GetNextRetryTime_HigherAttempts_ShouldBeFurtherInFuture()
    {
        // Arrange
        var attempt0Time = PaymentProcessorRetryHelper.GetNextRetryTime(0);
        var attempt4Time = PaymentProcessorRetryHelper.GetNextRetryTime(4);

        // Assert - Attempt 4 should be significantly further in the future
        var attempt0Delay = attempt0Time - DateTime.UtcNow;
        var attempt4Delay = attempt4Time - DateTime.UtcNow;

        attempt4Delay.Should().BeGreaterThan(attempt0Delay);
    }

    // =========================================================================
    // Concurrent Access Tests
    // =========================================================================

    [Fact]
    public void CircuitBreaker_ConcurrentFailures_ShouldHandleSafely()
    {
        // Arrange
        var concurrentKey = $"concurrent_{Guid.NewGuid():N}";
        var tasks = new List<Task>();

        // Act - Record failures from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => PaymentProcessorRetryHelper.RecordFailure(concurrentKey)));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Should not throw and circuit should be open
        var state = PaymentProcessorRetryHelper.GetCircuitState(concurrentKey);
        state.Should().NotBeNull();
        state!.State.Should().Be(CircuitState.Open, "circuit should be open after many failures");

        // Cleanup
        PaymentProcessorRetryHelper.ResetCircuit(concurrentKey);
    }

    [Fact]
    public void CircuitBreaker_ConcurrentSuccessAndFailure_ShouldNotDeadlock()
    {
        // Arrange
        var concurrentKey = $"concurrent_mixed_{Guid.NewGuid():N}";

        // Pre-populate some failures
        for (int i = 0; i < 3; i++)
        {
            PaymentProcessorRetryHelper.RecordFailure(concurrentKey);
        }

        var tasks = new List<Task>();

        // Act - Mix of successes and failures from multiple threads
        for (int i = 0; i < 20; i++)
        {
            if (i % 2 == 0)
            {
                tasks.Add(Task.Run(() => PaymentProcessorRetryHelper.RecordFailure(concurrentKey)));
            }
            else
            {
                tasks.Add(Task.Run(() => PaymentProcessorRetryHelper.RecordSuccess(concurrentKey)));
            }
        }

        // Should complete without deadlock
        var completed = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

        // Assert
        completed.Should().BeTrue("operations should complete without deadlock");

        // Cleanup
        PaymentProcessorRetryHelper.ResetCircuit(concurrentKey);
    }
}
