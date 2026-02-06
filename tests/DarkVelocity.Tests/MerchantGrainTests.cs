using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for Merchant grain covering:
/// - Merchant creation and updates
/// - API key management (create, revoke, roll)
/// - Charges/payouts enablement
/// - Key validation
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MerchantGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MerchantGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMerchantGrain GetMerchantGrain(Guid orgId, Guid merchantId)
        => _fixture.Cluster.GrainFactory.GetGrain<IMerchantGrain>($"{orgId}:merchant:{merchantId}");

    // =========================================================================
    // Merchant Creation Tests
    // =========================================================================

    // Given: a complete merchant registration with business details, address, and statement descriptor
    // When: the merchant account is created
    // Then: the merchant is active with charges enabled, payouts disabled, and all details persisted
    [Fact]
    public async Task CreateMerchant_ValidCommand_ShouldCreateMerchant()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        var command = new CreateMerchantCommand(
            Name: "John's Restaurant",
            Email: "john@restaurant.com",
            BusinessName: "John's BBQ LLC",
            BusinessType: "restaurant",
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: "JOHNS BBQ",
            AddressLine1: "123 Main St",
            AddressLine2: "Suite 100",
            City: "Austin",
            State: "TX",
            PostalCode: "78701",
            Metadata: new Dictionary<string, string> { ["segment"] = "smb" });

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.MerchantId.Should().Be(merchantId);
        snapshot.Name.Should().Be("John's Restaurant");
        snapshot.Email.Should().Be("john@restaurant.com");
        snapshot.BusinessName.Should().Be("John's BBQ LLC");
        snapshot.BusinessType.Should().Be("restaurant");
        snapshot.Country.Should().Be("US");
        snapshot.DefaultCurrency.Should().Be("USD");
        snapshot.StatementDescriptor.Should().Be("JOHNS BBQ");
        snapshot.Status.Should().Be("active");
        snapshot.ChargesEnabled.Should().BeTrue();
        snapshot.PayoutsEnabled.Should().BeFalse(); // Payouts start disabled
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    // Given: a minimal merchant registration with only required fields
    // When: the merchant account is created
    // Then: the merchant is created with active status and default settings
    [Fact]
    public async Task CreateMerchant_MinimalCommand_ShouldCreateMerchant()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        var command = new CreateMerchantCommand(
            Name: "Basic Merchant",
            Email: "basic@example.com",
            BusinessName: "Basic LLC",
            BusinessType: null,
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            State: null,
            PostalCode: null,
            Metadata: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Name.Should().Be("Basic Merchant");
        snapshot.Status.Should().Be("active");
    }

    // Given: a merchant account that already exists
    // When: a second creation is attempted for the same merchant
    // Then: the operation is rejected to prevent duplicate merchant accounts
    [Fact]
    public async Task CreateMerchant_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "First", "first@example.com", "First LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var act = () => grain.CreateAsync(new CreateMerchantCommand(
            "Second", "second@example.com", "Second LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // Given: a merchant grain that has never been registered
    // When: the existence check is performed
    // Then: the merchant does not exist
    [Fact]
    public async Task ExistsAsync_NewMerchant_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    // Given: a merchant account that has been created
    // When: the existence check is performed
    // Then: the merchant exists
    [Fact]
    public async Task ExistsAsync_CreatedMerchant_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    // =========================================================================
    // Merchant Update Tests
    // =========================================================================

    // Given: an existing merchant account with an original name
    // When: the merchant name is updated
    // Then: the merchant snapshot reflects the new name and records the update timestamp
    [Fact]
    public async Task UpdateMerchant_ChangeName_ShouldUpdateName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Original Name", "original@example.com", "Original LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateMerchantCommand(
            Name: "Updated Name",
            BusinessName: null,
            BusinessType: null,
            StatementDescriptor: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            State: null,
            PostalCode: null,
            Metadata: null));

        // Assert
        snapshot.Name.Should().Be("Updated Name");
        snapshot.UpdatedAt.Should().NotBeNull();
    }

    // Given: an existing merchant with a registered address
    // When: the merchant address is updated to a new location
    // Then: all address fields reflect the new location details
    [Fact]
    public async Task UpdateMerchant_ChangeAddress_ShouldUpdateAddress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, "123 Old St", null, "Old City", "TX", "00000", null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateMerchantCommand(
            Name: null,
            BusinessName: null,
            BusinessType: null,
            StatementDescriptor: null,
            AddressLine1: "456 New Ave",
            AddressLine2: "Floor 2",
            City: "New City",
            State: "CA",
            PostalCode: "90210",
            Metadata: null));

        // Assert
        snapshot.AddressLine1.Should().Be("456 New Ave");
        snapshot.AddressLine2.Should().Be("Floor 2");
        snapshot.City.Should().Be("New City");
        snapshot.State.Should().Be("CA");
        snapshot.PostalCode.Should().Be("90210");
    }

    // Given: a merchant grain that has never been created
    // When: an update is attempted on the non-existent merchant
    // Then: the operation fails because the merchant was not found
    [Fact]
    public async Task UpdateMerchant_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        // Act
        var act = () => grain.UpdateAsync(new UpdateMerchantCommand(
            "Name", null, null, null, null, null, null, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // =========================================================================
    // API Key Management Tests - Creation
    // =========================================================================

    // Given: an active merchant account
    // When: a secret test API key is created
    // Then: the key has the sk_test_ prefix and is active in test mode
    [Fact]
    public async Task CreateApiKey_SecretTestKey_ShouldHaveCorrectPrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Test API Key", "secret", isLive: false, expiresAt: null);

        // Assert
        apiKey.Name.Should().Be("Test API Key");
        apiKey.KeyType.Should().Be("secret");
        apiKey.KeyPrefix.Should().Be("sk_test_");
        apiKey.KeyHint.Should().StartWith("sk_test_");
        apiKey.IsLive.Should().BeFalse();
        apiKey.IsActive.Should().BeTrue();
    }

    // Given: an active merchant account
    // When: a secret live API key is created
    // Then: the key has the sk_live_ prefix and is active in live mode
    [Fact]
    public async Task CreateApiKey_SecretLiveKey_ShouldHaveCorrectPrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Live API Key", "secret", isLive: true, expiresAt: null);

        // Assert
        apiKey.KeyPrefix.Should().Be("sk_live_");
        apiKey.KeyHint.Should().StartWith("sk_live_");
        apiKey.IsLive.Should().BeTrue();
    }

    // Given: an active merchant account
    // When: a publishable test API key is created
    // Then: the key has the pk_test_ prefix for client-side use in test mode
    [Fact]
    public async Task CreateApiKey_PublishableTestKey_ShouldHaveCorrectPrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Publishable Key", "publishable", isLive: false, expiresAt: null);

        // Assert
        apiKey.KeyPrefix.Should().Be("pk_test_");
        apiKey.KeyHint.Should().StartWith("pk_test_");
    }

    [Fact]
    public async Task CreateApiKey_PublishableLiveKey_ShouldHaveCorrectPrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Live Publishable", "publishable", isLive: true, expiresAt: null);

        // Assert
        apiKey.KeyPrefix.Should().Be("pk_live_");
        apiKey.KeyHint.Should().StartWith("pk_live_");
    }

    [Fact]
    public async Task CreateApiKey_WithExpiration_ShouldSetExpiresAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Expiring Key", "secret", isLive: false, expiresAt: expiresAt);

        // Assert
        apiKey.ExpiresAt.Should().BeCloseTo(expiresAt, precision: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateApiKey_ExceedsLimit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Create max keys (20)
        for (int i = 0; i < 20; i++)
        {
            await grain.CreateApiKeyAsync($"Key {i}", "secret", false, null);
        }

        // Act
        var act = () => grain.CreateApiKeyAsync("Key 21", "secret", false, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum*API keys*");
    }

    // =========================================================================
    // API Key Management Tests - Retrieval
    // =========================================================================

    [Fact]
    public async Task GetApiKeys_ShouldReturnAllActiveKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        await grain.CreateApiKeyAsync("Key 1", "secret", false, null);
        await grain.CreateApiKeyAsync("Key 2", "publishable", false, null);
        await grain.CreateApiKeyAsync("Key 3", "secret", true, null);

        // Act
        var keys = await grain.GetApiKeysAsync();

        // Assert
        keys.Should().HaveCount(3);
        keys.Should().Contain(k => k.Name == "Key 1");
        keys.Should().Contain(k => k.Name == "Key 2");
        keys.Should().Contain(k => k.Name == "Key 3");
    }

    [Fact]
    public async Task GetApiKeys_ShouldNotReturnRevokedKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var key1 = await grain.CreateApiKeyAsync("Active Key", "secret", false, null);
        var key2 = await grain.CreateApiKeyAsync("Revoked Key", "secret", false, null);

        await grain.RevokeApiKeyAsync(key2.KeyId);

        // Act
        var keys = await grain.GetApiKeysAsync();

        // Assert
        keys.Should().HaveCount(1);
        keys.Should().Contain(k => k.Name == "Active Key");
        keys.Should().NotContain(k => k.Name == "Revoked Key");
    }

    // =========================================================================
    // API Key Management Tests - Revocation
    // =========================================================================

    [Fact]
    public async Task RevokeApiKey_ExistingKey_ShouldDeactivate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var apiKey = await grain.CreateApiKeyAsync("Key to Revoke", "secret", false, null);

        // Act
        await grain.RevokeApiKeyAsync(apiKey.KeyId);

        // Assert
        var keys = await grain.GetApiKeysAsync();
        keys.Should().NotContain(k => k.KeyId == apiKey.KeyId);
    }

    [Fact]
    public async Task RevokeApiKey_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        var act = () => grain.RevokeApiKeyAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RevokeApiKey_AlreadyRevoked_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var apiKey = await grain.CreateApiKeyAsync("Key", "secret", false, null);
        await grain.RevokeApiKeyAsync(apiKey.KeyId);

        // Act
        var act = () => grain.RevokeApiKeyAsync(apiKey.KeyId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // =========================================================================
    // API Key Management Tests - Rolling
    // =========================================================================

    [Fact]
    public async Task RollApiKey_ShouldRevokeOldAndCreateNew()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var originalKey = await grain.CreateApiKeyAsync("Original Key", "secret", false, null);

        // Act
        var newKey = await grain.RollApiKeyAsync(originalKey.KeyId, expiresAt: null);

        // Assert
        newKey.KeyId.Should().NotBe(originalKey.KeyId);
        newKey.Name.Should().Be(originalKey.Name);
        newKey.KeyType.Should().Be(originalKey.KeyType);
        newKey.KeyPrefix.Should().Be(originalKey.KeyPrefix);
        newKey.IsLive.Should().Be(originalKey.IsLive);
        newKey.IsActive.Should().BeTrue();

        // Old key should be gone
        var keys = await grain.GetApiKeysAsync();
        keys.Should().NotContain(k => k.KeyId == originalKey.KeyId);
        keys.Should().Contain(k => k.KeyId == newKey.KeyId);
    }

    [Fact]
    public async Task RollApiKey_WithNewExpiration_ShouldSetExpiration()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        var originalKey = await grain.CreateApiKeyAsync("Key", "secret", false, null);
        var newExpiration = DateTime.UtcNow.AddDays(90);

        // Act
        var newKey = await grain.RollApiKeyAsync(originalKey.KeyId, expiresAt: newExpiration);

        // Assert
        newKey.ExpiresAt.Should().BeCloseTo(newExpiration, precision: TimeSpan.FromSeconds(1));
    }

    // =========================================================================
    // Charges/Payouts Enablement Tests
    // =========================================================================

    [Fact]
    public async Task EnableCharges_ShouldSetChargesEnabledTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        await grain.DisableChargesAsync();

        // Act
        await grain.EnableChargesAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ChargesEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisableCharges_ShouldSetChargesEnabledFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        await grain.DisableChargesAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ChargesEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task EnablePayouts_ShouldSetPayoutsEnabledTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        // Act
        await grain.EnablePayoutsAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PayoutsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisablePayouts_ShouldSetPayoutsEnabledFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = GetMerchantGrain(orgId, merchantId);

        await grain.CreateAsync(new CreateMerchantCommand(
            "Merchant", "merchant@example.com", "Merchant LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        await grain.EnablePayoutsAsync();

        // Act
        await grain.DisablePayoutsAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PayoutsEnabled.Should().BeFalse();
    }

    // =========================================================================
    // Multi-Tenancy Tests
    // =========================================================================

    [Fact]
    public async Task Merchants_DifferentOrgs_ShouldBeIsolated()
    {
        // Arrange
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var merchantId = Guid.NewGuid(); // Same merchant ID

        var grain1 = GetMerchantGrain(org1Id, merchantId);
        var grain2 = GetMerchantGrain(org2Id, merchantId);

        // Act
        await grain1.CreateAsync(new CreateMerchantCommand(
            "Org1 Merchant", "org1@example.com", "Org1 LLC", null, "US", "USD",
            null, null, null, null, null, null, null));

        await grain2.CreateAsync(new CreateMerchantCommand(
            "Org2 Merchant", "org2@example.com", "Org2 LLC", null, "GB", "GBP",
            null, null, null, null, null, null, null));

        // Assert
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();

        snapshot1.Name.Should().Be("Org1 Merchant");
        snapshot1.DefaultCurrency.Should().Be("USD");

        snapshot2.Name.Should().Be("Org2 Merchant");
        snapshot2.DefaultCurrency.Should().Be("GBP");
    }
}
