using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentGatewayGrainTests
{
    private readonly TestCluster _cluster;

    public PaymentGatewayGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Merchant Grain Tests
    // ============================================================================

    [Fact]
    public async Task MerchantGrain_Create_CreatesMerchantSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IMerchantGrain>(
            GrainKeys.Merchant(orgId, merchantId));

        var command = new CreateMerchantCommand(
            Name: "Test Restaurant",
            Email: "test@restaurant.com",
            BusinessName: "Test Restaurant LLC",
            BusinessType: "restaurant",
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: "TEST RESTAURANT",
            AddressLine1: "123 Main St",
            AddressLine2: null,
            City: "New York",
            State: "NY",
            PostalCode: "10001",
            Metadata: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.MerchantId.Should().Be(merchantId);
        snapshot.Name.Should().Be("Test Restaurant");
        snapshot.Email.Should().Be("test@restaurant.com");
        snapshot.ChargesEnabled.Should().BeTrue();
        snapshot.PayoutsEnabled.Should().BeFalse();
        snapshot.Status.Should().Be("active");
    }

    [Fact]
    public async Task MerchantGrain_CreateApiKey_CreatesKeySuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IMerchantGrain>(
            GrainKeys.Merchant(orgId, merchantId));

        await grain.CreateAsync(new CreateMerchantCommand(
            Name: "Test Merchant",
            Email: "test@merchant.com",
            BusinessName: "Test LLC",
            BusinessType: null,
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            State: null,
            PostalCode: null,
            Metadata: null));

        // Act
        var apiKey = await grain.CreateApiKeyAsync("Test Key", "secret", false, null);

        // Assert
        apiKey.Name.Should().Be("Test Key");
        apiKey.KeyType.Should().Be("secret");
        apiKey.IsLive.Should().BeFalse();
        apiKey.IsActive.Should().BeTrue();
        apiKey.KeyPrefix.Should().Be("sk_test_");
    }

    [Fact]
    public async Task MerchantGrain_RevokeApiKey_DeactivatesKey()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IMerchantGrain>(
            GrainKeys.Merchant(orgId, merchantId));

        await grain.CreateAsync(new CreateMerchantCommand(
            Name: "Test Merchant",
            Email: "test@merchant.com",
            BusinessName: "Test LLC",
            BusinessType: null,
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            State: null,
            PostalCode: null,
            Metadata: null));

        var apiKey = await grain.CreateApiKeyAsync("Key to Revoke", "secret", false, null);

        // Act
        await grain.RevokeApiKeyAsync(apiKey.KeyId);
        var keys = await grain.GetApiKeysAsync();

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public async Task MerchantGrain_EnablePayouts_UpdatesFlag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IMerchantGrain>(
            GrainKeys.Merchant(orgId, merchantId));

        await grain.CreateAsync(new CreateMerchantCommand(
            Name: "Test Merchant",
            Email: "test@merchant.com",
            BusinessName: "Test LLC",
            BusinessType: null,
            Country: "US",
            DefaultCurrency: "USD",
            StatementDescriptor: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            State: null,
            PostalCode: null,
            Metadata: null));

        // Act
        await grain.EnablePayoutsAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.PayoutsEnabled.Should().BeTrue();
    }

    // ============================================================================
    // Terminal Grain Tests
    // ============================================================================

    [Fact]
    public async Task TerminalGrain_Register_CreatesTerminalSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITerminalGrain>(
            GrainKeys.Terminal(orgId, terminalId));

        var command = new RegisterTerminalCommand(
            LocationId: Guid.NewGuid(),
            Label: "Front Counter Terminal",
            DeviceType: "WisePOS E",
            SerialNumber: "WSP123456",
            Metadata: null);

        // Act
        var snapshot = await grain.RegisterAsync(command);

        // Assert
        snapshot.TerminalId.Should().Be(terminalId);
        snapshot.Label.Should().Be("Front Counter Terminal");
        snapshot.DeviceType.Should().Be("WisePOS E");
        snapshot.Status.Should().Be(TerminalStatus.Active);
    }

    [Fact]
    public async Task TerminalGrain_Heartbeat_UpdatesLastSeenAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITerminalGrain>(
            GrainKeys.Terminal(orgId, terminalId));

        await grain.RegisterAsync(new RegisterTerminalCommand(
            LocationId: Guid.NewGuid(),
            Label: "Test Terminal",
            DeviceType: null,
            SerialNumber: null,
            Metadata: null));

        // Act
        await grain.HeartbeatAsync("192.168.1.100", "1.2.3");
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.LastSeenAt.Should().NotBeNull();
        snapshot.IpAddress.Should().Be("192.168.1.100");
        snapshot.SoftwareVersion.Should().Be("1.2.3");
    }

    [Fact]
    public async Task TerminalGrain_Deactivate_ChangesStatusToInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITerminalGrain>(
            GrainKeys.Terminal(orgId, terminalId));

        await grain.RegisterAsync(new RegisterTerminalCommand(
            LocationId: Guid.NewGuid(),
            Label: "Test Terminal",
            DeviceType: null,
            SerialNumber: null,
            Metadata: null));

        // Act
        await grain.DeactivateAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(TerminalStatus.Inactive);
    }

    // ============================================================================
    // Refund Grain Tests
    // ============================================================================

    [Fact]
    public async Task RefundGrain_Create_CreatesRefundSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRefundGrain>(
            GrainKeys.Refund(orgId, refundId));

        var command = new CreateRefundCommand(
            PaymentIntentId: Guid.NewGuid(),
            Amount: 1500,
            Currency: "USD",
            Reason: "requested_by_customer",
            Metadata: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.RefundId.Should().Be(refundId);
        snapshot.Amount.Should().Be(1500);
        snapshot.Currency.Should().Be("USD");
        snapshot.Status.Should().Be(RefundStatus.Pending);
        snapshot.ReceiptNumber.Should().StartWith("RF-");
    }

    [Fact]
    public async Task RefundGrain_Process_ChangesStatusToSucceeded()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRefundGrain>(
            GrainKeys.Refund(orgId, refundId));

        await grain.CreateAsync(new CreateRefundCommand(
            PaymentIntentId: Guid.NewGuid(),
            Amount: 1000,
            Currency: "USD",
            Reason: null,
            Metadata: null));

        // Act
        var snapshot = await grain.ProcessAsync();

        // Assert
        snapshot.Status.Should().Be(RefundStatus.Succeeded);
        snapshot.SucceededAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefundGrain_Fail_RecordsFailureReason()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRefundGrain>(
            GrainKeys.Refund(orgId, refundId));

        await grain.CreateAsync(new CreateRefundCommand(
            PaymentIntentId: Guid.NewGuid(),
            Amount: 1000,
            Currency: "USD",
            Reason: null,
            Metadata: null));

        // Act
        var snapshot = await grain.FailAsync("Insufficient funds");

        // Assert
        snapshot.Status.Should().Be(RefundStatus.Failed);
        snapshot.FailureReason.Should().Be("Insufficient funds");
    }

    // ============================================================================
    // Webhook Endpoint Grain Tests
    // ============================================================================

    [Fact]
    public async Task WebhookEndpointGrain_Create_CreatesEndpointSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IWebhookEndpointGrain>(
            GrainKeys.Webhook(orgId, endpointId));

        var command = new CreateWebhookEndpointCommand(
            Url: "https://example.com/webhooks",
            Description: "Main webhook endpoint",
            EnabledEvents: new[] { "payment_intent.succeeded", "refund.*" },
            Secret: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.EndpointId.Should().Be(endpointId);
        snapshot.Url.Should().Be("https://example.com/webhooks");
        snapshot.Enabled.Should().BeTrue();
        snapshot.EnabledEvents.Should().HaveCount(2);
    }

    [Fact]
    public async Task WebhookEndpointGrain_ShouldReceiveEvent_FiltersCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IWebhookEndpointGrain>(
            GrainKeys.Webhook(orgId, endpointId));

        await grain.CreateAsync(new CreateWebhookEndpointCommand(
            Url: "https://example.com/webhooks",
            Description: null,
            EnabledEvents: new[] { "payment_intent.succeeded", "refund.*" },
            Secret: null));

        // Act
        var shouldReceivePayment = await grain.ShouldReceiveEventAsync("payment_intent.succeeded");
        var shouldReceiveRefundCreated = await grain.ShouldReceiveEventAsync("refund.created");
        var shouldReceiveCharge = await grain.ShouldReceiveEventAsync("charge.succeeded");

        // Assert
        shouldReceivePayment.Should().BeTrue();
        shouldReceiveRefundCreated.Should().BeTrue();
        shouldReceiveCharge.Should().BeFalse();
    }

    [Fact]
    public async Task WebhookEndpointGrain_Disable_StopsReceivingEvents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IWebhookEndpointGrain>(
            GrainKeys.Webhook(orgId, endpointId));

        await grain.CreateAsync(new CreateWebhookEndpointCommand(
            Url: "https://example.com/webhooks",
            Description: null,
            EnabledEvents: new[] { "*" },
            Secret: null));

        // Act
        await grain.DisableAsync();
        var shouldReceive = await grain.ShouldReceiveEventAsync("payment_intent.succeeded");

        // Assert
        shouldReceive.Should().BeFalse();
    }

    [Fact]
    public async Task WebhookEndpointGrain_RecordDeliveryAttempt_TracksDeliveries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IWebhookEndpointGrain>(
            GrainKeys.Webhook(orgId, endpointId));

        await grain.CreateAsync(new CreateWebhookEndpointCommand(
            Url: "https://example.com/webhooks",
            Description: null,
            EnabledEvents: new[] { "*" },
            Secret: null));

        // Act
        await grain.RecordDeliveryAttemptAsync(200, true, null);
        await grain.RecordDeliveryAttemptAsync(500, false, "Internal server error");

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.RecentDeliveries.Should().HaveCount(2);
        snapshot.LastDeliveryAt.Should().NotBeNull();
    }
}
