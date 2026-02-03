using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExpenseGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExpenseGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IExpenseGrain GetGrain(Guid orgId, Guid siteId, Guid expenseId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IExpenseGrain>(
            GrainKeys.Expense(orgId, siteId, expenseId));
    }

    [Fact]
    public async Task RecordAsync_ShouldCreateExpense()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        // Act
        var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
            orgId,
            siteId,
            expenseId,
            ExpenseCategory.Utilities,
            "Monthly electricity bill",
            250.00m,
            new DateOnly(2024, 1, 15),
            Guid.NewGuid(),
            "USD",
            VendorName: "City Power & Light"));

        // Assert
        snapshot.ExpenseId.Should().Be(expenseId);
        snapshot.Category.Should().Be(ExpenseCategory.Utilities);
        snapshot.Description.Should().Be("Monthly electricity bill");
        snapshot.Amount.Should().Be(250.00m);
        snapshot.VendorName.Should().Be("City Power & Light");
        snapshot.Status.Should().Be(ExpenseStatus.Pending);
    }

    [Fact]
    public async Task RecordAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Rent,
            "Monthly rent", 5000m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        // Act
        var act = () => grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Rent,
            "Monthly rent", 5000m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expense already exists");
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyExpense()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Supplies,
            "Office supplies", 150m, new DateOnly(2024, 1, 10), Guid.NewGuid()));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateExpenseCommand(
            Guid.NewGuid(),
            Description: "Office supplies and cleaning materials",
            Amount: 175.50m));

        // Assert
        snapshot.Description.Should().Be("Office supplies and cleaning materials");
        snapshot.Amount.Should().Be(175.50m);
    }

    [Fact]
    public async Task ApproveAsync_ShouldChangeStatusToApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Marketing,
            "Social media ads", 500m, new DateOnly(2024, 1, 20), Guid.NewGuid()));

        // Act
        var snapshot = await grain.ApproveAsync(new ApproveExpenseCommand(approverId));

        // Assert
        snapshot.Status.Should().Be(ExpenseStatus.Approved);
        snapshot.ApprovedBy.Should().Be(approverId);
        snapshot.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveAsync_NotPending_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Equipment,
            "New printer", 300m, new DateOnly(2024, 1, 25), Guid.NewGuid()));

        await grain.ApproveAsync(new ApproveExpenseCommand(Guid.NewGuid()));

        // Act
        var act = () => grain.ApproveAsync(new ApproveExpenseCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot approve expense in status*");
    }

    [Fact]
    public async Task RejectAsync_ShouldChangeStatusToRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Travel,
            "Flight to conference", 800m, new DateOnly(2024, 2, 1), Guid.NewGuid()));

        // Act
        var snapshot = await grain.RejectAsync(new RejectExpenseCommand(
            Guid.NewGuid(),
            "Conference cancelled"));

        // Assert
        snapshot.Status.Should().Be(ExpenseStatus.Rejected);
        snapshot.Notes.Should().Contain("Conference cancelled");
    }

    [Fact]
    public async Task MarkPaidAsync_ShouldChangeStatusToPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Insurance,
            "Liability insurance", 2000m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        await grain.ApproveAsync(new ApproveExpenseCommand(Guid.NewGuid()));

        // Act
        var snapshot = await grain.MarkPaidAsync(new MarkExpensePaidCommand(
            Guid.NewGuid(),
            new DateOnly(2024, 1, 5),
            "CHK-12345",
            PaymentMethod.Check));

        // Assert
        snapshot.Status.Should().Be(ExpenseStatus.Paid);
        snapshot.ReferenceNumber.Should().Be("CHK-12345");
        snapshot.PaymentMethod.Should().Be(PaymentMethod.Check);
    }

    [Fact]
    public async Task VoidAsync_ShouldChangeStatusToVoided()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Maintenance,
            "HVAC repair", 450m, new DateOnly(2024, 1, 18), Guid.NewGuid()));

        // Act
        await grain.VoidAsync(new VoidExpenseCommand(
            Guid.NewGuid(),
            "Duplicate entry"));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ExpenseStatus.Voided);
        snapshot.Notes.Should().Contain("Duplicate entry");
    }

    [Fact]
    public async Task AttachDocumentAsync_ShouldAddDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Supplies,
            "Cleaning supplies", 85m, new DateOnly(2024, 1, 22), Guid.NewGuid()));

        // Act
        var snapshot = await grain.AttachDocumentAsync(new AttachDocumentCommand(
            "https://storage.example.com/receipts/receipt-123.pdf",
            "receipt-123.pdf",
            Guid.NewGuid()));

        // Assert
        snapshot.DocumentUrl.Should().Be("https://storage.example.com/receipts/receipt-123.pdf");
        snapshot.DocumentFilename.Should().Be("receipt-123.pdf");
    }

    [Fact]
    public async Task UpdateAsync_VoidedExpense_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Other,
            "Miscellaneous", 50m, new DateOnly(2024, 1, 28), Guid.NewGuid()));

        await grain.VoidAsync(new VoidExpenseCommand(Guid.NewGuid(), "Error"));

        // Act
        var act = () => grain.UpdateAsync(new UpdateExpenseCommand(
            Guid.NewGuid(),
            Amount: 75m));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot modify voided expense");
    }

    [Fact]
    public async Task ExistsAsync_NewGrain_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_AfterRecord_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Licenses,
            "Health permit", 200m, new DateOnly(2024, 1, 30), Guid.NewGuid()));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RecordAsync_WithTags_ShouldStoreTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        // Act
        var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Professional,
            "Accounting services", 1500m, new DateOnly(2024, 1, 31), Guid.NewGuid(),
            Tags: new[] { "tax-related", "annual", "accounting" }));

        // Assert
        snapshot.Tags.Should().Contain("tax-related");
        snapshot.Tags.Should().Contain("annual");
        snapshot.Tags.Should().Contain("accounting");
    }

    [Fact]
    public async Task RecordAsync_WithTaxInfo_ShouldStoreCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        // Act
        var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Equipment,
            "Commercial oven", 5000m, new DateOnly(2024, 2, 1), Guid.NewGuid(),
            TaxAmount: 400m,
            IsTaxDeductible: true));

        // Assert
        snapshot.TaxAmount.Should().Be(400m);
        snapshot.IsTaxDeductible.Should().BeTrue();
    }
}
