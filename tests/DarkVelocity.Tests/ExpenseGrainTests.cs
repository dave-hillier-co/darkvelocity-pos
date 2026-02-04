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

    #region SetRecurrence Tests

    [Fact]
    public async Task SetRecurrenceAsync_ShouldSetRecurrencePattern()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Rent,
            "Monthly rent", 5000m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            DayOfMonth = 1
        };

        // Act
        var snapshot = await grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        snapshot.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public async Task SetRecurrenceAsync_WeeklyPattern_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Supplies,
            "Weekly cleaning service", 150m, new DateOnly(2024, 1, 5), Guid.NewGuid()));

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 1,
            DayOfWeek = DayOfWeek.Friday
        };

        // Act
        var snapshot = await grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        snapshot.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public async Task SetRecurrenceAsync_QuarterlyPattern_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Insurance,
            "Quarterly insurance premium", 2500m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Quarterly,
            Interval = 1
        };

        // Act
        var snapshot = await grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        snapshot.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public async Task SetRecurrenceAsync_OnNonExistentExpense_ShouldThrow()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1
        };

        // Act
        var act = () => grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expense not initialized");
    }

    [Fact]
    public async Task SetRecurrenceAsync_BiWeeklyPattern_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Payroll,
            "Payroll processing fee", 100m, new DateOnly(2024, 1, 15), Guid.NewGuid()));

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.BiWeekly,
            Interval = 1
        };

        // Act
        var snapshot = await grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        snapshot.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public async Task SetRecurrenceAsync_WithEndDate_ShouldStorePattern()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, expenseId);

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Equipment,
            "Equipment lease", 500m, new DateOnly(2024, 1, 1), Guid.NewGuid()));

        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            EndDate = new DateOnly(2024, 12, 31)
        };

        // Act
        var snapshot = await grain.SetRecurrenceAsync(new SetRecurrenceCommand(pattern, Guid.NewGuid()));

        // Assert
        snapshot.IsRecurring.Should().BeTrue();
    }

    #endregion
}

