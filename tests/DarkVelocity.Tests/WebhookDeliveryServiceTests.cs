using DarkVelocity.Host.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DarkVelocity.Tests;

[Trait("Category", "Unit")]
public class WebhookDeliveryServiceTests
{
    #region Signature Tests

    [Fact]
    public void GenerateSignature_ShouldReturnHmacSha256()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "test", "data": {"id": 1}}""";
        var secret = "test-secret-key";

        // Act
        var signature = service.GenerateSignature(payload, secret);

        // Assert
        signature.Should().NotBeNullOrEmpty();
        signature.Should().HaveLength(64); // SHA256 produces 64 hex chars
        signature.Should().MatchRegex("^[a-f0-9]+$"); // Lowercase hex
    }

    [Fact]
    public void GenerateSignature_SameInputs_ShouldReturnSameSignature()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";
        var secret = "secret123";

        // Act
        var signature1 = service.GenerateSignature(payload, secret);
        var signature2 = service.GenerateSignature(payload, secret);

        // Assert
        signature1.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_DifferentSecrets_ShouldReturnDifferentSignatures()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";

        // Act
        var signature1 = service.GenerateSignature(payload, "secret1");
        var signature2 = service.GenerateSignature(payload, "secret2");

        // Assert
        signature1.Should().NotBe(signature2);
    }

    [Fact]
    public void VerifySignature_ValidSignature_ShouldReturnTrue()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";
        var secret = "test-secret";
        var signature = service.GenerateSignature(payload, secret);

        // Act
        var isValid = service.VerifySignature(payload, signature, secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_WithSha256Prefix_ShouldReturnTrue()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";
        var secret = "test-secret";
        var signature = service.GenerateSignature(payload, secret);

        // Act
        var isValid = service.VerifySignature(payload, $"sha256={signature}", secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_InvalidSignature_ShouldReturnFalse()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";
        var secret = "test-secret";

        // Act
        var isValid = service.VerifySignature(payload, "invalid-signature", secret);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_WrongSecret_ShouldReturnFalse()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = """{"event": "order.created"}""";
        var signature = service.GenerateSignature(payload, "correct-secret");

        // Act
        var isValid = service.VerifySignature(payload, signature, "wrong-secret");

        // Assert
        isValid.Should().BeFalse();
    }

    #endregion

    #region Stub Delivery Tests

    [Fact]
    public async Task DeliverAsync_Stub_ShouldReturnSuccess()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var payload = new { @event = "order.created", data = new { orderId = "12345" } };

        // Act
        var result = await service.DeliverAsync(
            "https://example.com/webhook",
            payload,
            "secret123");

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.ResponseBody.Should().Contain("ok");
        result.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DeliverAsync_Stub_WithHeaders_ShouldSucceed()
    {
        // Arrange
        var service = new StubWebhookDeliveryService(NullLogger<StubWebhookDeliveryService>.Instance);
        var headers = new Dictionary<string, string>
        {
            { "X-Custom-Header", "custom-value" },
            { "Authorization", "Bearer token123" }
        };

        // Act
        var result = await service.DeliverAsync(
            "https://example.com/webhook",
            new { test = true },
            headers: headers);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Result Factory Tests

    [Fact]
    public void WebhookDeliveryResult_Succeeded_ShouldSetCorrectProperties()
    {
        // Act
        var result = WebhookDeliveryResult.Succeeded(
            statusCode: 200,
            responseBody: """{"status": "received"}""",
            responseTimeMs: 150,
            responseHeaders: new Dictionary<string, string> { { "X-Request-Id", "abc123" } });

        // Assert
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.ResponseBody.Should().Contain("received");
        result.ResponseTimeMs.Should().Be(150);
        result.ResponseHeaders.Should().ContainKey("X-Request-Id");
        result.ShouldRetry.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void WebhookDeliveryResult_Failed_ShouldSetCorrectProperties()
    {
        // Act
        var result = WebhookDeliveryResult.Failed(
            errorMessage: "Connection timeout",
            statusCode: 504,
            responseBody: null,
            responseTimeMs: 30000,
            shouldRetry: true);

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(504);
        result.ErrorMessage.Should().Be("Connection timeout");
        result.ResponseTimeMs.Should().Be(30000);
        result.ShouldRetry.Should().BeTrue();
    }

    [Fact]
    public void WebhookDeliveryResult_Failed_WithNonRetryable_ShouldNotRetry()
    {
        // Act
        var result = WebhookDeliveryResult.Failed(
            errorMessage: "Invalid URL",
            shouldRetry: false);

        // Assert
        result.Success.Should().BeFalse();
        result.ShouldRetry.Should().BeFalse();
    }

    #endregion
}
