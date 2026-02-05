using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ChargebackGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ChargebackGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ReceiveAsync_ShouldCreateChargeback()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var deadline = DateTime.UtcNow.AddDays(14);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));

        // Act
        var result = await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId,
            paymentId,
            100m,
            "CB001",
            "Merchandise not received",
            "PROC-123",
            deadline));

        // Assert
        result.ChargebackId.Should().Be(chargebackId);
        result.DisputeDeadline.Should().Be(deadline);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ChargebackStatus.Pending);
        state.Amount.Should().Be(100m);
        state.ReasonCode.Should().Be("CB001");
    }

    [Fact]
    public async Task AcknowledgeAsync_ShouldAcknowledgeChargeback()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));

        // Act
        await grain.AcknowledgeAsync(userId, "Reviewed by manager");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ChargebackStatus.Acknowledged);
        state.AcknowledgedBy.Should().Be(userId);
    }

    [Fact]
    public async Task UploadEvidenceAsync_ShouldAddEvidence()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));
        await grain.AcknowledgeAsync(userId, "Reviewed");

        // Act
        var result = await grain.UploadEvidenceAsync(new UploadEvidenceCommand(
            "receipt",
            "Customer signed receipt",
            "s3://bucket/receipt-123.pdf",
            userId));

        // Assert
        result.EvidenceId.Should().NotBeEmpty();
        var evidence = await grain.GetEvidenceAsync();
        evidence.Should().HaveCount(1);
        evidence[0].EvidenceType.Should().Be("receipt");
    }

    [Fact]
    public async Task DisputeAsync_ShouldDisputeChargeback()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));
        await grain.AcknowledgeAsync(userId, "Reviewed");
        await grain.UploadEvidenceAsync(new UploadEvidenceCommand(
            "receipt", "Receipt", "s3://bucket/receipt.pdf", userId));

        // Act
        await grain.DisputeAsync(new DisputeChargebackCommand(
            "Customer received goods, evidence attached", userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ChargebackStatus.Disputed);
        state.DisputeReference.Should().StartWith("DISP-");
    }

    [Fact]
    public async Task AcceptAsync_ShouldAcceptChargeback()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));
        await grain.AcknowledgeAsync(userId, "Reviewed");

        // Act
        await grain.AcceptAsync(userId, "Valid claim, accepting loss");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ChargebackStatus.Lost);
        state.Resolution.Should().Be(ChargebackResolution.AcceptedByMerchant);
    }

    [Fact]
    public async Task ResolveAsync_WonByMerchant_ShouldSetWonStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));
        await grain.AcknowledgeAsync(userId, "Reviewed");
        await grain.DisputeAsync(new DisputeChargebackCommand("Evidence provided", userId));

        // Act
        await grain.ResolveAsync(new ResolveChargebackCommand(
            ChargebackResolution.WonByMerchant,
            0m,
            "RESOLVED-123",
            "Dispute upheld"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ChargebackStatus.Won);
        state.Resolution.Should().Be(ChargebackResolution.WonByMerchant);
        state.FinalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task AddNoteAsync_ShouldAddNote()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(14)));

        // Act
        await grain.AddNoteAsync("Contacted customer via phone", userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Notes.Should().HaveCount(1);
        state.Notes[0].Content.Should().Be("Contacted customer via phone");
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deadline = DateTime.UtcNow.AddDays(7);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, paymentId, 150m, "CB002", "Duplicate charge", "PROC-456", deadline));
        await grain.AcknowledgeAsync(userId, "Reviewed");
        await grain.UploadEvidenceAsync(new UploadEvidenceCommand(
            "invoice", "Original invoice", "s3://bucket/invoice.pdf", userId));

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.ChargebackId.Should().Be(chargebackId);
        summary.PaymentId.Should().Be(paymentId);
        summary.Amount.Should().Be(150m);
        summary.Status.Should().Be(ChargebackStatus.EvidenceGathering);
        summary.EvidenceCount.Should().Be(1);
        summary.DaysUntilDeadline.Should().BeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public async Task DisputeAsync_PastDeadline_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var chargebackId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChargebackGrain>(
            GrainKeys.Chargeback(orgId, chargebackId));
        await grain.ReceiveAsync(new ReceiveChargebackCommand(
            orgId, Guid.NewGuid(), 100m, "CB001", "Test", "PROC-123",
            DateTime.UtcNow.AddDays(-1))); // Past deadline
        await grain.AcknowledgeAsync(userId, "Reviewed");

        // Act
        var act = () => grain.DisputeAsync(new DisputeChargebackCommand("Trying to dispute", userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deadline*");
    }
}