/// <summary>
/// Tests for the ExpenseIndexGrain which manages expense indexing and querying at site level.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExpenseIndexGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExpenseIndexGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IExpenseIndexGrain GetIndexGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IExpenseIndexGrain>(
            GrainKeys.Site(orgId, siteId));
    }

    private ExpenseSummary CreateExpenseSummary(
        Guid? expenseId = null,
        ExpenseCategory category = ExpenseCategory.Supplies,
        string description = "Test expense",
        decimal amount = 100m,
        DateOnly? expenseDate = null,
        string? vendorName = null,
        ExpenseStatus status = ExpenseStatus.Pending)
    {
        return new ExpenseSummary(
            expenseId ?? Guid.NewGuid(),
            category,
            description,
            amount,
            "USD",
            expenseDate ?? new DateOnly(2024, 1, 15),
            vendorName,
            status,
            HasDocument: false);
    }

    #region RegisterExpense Tests

    [Fact]
    public async Task RegisterExpenseAsync_ShouldRegisterExpense()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expense = CreateExpenseSummary(
            category: ExpenseCategory.Utilities,
            description: "Electric bill",
            amount: 250m);

        // Act
        await grain.RegisterExpenseAsync(expense);

        // Assert
        var result = await grain.QueryAsync(new ExpenseQuery());
        result.Expenses.Should().Contain(e => e.ExpenseId == expense.ExpenseId);
    }

    [Fact]
    public async Task RegisterExpenseAsync_MultipleExpenses_ShouldRegisterAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expenses = new[]
        {
            CreateExpenseSummary(category: ExpenseCategory.Rent, amount: 5000m),
            CreateExpenseSummary(category: ExpenseCategory.Utilities, amount: 300m),
            CreateExpenseSummary(category: ExpenseCategory.Supplies, amount: 150m)
        };

        // Act
        foreach (var expense in expenses)
        {
            await grain.RegisterExpenseAsync(expense);
        }

        // Assert
        var result = await grain.QueryAsync(new ExpenseQuery());
        result.TotalCount.Should().Be(3);
    }

    #endregion

    #region QueryAsync - Date Range Tests

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ShouldReturnMatchingExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 1), amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 15), amount: 200m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 2, 1), amount: 300m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            FromDate: new DateOnly(2024, 1, 10),
            ToDate: new DateOnly(2024, 1, 31)));

        // Assert
        result.TotalCount.Should().Be(1);
        result.Expenses[0].Amount.Should().Be(200m);
    }

    [Fact]
    public async Task QueryAsync_FilterByFromDateOnly_ShouldReturnExpensesAfter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 1), amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 15), amount: 200m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 2, 1), amount: 300m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            FromDate: new DateOnly(2024, 1, 10)));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_FilterByToDateOnly_ShouldReturnExpensesBefore()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 1), amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 1, 15), amount: 200m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseDate: new DateOnly(2024, 2, 1), amount: 300m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            ToDate: new DateOnly(2024, 1, 20)));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region QueryAsync - Category Filter Tests

    [Fact]
    public async Task QueryAsync_FilterByCategory_ShouldReturnMatchingExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 300m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 250m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Supplies, amount: 150m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            Category: ExpenseCategory.Utilities));

        // Assert
        result.TotalCount.Should().Be(2);
        result.TotalAmount.Should().Be(550m);
    }

    #endregion

    #region QueryAsync - Status Filter Tests

    [Fact]
    public async Task QueryAsync_FilterByStatus_ShouldReturnMatchingExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            status: ExpenseStatus.Pending, amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            status: ExpenseStatus.Approved, amount: 200m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            status: ExpenseStatus.Paid, amount: 300m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            status: ExpenseStatus.Pending, amount: 400m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            Status: ExpenseStatus.Pending));

        // Assert
        result.TotalCount.Should().Be(2);
        result.TotalAmount.Should().Be(500m);
    }

    #endregion

    #region QueryAsync - Vendor Filter Tests

    [Fact]
    public async Task QueryAsync_FilterByVendor_ShouldReturnMatchingExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            vendorName: "City Power & Light", amount: 300m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            vendorName: "City Water Works", amount: 150m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            vendorName: "Office Depot", amount: 200m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            VendorName: "City"));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_FilterByVendor_ShouldBeCaseInsensitive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            vendorName: "STAPLES", amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            vendorName: "staples office", amount: 200m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            VendorName: "staples"));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region QueryAsync - Amount Range Tests

    [Fact]
    public async Task QueryAsync_FilterByAmountRange_ShouldReturnMatchingExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 50m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 150m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 250m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 500m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            MinAmount: 100m,
            MaxAmount: 300m));

        // Assert
        result.TotalCount.Should().Be(2);
        result.Expenses.Should().OnlyContain(e => e.Amount >= 100m && e.Amount <= 300m);
    }

    [Fact]
    public async Task QueryAsync_FilterByMinAmountOnly_ShouldReturnExpensesAbove()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 50m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 150m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 250m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            MinAmount: 100m));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_FilterByMaxAmountOnly_ShouldReturnExpensesBelow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 50m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 150m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 250m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            MaxAmount: 200m));

        // Assert
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region QueryAsync - Pagination Tests

    [Fact]
    public async Task QueryAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        // Register 10 expenses with different amounts to ensure ordering
        for (int i = 1; i <= 10; i++)
        {
            await grain.RegisterExpenseAsync(CreateExpenseSummary(
                amount: i * 100m,
                expenseDate: new DateOnly(2024, 1, i)));
        }

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            Skip: 3,
            Take: 3));

        // Assert
        result.TotalCount.Should().Be(10);
        result.Expenses.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_SkipBeyondTotal_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 200m));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            Skip: 10,
            Take: 5));

        // Assert
        result.TotalCount.Should().Be(2);
        result.Expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_DefaultPagination_ShouldUseDefaults()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        for (int i = 0; i < 60; i++)
        {
            await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: i * 10m));
        }

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery());

        // Assert
        result.TotalCount.Should().Be(60);
        result.Expenses.Should().HaveCount(50); // Default Take is 50
    }

    #endregion

    #region QueryAsync - Combined Filters Tests

    [Fact]
    public async Task QueryAsync_CombinedFilters_ShouldApplyAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities,
            amount: 300m,
            expenseDate: new DateOnly(2024, 1, 15),
            status: ExpenseStatus.Approved));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities,
            amount: 150m,
            expenseDate: new DateOnly(2024, 1, 10),
            status: ExpenseStatus.Pending));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent,
            amount: 5000m,
            expenseDate: new DateOnly(2024, 1, 1),
            status: ExpenseStatus.Approved));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities,
            amount: 250m,
            expenseDate: new DateOnly(2024, 2, 1),
            status: ExpenseStatus.Approved));

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery(
            Category: ExpenseCategory.Utilities,
            Status: ExpenseStatus.Approved,
            FromDate: new DateOnly(2024, 1, 1),
            ToDate: new DateOnly(2024, 1, 31)));

        // Assert
        result.TotalCount.Should().Be(1);
        result.Expenses[0].Amount.Should().Be(300m);
    }

    #endregion

    #region GetCategoryTotals Tests

    [Fact]
    public async Task GetCategoryTotalsAsync_ShouldReturnTotalsByCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 300m, status: ExpenseStatus.Approved,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 250m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 20)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Supplies, amount: 150m, status: ExpenseStatus.Pending,
            expenseDate: new DateOnly(2024, 1, 25)));

        // Act
        var totals = await grain.GetCategoryTotalsAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // Assert
        totals.Should().HaveCount(4);

        var rentTotal = totals.FirstOrDefault(t => t.Category == ExpenseCategory.Rent);
        rentTotal.Should().NotBeNull();
        rentTotal!.TotalAmount.Should().Be(5000m);
        rentTotal.Count.Should().Be(1);

        var utilitiesTotal = totals.FirstOrDefault(t => t.Category == ExpenseCategory.Utilities);
        utilitiesTotal.Should().NotBeNull();
        utilitiesTotal!.TotalAmount.Should().Be(550m);
        utilitiesTotal.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_ShouldExcludeVoidedAndRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 300m, status: ExpenseStatus.Approved,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 200m, status: ExpenseStatus.Voided,
            expenseDate: new DateOnly(2024, 1, 16)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 150m, status: ExpenseStatus.Rejected,
            expenseDate: new DateOnly(2024, 1, 17)));

        // Act
        var totals = await grain.GetCategoryTotalsAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // Assert
        var utilitiesTotal = totals.FirstOrDefault(t => t.Category == ExpenseCategory.Utilities);
        utilitiesTotal.Should().NotBeNull();
        utilitiesTotal!.TotalAmount.Should().Be(300m); // Only the approved one
        utilitiesTotal.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 2, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 3, 1)));

        // Act
        var totals = await grain.GetCategoryTotalsAsync(
            new DateOnly(2024, 1, 15),
            new DateOnly(2024, 2, 15));

        // Assert
        var rentTotal = totals.FirstOrDefault(t => t.Category == ExpenseCategory.Rent);
        rentTotal.Should().NotBeNull();
        rentTotal!.TotalAmount.Should().Be(5000m); // Only Feb 1st falls in range
        rentTotal.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_ShouldOrderByAmountDescending()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Supplies, amount: 100m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Rent, amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            category: ExpenseCategory.Utilities, amount: 300m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 10)));

        // Act
        var totals = await grain.GetCategoryTotalsAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // Assert
        totals[0].Category.Should().Be(ExpenseCategory.Rent);
        totals[1].Category.Should().Be(ExpenseCategory.Utilities);
        totals[2].Category.Should().Be(ExpenseCategory.Supplies);
    }

    #endregion

    #region GetTotal Tests

    [Fact]
    public async Task GetTotalAsync_ShouldReturnTotalExpenses()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 5000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 300m, status: ExpenseStatus.Approved,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 150m, status: ExpenseStatus.Pending,
            expenseDate: new DateOnly(2024, 1, 20)));

        // Act
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // Assert
        total.Should().Be(5450m);
    }

    [Fact]
    public async Task GetTotalAsync_ShouldExcludeVoidedAndRejected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 500m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 300m, status: ExpenseStatus.Voided,
            expenseDate: new DateOnly(2024, 1, 16)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 200m, status: ExpenseStatus.Rejected,
            expenseDate: new DateOnly(2024, 1, 17)));

        // Act
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));

        // Assert
        total.Should().Be(500m);
    }

    [Fact]
    public async Task GetTotalAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 1000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 2000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 3000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 2, 1)));

        // Act
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 1, 10),
            new DateOnly(2024, 1, 31));

        // Assert
        total.Should().Be(2000m);
    }

    [Fact]
    public async Task GetTotalAsync_EmptyDateRange_ShouldReturnZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 1000m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 1)));

        // Act
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 6, 1),
            new DateOnly(2024, 6, 30));

        // Assert
        total.Should().Be(0m);
    }

    #endregion

    #region RemoveExpense Tests

    [Fact]
    public async Task RemoveExpenseAsync_ShouldRemoveFromIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expenseId = Guid.NewGuid();
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseId: expenseId, amount: 100m));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(amount: 200m));

        // Act
        await grain.RemoveExpenseAsync(expenseId);

        // Assert
        var result = await grain.QueryAsync(new ExpenseQuery());
        result.TotalCount.Should().Be(1);
        result.Expenses.Should().NotContain(e => e.ExpenseId == expenseId);
    }

    [Fact]
    public async Task RemoveExpenseAsync_NonExistent_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        // Act
        var act = () => grain.RemoveExpenseAsync(Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveExpenseAsync_ShouldUpdateTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expenseId = Guid.NewGuid();
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseId: expenseId, amount: 500m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 15)));
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            amount: 300m, status: ExpenseStatus.Paid,
            expenseDate: new DateOnly(2024, 1, 20)));

        // Act
        await grain.RemoveExpenseAsync(expenseId);

        // Assert
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 31));
        total.Should().Be(300m);
    }

    #endregion

    #region UpdateExpense Tests

    [Fact]
    public async Task UpdateExpenseAsync_ShouldUpdateInIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expenseId = Guid.NewGuid();
        await grain.RegisterExpenseAsync(CreateExpenseSummary(
            expenseId: expenseId,
            amount: 100m,
            status: ExpenseStatus.Pending));

        var updatedExpense = CreateExpenseSummary(
            expenseId: expenseId,
            amount: 150m,
            status: ExpenseStatus.Approved);

        // Act
        await grain.UpdateExpenseAsync(updatedExpense);

        // Assert
        var result = await grain.QueryAsync(new ExpenseQuery());
        var expense = result.Expenses.First(e => e.ExpenseId == expenseId);
        expense.Amount.Should().Be(150m);
        expense.Status.Should().Be(ExpenseStatus.Approved);
    }

    [Fact]
    public async Task UpdateExpenseAsync_NonExistent_ShouldNotAdd()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        var expense = CreateExpenseSummary(amount: 100m);

        // Act
        await grain.UpdateExpenseAsync(expense);

        // Assert
        var result = await grain.QueryAsync(new ExpenseQuery());
        result.TotalCount.Should().Be(0);
    }

    #endregion

    #region Empty Index Tests

    [Fact]
    public async Task QueryAsync_EmptyIndex_ShouldReturnEmptyResult()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        // Act
        var result = await grain.QueryAsync(new ExpenseQuery());

        // Assert
        result.TotalCount.Should().Be(0);
        result.TotalAmount.Should().Be(0m);
        result.Expenses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoryTotalsAsync_EmptyIndex_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        // Act
        var totals = await grain.GetCategoryTotalsAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31));

        // Assert
        totals.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTotalAsync_EmptyIndex_ShouldReturnZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetIndexGrain(orgId, siteId);

        // Act
        var total = await grain.GetTotalAsync(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31));

        // Assert
        total.Should().Be(0m);
    }

    #endregion
}
