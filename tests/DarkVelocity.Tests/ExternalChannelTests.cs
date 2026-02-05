using System.Text;
using System.Text.Json;
using DarkVelocity.Host;
using DarkVelocity.Host.Adapters;
using DarkVelocity.Host.Endpoints;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// External Order Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExternalOrderGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExternalOrderGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IExternalOrderGrain GetExternalOrderGrain(Guid orgId, Guid orderId)
        => _fixture.Cluster.GrainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, orderId));

    [Fact]
    public async Task ReceiveAsync_ShouldCreateExternalOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        var order = CreateTestExternalOrder(locationId, channelId);

        // Act
        var result = await grain.ReceiveAsync(order);

        // Assert
        result.ExternalOrderId.Should().NotBe(Guid.Empty);
        result.LocationId.Should().Be(locationId);
        result.PlatformOrderId.Should().Be("UBER-12345");
        result.PlatformOrderNumber.Should().Be("#1001");
        result.Status.Should().Be(ExternalOrderStatus.Pending);
        result.OrderType.Should().Be(ExternalOrderType.Delivery);
        result.Customer.Name.Should().Be("John Doe");
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AcceptAsync_ShouldTransitionToAccepted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));
        var estimatedPickup = DateTime.UtcNow.AddMinutes(30);

        // Act
        var result = await grain.AcceptAsync(estimatedPickup);

        // Assert
        result.Status.Should().Be(ExternalOrderStatus.Accepted);
        result.AcceptedAt.Should().NotBeNull();
        result.EstimatedPickupAt.Should().Be(estimatedPickup);
    }

    [Fact]
    public async Task RejectAsync_ShouldTransitionToRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));

        // Act
        var result = await grain.RejectAsync("Kitchen too busy");

        // Assert
        result.Status.Should().Be(ExternalOrderStatus.Rejected);
        result.ErrorMessage.Should().Be("Kitchen too busy");
    }

    [Fact]
    public async Task OrderStatusProgression_ShouldFollowWorkflow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act & Assert - Progress through workflow
        await grain.SetPreparingAsync();
        var preparing = await grain.GetSnapshotAsync();
        preparing.Status.Should().Be(ExternalOrderStatus.Preparing);

        await grain.SetReadyAsync();
        var ready = await grain.GetSnapshotAsync();
        ready.Status.Should().Be(ExternalOrderStatus.Ready);

        await grain.SetPickedUpAsync();
        var pickedUp = await grain.GetSnapshotAsync();
        pickedUp.Status.Should().Be(ExternalOrderStatus.PickedUp);
        pickedUp.ActualPickupAt.Should().NotBeNull();

        await grain.SetDeliveredAsync();
        var delivered = await grain.GetSnapshotAsync();
        delivered.Status.Should().Be(ExternalOrderStatus.Delivered);
    }

    [Fact]
    public async Task CancelAsync_ShouldTransitionToCancelled()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));
        await grain.AcceptAsync(DateTime.UtcNow.AddMinutes(30));

        // Act
        await grain.CancelAsync("Customer cancelled");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExternalOrderStatus.Cancelled);
        snapshot.ErrorMessage.Should().Be("Customer cancelled");
    }

    [Fact]
    public async Task LinkInternalOrderAsync_ShouldSetInternalOrderId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var internalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));

        // Act
        await grain.LinkInternalOrderAsync(internalOrderId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.InternalOrderId.Should().Be(internalOrderId);
    }

    [Fact]
    public async Task IncrementRetryAsync_ShouldIncreaseRetryCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));

        // Act
        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateCourierAsync_ShouldUpdateCourierInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var externalOrderId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetExternalOrderGrain(orgId, externalOrderId);

        await grain.ReceiveAsync(CreateTestExternalOrder(locationId, channelId));

        var courier = new CourierInfo(
            FirstName: "Mike",
            LastName: "Driver",
            PhoneNumber: "+1555123456",
            Provider: "UberEats",
            Status: 30); // On the way

        // Act
        await grain.UpdateCourierAsync(courier);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Courier.Should().NotBeNull();
        snapshot.Courier!.FirstName.Should().Be("Mike");
        snapshot.Courier!.Provider.Should().Be("UberEats");
    }

    private static ExternalOrderReceived CreateTestExternalOrder(Guid locationId, Guid channelId)
    {
        return new ExternalOrderReceived(
            LocationId: locationId,
            DeliveryPlatformId: channelId,
            PlatformOrderId: "UBER-12345",
            PlatformOrderNumber: "#1001",
            ChannelDisplayId: "#1001",
            OrderType: ExternalOrderType.Delivery,
            PlacedAt: DateTime.UtcNow,
            ScheduledPickupAt: null,
            ScheduledDeliveryAt: null,
            IsAsapDelivery: true,
            Customer: new ExternalOrderCustomer(
                Name: "John Doe",
                Phone: "+1555123456",
                Email: "john@example.com",
                DeliveryAddress: new DeliveryAddress(
                    Street: "123 Main St",
                    PostalCode: "12345",
                    City: "Anytown",
                    Country: "US",
                    ExtraAddressInfo: "Apt 4B")),
            Courier: null,
            Items: new List<ExternalOrderItem>
            {
                new("BURGER-001", null, "Classic Burger", 2, 12.99m, 25.98m, "No onions", null),
                new("FRIES-001", null, "Large Fries", 1, 4.99m, 4.99m, null, null)
            },
            Subtotal: 30.97m,
            DeliveryFee: 4.99m,
            ServiceFee: 2.50m,
            Tax: 3.10m,
            Tip: 5.00m,
            Total: 46.56m,
            Currency: "USD",
            Discounts: null,
            Packaging: new PackagingPreferences(true, false, null),
            SpecialInstructions: "Please ring doorbell",
            PlatformRawPayload: "{}");
    }
}

