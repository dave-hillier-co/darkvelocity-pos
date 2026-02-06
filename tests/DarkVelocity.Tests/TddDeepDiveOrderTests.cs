using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TddDeepDiveOrderTests
{
    private readonly TestClusterFixture _fixture;

    public TddDeepDiveOrderTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrderGrain GetOrderGrain(Guid orgId, Guid siteId, Guid orderId)
        => _fixture.Cluster.GrainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));

    // -----------------------------------------------------------------------
    // 1. ServiceChargeTaxOnZeroSubtotal
    // -----------------------------------------------------------------------
    // Given: an order where all lines are voided (subtotal=0) but a service charge exists
    // When: totals are recalculated
    // Then: no division by zero / NaN in tax calculation
    //       OrderState.cs:337 checks Subtotal > 0 before computing weighted tax rate
    [Fact]
    public async Task ServiceChargeTaxOnZeroSubtotal_ShouldNotProduceNaNOrDivideByZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        var lineResult = await grain.AddLineAsync(
            new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m, TaxRate: 10.0m));

        // Add a taxable service charge while subtotal is still $10
        await grain.AddServiceChargeAsync("Gratuity", 18.0m, isTaxable: true);

        // Void the only line so subtotal drops to 0
        var lines = await grain.GetLinesAsync();
        await grain.VoidLineAsync(new VoidLineCommand(lines[0].Id, userId, "Test void"));

        // Act - recalculate totals with zero subtotal but existing service charge
        var totals = await grain.RecalculateTotalsAsync();

        // Assert
        totals.Subtotal.Should().Be(0m);
        totals.TaxTotal.Should().Be(0m, "service charge tax should be zero when subtotal is zero (no weighted rate to compute)");
        totals.GrandTotal.Should().NotBe(decimal.MinValue, "grand total should be a valid number");
        // Ensure no NaN-like behavior: all values should be finite decimals
        (totals.TaxTotal >= 0m || totals.TaxTotal <= 0m).Should().BeTrue("TaxTotal must not be NaN");
    }

    // -----------------------------------------------------------------------
    // 2. SplitByAmounts_ZeroGrandTotal_ShouldNotDivideByZero
    // -----------------------------------------------------------------------
    // Given: an order with a $10 item and a $10 fixed discount (GrandTotal=0)
    // When: CalculateSplitByAmountsAsync is called with amounts
    // Then: it should not throw a DivideByZeroException
    //       The issue is at OrderGrain.cs:1291 where taxRatio = State.TaxTotal / State.GrandTotal
    //       will divide by zero when GrandTotal is 0
    [Fact]
    public async Task SplitByAmounts_ZeroGrandTotal_ShouldNotDivideByZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m));

        // Apply a $10 fixed discount to bring GrandTotal to 0
        await grain.ApplyDiscountAsync(
            new ApplyDiscountCommand("Full Comp", DiscountType.FixedAmount, 10.00m, userId));

        var totals = await grain.GetTotalsAsync();
        totals.GrandTotal.Should().Be(0m, "precondition: discount cancels out the subtotal");

        // Act - this should not throw DivideByZeroException
        var act = () => grain.CalculateSplitByAmountsAsync(new List<decimal> { 0m });

        // Assert
        await act.Should().NotThrowAsync<DivideByZeroException>(
            "splitting a zero-total order should not cause division by zero at taxRatio calculation");
    }

    // -----------------------------------------------------------------------
    // 3. SplitByPeople_ShareTotalsSumToBalanceDue
    // -----------------------------------------------------------------------
    // Given: an order with $10.00 total (no tax)
    // When: split by 3 people
    // Then: all shares' Total values must sum exactly to BalanceDue ($10.00)
    //       Verify baseShare = $3.33, remainder = $0.01, first share = $3.34
    [Fact]
    public async Task SplitByPeople_ShareTotalsSumToBalanceDue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m));

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(3);

        // Assert
        result.Shares.Should().HaveCount(3);
        result.BalanceDue.Should().Be(10.00m);

        // baseShare = floor(10.00 / 3 * 100) / 100 = floor(333.33) / 100 = 3.33
        // remainder = 10.00 - (3.33 * 3) = 10.00 - 9.99 = 0.01
        // First share gets 3.33 + 0.01 = 3.34
        result.Shares[0].Total.Should().Be(3.34m, "first share should include the remainder penny");
        result.Shares[1].Total.Should().Be(3.33m);
        result.Shares[2].Total.Should().Be(3.33m);

        var sumOfTotals = result.Shares.Sum(s => s.Total);
        sumOfTotals.Should().Be(result.BalanceDue,
            "the sum of all share totals must exactly equal the balance due to avoid over/under collection");
    }

    // -----------------------------------------------------------------------
    // 4. SplitByPeople_TaxSharesSumToTaxTotal
    // -----------------------------------------------------------------------
    // Given: an order with items that have 10% tax
    // When: split by 3 people
    // Then: all shares' Tax values must sum to TaxTotal
    [Fact]
    public async Task SplitByPeople_TaxSharesSumToTaxTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        // $10.00 item with 10% tax => TaxAmount = $1.00 per line
        // Subtotal = $10.00, TaxTotal = $1.00, GrandTotal = $11.00
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m, TaxRate: 10.0m));

        var totals = await grain.GetTotalsAsync();
        totals.TaxTotal.Should().BeGreaterThan(0m, "precondition: tax must be nonzero for this test");

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(3);

        // Assert
        result.Shares.Should().HaveCount(3);

        var sumOfTax = result.Shares.Sum(s => s.Tax);
        sumOfTax.Should().Be(totals.TaxTotal,
            "the sum of all shares' tax must exactly equal the order's TaxTotal to prevent rounding leakage");
    }

    // -----------------------------------------------------------------------
    // 5. OrderDiscount_ExceedingSubtotal_GrandTotalNotNegative
    // -----------------------------------------------------------------------
    // Given: an order with $10 subtotal
    // When: a $15 fixed discount is applied
    // Then: GrandTotal should not go below 0
    //       OrderGrain.cs:803 computes discountAmount = command.Value for FixedAmount without capping
    //       OrderState.cs:346 does GrandTotal = Subtotal - DiscountTotal + ServiceChargeTotal + TaxTotal
    //       which could go negative
    [Fact]
    public async Task OrderDiscount_ExceedingSubtotal_GrandTotalNotNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 1, 10.00m));

        // Act - apply a discount exceeding the subtotal
        await grain.ApplyDiscountAsync(
            new ApplyDiscountCommand("Overly Generous", DiscountType.FixedAmount, 15.00m, userId));

        var totals = await grain.GetTotalsAsync();

        // Assert
        // A $15 discount on a $10 subtotal means DiscountTotal = $15
        // GrandTotal = 10 - 15 + 0 + 0 = -5 (this is the suspected bug)
        // The system should either cap the discount at subtotal or ensure GrandTotal >= 0
        totals.GrandTotal.Should().BeGreaterThanOrEqualTo(0m,
            "a discount exceeding the subtotal should not result in a negative grand total - " +
            "the customer should never be owed money for placing an order");
    }

    // -----------------------------------------------------------------------
    // 6. ServiceChargeTax_WeightedRate_CalculatesCorrectly
    // -----------------------------------------------------------------------
    // Given: an order with items at different tax rates (one at 10%, one at 20%)
    // When: a taxable service charge is added
    // Then: the service charge tax should use the weighted average of the tax rates
    //       based on line totals
    [Fact]
    public async Task ServiceChargeTax_WeightedRate_CalculatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));

        // Item 1: $100 at 10% tax => line tax = $10.00
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Steak", 1, 100.00m, TaxRate: 10.0m));
        // Item 2: $100 at 20% tax => line tax = $20.00
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Wine", 1, 100.00m, TaxRate: 20.0m));

        // Subtotal = $200, line taxes = $30
        // Weighted average tax rate = (100 * 10 + 100 * 20) / 200 = 3000 / 200 = 15%

        // Act - add a 10% taxable service charge
        await grain.AddServiceChargeAsync("Service Charge", 10.0m, isTaxable: true);

        var totals = await grain.GetTotalsAsync();

        // Assert
        // Service charge amount = $200 * 10% = $20
        totals.ServiceChargeTotal.Should().Be(20.00m);

        // Service charge tax = $20 * (15% / 100) = $20 * 0.15 = $3.00
        // Total tax = line taxes ($30) + service charge tax ($3) = $33
        var expectedLineTax = 30.00m;     // $10 + $20
        var expectedServiceChargeTax = 3.00m; // $20 * 15%
        var expectedTotalTax = expectedLineTax + expectedServiceChargeTax;

        totals.TaxTotal.Should().Be(expectedTotalTax,
            "service charge tax should use weighted average rate (15%) of the two line tax rates (10% and 20%)");
    }

    // -----------------------------------------------------------------------
    // 7. SplitByPeople_SinglePerson_ReturnsFullBalance
    // -----------------------------------------------------------------------
    // Given: an order with balance due
    // When: split by 1 person
    // Then: the single share equals the full balance
    [Fact]
    public async Task SplitByPeople_SinglePerson_ReturnsFullBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetOrderGrain(orgId, siteId, orderId);

        await grain.CreateAsync(new CreateOrderCommand(orgId, siteId, userId, OrderType.DineIn));
        await grain.AddLineAsync(new AddLineCommand(Guid.NewGuid(), "Burger", 2, 12.99m, TaxRate: 10.0m));

        var totals = await grain.GetTotalsAsync();
        totals.BalanceDue.Should().BeGreaterThan(0m, "precondition: order must have a positive balance");

        // Act
        var result = await grain.CalculateSplitByPeopleAsync(1);

        // Assert
        result.Shares.Should().HaveCount(1);
        result.Shares[0].Total.Should().Be(totals.BalanceDue,
            "a single-person split should return the entire balance as one share");
        result.Shares[0].Tax.Should().Be(totals.TaxTotal,
            "the single share's tax should equal the full tax total");
        result.IsValid.Should().BeTrue();
    }
}
