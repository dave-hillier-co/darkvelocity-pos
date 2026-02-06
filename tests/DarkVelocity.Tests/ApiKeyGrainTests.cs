using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class ApiKeyGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ApiKeyGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a new secret API key in test mode with admin role
    // When: the API key is created for an organization
    // Then: the key has an "sk_test_" prefix, an assigned ID, and a creation timestamp
    [Fact]
    public async Task CreateAsync_ShouldCreateApiKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var command = new CreateApiKeyCommand(
            OrganizationId: orgId,
            UserId: userId,
            Name: "Test API Key",
            Description: "A test key",
            Type: ApiKeyType.Secret,
            IsTestMode: true,
            Scopes: null,
            CustomClaims: null,
            Roles: ["admin"],
            AllowedSiteIds: null,
            AllowedIpRanges: null,
            RateLimitPerMinute: null,
            ExpiresAt: null);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(keyId);
        result.ApiKey.Should().StartWith("sk_test_");
        result.KeyPrefix.Should().Contain("sk_test_");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a new publishable API key in live mode
    // When: the API key is created
    // Then: the key has a "pk_live_" prefix
    [Fact]
    public async Task CreateAsync_PublishableKey_ShouldHavePkPrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var command = new CreateApiKeyCommand(
            OrganizationId: orgId,
            UserId: userId,
            Name: "Publishable Key",
            Description: null,
            Type: ApiKeyType.Publishable,
            IsTestMode: false,
            Scopes: null,
            CustomClaims: null,
            Roles: null,
            AllowedSiteIds: null,
            AllowedIpRanges: null,
            RateLimitPerMinute: null,
            ExpiresAt: null);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.ApiKey.Should().StartWith("pk_live_");
    }

    // Given: a secret API key in test mode with admin and manager roles and a rate limit
    // When: the key state is retrieved
    // Then: all configuration including ID, organization, name, type, roles, and rate limit are returned
    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, ["admin", "manager"], null, null, 100, DateTime.UtcNow.AddDays(30)));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(keyId);
        state.OrganizationId.Should().Be(orgId);
        state.UserId.Should().Be(userId);
        state.Name.Should().Be("Test Key");
        state.Type.Should().Be(ApiKeyType.Secret);
        state.IsTestMode.Should().BeTrue();
        state.Status.Should().Be(ApiKeyStatus.Active);
        state.Roles.Should().Contain("admin");
        state.Roles.Should().Contain("manager");
        state.RateLimitPerMinute.Should().Be(100);
    }

    // Given: an active secret API key in test mode
    // When: the key is validated with the correct key value
    // Then: validation succeeds and returns the key's identity (ID, user, org, type, and test mode)
    [Fact]
    public async Task ValidateAsync_ValidKey_ShouldReturnSuccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var createResult = await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        var result = await grain.ValidateAsync(createResult.ApiKey, "127.0.0.1");

        // Assert
        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be(keyId);
        result.UserId.Should().Be(userId);
        result.OrganizationId.Should().Be(orgId);
        result.Type.Should().Be(ApiKeyType.Secret);
        result.IsTestMode.Should().BeTrue();
    }

    // Given: an active API key
    // When: the key is validated with an incorrect key value
    // Then: validation fails with "Invalid API key" error
    [Fact]
    public async Task ValidateAsync_InvalidKey_ShouldReturnFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        var result = await grain.ValidateAsync("sk_test_invalid_key_here", "127.0.0.1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Invalid API key");
    }

    // Given: an API key that has been revoked
    // When: the key is validated with the correct key value
    // Then: validation fails with "API key has been revoked" error
    [Fact]
    public async Task ValidateAsync_RevokedKey_ShouldReturnFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var createResult = await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        await grain.RevokeAsync(userId, "Testing revocation");

        // Act
        var result = await grain.ValidateAsync(createResult.ApiKey, "127.0.0.1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("API key has been revoked");
    }

    // Given: an API key created with an already-passed expiration date
    // When: the key is validated
    // Then: validation fails with "API key has expired" error
    [Fact]
    public async Task ValidateAsync_ExpiredKey_ShouldReturnFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var createResult = await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null,
            ExpiresAt: DateTime.UtcNow.AddSeconds(-1))); // Already expired

        // Act
        var result = await grain.ValidateAsync(createResult.ApiKey, "127.0.0.1");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("API key has expired");
    }

    // Given: an existing API key with original name
    // When: the key is updated with a new name, description, custom claims, roles, and rate limit
    // Then: all updated fields are persisted and the version increments
    [Fact]
    public async Task UpdateAsync_ShouldUpdateKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Original Name", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        await grain.UpdateAsync(new UpdateApiKeyCommand(
            Name: "Updated Name",
            Description: "New description",
            Scopes: null,
            CustomClaims: new Dictionary<string, string> { ["env"] = "production" },
            Roles: ["employee"],
            AllowedSiteIds: null,
            AllowedIpRanges: null,
            RateLimitPerMinute: 500,
            ExpiresAt: null));

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Name");
        state.Description.Should().Be("New description");
        state.CustomClaims.Should().ContainKey("env");
        state.CustomClaims["env"].Should().Be("production");
        state.Roles.Should().Contain("employee");
        state.RateLimitPerMinute.Should().Be(500);
        state.Version.Should().Be(2);
    }

    // Given: an active API key
    // When: the key is revoked with a reason
    // Then: the key status changes to Revoked with the reason, revoker, and timestamp recorded
    [Fact]
    public async Task RevokeAsync_ShouldRevokeKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        await grain.RevokeAsync(userId, "Security concern");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ApiKeyStatus.Revoked);
        state.RevocationReason.Should().Be("Security concern");
        state.RevokedBy.Should().Be(userId);
        state.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: an active API key with no prior usage
    // When: the key is used three times from different IP addresses
    // Then: the usage count is 3, the last IP is recorded, and the last-used timestamp is set
    [Fact]
    public async Task RecordUsageAsync_ShouldTrackUsage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        await grain.RecordUsageAsync("192.168.1.100");
        await grain.RecordUsageAsync("192.168.1.101");
        await grain.RecordUsageAsync("192.168.1.102");

        // Assert
        var state = await grain.GetStateAsync();
        state.UsageCount.Should().Be(3);
        state.LastUsedFromIp.Should().Be("192.168.1.102");
        state.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a secret API key in live mode with orders and customers scopes and an expiry date
    // When: the key summary is retrieved
    // Then: the summary includes ID, name, description, type, test mode, status, and scope resource names
    [Fact]
    public async Task GetSummaryAsync_ShouldReturnSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var scopes = new List<ApiKeyScope>
        {
            new() { Resource = "orders", Actions = ["read", "write"] },
            new() { Resource = "customers", Actions = ["read"] }
        };

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Summary Test Key", "For testing", ApiKeyType.Secret, false,
            scopes, null, null, null, null, null, DateTime.UtcNow.AddDays(7)));

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.Id.Should().Be(keyId);
        summary.Name.Should().Be("Summary Test Key");
        summary.Description.Should().Be("For testing");
        summary.Type.Should().Be(ApiKeyType.Secret);
        summary.IsTestMode.Should().BeFalse();
        summary.Status.Should().Be(ApiKeyStatus.Active);
        summary.Scopes.Should().Contain("orders");
        summary.Scopes.Should().Contain("customers");
    }

    // Given: a new API key with three custom claims (tenant tier, feature flags, max requests)
    // When: the key is created
    // Then: all three custom claims are stored with their correct values
    [Fact]
    public async Task CreateAsync_WithCustomClaims_ShouldStoreClaims()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var customClaims = new Dictionary<string, string>
        {
            ["tenant_tier"] = "enterprise",
            ["feature_flags"] = "beta,experimental",
            ["max_requests"] = "10000"
        };

        // Act
        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Custom Claims Key", null, ApiKeyType.Secret, true,
            null, customClaims, null, null, null, null, null));

        // Assert
        var state = await grain.GetStateAsync();
        state.CustomClaims.Should().HaveCount(3);
        state.CustomClaims["tenant_tier"].Should().Be("enterprise");
        state.CustomClaims["feature_flags"].Should().Be("beta,experimental");
        state.CustomClaims["max_requests"].Should().Be("10000");
    }

    // Given: a new API key with scopes for orders (read/write/delete), menu (read), and inventory (read/write)
    // When: the key is created
    // Then: all three scopes are stored with their respective actions
    [Fact]
    public async Task CreateAsync_WithScopes_ShouldStoreScopes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        var scopes = new List<ApiKeyScope>
        {
            new() { Resource = "orders", Actions = ["read", "write", "delete"] },
            new() { Resource = "menu", Actions = ["read"] },
            new() { Resource = "inventory", Actions = ["read", "write"] }
        };

        // Act
        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Scoped Key", null, ApiKeyType.Secret, true,
            scopes, null, null, null, null, null, null));

        // Assert
        var state = await grain.GetStateAsync();
        state.Scopes.Should().HaveCount(3);
        state.Scopes.Should().Contain(s => s.Resource == "orders" && s.Actions.Contains("delete"));
        state.Scopes.Should().Contain(s => s.Resource == "menu" && s.Actions.Count == 1);
    }

    // Given: a new API key restricted to two specific sites
    // When: the key is created
    // Then: both allowed site IDs are stored in the key configuration
    [Fact]
    public async Task CreateAsync_WithSiteRestrictions_ShouldStoreSites()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var siteId1 = Guid.NewGuid();
        var siteId2 = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        // Act
        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Site Restricted Key", null, ApiKeyType.Secret, true,
            null, null, null, [siteId1, siteId2], null, null, null));

        // Assert
        var state = await grain.GetStateAsync();
        state.AllowedSiteIds.Should().HaveCount(2);
        state.AllowedSiteIds.Should().Contain(siteId1);
        state.AllowedSiteIds.Should().Contain(siteId2);
    }

    // Given: an API key grain that has never been created
    // When: checking if the key exists
    // Then: the key does not exist
    [Fact]
    public async Task ExistsAsync_NonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    // Given: an API key that has been created
    // When: checking if the key exists
    // Then: the key exists
    [Fact]
    public async Task ExistsAsync_ExistingKey_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

        await grain.CreateAsync(new CreateApiKeyCommand(
            orgId, userId, "Test Key", null, ApiKeyType.Secret, true,
            null, null, null, null, null, null, null));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
