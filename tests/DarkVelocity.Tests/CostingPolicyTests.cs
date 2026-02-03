using DarkVelocity.Host.Costing;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Trait("Category", "Unit")]
public class CostingPolicyTests
{
    private static List<StockBatch> CreateTestBatches()
    {
        return
        [
            new StockBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = "BATCH001",
                ReceivedDate = DateTime.UtcNow.AddDays(-10),
                Quantity = 100,
                OriginalQuantity = 100,
                UnitCost = 5.00m,
                TotalCost = 500.00m,
                Status = BatchStatus.Active
            },
            new StockBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = "BATCH002",
                ReceivedDate = DateTime.UtcNow.AddDays(-5),
                Quantity = 100,
                OriginalQuantity = 100,
                UnitCost = 7.00m,
                TotalCost = 700.00m,
                Status = BatchStatus.Active
            },
            new StockBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = "BATCH003",
                ReceivedDate = DateTime.UtcNow.AddDays(-1),
                Quantity = 50,
                OriginalQuantity = 50,
                UnitCost = 8.00m,
                TotalCost = 400.00m,
                Status = BatchStatus.Active
            }
        ];
    }

    [Fact]
    public void FifoCostingPolicy_ShouldConsumeOldestFirst()
    {
        // Arrange
        var policy = new FifoCostingPolicy();
        var batches = CreateTestBatches();

        // Act
        var result = policy.CalculateCost(batches, 150, "lb", DateTime.UtcNow);

        // Assert
        result.Quantity.Should().Be(150);
        result.Method.Should().Be(CostingMethod.FIFO);
        result.BatchBreakdown.Should().HaveCount(2);

        // Should consume all of BATCH001 first (100 units @ $5)
        result.BatchBreakdown![0].BatchNumber.Should().Be("BATCH001");
        result.BatchBreakdown[0].Quantity.Should().Be(100);
        result.BatchBreakdown[0].UnitCost.Should().Be(5.00m);

        // Then 50 from BATCH002 (50 units @ $7)
        result.BatchBreakdown[1].BatchNumber.Should().Be("BATCH002");
        result.BatchBreakdown[1].Quantity.Should().Be(50);
        result.BatchBreakdown[1].UnitCost.Should().Be(7.00m);

        // Total cost: (100 * 5) + (50 * 7) = 500 + 350 = 850
        result.TotalCost.Should().Be(850m);
    }

    [Fact]
    public void LifoCostingPolicy_ShouldConsumeNewestFirst()
    {
        // Arrange
        var policy = new LifoCostingPolicy();
        var batches = CreateTestBatches();

        // Act
        var result = policy.CalculateCost(batches, 150, "lb", DateTime.UtcNow);

        // Assert
        result.Quantity.Should().Be(150);
        result.Method.Should().Be(CostingMethod.LIFO);
        result.BatchBreakdown.Should().HaveCount(2);

        // Should consume all of BATCH003 first (50 units @ $8)
        result.BatchBreakdown![0].BatchNumber.Should().Be("BATCH003");
        result.BatchBreakdown[0].Quantity.Should().Be(50);
        result.BatchBreakdown[0].UnitCost.Should().Be(8.00m);

        // Then 100 from BATCH002 (100 units @ $7)
        result.BatchBreakdown[1].BatchNumber.Should().Be("BATCH002");
        result.BatchBreakdown[1].Quantity.Should().Be(100);
        result.BatchBreakdown[1].UnitCost.Should().Be(7.00m);

        // Total cost: (50 * 8) + (100 * 7) = 400 + 700 = 1100
        result.TotalCost.Should().Be(1100m);
    }

    [Fact]
    public void WeightedAverageCostingPolicy_ShouldUseWeightedAverage()
    {
        // Arrange
        var policy = new WeightedAverageCostingPolicy();
        var batches = CreateTestBatches();

        // Act
        var result = policy.CalculateCost(batches, 150, "lb", DateTime.UtcNow);

        // Assert
        result.Quantity.Should().Be(150);
        result.Method.Should().Be(CostingMethod.WAC);

        // WAC = (100*5 + 100*7 + 50*8) / 250 = (500 + 700 + 400) / 250 = 6.40
        result.UnitCost.Should().Be(6.40m);
        result.TotalCost.Should().Be(150 * 6.40m);
    }

    [Fact]
    public void StandardCostingPolicy_ShouldUseMostRecentCost()
    {
        // Arrange
        var policy = new StandardCostingPolicy();
        var batches = CreateTestBatches();

        // Act
        var result = policy.CalculateCost(batches, 150, "lb", DateTime.UtcNow);

        // Assert
        result.Quantity.Should().Be(150);
        result.Method.Should().Be(CostingMethod.Standard);

        // Should use the most recent batch cost ($8)
        result.UnitCost.Should().Be(8.00m);
        result.TotalCost.Should().Be(1200m);
    }

    [Fact]
    public void StandardCostingPolicy_WithLookup_ShouldUseCustomLookup()
    {
        // Arrange
        var ingredientId = Guid.NewGuid();
        var standardCost = 6.50m;
        var policy = new StandardCostingPolicy(id => standardCost);

        // Act
        var result = policy.CalculateCost(ingredientId, 100, "lb", 5.00m, DateTime.UtcNow);

        // Assert
        result.UnitCost.Should().Be(standardCost);
        result.TotalCost.Should().Be(650m);
    }

    [Fact]
    public void FifoCostingPolicy_WithInsufficientStock_ShouldReturnPartial()
    {
        // Arrange
        var policy = new FifoCostingPolicy();
        var batches = CreateTestBatches(); // Total: 250 units

        // Act
        var result = policy.CalculateCost(batches, 300, "lb", DateTime.UtcNow);

        // Assert
        result.Quantity.Should().Be(250); // Only 250 available
        result.BatchBreakdown.Should().HaveCount(3); // All batches consumed
    }

    [Fact]
    public void WeightedAverageCostingPolicy_CalculateNewWAC_ShouldBeCorrect()
    {
        // Arrange
        var existingQty = 100m;
        var existingWAC = 5.00m;
        var newQty = 50m;
        var newCost = 8.00m;

        // Act
        var newWAC = WeightedAverageCostingPolicy.CalculateNewWAC(
            existingQty, existingWAC, newQty, newCost);

        // Assert
        // (100 * 5 + 50 * 8) / 150 = (500 + 400) / 150 = 6.00
        newWAC.Should().Be(6.00m);
    }

    [Fact]
    public void CostingPolicyFactory_ShouldCreateCorrectPolicy()
    {
        // Act & Assert
        CostingPolicyFactory.Create(CostingMethod.FIFO).Should().BeOfType<FifoCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.LIFO).Should().BeOfType<LifoCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.WAC).Should().BeOfType<WeightedAverageCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.Standard).Should().BeOfType<StandardCostingPolicy>();
    }

    [Fact]
    public void FifoCostingPolicy_WithExhaustedBatches_ShouldSkipThem()
    {
        // Arrange
        var policy = new FifoCostingPolicy();
        var batches = new List<StockBatch>
        {
            new StockBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = "BATCH001",
                ReceivedDate = DateTime.UtcNow.AddDays(-10),
                Quantity = 0, // Exhausted
                OriginalQuantity = 100,
                UnitCost = 5.00m,
                TotalCost = 500.00m,
                Status = BatchStatus.Exhausted
            },
            new StockBatch
            {
                Id = Guid.NewGuid(),
                BatchNumber = "BATCH002",
                ReceivedDate = DateTime.UtcNow.AddDays(-5),
                Quantity = 100,
                OriginalQuantity = 100,
                UnitCost = 7.00m,
                TotalCost = 700.00m,
                Status = BatchStatus.Active
            }
        };

        // Act
        var result = policy.CalculateCost(batches, 50, "lb", DateTime.UtcNow);

        // Assert
        result.BatchBreakdown.Should().HaveCount(1);
        result.BatchBreakdown![0].BatchNumber.Should().Be("BATCH002");
        result.UnitCost.Should().Be(7.00m);
    }

    [Fact]
    public void AllPolicies_ShouldIncludeAsOfDate()
    {
        // Arrange
        var batches = CreateTestBatches();
        var asOfDate = DateTime.UtcNow;

        // Act & Assert
        var fifo = new FifoCostingPolicy().CalculateCost(batches, 50, "lb", asOfDate);
        fifo.AsOfDate.Should().Be(asOfDate);

        var lifo = new LifoCostingPolicy().CalculateCost(batches, 50, "lb", asOfDate);
        lifo.AsOfDate.Should().Be(asOfDate);

        var wac = new WeightedAverageCostingPolicy().CalculateCost(batches, 50, "lb", asOfDate);
        wac.AsOfDate.Should().Be(asOfDate);

        var standard = new StandardCostingPolicy().CalculateCost(batches, 50, "lb", asOfDate);
        standard.AsOfDate.Should().Be(asOfDate);
    }
}