// ============================================================================
// Menu Sync Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuSyncGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuSyncGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuSyncGrain GetMenuSyncGrain(Guid orgId, Guid syncId)
        => _fixture.Cluster.GrainFactory.GetGrain<IMenuSyncGrain>(GrainKeys.MenuSync(orgId, syncId));

    [Fact]
    public async Task StartAsync_ShouldInitializeSync()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        var command = new StartMenuSyncCommand(channelId, null);

        // Act
        var result = await grain.StartAsync(command);

        // Assert
        result.MenuSyncId.Should().NotBe(Guid.Empty);
        result.DeliveryPlatformId.Should().Be(channelId);
        result.Status.Should().Be(MenuSyncStatus.InProgress);
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ItemsSynced.Should().Be(0);
        result.ItemsFailed.Should().Be(0);
    }

    [Fact]
    public async Task RecordItemSyncedAsync_ShouldTrackSyncedItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));

        var mapping = new MenuItemMappingRecord(
            InternalMenuItemId: Guid.NewGuid(),
            PlatformItemId: "PLU-001",
            PlatformCategoryId: "CAT-BURGERS",
            PriceOverride: null,
            IsAvailable: true);

        // Act
        await grain.RecordItemSyncedAsync(mapping);
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(
            Guid.NewGuid(), "PLU-002", "CAT-BURGERS", 9.99m, true));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemsSynced.Should().Be(2);
    }

    [Fact]
    public async Task RecordItemFailedAsync_ShouldTrackFailures()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));

        // Act
        await grain.RecordItemFailedAsync(Guid.NewGuid(), "Invalid PLU format");
        await grain.RecordItemFailedAsync(Guid.NewGuid(), "Price must be positive");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemsFailed.Should().Be(2);
        snapshot.Errors.Should().HaveCount(2);
        snapshot.Errors.Should().Contain("Invalid PLU format");
    }

    [Fact]
    public async Task CompleteAsync_ShouldFinalizeSync()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));
        await grain.RecordItemSyncedAsync(new MenuItemMappingRecord(
            Guid.NewGuid(), "PLU-001", "CAT-1", null, true));

        // Act
        await grain.CompleteAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Completed);
        snapshot.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FailAsync_ShouldMarkSyncAsFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var syncId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var grain = GetMenuSyncGrain(orgId, syncId);

        await grain.StartAsync(new StartMenuSyncCommand(channelId, null));

        // Act
        await grain.FailAsync("Connection to platform failed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(MenuSyncStatus.Failed);
        snapshot.Errors.Should().Contain("Connection to platform failed");
    }
}

// ============================================================================
// Deliverect Webhook Parsing Tests
// ============================================================================

public class DeliverectWebhookParsingTests
{
    [Fact]
    public void ParseDeliverectOrder_ShouldMapFieldsCorrectly()
    {
        // Arrange
        var webhookPayload = @"{
            ""event"": ""order.created"",
            ""accountId"": ""acc-123"",
            ""locationId"": ""loc-456"",
            ""order"": {
                ""_id"": ""deliverect-order-789"",
                ""channelOrderId"": ""UBER-12345"",
                ""channelOrderDisplayId"": ""#1001"",
                ""status"": 10,
                ""orderType"": 1,
                ""customer"": {
                    ""name"": ""John Doe"",
                    ""phoneNumber"": ""+1234567890"",
                    ""email"": ""john@example.com""
                },
                ""deliveryAddress"": {
                    ""street"": ""123 Main St"",
                    ""postalCode"": ""12345"",
                    ""city"": ""Anytown"",
                    ""country"": ""US""
                },
                ""items"": [{
                    ""plu"": ""BURGER-001"",
                    ""name"": ""Classic Burger"",
                    ""quantity"": 2,
                    ""price"": 1299,
                    ""subItems"": [{
                        ""name"": ""Extra Cheese"",
                        ""price"": 150
                    }]
                }],
                ""payment"": {
                    ""amount"": 2898,
                    ""type"": 1
                },
                ""deliveryCost"": 499,
                ""tip"": 200
            }
        }";

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<DeliverectWebhookPayload>(webhookPayload, options);