public class ApiKeyRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ApiKeyRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a new API key registry for an organization user
    // When: the registry is initialized
    // Then: the registry exists
    [Fact]
    public async Task InitializeAsync_ShouldInitializeRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(orgId, userId));

        // Act
        await grain.InitializeAsync(orgId, userId);

        // Assert
        var exists = await grain.ExistsAsync();
        exists.Should().BeTrue();
    }

    // Given: an initialized API key registry with no keys
    // When: a key is registered with its hash
    // Then: the key ID appears in the registry's key list
    [Fact]
    public async Task RegisterKeyAsync_ShouldAddKeyToRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(orgId, userId));

        await grain.InitializeAsync(orgId, userId);

        // Act
        await grain.RegisterKeyAsync(keyId, "test_hash_123");

        // Assert
        var keyIds = await grain.GetKeyIdsAsync();
        keyIds.Should().Contain(keyId);
    }

    // Given: an API key registry with a registered key
    // When: the key is unregistered
    // Then: the key ID no longer appears in the registry's key list
    [Fact]
    public async Task UnregisterKeyAsync_ShouldRemoveKeyFromRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(orgId, userId));

        await grain.InitializeAsync(orgId, userId);
        await grain.RegisterKeyAsync(keyId, "test_hash_123");

        // Act
        await grain.UnregisterKeyAsync(keyId, "test_hash_123");

        // Assert
        var keyIds = await grain.GetKeyIdsAsync();
        keyIds.Should().NotContain(keyId);
    }

    // Given: an API key registry with a key registered under a specific hash
    // When: the registry is searched by that hash
    // Then: the correct key ID is returned
    [Fact]
    public async Task FindKeyIdByHashAsync_ShouldFindKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var keyHash = "unique_hash_456";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(orgId, userId));

        await grain.InitializeAsync(orgId, userId);
        await grain.RegisterKeyAsync(keyId, keyHash);

        // Act
        var foundKeyId = await grain.FindKeyIdByHashAsync(keyHash);

        // Assert
        foundKeyId.Should().Be(keyId);
    }

    // Given: an initialized API key registry with no matching hashes
    // When: the registry is searched by a nonexistent hash
    // Then: null is returned
    [Fact]
    public async Task FindKeyIdByHashAsync_NotFound_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyRegistryGrain>(
            GrainKeys.ApiKeyRegistry(orgId, userId));

        await grain.InitializeAsync(orgId, userId);

        // Act
        var foundKeyId = await grain.FindKeyIdByHashAsync("nonexistent_hash");

        // Assert
        foundKeyId.Should().BeNull();
    }
}

