using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class WebhookSubscriptionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public WebhookSubscriptionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IWebhookSubscriptionGrain> CreateWebhookAsync(
        Guid orgId,
        Guid webhookId,
        string name = "Test Webhook",
        string url = "https://example.com/webhook")
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
        await grain.CreateAsync(new CreateWebhookCommand(
            orgId,
            name,
            url,
            new List<string> { "order.created", "order.completed" }));
        return grain;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateWebhook()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));

        var command = new CreateWebhookCommand(
            orgId,
            "Order Events",
            "https://api.example.com/webhooks/orders",
            new List<string> { "order.created", "order.updated", "order.completed" },
            Secret: "secret123",
            Headers: new Dictionary<string, string> { { "X-Api-Key", "key123" } });

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(webhookId);
        result.Name.Should().Be("Order Events");

        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Order Events");
        state.Url.Should().Be("https://api.example.com/webhooks/orders");
        state.Secret.Should().Be("secret123");
        state.Headers.Should().ContainKey("X-Api-Key");
        state.Events.Should().HaveCount(3);
        state.Status.Should().Be(WebhookStatus.Active);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateWebhook()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        await grain.UpdateAsync(new UpdateWebhookCommand(
            Name: "Updated Webhook",
            Url: "https://new-url.com/webhook",
            Secret: "newSecret",
            EventTypes: new List<string> { "booking.created" }));

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Webhook");
        state.Url.Should().Be("https://new-url.com/webhook");
        state.Secret.Should().Be("newSecret");
        state.Events.Should().HaveCount(1);
        state.Events[0].EventType.Should().Be("booking.created");
    }

    [Fact]
    public async Task SubscribeToEventAsync_ShouldAddEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        await grain.SubscribeToEventAsync("customer.created");
        await grain.SubscribeToEventAsync("payment.completed");

        // Assert
        (await grain.IsSubscribedToEventAsync("customer.created")).Should().BeTrue();
        (await grain.IsSubscribedToEventAsync("payment.completed")).Should().BeTrue();
    }

    [Fact]
    public async Task UnsubscribeFromEventAsync_ShouldRemoveEvent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        await grain.UnsubscribeFromEventAsync("order.created");

        // Assert
        (await grain.IsSubscribedToEventAsync("order.created")).Should().BeFalse();
        (await grain.IsSubscribedToEventAsync("order.completed")).Should().BeTrue();
    }

    [Fact]
    public async Task PauseAsync_ShouldPauseWebhook()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        await grain.PauseAsync();

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(WebhookStatus.Paused);

        var state = await grain.GetStateAsync();
        state.PausedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResumeAsync_ShouldResumeWebhook()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);
        await grain.PauseAsync();

        // Act
        await grain.ResumeAsync();

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(WebhookStatus.Active);

        var state = await grain.GetStateAsync();
        state.PausedAt.Should().BeNull();
        state.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_ShouldMarkAsDeleted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        await grain.DeleteAsync();

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(WebhookStatus.Deleted);
    }

    [Fact]
    public async Task DeliverAsync_ShouldRecordDelivery()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        var result = await grain.DeliverAsync("order.created", """{"orderId":"123"}""");

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);

        var deliveries = await grain.GetRecentDeliveriesAsync();
        deliveries.Should().HaveCount(1);
        deliveries[0].EventType.Should().Be("order.created");
        deliveries[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeliverAsync_WhenPaused_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);
        await grain.PauseAsync();

        // Act
        var act = () => grain.DeliverAsync("order.created", "{}");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Webhook is not active*");
    }

    [Fact]
    public async Task DeliverAsync_WhenNotSubscribed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        var act = () => grain.DeliverAsync("customer.created", "{}");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not subscribed to event*");
    }

    [Fact]
    public async Task RecordDeliveryAsync_ShouldTrackDeliveries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordDeliveryAsync(new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                EventType = "order.created",
                AttemptedAt = DateTime.UtcNow,
                StatusCode = 200,
                Success = true,
                RetryCount = 0
            });
        }

        // Assert
        var deliveries = await grain.GetRecentDeliveriesAsync();
        deliveries.Should().HaveCount(5);

        var state = await grain.GetStateAsync();
        state.LastDeliveryAt.Should().NotBeNull();
        state.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task RecordDeliveryAsync_WithFailures_ShouldTrackFailures()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act - Record 2 failures
        await grain.RecordDeliveryAsync(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventType = "order.created",
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 500,
            Success = false,
            ErrorMessage = "Server Error"
        });

        await grain.RecordDeliveryAsync(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventType = "order.created",
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 500,
            Success = false,
            ErrorMessage = "Server Error"
        });

        // Assert
        var state = await grain.GetStateAsync();
        state.ConsecutiveFailures.Should().Be(2);
        state.Status.Should().Be(WebhookStatus.Active); // Still active, not yet at max retries
    }

    [Fact]
    public async Task RecordDeliveryAsync_WithMaxFailures_ShouldMarkFailed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act - Record 3 failures (default max retries)
        for (int i = 0; i < 3; i++)
        {
            await grain.RecordDeliveryAsync(new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                EventType = "order.created",
                AttemptedAt = DateTime.UtcNow,
                StatusCode = 500,
                Success = false
            });
        }

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(WebhookStatus.Failed);
    }

    [Fact]
    public async Task RecordDeliveryAsync_SuccessAfterFailures_ShouldResetCounter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Record some failures
        await grain.RecordDeliveryAsync(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventType = "order.created",
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 500,
            Success = false
        });

        await grain.RecordDeliveryAsync(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventType = "order.created",
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 500,
            Success = false
        });

        // Act - Record success
        await grain.RecordDeliveryAsync(new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EventType = "order.created",
            AttemptedAt = DateTime.UtcNow,
            StatusCode = 200,
            Success = true
        });

        // Assert
        var state = await grain.GetStateAsync();
        state.ConsecutiveFailures.Should().Be(0);
        state.Status.Should().Be(WebhookStatus.Active);
    }

    [Fact]
    public async Task GetRecentDeliveriesAsync_ShouldLimitTo100()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Act - Record more than 100 deliveries
        for (int i = 0; i < 110; i++)
        {
            await grain.RecordDeliveryAsync(new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                EventType = "order.created",
                AttemptedAt = DateTime.UtcNow,
                StatusCode = 200,
                Success = true
            });
        }

        // Assert
        var deliveries = await grain.GetRecentDeliveriesAsync();
        deliveries.Should().HaveCount(100);
    }

    [Fact]
    public async Task IsSubscribedToEventAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var grain = await CreateWebhookAsync(orgId, webhookId);

        // Assert
        (await grain.IsSubscribedToEventAsync("order.created")).Should().BeTrue();
        (await grain.IsSubscribedToEventAsync("order.completed")).Should().BeTrue();
        (await grain.IsSubscribedToEventAsync("customer.created")).Should().BeFalse();
    }
}
