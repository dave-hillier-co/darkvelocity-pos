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

    // Given: A new expense grain for a site
    // When: A $250 utilities expense for a monthly electricity bill is recorded
    // Then: The expense is created with pending status and the correct category, description, and vendor
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

    // Given: An expense that has already been recorded for a site
    // When: A second expense is recorded with the same expense ID
    // Then: An error is thrown indicating the expense already exists
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

    // Given: A recorded supplies expense of $150 for office supplies
    // When: The description and amount are updated to include cleaning materials at $175.50
    // Then: The expense reflects the updated description and amount
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

    // Given: A pending marketing expense of $500 for social media ads
    // When: The expense is approved by a manager
    // Then: The status changes to Approved with the approver ID and approval timestamp recorded
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

    // Given: An expense that has already been approved
    // When: A second approval attempt is made
    // Then: An error is thrown because only pending expenses can be approved
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

    // Given: A pending travel expense of $800 for a conference flight
    // When: The expense is rejected with a cancellation reason
    // Then: The status changes to Rejected and the rejection reason is recorded in notes
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

    // Given: An approved insurance expense of $2,000
    // When: The expense is marked as paid with a check reference number
    // Then: The status changes to Paid with the check number and payment method recorded
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

    // Given: A pending maintenance expense of $450 for HVAC repair
    // When: The expense is voided as a duplicate entry
    // Then: The status changes to Voided with the void reason recorded in notes
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

    // Given: A recorded supplies expense for cleaning supplies
    // When: A receipt PDF document is attached to the expense
    // Then: The document URL and filename are stored on the expense
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

    // Given: An expense that has been voided
    // When: An update to the voided expense amount is attempted
    // Then: An error is thrown because voided expenses cannot be modified
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

    // Given: A new, uninitialized expense grain
    // When: The existence check is performed
    // Then: The grain reports it does not exist
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

    // Given: An expense grain after a licenses expense has been recorded
    // When: The existence check is performed
    // Then: The grain reports it exists
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

    // Given: A new expense grain for professional services
    // When: An accounting services expense is recorded with tax-related, annual, and accounting tags
    // Then: All three tags are stored on the expense
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

    // Given: A new expense grain for equipment purchase
    // When: A $5,000 commercial oven expense is recorded with $400 tax amount and tax-deductible flag
    // Then: The tax amount and deductibility flag are correctly stored
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

    // Given: A recorded monthly rent expense of $5,000
    // When: A monthly recurrence pattern with day-of-month 1 is set
    // Then: The expense is flagged as recurring
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

    // Given: A recorded weekly cleaning service expense of $150
    // When: A weekly recurrence pattern for every Friday is set
    // Then: The expense is flagged as recurring
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

    // Given: A recorded quarterly insurance premium expense of $2,500
    // When: A quarterly recurrence pattern is set
    // Then: The expense is flagged as recurring
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

    // Given: A new, uninitialized expense grain with no recorded expense
    // When: A recurrence pattern is set on the non-existent expense
    // Then: An error is thrown because the expense has not been initialized
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

    // Given: A recorded bi-weekly payroll processing fee of $100
    // When: A bi-weekly recurrence pattern is set
    // Then: The expense is flagged as recurring
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

    // Given: A recorded equipment lease expense of $500
    // When: A monthly recurrence pattern with an end date of December 31, 2024 is set
    // Then: The expense is flagged as recurring with a finite duration
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

    // Given: An empty expense index for a site
    // When: A $250 utilities expense is registered in the index
    // Then: The expense appears in query results
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

    // Given: An empty expense index for a site
    // When: Three expenses (rent, utilities, supplies) are registered
    // Then: The index contains all three expenses
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

    // Given: Three expenses dated Jan 1, Jan 15, and Feb 1
    // When: The index is queried for the date range Jan 10 through Jan 31
    // Then: Only the Jan 15 expense is returned
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

    // Given: Three expenses dated Jan 1, Jan 15, and Feb 1
    // When: The index is queried with only a from-date of Jan 10
    // Then: The two expenses on or after Jan 10 are returned
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

    // Given: Three expenses dated Jan 1, Jan 15, and Feb 1
    // When: The index is queried with only a to-date of Jan 20
    // Then: The two expenses on or before Jan 20 are returned
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

    // Given: An index with rent, two utilities, and supplies expenses
    // When: The index is queried filtering by the Utilities category
    // Then: Only the two utilities expenses are returned totaling $550
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

    // Given: An index with expenses in Pending, Approved, and Paid statuses
    // When: The index is queried filtering by Pending status
    // Then: Only the two pending expenses are returned totaling $500
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

    // Given: An index with expenses from "City Power & Light", "City Water Works", and "Office Depot"
    // When: The index is queried with vendor filter "City"
    // Then: The two City-prefixed vendor expenses are returned
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

    // Given: An index with expenses from "STAPLES" and "staples office" (mixed case)
    // When: The index is queried with vendor filter "staples" in lowercase
    // Then: Both expenses are returned regardless of vendor name casing
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

    // Given: An index with expenses of $50, $150, $250, and $500
    // When: The index is queried for amounts between $100 and $300
    // Then: Only the $150 and $250 expenses are returned
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

    // Given: An index with expenses of $50, $150, and $250
    // When: The index is queried with a minimum amount of $100
    // Then: Only the two expenses at or above $100 are returned
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

    // Given: An index with expenses of $50, $150, and $250
    // When: The index is queried with a maximum amount of $200
    // Then: Only the two expenses at or below $200 are returned
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

    // Given: An index with 10 expenses registered on consecutive dates
    // When: The index is queried with skip 3 and take 3 for pagination
    // Then: 3 expenses are returned and the total count remains 10
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

    // Given: An index with only 2 expenses
    // When: The index is queried with skip 10 (beyond total count)
    // Then: An empty result set is returned with total count of 2
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

    // Given: An index with 60 registered expenses
    // When: The index is queried with no pagination parameters
    // Then: The default page size of 50 is applied, returning 50 expenses with total count of 60
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

    // Given: An index with expenses of various categories, statuses, and dates
    // When: The index is queried combining Utilities category, Approved status, and January date range filters
    // Then: Only the single expense matching all three criteria is returned
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

    // Given: An index with expenses across rent, utilities, and supplies categories in January
    // When: Category totals are requested for January
    // Then: Totals are grouped by category with correct amounts and counts (e.g., utilities $550 from 2 expenses)
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

    // Given: An index with an approved, a voided, and a rejected utilities expense
    // When: Category totals are requested for January
    // Then: Only the approved expense is counted, excluding voided and rejected entries
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

    // Given: An index with three monthly rent expenses in Jan, Feb, and Mar
    // When: Category totals are requested for the date range Jan 15 through Feb 15
    // Then: Only the February rent expense falls within the range
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

    // Given: An index with supplies ($100), rent ($5,000), and utilities ($300) expenses
    // When: Category totals are requested for January
    // Then: Categories are ordered by total amount descending: rent, utilities, supplies
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

    // Given: An index with paid ($5,000), approved ($300), and pending ($150) expenses in January
    // When: The total is requested for the January date range
    // Then: All non-voided, non-rejected expenses are summed to $5,450
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

    // Given: An index with a paid ($500), a voided ($300), and a rejected ($200) expense
    // When: The total is requested for January
    // Then: Only the paid expense of $500 is counted, excluding voided and rejected
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

    // Given: An index with expenses on Jan 1 ($1,000), Jan 15 ($2,000), and Feb 1 ($3,000)
    // When: The total is requested for Jan 10 through Jan 31
    // Then: Only the Jan 15 expense of $2,000 is included in the total
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

    // Given: An index with a January expense but no expenses in June
    // When: The total is requested for the June date range
    // Then: The total is zero since no expenses fall within the range
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

    // Given: An index with two registered expenses
    // When: One expense is removed by its ID
    // Then: Only the remaining expense is present in query results
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

    // Given: An empty expense index
    // When: A removal is attempted for a non-existent expense ID
    // Then: The operation completes without error (no-op)
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

    // Given: An index with a $500 and a $300 paid expense
    // When: The $500 expense is removed from the index
    // Then: The total for the period drops to $300
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

    // Given: A pending expense of $100 registered in the index
    // When: The expense is updated to $150 with Approved status
    // Then: The index reflects the updated amount and status
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

    // Given: An empty expense index
    // When: An update is attempted for a non-existent expense ID
    // Then: No expense is added to the index (no-op)
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

    // Given: An empty expense index with no registered expenses
    // When: The index is queried with no filters
    // Then: The result has zero total count, zero total amount, and an empty expense list
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

    // Given: An empty expense index with no registered expenses
    // When: Category totals are requested for the full year
    // Then: An empty list of category totals is returned
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

    // Given: An empty expense index with no registered expenses
    // When: The total is requested for the full year
    // Then: The total is zero
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
