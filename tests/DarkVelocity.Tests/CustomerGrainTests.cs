using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    // ==================== Visit History Tests ====================

    [Fact]
    public async Task RecordVisitAsync_ShouldAddToVisitHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.RecordVisitAsync(new RecordVisitCommand(
            siteId,
            orderId,
            75.50m,
            SiteName: "Downtown Restaurant",
            BookingId: bookingId,
            PartySize: 4,
            PointsEarned: 75,
            Notes: "Birthday dinner"));

        // Assert
        var history = await grain.GetVisitHistoryAsync();
        history.Should().HaveCount(1);
        history[0].SiteId.Should().Be(siteId);
        history[0].SiteName.Should().Be("Downtown Restaurant");
        history[0].OrderId.Should().Be(orderId);
        history[0].BookingId.Should().Be(bookingId);
        history[0].SpendAmount.Should().Be(75.50m);
        history[0].PartySize.Should().Be(4);
        history[0].PointsEarned.Should().Be(75);
        history[0].Notes.Should().Be("Birthday dinner");
    }

    [Fact]
    public async Task GetVisitHistoryAsync_ShouldReturnVisitsInReverseChronologicalOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record multiple visits
        for (int i = 1; i <= 5; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                i * 10m,
                SiteName: $"Visit {i}"));
        }

        // Act
        var history = await grain.GetVisitHistoryAsync();

        // Assert - most recent should be first
        history.Should().HaveCount(5);
        history[0].SiteName.Should().Be("Visit 5");
        history[4].SiteName.Should().Be("Visit 1");
    }

    [Fact]
    public async Task GetVisitHistoryAsync_ShouldRespectLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record 10 visits
        for (int i = 0; i < 10; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                10m));
        }

        // Act
        var history = await grain.GetVisitHistoryAsync(limit: 5);

        // Assert
        history.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetVisitHistoryAsync_ShouldLimitTo50Visits()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record 60 visits
        for (int i = 0; i < 60; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                10m));
        }

        // Act
        var history = await grain.GetVisitHistoryAsync();

        // Assert - should be limited to 50
        history.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetVisitsBySiteAsync_ShouldFilterBySite()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record visits at different sites
        await grain.RecordVisitAsync(new RecordVisitCommand(siteA, Guid.NewGuid(), 20m, SiteName: "Site A"));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteB, Guid.NewGuid(), 30m, SiteName: "Site B"));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteA, Guid.NewGuid(), 25m, SiteName: "Site A"));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteB, Guid.NewGuid(), 35m, SiteName: "Site B"));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteA, Guid.NewGuid(), 30m, SiteName: "Site A"));

        // Act
        var siteAVisits = await grain.GetVisitsBySiteAsync(siteA);
        var siteBVisits = await grain.GetVisitsBySiteAsync(siteB);

        // Assert
        siteAVisits.Should().HaveCount(3);
        siteAVisits.Should().OnlyContain(v => v.SiteId == siteA);

        siteBVisits.Should().HaveCount(2);
        siteBVisits.Should().OnlyContain(v => v.SiteId == siteB);
    }

    [Fact]
    public async Task GetLastVisitAsync_ShouldReturnMostRecentVisit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        await grain.RecordVisitAsync(new RecordVisitCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            20m,
            SiteName: "First Visit"));
        await grain.RecordVisitAsync(new RecordVisitCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            30m,
            SiteName: "Second Visit"));
        await grain.RecordVisitAsync(new RecordVisitCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            40m,
            SiteName: "Last Visit"));

        // Act
        var lastVisit = await grain.GetLastVisitAsync();

        // Assert
        lastVisit.Should().NotBeNull();
        lastVisit!.SiteName.Should().Be("Last Visit");
        lastVisit.SpendAmount.Should().Be(40m);
    }

    [Fact]
    public async Task GetLastVisitAsync_WhenNoVisits_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var lastVisit = await grain.GetLastVisitAsync();

        // Assert
        lastVisit.Should().BeNull();
    }

    // ==================== Preferences Tests ====================

    [Fact]
    public async Task UpdatePreferencesAsync_ShouldUpdateAllPreferences()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdatePreferencesAsync(new UpdatePreferencesCommand(
            DietaryRestrictions: new List<string> { "Vegetarian", "Gluten-Free" },
            Allergens: new List<string> { "Peanuts", "Shellfish" },
            SeatingPreference: "Window booth",
            Notes: "Always prefers sparkling water"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.DietaryRestrictions.Should().BeEquivalentTo(new[] { "Vegetarian", "Gluten-Free" });
        state.Preferences.Allergens.Should().BeEquivalentTo(new[] { "Peanuts", "Shellfish" });
        state.Preferences.SeatingPreference.Should().Be("Window booth");
        state.Preferences.Notes.Should().Be("Always prefers sparkling water");
    }

    [Fact]
    public async Task UpdatePreferencesAsync_ShouldPartiallyUpdate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Set initial preferences
        await grain.UpdatePreferencesAsync(new UpdatePreferencesCommand(
            DietaryRestrictions: new List<string> { "Vegetarian" },
            SeatingPreference: "Patio"));

        // Act - only update seating preference
        await grain.UpdatePreferencesAsync(new UpdatePreferencesCommand(
            SeatingPreference: "Indoor"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.DietaryRestrictions.Should().BeEquivalentTo(new[] { "Vegetarian" }); // Preserved
        state.Preferences.SeatingPreference.Should().Be("Indoor"); // Updated
    }

    [Fact]
    public async Task AddDietaryRestrictionAsync_ShouldAddRestriction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddDietaryRestrictionAsync("Vegan");
        await grain.AddDietaryRestrictionAsync("Halal");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.DietaryRestrictions.Should().Contain("Vegan");
        state.Preferences.DietaryRestrictions.Should().Contain("Halal");
    }

    [Fact]
    public async Task AddDietaryRestrictionAsync_ShouldNotDuplicate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddDietaryRestrictionAsync("Vegan");
        await grain.AddDietaryRestrictionAsync("Vegan");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.DietaryRestrictions.Count(r => r == "Vegan").Should().Be(1);
    }

    [Fact]
    public async Task RemoveDietaryRestrictionAsync_ShouldRemoveRestriction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        await grain.AddDietaryRestrictionAsync("Vegan");
        await grain.AddDietaryRestrictionAsync("Kosher");

        // Act
        await grain.RemoveDietaryRestrictionAsync("Vegan");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.DietaryRestrictions.Should().NotContain("Vegan");
        state.Preferences.DietaryRestrictions.Should().Contain("Kosher");
    }

    [Fact]
    public async Task AddAllergenAsync_ShouldAddAllergen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddAllergenAsync("Peanuts");
        await grain.AddAllergenAsync("Tree Nuts");
        await grain.AddAllergenAsync("Dairy");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.Allergens.Should().HaveCount(3);
        state.Preferences.Allergens.Should().Contain("Peanuts");
        state.Preferences.Allergens.Should().Contain("Tree Nuts");
        state.Preferences.Allergens.Should().Contain("Dairy");
    }

    [Fact]
    public async Task AddAllergenAsync_ShouldNotDuplicate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddAllergenAsync("Peanuts");
        await grain.AddAllergenAsync("Peanuts");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.Allergens.Count(a => a == "Peanuts").Should().Be(1);
    }

    [Fact]
    public async Task RemoveAllergenAsync_ShouldRemoveAllergen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        await grain.AddAllergenAsync("Peanuts");
        await grain.AddAllergenAsync("Shellfish");

        // Act
        await grain.RemoveAllergenAsync("Peanuts");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.Allergens.Should().NotContain("Peanuts");
        state.Preferences.Allergens.Should().Contain("Shellfish");
    }

    [Fact]
    public async Task SetSeatingPreferenceAsync_ShouldSetPreference()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.SetSeatingPreferenceAsync("Quiet corner booth");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.SeatingPreference.Should().Be("Quiet corner booth");
    }

    [Fact]
    public async Task SetSeatingPreferenceAsync_ShouldOverwriteExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        await grain.SetSeatingPreferenceAsync("Window seat");

        // Act
        await grain.SetSeatingPreferenceAsync("Bar seating");

        // Assert
        var state = await grain.GetStateAsync();
        state.Preferences.SeatingPreference.Should().Be("Bar seating");
    }

    // ==================== Tag Tests ====================

    [Fact]
    public async Task RemoveTagAsync_ShouldRemoveTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        await grain.AddTagAsync("VIP");
        await grain.AddTagAsync("Regular");

        // Act
        await grain.RemoveTagAsync("VIP");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Should().NotContain("VIP");
        state.Tags.Should().Contain("Regular");
    }

    [Fact]
    public async Task AddTagAsync_ShouldNotDuplicate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.AddTagAsync("VIP");
        await grain.AddTagAsync("VIP");

        // Assert
        var state = await grain.GetStateAsync();
        state.Tags.Count(t => t == "VIP").Should().Be(1);
    }

    // ==================== Referral Tests ====================

    [Fact]
    public async Task SetReferralCodeAsync_ShouldSetCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.SetReferralCodeAsync("JOHN2024");

        // Assert
        var state = await grain.GetStateAsync();
        state.ReferralCode.Should().Be("JOHN2024");
    }

    [Fact]
    public async Task SetReferredByAsync_ShouldSetReferrer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var referrerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.SetReferredByAsync(referrerId);

        // Assert
        var state = await grain.GetStateAsync();
        state.ReferredBy.Should().Be(referrerId);
    }

    [Fact]
    public async Task IncrementReferralCountAsync_ShouldIncrementCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.IncrementReferralCountAsync();
        await grain.IncrementReferralCountAsync();
        await grain.IncrementReferralCountAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.SuccessfulReferrals.Should().Be(3);
    }

    // ==================== Merge Tests ====================

    [Fact]
    public async Task MergeFromAsync_ShouldTrackMergedCustomers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sourceCustomerId1 = Guid.NewGuid();
        var sourceCustomerId2 = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.MergeFromAsync(sourceCustomerId1);
        await grain.MergeFromAsync(sourceCustomerId2);

        // Assert
        var state = await grain.GetStateAsync();
        state.MergedFrom.Should().HaveCount(2);
        state.MergedFrom.Should().Contain(sourceCustomerId1);
        state.MergedFrom.Should().Contain(sourceCustomerId2);
    }

    // ==================== Delete Tests ====================

    [Fact]
    public async Task DeleteAsync_ShouldMarkAsInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.DeleteAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(CustomerStatus.Inactive);
    }
}