        // Assert
        payload.Should().NotBeNull();
        payload!.Event.Should().Be("order.created");
        payload.AccountId.Should().Be("acc-123");
        payload.Order.Should().NotBeNull();
        payload.Order!.ChannelOrderId.Should().Be("UBER-12345");
        payload.Order!.Customer!.Name.Should().Be("John Doe");
        payload.Order!.Items.Should().HaveCount(1);
        payload.Order!.Items![0].Plu.Should().Be("BURGER-001");
        payload.Order!.Items![0].Price.Should().Be(1299);
    }
}

// ============================================================================
// Webhook Signature Validation Tests
// ============================================================================

public class WebhookSignatureValidationTests
{
    [Fact]
    public void ValidateHmacSignature_WithValidSignature_ShouldReturnTrue()
    {
        // Arrange
        var payload = @"{""event"":""order.created""}";
        var secret = "webhook-secret-123";

        // Calculate expected signature
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        // Act
        var isValid = ValidateSignature(payload, signature, secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateHmacSignature_WithPrefixedSignature_ShouldReturnTrue()
    {
        // Arrange
        var payload = @"{""event"":""order.created""}";
        var secret = "webhook-secret-123";

        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        // Act
        var isValid = ValidateSignature(payload, signature, secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateHmacSignature_WithInvalidSignature_ShouldReturnFalse()
    {
        // Arrange
        var payload = @"{""event"":""order.created""}";
        var secret = "webhook-secret-123";
        var invalidSignature = "invalid-signature";

        // Act
        var isValid = ValidateSignature(payload, invalidSignature, secret);

        // Assert
        isValid.Should().BeFalse();
    }

    private static bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        try
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

            var providedSignature = signature.Contains('=')
                ? signature.Split('=').Last().ToLowerInvariant()
                : signature.ToLowerInvariant();

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(providedSignature));
        }
        catch
        {
            return false;
        }
    }
}

// ============================================================================
// Platform Adapter Tests
// ============================================================================

public class DeliverectAdapterTests
{
    [Fact]
    public void MapOrderType_ShouldMapCorrectly()
    {
        // Deliverect order types: 1=Delivery, 2=Pickup, 3=DineIn
        MapOrderType(1).Should().Be(ExternalOrderType.Delivery);
        MapOrderType(2).Should().Be(ExternalOrderType.Pickup);
        MapOrderType(3).Should().Be(ExternalOrderType.DineIn);
        MapOrderType(99).Should().Be(ExternalOrderType.Delivery); // Default
        MapOrderType(null).Should().Be(ExternalOrderType.Delivery); // Default
    }

    [Fact]
    public void MapToDeliverectStatus_ShouldMapCorrectly()
    {
        // Internal to Deliverect status codes
        MapToDeliverectStatus(ExternalOrderStatus.Pending).Should().Be(10);
        MapToDeliverectStatus(ExternalOrderStatus.Accepted).Should().Be(20);
        MapToDeliverectStatus(ExternalOrderStatus.Preparing).Should().Be(30);
        MapToDeliverectStatus(ExternalOrderStatus.Ready).Should().Be(40);
        MapToDeliverectStatus(ExternalOrderStatus.PickedUp).Should().Be(50);
        MapToDeliverectStatus(ExternalOrderStatus.Delivered).Should().Be(60);
        MapToDeliverectStatus(ExternalOrderStatus.Cancelled).Should().Be(110);
        MapToDeliverectStatus(ExternalOrderStatus.Failed).Should().Be(120);
        MapToDeliverectStatus(ExternalOrderStatus.Rejected).Should().Be(120);
    }

    [Fact]
    public void PriceConversion_ShouldConvertFromCents()
    {
        // Deliverect uses cents, DarkVelocity uses decimal
        var deliverectPrice = 1299; // $12.99 in cents
        var internalPrice = deliverectPrice / 100m;
        internalPrice.Should().Be(12.99m);
    }

    private static ExternalOrderType MapOrderType(int? orderType) => orderType switch
    {
        1 => ExternalOrderType.Delivery,
        2 => ExternalOrderType.Pickup,
        3 => ExternalOrderType.DineIn,
        _ => ExternalOrderType.Delivery
    };

    private static int MapToDeliverectStatus(ExternalOrderStatus status) => status switch
    {
        ExternalOrderStatus.Pending => 10,
        ExternalOrderStatus.Accepted => 20,
        ExternalOrderStatus.Preparing => 30,
        ExternalOrderStatus.Ready => 40,
        ExternalOrderStatus.PickedUp => 50,
        ExternalOrderStatus.Delivered => 60,
        ExternalOrderStatus.Cancelled => 110,
        ExternalOrderStatus.Failed => 120,
        ExternalOrderStatus.Rejected => 120,
        _ => 10
    };
}
