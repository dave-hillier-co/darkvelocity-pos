using System.Security.Cryptography;

namespace DarkVelocity.Host.PaymentProcessors;

/// <summary>
/// Retry helper for payment processor operations.
/// Implements exponential backoff with jitter and circuit breaker pattern.
/// </summary>
public static class PaymentProcessorRetryHelper
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16)
    ];

    // Circuit breaker state (in production, this would be per-processor and persistent)
    private static readonly object CircuitBreakerLock = new();
    private static readonly Dictionary<string, CircuitBreakerState> CircuitBreakers = [];

    /// <summary>
    /// Gets the delay for the next retry attempt with jitter.
    /// </summary>
    public static TimeSpan GetRetryDelay(int attemptNumber)
    {
        if (attemptNumber < 0) attemptNumber = 0;
        if (attemptNumber >= BackoffDelays.Length) attemptNumber = BackoffDelays.Length - 1;

        var baseDelay = BackoffDelays[attemptNumber];
        var jitter = GetJitter(baseDelay);

        return baseDelay + jitter;
    }

    /// <summary>
    /// Calculates jitter (random variation) to prevent thundering herd.
    /// Jitter is +/- 25% of the base delay.
    /// </summary>
    private static TimeSpan GetJitter(TimeSpan baseDelay)
    {
        var jitterRange = baseDelay.TotalMilliseconds * 0.25;
        var jitterMs = (RandomNumberGenerator.GetInt32(0, (int)(jitterRange * 2)) - jitterRange);
        return TimeSpan.FromMilliseconds(jitterMs);
    }

    /// <summary>
    /// Determines if another retry should be attempted.
    /// </summary>
    public static bool ShouldRetry(int currentAttempt, string? errorCode)
    {
        if (currentAttempt >= MaxRetries) return false;

        // Don't retry terminal errors
        if (IsTerminalError(errorCode)) return false;

        return true;
    }

    /// <summary>
    /// Determines if the error is terminal (should not be retried).
    /// </summary>
    public static bool IsTerminalError(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode)) return false;

        // Terminal errors that shouldn't be retried
        return errorCode switch
        {
            // Stripe terminal errors
            "card_declined" => true,
            "insufficient_funds" => true,
            "expired_card" => true,
            "incorrect_cvc" => true,
            "incorrect_number" => true,
            "invalid_card_type" => true,
            "stolen_card" => true,
            "fraudulent" => true,
            "authentication_required" => false, // Can retry after 3DS
            "card_not_supported" => true,
            "currency_not_supported" => true,
            "duplicate_transaction" => true,
            "invalid_amount" => true,
            "invalid_cvc" => true,
            "invalid_expiry_month" => true,
            "invalid_expiry_year" => true,
            "invalid_number" => true,
            "postal_code_invalid" => true,

            // Adyen terminal errors
            "Refused" => true,
            "Not enough balance" => true,
            "Blocked Card" => true,
            "Expired Card" => true,
            "Invalid Card Number" => true,
            "Invalid Pin" => true,
            "Pin tries exceeded" => true,
            "Fraud" => true,
            "Shopper Cancelled" => true,
            "CVC Declined" => true,
            "Restricted Card" => true,
            "Revocation Of Auth" => true,
            "Declined Non Generic" => true,

            // Network/temporary errors that CAN be retried
            "processing_error" => false,
            "rate_limit" => false,
            "api_connection_error" => false,
            "api_error" => false,
            "timeout" => false,
            "Acquirer Error" => false,
            "Issuer Unavailable" => false,

            _ => false
        };
    }

    /// <summary>
    /// Determines if the error is retryable (transient).
    /// </summary>
    public static bool IsRetryableError(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode)) return false;

        return errorCode switch
        {
            "processing_error" => true,
            "rate_limit" => true,
            "api_connection_error" => true,
            "api_error" => true,
            "timeout" => true,
            "lock_timeout" => true,
            "idempotency_error" => false, // Don't retry with same key
            "Acquirer Error" => true,
            "Issuer Unavailable" => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates the next retry time based on attempt number.
    /// </summary>
    public static DateTime GetNextRetryTime(int attemptNumber)
    {
        return DateTime.UtcNow.Add(GetRetryDelay(attemptNumber));
    }

    // ========================================================================
    // Circuit Breaker Pattern
    // ========================================================================

    /// <summary>
    /// Checks if the circuit breaker allows the operation to proceed.
    /// </summary>
    public static bool IsCircuitOpen(string processorKey)
    {
        lock (CircuitBreakerLock)
        {
            if (!CircuitBreakers.TryGetValue(processorKey, out var state))
            {
                return false;
            }

            // Check if we're in half-open state (allow one request through)
            if (state.State == CircuitState.Open && DateTime.UtcNow >= state.ResetTime)
            {
                state.State = CircuitState.HalfOpen;
                return false;
            }

            return state.State == CircuitState.Open;
        }
    }

    /// <summary>
    /// Records a successful operation, potentially closing the circuit.
    /// </summary>
    public static void RecordSuccess(string processorKey)
    {
        lock (CircuitBreakerLock)
        {
            if (!CircuitBreakers.TryGetValue(processorKey, out var state))
            {
                return;
            }

            if (state.State == CircuitState.HalfOpen)
            {
                state.State = CircuitState.Closed;
                state.FailureCount = 0;
            }
            else if (state.State == CircuitState.Closed)
            {
                state.FailureCount = 0;
            }
        }
    }

    /// <summary>
    /// Records a failed operation, potentially opening the circuit.
    /// </summary>
    public static void RecordFailure(string processorKey, TimeSpan? openDuration = null)
    {
        lock (CircuitBreakerLock)
        {
            if (!CircuitBreakers.TryGetValue(processorKey, out var state))
            {
                state = new CircuitBreakerState();
                CircuitBreakers[processorKey] = state;
            }

            state.FailureCount++;
            state.LastFailureTime = DateTime.UtcNow;

            if (state.State == CircuitState.HalfOpen)
            {
                // Failed while testing - reopen circuit
                state.State = CircuitState.Open;
                state.ResetTime = DateTime.UtcNow.Add(openDuration ?? TimeSpan.FromSeconds(30));
            }
            else if (state.FailureCount >= 5)
            {
                // Too many failures - open circuit
                state.State = CircuitState.Open;
                state.ResetTime = DateTime.UtcNow.Add(openDuration ?? TimeSpan.FromSeconds(30));
            }
        }
    }

    /// <summary>
    /// Resets the circuit breaker for a processor.
    /// </summary>
    public static void ResetCircuit(string processorKey)
    {
        lock (CircuitBreakerLock)
        {
            CircuitBreakers.Remove(processorKey);
        }
    }

    /// <summary>
    /// Gets the current circuit breaker state for monitoring.
    /// </summary>
    public static CircuitBreakerInfo? GetCircuitState(string processorKey)
    {
        lock (CircuitBreakerLock)
        {
            if (!CircuitBreakers.TryGetValue(processorKey, out var state))
            {
                return null;
            }

            return new CircuitBreakerInfo(
                state.State,
                state.FailureCount,
                state.LastFailureTime,
                state.ResetTime);
        }
    }

    private class CircuitBreakerState
    {
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int FailureCount { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime ResetTime { get; set; }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public record CircuitBreakerInfo(
    CircuitState State,
    int FailureCount,
    DateTime? LastFailureTime,
    DateTime? ResetTime);

/// <summary>
/// Result of a retry operation.
/// </summary>
public record RetryResult<T>(
    bool Success,
    T? Result,
    int TotalAttempts,
    string? LastErrorCode,
    string? LastErrorMessage,
    bool CircuitBreakerTripped);
