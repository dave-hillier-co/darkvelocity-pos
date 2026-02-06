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

    // Given: Three stock batches received on different dates ($5, $7, $8 per unit)
    // When: 150 units are consumed using FIFO costing
    // Then: All 100 units of the oldest batch ($5) are consumed first, then 50 from the next ($7), totaling $850
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

    // Given: Three stock batches received on different dates ($5, $7, $8 per unit)
    // When: 150 units are consumed using LIFO costing
    // Then: All 50 units of the newest batch ($8) are consumed first, then 100 from the next ($7), totaling $1,100
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

    // Given: Three stock batches totaling 250 units with a weighted average cost of $6.40
    // When: 150 units are consumed using weighted average costing
    // Then: The unit cost is $6.40 and total cost is $960
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

    // Given: Three stock batches with the most recent batch at $8 per unit
    // When: 150 units are consumed using standard costing
    // Then: The unit cost is $8 (most recent) and total cost is $1,200
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

    // Given: A standard costing policy configured with a custom cost lookup returning $6.50
    // When: 100 units are consumed using the custom lookup
    // Then: The unit cost is $6.50 (from lookup) and total cost is $650
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

    // Given: Three stock batches totaling 250 units
    // When: 300 units are requested for FIFO consumption (exceeding available stock)
    // Then: Only 250 units are consumed across all 3 batches (partial fulfillment)
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

    // Given: An existing stock of 100 units at $5.00 WAC and a new delivery of 50 units at $8.00
    // When: The new weighted average cost is calculated after receiving the delivery
    // Then: The WAC is $6.00 based on the combined weighted total: (100*$5 + 50*$8) / 150
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

    // Given: A costing policy factory supporting FIFO, LIFO, WAC, and Standard methods
    // When: A policy is created for each costing method
    // Then: The factory returns the correct policy implementation type for each method
    [Fact]
    public void CostingPolicyFactory_ShouldCreateCorrectPolicy()
    {
        // Act & Assert
        CostingPolicyFactory.Create(CostingMethod.FIFO).Should().BeOfType<FifoCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.LIFO).Should().BeOfType<LifoCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.WAC).Should().BeOfType<WeightedAverageCostingPolicy>();
        CostingPolicyFactory.Create(CostingMethod.Standard).Should().BeOfType<StandardCostingPolicy>();
    }

    // Given: Two stock batches where the oldest (BATCH001) is fully exhausted and the second (BATCH002) has 100 units at $7
    // When: 50 units are consumed using FIFO costing
    // Then: The exhausted batch is skipped and all 50 units are drawn from BATCH002 at $7 per unit
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

    // Given: Three stock batches and a specific as-of date for cost calculation
    // When: 50 units are costed using each policy (FIFO, LIFO, WAC, Standard)
    // Then: Every costing result records the exact as-of date for audit traceability
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