[Collection(ClusterCollection.Name)]
public class ApiKeyLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ApiKeyLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a key hash registered in the global API key lookup
    // When: the hash is looked up
    // Then: the correct organization ID and key ID are returned
    [Fact]
    public async Task RegisterAndLookup_ShouldFindKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var keyHash = $"test_hash_{Guid.NewGuid()}";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());

        // Act
        await grain.RegisterAsync(keyHash, orgId, keyId);
        var result = await grain.LookupAsync(keyHash);

        // Assert
        result.Should().NotBeNull();
        result!.Value.OrganizationId.Should().Be(orgId);
        result!.Value.KeyId.Should().Be(keyId);
    }

    // Given: a key hash registered in the global API key lookup
    // When: the hash is unregistered and then looked up
    // Then: the lookup returns null
    [Fact]
    public async Task UnregisterAsync_ShouldRemoveKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var keyHash = $"test_hash_{Guid.NewGuid()}";
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());

        await grain.RegisterAsync(keyHash, orgId, keyId);

        // Act
        await grain.UnregisterAsync(keyHash);
        var result = await grain.LookupAsync(keyHash);

        // Assert
        result.Should().BeNull();
    }

    // Given: the global API key lookup with no matching hashes
    // When: a nonexistent hash is looked up
    // Then: null is returned
    [Fact]
    public async Task LookupAsync_NotFound_ShouldReturnNull()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IApiKeyLookupGrain>(GrainKeys.ApiKeyLookup());

        // Act
        var result = await grain.LookupAsync($"nonexistent_{Guid.NewGuid()}");

        // Assert
        result.Should().BeNull();
    }
}
