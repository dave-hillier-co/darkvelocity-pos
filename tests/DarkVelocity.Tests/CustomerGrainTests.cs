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

    // ==================== Loyalty Points Error Tests ====================

    [Fact]
    public async Task EarnPointsAsync_WithoutLoyaltyEnrollment_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        // Note: Customer is NOT enrolled in loyalty

        // Act
        var act = () => grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enrolled in loyalty*");
    }

    [Fact]
    public async Task RedeemPointsAsync_WithoutEnrollment_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        // Note: Customer is NOT enrolled in loyalty

        // Act
        var act = () => grain.RedeemPointsAsync(new RedeemPointsCommand(50, Guid.NewGuid(), "Reward"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enrolled in loyalty*");
    }

    // ==================== Reward Redemption Error Tests ====================

    [Fact]
    public async Task RedeemRewardAsync_ExpiredReward_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Issue a reward that's already expired
        var reward = await grain.IssueRewardAsync(new IssueRewardCommand(
            Guid.NewGuid(),
            "Expired Coffee",
            50,
            DateTime.UtcNow.AddDays(-1))); // Expired yesterday

        // Act
        var act = () => grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task RedeemRewardAsync_AlreadyRedeemed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));
        var reward = await grain.IssueRewardAsync(new IssueRewardCommand(
            Guid.NewGuid(),
            "Free Coffee",
            50,
            DateTime.UtcNow.AddDays(30)));

        // Redeem the reward first time
        await grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Act - Try to redeem the same reward again
        var act = () => grain.RedeemRewardAsync(new RedeemRewardCommand(reward.RewardId, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task RedeemRewardAsync_NotFound_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act - Try to redeem a reward that doesn't exist
        var act = () => grain.RedeemRewardAsync(new RedeemRewardCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ==================== Points Adjustment Tests ====================

    [Fact]
    public async Task AdjustPointsAsync_Positive_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Initial purchase"));

        // Act
        var result = await grain.AdjustPointsAsync(new AdjustPointsCommand(50, "Goodwill adjustment", Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(150);
        result.LifetimePoints.Should().Be(150); // Positive adjustments add to lifetime
    }

    [Fact]
    public async Task AdjustPointsAsync_Negative_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Initial purchase"));

        // Act
        var result = await grain.AdjustPointsAsync(new AdjustPointsCommand(-30, "Correction", Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(70);
        result.LifetimePoints.Should().Be(100); // Negative adjustments don't reduce lifetime
    }

    [Fact]
    public async Task AdjustPointsAsync_NegativeResultingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(50, "Initial purchase"));

        // Act - Try to adjust more than available balance
        var act = () => grain.AdjustPointsAsync(new AdjustPointsCommand(-100, "Over-correction", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negative balance*");
    }

    // ==================== Points and Rewards Expiry Tests ====================

    [Fact]
    public async Task ExpirePointsAsync_ShouldReduceBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(100, "Purchase"));

        // Act
        await grain.ExpirePointsAsync(30, DateTime.UtcNow);

        // Assert
        var balance = await grain.GetPointsBalanceAsync();
        balance.Should().Be(70);
    }

    [Fact]
    public async Task ExpireRewardsAsync_ShouldExpireMultiple()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));
        await grain.EarnPointsAsync(new EarnPointsCommand(200, "Purchase"));

        // Issue rewards that are already expired (negative days)
        await grain.IssueRewardAsync(new IssueRewardCommand(
            Guid.NewGuid(), "Expired Reward 1", 0, DateTime.UtcNow.AddDays(-1)));
        await grain.IssueRewardAsync(new IssueRewardCommand(
            Guid.NewGuid(), "Expired Reward 2", 0, DateTime.UtcNow.AddDays(-2)));
        // Issue one that's still valid
        await grain.IssueRewardAsync(new IssueRewardCommand(
            Guid.NewGuid(), "Valid Reward", 0, DateTime.UtcNow.AddDays(30)));

        // Act
        await grain.ExpireRewardsAsync();

        // Assert
        var availableRewards = await grain.GetAvailableRewardsAsync();
        availableRewards.Should().HaveCount(1);
        availableRewards[0].Name.Should().Be("Valid Reward");
    }

    // ==================== Enrollment Tests ====================

    [Fact]
    public async Task EnrollInLoyaltyAsync_AlreadyEnrolled_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Act - Try to enroll again
        var act = () => grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM002", Guid.NewGuid(), "Silver"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already enrolled*");
    }

    // ==================== Create Tests ====================

    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act - Try to create the same customer again
        var act = () => grain.CreateAsync(new CreateCustomerCommand(orgId, "Another", "Customer", "other@example.com"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // ==================== Update Tests ====================

    [Fact]
    public async Task UpdateAsync_Email_ShouldUpdateContactInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdateAsync(new UpdateCustomerCommand(Email: "newemail@example.com"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Contact.Email.Should().Be("newemail@example.com");
        state.Contact.Phone.Should().Be("+1234567890"); // Original phone preserved
    }

    [Fact]
    public async Task UpdateAsync_Phone_ShouldUpdateContactInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdateAsync(new UpdateCustomerCommand(Phone: "+9876543210"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Contact.Phone.Should().Be("+9876543210");
        state.Contact.Email.Should().Be("john@example.com"); // Original email preserved
    }

    // ==================== RFM Segmentation Tests ====================

    [Fact]
    public async Task CalculateRfmScoreAsync_NewCustomer_ShouldHaveLowScores()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var rfmScore = await grain.CalculateRfmScoreAsync();

        // Assert - new customer with no visits should have low scores
        rfmScore.RecencyScore.Should().Be(1); // Never visited = poor recency
        rfmScore.FrequencyScore.Should().Be(1); // No visits
        rfmScore.MonetaryScore.Should().Be(1); // No spend
        rfmScore.CombinedScore.Should().Be(3);
    }

    [Fact]
    public async Task CalculateRfmScoreAsync_FrequentHighSpender_ShouldHaveHighScores()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record many high-value visits (10+ visits, 100+ per visit = 1000+ total)
        for (int i = 0; i < 12; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // Act
        var rfmScore = await grain.CalculateRfmScoreAsync();

        // Assert - frequent high spender should have excellent scores
        rfmScore.RecencyScore.Should().BeGreaterThanOrEqualTo(4); // Recent visitor
        rfmScore.FrequencyScore.Should().Be(5); // 12 visits >= 10 = excellent
        rfmScore.MonetaryScore.Should().Be(5); // 1200 >= 1000 = excellent
    }

    [Fact]
    public async Task CalculateRfmScoreAsync_WithCustomThresholds_ShouldUseThresholds()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Record 3 visits totaling $300
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));

        // With default thresholds, $300 would be "Fair" (200-500)
        // With custom thresholds making $300 "Excellent", score should be higher
        var customThresholds = new SegmentationThresholds(
            MonetaryValueExcellent: 250m,
            MonetaryValueGood: 150m,
            MonetaryValueFair: 75m,
            MonetaryValuePoor: 25m);

        // Act
        var rfmScore = await grain.CalculateRfmScoreAsync(customThresholds);

        // Assert
        rfmScore.MonetaryScore.Should().Be(5); // $300 >= $250 = excellent with custom thresholds
    }

    [Fact]
    public async Task RecalculateSegmentAsync_ChampionCustomer_ShouldBeChampion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Make them a champion: recent, frequent, high spender
        for (int i = 0; i < 15; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // Act
        await grain.RecalculateSegmentAsync();

        // Assert
        var segment = await grain.GetSegmentAsync();
        segment.Should().Be(CustomerSegment.Champion);

        var history = await grain.GetSegmentHistoryAsync();
        history.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetSegmentHistoryAsync_ShouldTrackChanges()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Initial segment is New
        var initialSegment = await grain.GetSegmentAsync();
        initialSegment.Should().Be(CustomerSegment.New);

        // Make them a champion
        for (int i = 0; i < 15; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // Act - recalculate to change segment
        await grain.RecalculateSegmentAsync();

        // Assert
        var history = await grain.GetSegmentHistoryAsync();
        history.Should().NotBeEmpty();
        var lastChange = history.Last();
        lastChange.NewSegment.Should().Be(CustomerSegment.Champion);
        lastChange.RfmScore.Should().NotBeNull();
    }

    // ==================== GDPR Consent Tests ====================

    [Fact]
    public async Task UpdateConsentAsync_MarketingEmail_ShouldUpdateConsent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdateConsentAsync(new UpdateConsentCommand(
            MarketingEmail: true,
            ConsentVersion: "v1.0",
            IpAddress: "192.168.1.1",
            UserAgent: "TestBrowser/1.0"));

        // Assert
        var consent = await grain.GetConsentStatusAsync();
        consent.MarketingEmail.Should().BeTrue();
        consent.MarketingEmailConsentedAt.Should().NotBeNull();
        consent.ConsentVersion.Should().Be("v1.0");
    }

    [Fact]
    public async Task UpdateConsentAsync_MultipleConsents_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.UpdateConsentAsync(new UpdateConsentCommand(
            MarketingEmail: true,
            Sms: true,
            DataRetention: true,
            Profiling: false,
            ConsentVersion: "v2.0"));

        // Assert
        var consent = await grain.GetConsentStatusAsync();
        consent.MarketingEmail.Should().BeTrue();
        consent.Sms.Should().BeTrue();
        consent.DataRetention.Should().BeTrue();
        consent.Profiling.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateConsentAsync_Revocation_ShouldTrackInHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // First grant consent
        await grain.UpdateConsentAsync(new UpdateConsentCommand(MarketingEmail: true));

        // Act - revoke consent
        await grain.UpdateConsentAsync(new UpdateConsentCommand(MarketingEmail: false));

        // Assert
        var consent = await grain.GetConsentStatusAsync();
        consent.MarketingEmail.Should().BeFalse();

        var history = await grain.GetConsentHistoryAsync();
        history.Should().HaveCount(2);
        history[0].ConsentType.Should().Be("MarketingEmail");
        history[0].NewValue.Should().BeTrue();
        history[1].ConsentType.Should().Be("MarketingEmail");
        history[1].NewValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetConsentHistoryAsync_ShouldReturnAuditTrail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Multiple consent changes
        await grain.UpdateConsentAsync(new UpdateConsentCommand(MarketingEmail: true, IpAddress: "1.1.1.1"));
        await grain.UpdateConsentAsync(new UpdateConsentCommand(Sms: true, IpAddress: "2.2.2.2"));
        await grain.UpdateConsentAsync(new UpdateConsentCommand(MarketingEmail: false, IpAddress: "3.3.3.3"));

        // Act
        var history = await grain.GetConsentHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].IpAddress.Should().Be("1.1.1.1");
        history[1].IpAddress.Should().Be("2.2.2.2");
        history[2].IpAddress.Should().Be("3.3.3.3");
    }

    // ==================== Anonymization Tests ====================

    [Fact]
    public async Task AnonymizeAsync_ShouldRemovePII()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.UpdateAsync(new UpdateCustomerCommand(
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com"));

        // Record some activity
        await grain.RecordVisitAsync(new RecordVisitCommand(Guid.NewGuid(), Guid.NewGuid(), 100m));

        // Act
        await grain.AnonymizeAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.FirstName.Should().Be("REDACTED");
        state.LastName.Should().Be("REDACTED");
        state.DisplayName.Should().Be("REDACTED");
        state.Contact.Email.Should().BeNull();
        state.Contact.Phone.Should().BeNull();
        state.DateOfBirth.Should().BeNull();
        state.Status.Should().Be(CustomerStatus.Inactive);
        state.IsAnonymized.Should().BeTrue();
        state.AnonymizedAt.Should().NotBeNull();
        state.AnonymizedHash.Should().NotBeNullOrEmpty(); // Hash preserved for reference

        // Aggregate stats should be retained
        state.Stats.TotalSpend.Should().Be(100m);
        state.Stats.TotalVisits.Should().Be(1);
    }

    [Fact]
    public async Task RequestDataDeletionAsync_ShouldAnonymizeAndRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var result = await grain.RequestDataDeletionAsync("user@example.com", "GDPR request");

        // Assert
        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.IsAnonymized.Should().BeTrue();
        state.FirstName.Should().Be("REDACTED");
    }

    // ==================== VIP Detection Tests ====================

    [Fact]
    public async Task GrantVipStatusAsync_ShouldMakeCustomerVip()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        await grain.GrantVipStatusAsync(new GrantVipStatusCommand("Loyal customer", staffId));

        // Assert
        var isVip = await grain.IsVipAsync();
        isVip.Should().BeTrue();

        var vipStatus = await grain.GetVipStatusAsync();
        vipStatus.IsVip.Should().BeTrue();
        vipStatus.VipReason.Should().Be("Loyal customer");
        vipStatus.ManuallyAssigned.Should().BeTrue();
        vipStatus.VipSince.Should().NotBeNull();

        // Should also add VIP tag
        var state = await grain.GetStateAsync();
        state.Tags.Should().Contain("VIP");
    }

    [Fact]
    public async Task RevokeVipStatusAsync_ShouldRemoveVip()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GrantVipStatusAsync(new GrantVipStatusCommand("Initial grant"));

        // Act
        await grain.RevokeVipStatusAsync(new RevokeVipStatusCommand("No longer qualifies"));

        // Assert
        var isVip = await grain.IsVipAsync();
        isVip.Should().BeFalse();

        // VIP tag should be removed
        var state = await grain.GetStateAsync();
        state.Tags.Should().NotContain("VIP");
    }

    [Fact]
    public async Task CheckAndUpdateVipStatusAsync_HighSpender_ShouldAutoGrantVip()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Spend over $1000 (default threshold)
        for (int i = 0; i < 11; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // Act
        await grain.CheckAndUpdateVipStatusAsync();

        // Assert
        var isVip = await grain.IsVipAsync();
        isVip.Should().BeTrue();

        var vipStatus = await grain.GetVipStatusAsync();
        vipStatus.ManuallyAssigned.Should().BeFalse();
        vipStatus.SpendAtVipGrant.Should().Be(1100m);
    }

    [Fact]
    public async Task CheckAndUpdateVipStatusAsync_WithCustomThresholds_ShouldUseThresholds()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // 5 visits totaling $500
        for (int i = 0; i < 5; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // With default thresholds ($1000), they wouldn't qualify
        // With custom threshold ($400), they should

        // Act
        await grain.CheckAndUpdateVipStatusAsync(new VipThresholds(MinimumSpend: 400m));

        // Assert
        var isVip = await grain.IsVipAsync();
        isVip.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAndUpdateVipStatusAsync_RequireBoth_ShouldEnforceBothCriteria()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // High spend but few visits
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 500m));
        await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 600m));

        // Act - require both spend AND visits
        await grain.CheckAndUpdateVipStatusAsync(new VipThresholds(
            MinimumSpend: 500m,
            MinimumVisits: 5,
            RequireBoth: true));

        // Assert - should NOT be VIP because visits < 5
        var isVip = await grain.IsVipAsync();
        isVip.Should().BeFalse();
    }

    [Fact]
    public async Task GrantVipStatusAsync_AlreadyVip_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GrantVipStatusAsync(new GrantVipStatusCommand("First grant"));

        // Act
        var act = () => grain.GrantVipStatusAsync(new GrantVipStatusCommand("Second grant"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already VIP*");
    }

    // ==================== Birthday Reward Tests ====================

    [Fact]
    public async Task SetBirthdayAsync_ShouldStoreBirthday()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        var birthday = new DateOnly(1990, 6, 15);

        // Act
        await grain.SetBirthdayAsync(new SetBirthdayCommand(birthday));

        // Assert
        var state = await grain.GetStateAsync();
        state.DateOfBirth.Should().Be(birthday);
    }

    [Fact]
    public async Task IssueBirthdayRewardAsync_ShouldIssueReward()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Act
        var result = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Free Birthday Dessert", 30));

        // Assert
        result.RewardId.Should().NotBeEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var birthdayReward = await grain.GetCurrentBirthdayRewardAsync();
        birthdayReward.Should().NotBeNull();
        birthdayReward!.Year.Should().Be(DateTime.UtcNow.Year);
        birthdayReward.CurrentRewardId.Should().Be(result.RewardId);

        // Should also appear in regular rewards
        var rewards = await grain.GetAvailableRewardsAsync();
        rewards.Should().Contain(r => r.Name == "Free Birthday Dessert");
    }

    [Fact]
    public async Task IssueBirthdayRewardAsync_AlreadyIssuedThisYear_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));
        await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("First Reward"));

        // Act
        var act = () => grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Second Reward"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already issued this year*");
    }

    [Fact]
    public async Task IssueBirthdayRewardAsync_NoBirthdaySet_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        // Note: birthday NOT set

        // Act
        var act = () => grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Reward"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*birthday not set*");
    }

    [Fact]
    public async Task HasBirthdayRewardThisYearAsync_ShouldCheckCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1990, 6, 15)));

        // Before issuing
        var beforeIssue = await grain.HasBirthdayRewardThisYearAsync();
        beforeIssue.Should().BeFalse();

        // Issue reward
        await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Reward"));

        // After issuing
        var afterIssue = await grain.HasBirthdayRewardThisYearAsync();
        afterIssue.Should().BeTrue();
    }

    // ==================== Referral Tracking Tests ====================

    [Fact]
    public async Task GenerateReferralCodeAsync_ShouldGenerateUniqueCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var code = await grain.GenerateReferralCodeAsync();

        // Assert
        code.Should().NotBeNullOrEmpty();
        code.Should().StartWith("JOHN"); // From FirstName "John"
        code.Length.Should().BeGreaterThan(4);
    }

    [Fact]
    public async Task GenerateReferralCodeAsync_WithPrefix_ShouldUsePrefix()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var code = await grain.GenerateReferralCodeAsync(new GenerateReferralCodeCommand("VIP"));

        // Assert
        code.Should().StartWith("VIP");
    }

    [Fact]
    public async Task GenerateReferralCodeAsync_CalledTwice_ShouldReturnSameCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Act
        var code1 = await grain.GenerateReferralCodeAsync();
        var code2 = await grain.GenerateReferralCodeAsync();

        // Assert - should return same code
        code2.Should().Be(code1);
    }

    [Fact]
    public async Task CompleteReferralAsync_ShouldTrackReferralAndAwardPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var referredCustomerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();

        // Enroll in loyalty to receive points
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Act
        var result = await grain.CompleteReferralAsync(new CompleteReferralCommand(referredCustomerId, 100));

        // Assert
        result.Success.Should().BeTrue();
        result.PointsAwarded.Should().Be(100);
        result.TotalReferrals.Should().Be(1);
        result.CapReached.Should().BeFalse();

        var status = await grain.GetReferralStatusAsync();
        status.SuccessfulReferrals.Should().Be(1);
        status.TotalPointsEarnedFromReferrals.Should().Be(100);
        status.ReferredCustomers.Should().Contain(referredCustomerId);

        // Points should be added
        var points = await grain.GetPointsBalanceAsync();
        points.Should().Be(100);
    }

    [Fact]
    public async Task CompleteReferralAsync_AtCap_ShouldStopAwarding()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        await grain.GenerateReferralCodeAsync();
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Complete referrals up to cap (default is 10)
        for (int i = 0; i < 10; i++)
        {
            await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));
        }

        var capReached = await grain.HasReachedReferralCapAsync();
        capReached.Should().BeTrue();

        // Act - try to complete one more
        var result = await grain.CompleteReferralAsync(new CompleteReferralCommand(Guid.NewGuid(), 100));

        // Assert
        result.Success.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);
        result.CapReached.Should().BeTrue();
    }

    [Fact]
    public async Task GetReferralStatusAsync_ShouldReturnFullStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);
        var code = await grain.GenerateReferralCodeAsync();

        // Act
        var status = await grain.GetReferralStatusAsync();

        // Assert
        status.ReferralCode.Should().Be(code);
        status.SuccessfulReferrals.Should().Be(0);
        status.TotalPointsEarnedFromReferrals.Should().Be(0);
        status.ReferralRewardsCap.Should().Be(10);
    }

    // ==================== Combined Behavior Tests ====================

    [Fact]
    public async Task FullCustomerLifecycle_ShouldTrackAllMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await CreateCustomerAsync(orgId, customerId);

        // Set birthday
        await grain.SetBirthdayAsync(new SetBirthdayCommand(new DateOnly(1985, 3, 20)));

        // Update consent
        await grain.UpdateConsentAsync(new UpdateConsentCommand(
            MarketingEmail: true,
            Sms: true,
            DataRetention: true));

        // Enroll in loyalty
        await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(Guid.NewGuid(), "MEM001", Guid.NewGuid(), "Bronze"));

        // Generate referral code
        var referralCode = await grain.GenerateReferralCodeAsync();

        // Make several visits (enough to become VIP and Champion)
        for (int i = 0; i < 15; i++)
        {
            await grain.RecordVisitAsync(new RecordVisitCommand(siteId, Guid.NewGuid(), 100m));
        }

        // Check VIP and segment
        await grain.CheckAndUpdateVipStatusAsync(new VipThresholds(MinimumSpend: 1000m));
        await grain.RecalculateSegmentAsync();

        // Issue birthday reward
        var birthdayReward = await grain.IssueBirthdayRewardAsync(new IssueBirthdayRewardCommand("Birthday Treat"));

        // Act - get full state
        var state = await grain.GetStateAsync();

        // Assert
        state.DateOfBirth.Should().Be(new DateOnly(1985, 3, 20));
        state.Consent.MarketingEmail.Should().BeTrue();
        state.Consent.Sms.Should().BeTrue();
        state.Loyalty.Should().NotBeNull();
        state.Referral.ReferralCode.Should().Be(referralCode);
        state.VipStatus.IsVip.Should().BeTrue();
        state.Stats.Segment.Should().Be(CustomerSegment.Champion);
        state.Stats.TotalVisits.Should().Be(15);
        state.Stats.TotalSpend.Should().Be(1500m);
        state.CurrentBirthdayReward.Should().NotBeNull();
        state.Tags.Should().Contain("VIP");
    }
}
