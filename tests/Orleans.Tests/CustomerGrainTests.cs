using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
public class CustomerGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CustomerGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ICustomerGrain> CreateCustomerAsync(Guid orgId, Guid customerId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
        await grain.CreateAsync(new CreateCustomerCommand(orgId, "John", "Doe", "john@example.com", "+1234567890"));
        return grain;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCustomer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));

        // Act
        var result = await grain.CreateAsync(new CreateCustomerCommand(orgId, "Jane", "Smith", "jane@example.com"));

        // Assert
        result.Id.Should().Be(customerId);
        result.DisplayName.Should().Be("Jane Smith");
        var state = await grain.GetStateAsync();
        state.Contact.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCustomer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdateAsync(new UpdateCustomerCommand(FirstName: "Johnny", LastName: "Updated"));

        // Assert
        var state = await grain.GetStateAsync();
        state.FirstName.Should().Be("Johnny");
        state.DisplayName.Should().Be("Johnny Updated");
    }

    [Fact]
    public async Task EnrollInLoyaltyAsync_ShouldEnrollCustomer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(programId, "MEM001", tierId, "Bronze"));

        // Assert
        var isLoyalty = await grain.IsLoyaltyMemberAsync();
        isLoyalty.Should().BeTrue();

        var state = await grain.GetStateAsync();
        state.Loyalty!.MemberNumber.Should().Be("MEM001");
        state.Loyalty.TierName.Should().Be("Bronze");
    }

    [Fact]
    public async Task EarnPointsAsync_ShouldAddPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Act
        var result = await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase", null, null, 50m));

        // Assert
        result.NewBalance.Should().Be(100);
        result.LifetimePoints.Should().Be(100);
    }

    [Fact]
    public async Task RedeemPointsAsync_ShouldDeductPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Act
        var result = await grain.RedeemPointsAsync(new RedeemPointsCommand(30, Guid.NewGuid(), "Reward redemption"));

        // Assert
        result.NewBalance.Should().Be(70);
        result.LifetimePoints.Should().Be(100); // Lifetime unchanged
    }

    [Fact]
    public async Task RedeemPointsAsync_InsufficientPoints_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(50, "Purchase"));

        // Act
        var act = () => grain.RedeemPointsAsync(new RedeemPointsCommand(100, Guid.NewGuid(), "Too much"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Insufficient points");
    }

    [Fact]
    public async Task IssueRewardAsync_ShouldIssueReward()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Act
        var result = await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Free Coffee", 50, DateTime.UtcNow.AddDays(30)));

        // Assert
        result.RewardId.Should().NotBeEmpty();
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().HaveCount(1);
        rewards[0].Name.Should().Be("Free Coffee");

        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(50); // 100 - 50
    }

    [Fact]
    public async Task RedeemRewardAsync_ShouldRedeemReward()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));
        var reward = await grain.IssueRewardAsync(new IssueRewardCommand(Guid.NewGuid(), "Free Coffee", 50, DateTime.UtcNow.AddDays(30)));

        // Act
        await grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordVisitAsync_ShouldUpdateStats()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 75.50m));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 24.50m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Stats.TotalVisits.Should().Be(2);
        state.Stats.TotalSpend.Should().Be(100m);
        state.Stats.AverageCheck.Should().Be(50m);
        state.Stats.LastVisitSiteId.Should().Be(siteId);
    }

    [Fact]
    public async Task AddTagAsync_ShouldAddTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddTagAsync("VIP");
        await grain.AddTagAsync("Regular");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().Contain("VIP");
        state.Tags.Should().Contain("Regular");
    }

    [Fact]
    public async Task AddNoteAsync_ShouldAddNote()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddNoteAsync("Prefers window seating", staffId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Notes.Should().HaveCount(1);
        state.Notes[0].Content.Should().Be("Prefers window seating");
    }

    [Fact]
    public async Task PromoteTierAsync_ShouldUpdateTier()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var silverTierId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Act
        await grain.PromoteTierAsync(silverTierId, "Silver", 500);

        // Assert
        var state = await grain.GetStateAsync();
        state.Loyalty!.TierId.Should().Be(silverTierId);
        state.Loyalty.TierName.Should().Be("Silver");
    }

    [Fact]
    public async Task AnonymizeAsync_ShouldAnonymizeData()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AnonymizeAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.FirstName.Should().Be("REDACTED");
        state.LastName.Should().Be("REDACTED");
        state.DisplayName.Should().Be("REDACTED");
        state.Status.Should().Be(CustomerStatus.Inactive);
    }
}
