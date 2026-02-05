using DarkVelocity.Host.Domains.System;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using FluentAssertions;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class NotificationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public NotificationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private string GetGrainKey(Guid orgId) => $"{orgId}:notifications";

    #region Email Notification Tests

    [Fact]
    public async Task SendEmailAsync_ShouldSendAndReturnNotification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.SendEmailAsync(new SendEmailCommand(
            To: "test@example.com",
            Subject: "Test Email",
            Body: "<h1>Hello World</h1>"));

        // Assert
        notification.Should().NotBeNull();
        notification.NotificationId.Should().NotBeEmpty();
        notification.Type.Should().Be(NotificationType.Email);
        notification.Recipient.Should().Be("test@example.com");
        notification.Subject.Should().Be("Test Email");
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendEmailAsync_ShouldPersistNotification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var sent = await grain.SendEmailAsync(new SendEmailCommand(
            To: "test@example.com",
            Subject: "Test Email",
            Body: "Test body"));

        var retrieved = await grain.GetNotificationAsync(sent.NotificationId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.NotificationId.Should().Be(sent.NotificationId);
        retrieved.Recipient.Should().Be("test@example.com");
    }

    #endregion

    #region SMS Notification Tests

    [Fact]
    public async Task SendSmsAsync_ShouldSendAndReturnNotification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.SendSmsAsync(new SendSmsCommand(
            To: "+1234567890",
            Message: "Test SMS message"));

        // Assert
        notification.Should().NotBeNull();
        notification.Type.Should().Be(NotificationType.Sms);
        notification.Recipient.Should().Be("+1234567890");
        notification.Body.Should().Be("Test SMS message");
        notification.Status.Should().Be(NotificationStatus.Sent);
    }

    #endregion

    #region Push Notification Tests

    [Fact]
    public async Task SendPushAsync_ShouldSendAndReturnNotification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.SendPushAsync(new SendPushCommand(
            DeviceToken: "device-token-12345",
            Title: "Test Push",
            Body: "Test push notification body",
            Data: new Dictionary<string, string> { { "orderId", "123" } }));

        // Assert
        notification.Should().NotBeNull();
        notification.Type.Should().Be(NotificationType.Push);
        notification.Recipient.Should().Be("device-token-12345");
        notification.Subject.Should().Be("Test Push");
        notification.Status.Should().Be(NotificationStatus.Sent);
    }

    #endregion

    #region Slack Notification Tests

    [Fact]
    public async Task SendSlackAsync_ShouldSendAndReturnNotification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.SendSlackAsync(new SendSlackCommand(
            WebhookUrl: "https://hooks.slack.com/services/xxx",
            Message: "Test Slack message",
            Channel: "#general"));

        // Assert
        notification.Should().NotBeNull();
        notification.Type.Should().Be(NotificationType.Slack);
        notification.Body.Should().Be("Test Slack message");
        notification.Status.Should().Be(NotificationStatus.Sent);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetNotificationsAsync_ShouldReturnFilteredByType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Send different types
        await grain.SendEmailAsync(new SendEmailCommand("test@example.com", "Subject", "Body"));
        await grain.SendSmsAsync(new SendSmsCommand("+1234567890", "SMS"));
        await grain.SendEmailAsync(new SendEmailCommand("test2@example.com", "Subject 2", "Body 2"));

        // Act
        var emailNotifications = await grain.GetNotificationsAsync(type: NotificationType.Email);
        var smsNotifications = await grain.GetNotificationsAsync(type: NotificationType.Sms);

        // Assert
        emailNotifications.Should().HaveCount(2);
        emailNotifications.Should().OnlyContain(n => n.Type == NotificationType.Email);

        smsNotifications.Should().HaveCount(1);
        smsNotifications[0].Type.Should().Be(NotificationType.Sms);
    }

    [Fact]
    public async Task GetNotificationsAsync_ShouldRespectLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Send multiple notifications
        for (int i = 0; i < 10; i++)
        {
            await grain.SendEmailAsync(new SendEmailCommand($"test{i}@example.com", "Subject", "Body"));
        }

        // Act
        var notifications = await grain.GetNotificationsAsync(limit: 5);

        // Assert
        notifications.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetNotificationsAsync_ShouldReturnInDescendingOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        await grain.SendEmailAsync(new SendEmailCommand("first@example.com", "First", "Body"));
        await Task.Delay(10);
        await grain.SendEmailAsync(new SendEmailCommand("second@example.com", "Second", "Body"));
        await Task.Delay(10);
        await grain.SendEmailAsync(new SendEmailCommand("third@example.com", "Third", "Body"));

        // Act
        var notifications = await grain.GetNotificationsAsync();

        // Assert
        notifications.Should().HaveCount(3);
        notifications[0].Recipient.Should().Be("third@example.com"); // Most recent first
        notifications[2].Recipient.Should().Be("first@example.com"); // Oldest last
    }

    #endregion

    #region Channel Management Tests

    [Fact]
    public async Task AddChannelAsync_ShouldAddChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var channel = await grain.AddChannelAsync(new NotificationChannelConfig
        {
            ChannelId = Guid.Empty,
            Type = NotificationType.Email,
            Target = "alerts@example.com",
            IsEnabled = true,
            MinimumSeverity = AlertSeverity.High
        });

        // Assert
        channel.ChannelId.Should().NotBeEmpty();

        var channels = await grain.GetChannelsAsync();
        channels.Should().ContainSingle(c => c.Target == "alerts@example.com");
    }

    [Fact]
    public async Task UpdateChannelAsync_ShouldUpdateExistingChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        var channel = await grain.AddChannelAsync(new NotificationChannelConfig
        {
            ChannelId = Guid.Empty,
            Type = NotificationType.Email,
            Target = "alerts@example.com",
            IsEnabled = true
        });

        // Act
        await grain.UpdateChannelAsync(channel with { Target = "updated@example.com" });

        // Assert
        var channels = await grain.GetChannelsAsync();
        channels.Should().ContainSingle(c => c.ChannelId == channel.ChannelId);
        channels[0].Target.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task RemoveChannelAsync_ShouldRemoveChannel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        var channel = await grain.AddChannelAsync(new NotificationChannelConfig
        {
            ChannelId = Guid.Empty,
            Type = NotificationType.Email,
            Target = "alerts@example.com",
            IsEnabled = true
        });

        // Act
        await grain.RemoveChannelAsync(channel.ChannelId);

        // Assert
        var channels = await grain.GetChannelsAsync();
        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task SetChannelEnabledAsync_ShouldToggleChannelState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        var channel = await grain.AddChannelAsync(new NotificationChannelConfig
        {
            ChannelId = Guid.Empty,
            Type = NotificationType.Email,
            Target = "alerts@example.com",
            IsEnabled = true
        });

        // Act
        await grain.SetChannelEnabledAsync(channel.ChannelId, false);

        // Assert
        var channels = await grain.GetChannelsAsync();
        channels[0].IsEnabled.Should().BeFalse();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SendEmailAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        // Note: Not calling InitializeAsync

        // Act & Assert
        var act = async () => await grain.SendEmailAsync(new SendEmailCommand(
            To: "test@example.com",
            Subject: "Test",
            Body: "Body"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task GetNotificationAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.GetNotificationAsync(Guid.NewGuid());

        // Assert
        notification.Should().BeNull();
    }

    #endregion

    #region Stream Event Tests

    [Fact]
    public async Task SendEmailAsync_ShouldPublishNotificationEvents()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var receivedEvents = new List<IStreamEvent>();

        var streamProvider = _fixture.Cluster.Client.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.NotificationStreamNamespace, orgId.ToString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        var subscription = await stream.SubscribeAsync((evt, token) =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        try
        {
            var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
            await grain.InitializeAsync(orgId);

            // Act
            var notification = await grain.SendEmailAsync(new SendEmailCommand(
                To: "test@example.com",
                Subject: "Test Email",
                Body: "Test body"));

            // Wait for event propagation
            await Task.Delay(500);

            // Assert
            receivedEvents.Should().Contain(e => e is NotificationQueuedEvent);
            receivedEvents.Should().Contain(e => e is NotificationSentEvent);

            var queuedEvent = receivedEvents.OfType<NotificationQueuedEvent>().First();
            queuedEvent.NotificationId.Should().Be(notification.NotificationId);
            queuedEvent.NotificationType.Should().Be("Email");
            queuedEvent.Recipient.Should().Be("test@example.com");
        }
        finally
        {
            await subscription.UnsubscribeAsync();
        }
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task SendEmailAsync_WithMetadata_ShouldPersistMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        var metadata = new Dictionary<string, string>
        {
            { "orderId", "12345" },
            { "customerName", "John Doe" }
        };

        // Act
        var notification = await grain.SendEmailAsync(new SendEmailCommand(
            To: "test@example.com",
            Subject: "Order Confirmation",
            Body: "Your order has been confirmed",
            Metadata: metadata));

        // Assert
        var retrieved = await grain.GetNotificationAsync(notification.NotificationId);
        retrieved.Should().NotBeNull();
        retrieved!.Metadata.Should().NotBeNull();
        retrieved.Metadata!["orderId"].Should().Be("12345");
        retrieved.Metadata["customerName"].Should().Be("John Doe");
    }

    [Fact]
    public async Task SendEmailAsync_WithTriggeredByAlertId_ShouldPersist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<INotificationGrain>(GetGrainKey(orgId));
        await grain.InitializeAsync(orgId);

        // Act
        var notification = await grain.SendEmailAsync(new SendEmailCommand(
            To: "test@example.com",
            Subject: "Alert Notification",
            Body: "Alert triggered",
            TriggeredByAlertId: alertId));

        // Assert
        var retrieved = await grain.GetNotificationAsync(notification.NotificationId);
        retrieved.Should().NotBeNull();
        retrieved!.TriggeredByAlertId.Should().Be(alertId);

        // Should be retrievable by alert
        var alertNotifications = await grain.GetNotificationsForAlertAsync(alertId);
        alertNotifications.Should().ContainSingle(n => n.NotificationId == notification.NotificationId);
    }

    #endregion
}
