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

    // Given: An idempotency grain for a payment authorization operation
    // When: Two keys are generated for the same entity and operation
    // Then: Each key is unique and follows the expected prefix format
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

    // Given: An idempotency grain for a payment capture operation
    // When: A key is generated with a custom 30-minute TTL
    // Then: The key status reflects an expiry time approximately 30 minutes in the future
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

    // Given: A generated idempotency key for a refund operation that has not been used
    // When: The key is checked
    // Then: The key exists, is not marked as used, and has no previous success result
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

    // Given: An idempotency grain with no keys generated
    // When: A nonexistent key is checked
    // Then: The result indicates the key does not exist and has not been used
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

    // Given: A generated idempotency key for a void operation that has been marked as successfully used
    // When: The key is checked
    // Then: The result indicates the key exists, was already used successfully, and includes the result hash
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

    // Given: A generated idempotency key for an authorization operation
    // When: The key is marked as successfully used with a result hash
    // Then: The key status shows it as used, successful, with the result hash and usage timestamp
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

    // Given: A generated idempotency key for a payment capture operation
    // When: The key is marked as used with a failure outcome
    // Then: The key status shows it as used but unsuccessful
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

    // Given: An idempotency grain with no prior keys
    // When: A nonexistent key is directly marked as used
    // Then: The key is created and marked as used in a single operation
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

    // Given: A new idempotency key that has not been registered
    // When: Acquisition is attempted for an authorization operation
    // Then: The acquisition succeeds, reserving the key for use
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

    // Given: An idempotency key that was previously used for a successful authorization
    // When: Acquisition of the same key is attempted again
    // Then: The acquisition fails to prevent duplicate execution of the successful operation
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

    // Given: An idempotency key that was previously used for a failed authorization
    // When: Acquisition of the same key is attempted again
    // Then: The acquisition succeeds to allow retry of the failed operation
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

    // Given: An idempotency key generated with a 1-millisecond TTL that has already expired
    // When: Expired key cleanup is triggered
    // Then: The expired key is removed and no longer retrievable
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

    // Given: A generated idempotency key for a refund operation with a 2-hour TTL
    // When: The key status is retrieved
    // Then: The status includes the key, operation, related entity, unused state, and future expiry
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

    // Given: An idempotency grain with no keys generated
    // When: The status of a nonexistent key is requested
    // Then: Null is returned indicating the key does not exist
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
public class IdempotencyKeyExtensionsTests
{
    // Given: A payment operation result object
    // When: The result hash is computed twice for the same object
    // Then: Both hashes are identical and have the expected fixed length
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

    // Given: Two different payment operation result objects
    // When: The result hash is computed for each
    // Then: The hashes are different, distinguishing the two results
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

    // Given: A null payment operation result
    // When: The result hash is computed
    // Then: The hash returns the string "null"
    [Fact]
    public void ComputeResultHash_NullObject_ShouldReturnNullString()
    {
        // Act
        var hash = IdempotencyKeyExtensions.ComputeResultHash<object?>(null);

        // Assert
        hash.Should().Be("null");
    }

    // Given: An organization ID
    // When: The idempotency grain key is generated
    // Then: The key follows the format "{orgId}:idempotency"
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
