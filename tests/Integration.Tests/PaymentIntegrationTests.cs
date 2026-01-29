using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Payment Processing workflows.
///
/// Business Scenarios Covered:
/// - Cash payment flow (with change calculation)
/// - Card payment flow (pending -> completed)
/// - Split payments (multiple payment methods)
/// - Payment refunds
/// - Payment voids
/// - Tips handling
/// </summary>
public class PaymentIntegrationTests : IClassFixture<PaymentsServiceFixture>
{
    private readonly PaymentsServiceFixture _fixture;
    private readonly HttpClient _client;

    public PaymentIntegrationTests(PaymentsServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Cash Payment Flow

    [Fact]
    public async Task CreateCashPayment_FullPayment_CreatesCompletedPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 25.00m,
            ReceivedAmount: 25.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(25.00m);
        payment.Status.Should().Be("completed");
        payment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCashPayment_WithOverpayment_CalculatesChange()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 18.75m,
            ReceivedAmount: 20.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        // Assert
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment!.Amount.Should().Be(18.75m);
        payment.ReceivedAmount.Should().Be(20.00m);
        payment.ChangeAmount.Should().Be(1.25m);
    }

    [Fact]
    public async Task CreateCashPayment_WithTip_CalculatesTotal()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 50.00m,
            TipAmount: 10.00m,
            ReceivedAmount: 60.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        // Assert
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment!.Amount.Should().Be(50.00m);
        payment.TipAmount.Should().Be(10.00m);
        payment.TotalAmount.Should().Be(60.00m);
        payment.ReceivedAmount.Should().Be(60.00m);
        payment.ChangeAmount.Should().Be(0m);
    }

    [Fact]
    public async Task CreateCashPayment_InsufficientAmount_ReturnsBadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 30.00m,
            ReceivedAmount: 20.00m); // Less than amount

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCashPayment_WithWrongMethodType_ReturnsBadRequest()
    {
        // Arrange - Try to use card method for cash payment
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId, // Wrong type
            Amount: 25.00m,
            ReceivedAmount: 25.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Card Payment Flow

    [Fact]
    public async Task CreateCardPayment_CreatesPendingPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 45.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.Status.Should().Be("pending");
        payment.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateCardPayment_WithTip_AddsTipToTotal()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 50.00m,
            TipAmount: 10.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            request);

        // Assert
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment!.Amount.Should().Be(50.00m);
        payment.TipAmount.Should().Be(10.00m);
        payment.TotalAmount.Should().Be(60.00m);
    }

    [Fact]
    public async Task CompleteCardPayment_TransitionsToCompleted()
    {
        // Arrange - Create pending card payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 75.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var completeRequest = new CompleteCardPaymentRequest(
            StripePaymentIntentId: "pi_test_123456789",
            CardBrand: "Visa",
            CardLastFour: "4242");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/complete",
            completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = await response.Content.ReadFromJsonAsync<PaymentDto>();
        completed!.Status.Should().Be("completed");
        completed.CardBrand.Should().Be("Visa");
        completed.CardLastFour.Should().Be("4242");
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteCardPayment_AlreadyCompleted_ReturnsBadRequest()
    {
        // Arrange - Create and complete a card payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 50.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var completeRequest = new CompleteCardPaymentRequest(
            StripePaymentIntentId: "pi_test_111",
            CardBrand: "Visa",
            CardLastFour: "4242");

        // Complete it first time
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/complete",
            completeRequest);

        // Act - Try to complete again
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment.Id}/complete",
            completeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCardPayment_WithWrongMethodType_ReturnsBadRequest()
    {
        // Arrange - Try to use cash method for card payment
        var orderId = Guid.NewGuid();
        var request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId, // Wrong type
            Amount: 25.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Split Payments

    [Fact]
    public async Task SplitPayment_CashThenCard_BothSucceed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderTotal = 100.00m;

        // First payment: $40 cash
        var cashRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 40.00m,
            ReceivedAmount: 40.00m);

        // Act - Create cash payment
        var cashResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            cashRequest);

        // Assert
        cashResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var cashPayment = await cashResponse.Content.ReadFromJsonAsync<PaymentDto>();
        cashPayment!.Amount.Should().Be(40.00m);

        // Second payment: $60 card
        var cardRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 60.00m);

        var cardResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            cardRequest);

        // Assert
        cardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var cardPayment = await cardResponse.Content.ReadFromJsonAsync<PaymentDto>();
        cardPayment!.Amount.Should().Be(60.00m);

        // Verify both payments exist for the order
        var paymentsResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/by-order/{orderId}");
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();

        payments!.Embedded.Items.Should().HaveCount(2);
        payments.Embedded.Items.Sum(p => p.Amount).Should().Be(orderTotal);
    }

    [Fact]
    public async Task SplitPayment_MultipleCards_AllSucceed()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        // First card: $30
        var card1Request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 30.00m);

        // Second card: $30
        var card2Request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 30.00m);

        // Third card: $40
        var card3Request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 40.00m);

        // Act
        var response1 = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            card1Request);
        var response2 = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            card2Request);
        var response3 = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            card3Request);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        response3.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify all payments exist
        var paymentsResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/by-order/{orderId}");
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();

        payments!.Embedded.Items.Should().HaveCount(3);
        payments.Embedded.Items.Sum(p => p.Amount).Should().Be(100.00m);
    }

    #endregion

    #region Refunds

    [Fact]
    public async Task RefundPayment_CompletedCashPayment_Succeeds()
    {
        // Arrange - Create a completed cash payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 35.00m,
            ReceivedAmount: 35.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var refundRequest = new RefundPaymentRequest(
            Reason: "Customer returned item");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/refund",
            refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refunded = await response.Content.ReadFromJsonAsync<PaymentDto>();
        refunded!.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task RefundPayment_CompletedCardPayment_Succeeds()
    {
        // Arrange - Create and complete a card payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 45.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Complete the card payment
        var completeRequest = new CompleteCardPaymentRequest(
            StripePaymentIntentId: "pi_test_refund",
            CardBrand: "Mastercard",
            CardLastFour: "5555");

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/complete",
            completeRequest);

        var refundRequest = new RefundPaymentRequest(
            Reason: "Order cancelled");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment.Id}/refund",
            refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refunded = await response.Content.ReadFromJsonAsync<PaymentDto>();
        refunded!.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task RefundPayment_PendingPayment_ReturnsBadRequest()
    {
        // Arrange - Create a pending card payment (not completed)
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CardPaymentMethodId,
            Amount: 25.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var refundRequest = new RefundPaymentRequest(Reason: "Test");

        // Act - Try to refund pending payment
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/refund",
            refundRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefundPayment_AlreadyRefunded_ReturnsBadRequest()
    {
        // Arrange - Create and refund a payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 20.00m,
            ReceivedAmount: 20.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Refund it once
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/refund",
            new RefundPaymentRequest(Reason: "First refund"));

        // Act - Try to refund again
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment.Id}/refund",
            new RefundPaymentRequest(Reason: "Second refund"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Voids

    [Fact]
    public async Task VoidPayment_CompletedPayment_Succeeds()
    {
        // Arrange - Create a completed cash payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 15.00m,
            ReceivedAmount: 20.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var voidRequest = new VoidPaymentRequest(Reason: "Wrong order");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/void",
            voidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var voided = await response.Content.ReadFromJsonAsync<PaymentDto>();
        voided!.Status.Should().Be("voided");
    }

    [Fact]
    public async Task VoidPayment_AlreadyRefunded_ReturnsBadRequest()
    {
        // Arrange - Create and refund a payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 10.00m,
            ReceivedAmount: 10.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Refund it
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment!.Id}/refund",
            new RefundPaymentRequest(Reason: "Refund"));

        // Act - Try to void
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{payment.Id}/void",
            new VoidPaymentRequest(Reason: "Try void"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Payment Queries

    [Fact]
    public async Task GetPayments_ReturnsPaymentsForLocation()
    {
        // Arrange - Create a payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 25.00m,
            ReceivedAmount: 25.00m);

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPaymentsByOrder_ReturnsOnlyOrderPayments()
    {
        // Arrange - Create payments for specific order
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 50.00m,
            ReceivedAmount: 50.00m);

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/by-order/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();
        collection!.Embedded.Items.Should().OnlyContain(p => p.OrderId == orderId);
    }

    [Fact]
    public async Task GetPaymentById_ReturnsPayment()
    {
        // Arrange - Create a payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.CashPaymentMethodId,
            Amount: 30.00m,
            ReceivedAmount: 30.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetPaymentById_NotFound_Returns404()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
