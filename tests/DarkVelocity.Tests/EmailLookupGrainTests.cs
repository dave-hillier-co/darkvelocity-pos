using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class EmailLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public EmailLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IEmailLookupGrain GetGrain() =>
        _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());

    #region RegisterEmailAsync Tests

    // Given: a global email lookup with no registrations
    // When: an email address is registered for a user in an organization
    // Then: the email should map to the correct organization and user
    [Fact]
    public async Task RegisterEmailAsync_ShouldRegisterEmail()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"register-{Guid.NewGuid()}@example.com";

        // Act
        await grain.RegisterEmailAsync(email, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
        mappings[0].OrganizationId.Should().Be(orgId);
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a global email lookup
    // When: an email is registered with mixed-case characters
    // Then: the email should be findable using lowercase
    [Fact]
    public async Task RegisterEmailAsync_ShouldNormalizeEmail_CaseInsensitive()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"TEST.USER-{Guid.NewGuid()}@EXAMPLE.COM";

        // Act
        await grain.RegisterEmailAsync(email, orgId, userId);

        // Assert - should find with lowercase
        var mappings = await grain.FindByEmailAsync(email.ToLower());
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a global email lookup
    // When: an email with leading and trailing whitespace is registered
    // Then: the email should be findable without whitespace
    [Fact]
    public async Task RegisterEmailAsync_ShouldTrimWhitespace()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var baseEmail = $"whitespace-{Guid.NewGuid()}@example.com";
        var emailWithWhitespace = $"  {baseEmail}  ";

        // Act
        await grain.RegisterEmailAsync(emailWithWhitespace, orgId, userId);

        // Assert - should find without whitespace
        var mappings = await grain.FindByEmailAsync(baseEmail);
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: an email already registered for a user in an organization
    // When: the same email is registered again for the same user and organization
    // Then: only one mapping should exist (idempotent operation)
    [Fact]
    public async Task RegisterEmailAsync_SameUserSameOrg_ShouldBeIdempotent()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"idempotent-{Guid.NewGuid()}@example.com";

        // Act - register twice with same user
        await grain.RegisterEmailAsync(email, orgId, userId);
        await grain.RegisterEmailAsync(email, orgId, userId);

        // Assert - should only have one mapping
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
    }

    // Given: an email already registered to a user in an organization
    // When: the same email is registered for a different user in the same organization
    // Then: the operation should be rejected as already registered to a different user
    [Fact]
    public async Task RegisterEmailAsync_DifferentUserSameOrg_ShouldThrow()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var email = $"duplicate-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId1);

        // Act & Assert
        var act = async () => await grain.RegisterEmailAsync(email, orgId, userId2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered to a different user*");
    }

    // Given: a global email lookup
    // When: the same email is registered for different users in different organizations
    // Then: both organization mappings should coexist
    [Fact]
    public async Task RegisterEmailAsync_SameEmailDifferentOrgs_ShouldAllowMultipleMappings()
    {
        // Arrange
        var grain = GetGrain();
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var email = $"multiorg-{Guid.NewGuid()}@example.com";

        // Act
        await grain.RegisterEmailAsync(email, org1Id, user1Id);
        await grain.RegisterEmailAsync(email, org2Id, user2Id);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().HaveCount(2);
        mappings.Should().Contain(m => m.OrganizationId == org1Id && m.UserId == user1Id);
        mappings.Should().Contain(m => m.OrganizationId == org2Id && m.UserId == user2Id);
    }

    // Given: a global email lookup
    // When: a null, empty, or whitespace-only email is registered
    // Then: the operation should be rejected with an argument error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterEmailAsync_WithInvalidEmail_ShouldThrow(string? invalidEmail)
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await grain.RegisterEmailAsync(invalidEmail!, orgId, userId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region FindByEmailAsync Tests

    // Given: a global email lookup with no matching registrations
    // When: searching for an unregistered email address
    // Then: an empty list should be returned
    [Fact]
    public async Task FindByEmailAsync_UnknownEmail_ShouldReturnEmptyList()
    {
        // Arrange
        var grain = GetGrain();
        var unknownEmail = $"unknown-{Guid.NewGuid()}@nonexistent.com";

        // Act
        var mappings = await grain.FindByEmailAsync(unknownEmail);

        // Assert
        mappings.Should().BeEmpty();
    }

    // Given: an email registered in lowercase
    // When: searching with the uppercase version of the email
    // Then: the mapping should be found regardless of case
    [Fact]
    public async Task FindByEmailAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"case-test-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email.ToLower(), orgId, userId);

        // Act - search with uppercase
        var mappings = await grain.FindByEmailAsync(email.ToUpper());

        // Assert
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a registered email address
    // When: searching with surrounding whitespace
    // Then: the mapping should be found after trimming
    [Fact]
    public async Task FindByEmailAsync_ShouldTrimWhitespace()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"trim-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId);

        // Act - search with whitespace
        var mappings = await grain.FindByEmailAsync($"  {email}  ");

        // Assert
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a global email lookup
    // When: searching with a null, empty, or whitespace-only email
    // Then: the operation should be rejected with an argument error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindByEmailAsync_WithInvalidEmail_ShouldThrow(string? invalidEmail)
    {
        // Arrange
        var grain = GetGrain();

        // Act & Assert
        var act = async () => await grain.FindByEmailAsync(invalidEmail!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Given: an email registered across three different organizations
    // When: searching for that email
    // Then: all three organization mappings should be returned
    [Fact]
    public async Task FindByEmailAsync_ShouldReturnAllOrgsForEmail()
    {
        // Arrange
        var grain = GetGrain();
        var email = $"allOrgs-{Guid.NewGuid()}@example.com";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var org3Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user3Id = Guid.NewGuid();

        await grain.RegisterEmailAsync(email, org1Id, user1Id);
        await grain.RegisterEmailAsync(email, org2Id, user2Id);
        await grain.RegisterEmailAsync(email, org3Id, user3Id);

        // Act
        var mappings = await grain.FindByEmailAsync(email);

        // Assert
        mappings.Should().HaveCount(3);
        mappings.Select(m => m.OrganizationId).Should().Contain(new[] { org1Id, org2Id, org3Id });
    }

    #endregion

    #region UnregisterEmailAsync Tests

    // Given: a registered email for a user in an organization
    // When: the email is unregistered for that organization
    // Then: the mapping should no longer exist
    [Fact]
    public async Task UnregisterEmailAsync_ShouldRemoveMapping()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"unregister-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId);

        // Act
        await grain.UnregisterEmailAsync(email, orgId);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().BeEmpty();
    }

    // Given: a global email lookup with no matching email
    // When: unregistering a nonexistent email
    // Then: the operation should complete without error
    [Fact]
    public async Task UnregisterEmailAsync_NonexistentEmail_ShouldNotThrow()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var nonexistentEmail = $"nonexistent-{Guid.NewGuid()}@example.com";

        // Act & Assert - should not throw
        var act = async () => await grain.UnregisterEmailAsync(nonexistentEmail, orgId);
        await act.Should().NotThrowAsync();
    }

    // Given: an email registered in two different organizations
    // When: the email is unregistered for one organization
    // Then: the other organization's mapping should remain intact
    [Fact]
    public async Task UnregisterEmailAsync_ShouldOnlyRemoveForSpecificOrg()
    {
        // Arrange
        var grain = GetGrain();
        var email = $"partial-unregister-{Guid.NewGuid()}@example.com";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        await grain.RegisterEmailAsync(email, org1Id, user1Id);
        await grain.RegisterEmailAsync(email, org2Id, user2Id);

        // Act
        await grain.UnregisterEmailAsync(email, org1Id);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
        mappings[0].OrganizationId.Should().Be(org2Id);
        mappings[0].UserId.Should().Be(user2Id);
    }

    // Given: an email registered in lowercase
    // When: unregistering with the uppercase version of the email
    // Then: the mapping should be removed regardless of case
    [Fact]
    public async Task UnregisterEmailAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"unregister-case-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email.ToLower(), orgId, userId);

        // Act - unregister with uppercase
        await grain.UnregisterEmailAsync(email.ToUpper(), orgId);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().BeEmpty();
    }

    // Given: a global email lookup
    // When: unregistering a null, empty, or whitespace-only email
    // Then: the operation should be rejected with an argument error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterEmailAsync_WithInvalidEmail_ShouldThrow(string? invalidEmail)
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await grain.UnregisterEmailAsync(invalidEmail!, orgId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Given: an email registered for one organization
    // When: attempting to unregister with a different organization ID
    // Then: the original mapping should remain unchanged
    [Fact]
    public async Task UnregisterEmailAsync_WrongOrg_ShouldNotAffectOtherOrgs()
    {
        // Arrange
        var grain = GetGrain();
        var email = $"wrong-org-{Guid.NewGuid()}@example.com";
        var registeredOrgId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await grain.RegisterEmailAsync(email, registeredOrgId, userId);

        // Act - try to unregister for wrong org
        await grain.UnregisterEmailAsync(email, wrongOrgId);

        // Assert - original mapping should still exist
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
        mappings[0].OrganizationId.Should().Be(registeredOrgId);
    }

    #endregion

    #region UpdateEmailAsync Tests

    // Given: a user with a registered email address
    // When: the email is updated to a new address
    // Then: the old email mapping should be removed and the new one registered
    [Fact]
    public async Task UpdateEmailAsync_ShouldRemoveOldAndAddNew()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldEmail = $"old-{Guid.NewGuid()}@example.com";
        var newEmail = $"new-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(oldEmail, orgId, userId);

        // Act
        await grain.UpdateEmailAsync(oldEmail, newEmail, orgId, userId);

        // Assert
        var oldMappings = await grain.FindByEmailAsync(oldEmail);
        oldMappings.Should().BeEmpty();

        var newMappings = await grain.FindByEmailAsync(newEmail);
        newMappings.Should().ContainSingle();
        newMappings[0].OrganizationId.Should().Be(orgId);
        newMappings[0].UserId.Should().Be(userId);
    }

    // Given: a user without a previously registered email
    // When: updating with a null old email and a new email address
    // Then: the new email should be registered without removing anything
    [Fact]
    public async Task UpdateEmailAsync_WithNullOldEmail_ShouldOnlyAddNew()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var newEmail = $"only-new-{Guid.NewGuid()}@example.com";

        // Act
        await grain.UpdateEmailAsync(null!, newEmail, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(newEmail);
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a user without a previously registered email
    // When: updating with an empty old email and a new email address
    // Then: the new email should be registered without removing anything
    [Fact]
    public async Task UpdateEmailAsync_WithEmptyOldEmail_ShouldOnlyAddNew()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var newEmail = $"empty-old-{Guid.NewGuid()}@example.com";

        // Act
        await grain.UpdateEmailAsync("", newEmail, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(newEmail);
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    // Given: a global email lookup
    // When: updating to a null, empty, or whitespace-only new email
    // Then: the operation should be rejected with an argument error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateEmailAsync_WithInvalidNewEmail_ShouldThrow(string? invalidNewEmail)
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldEmail = $"old-valid-{Guid.NewGuid()}@example.com";

        // Act & Assert
        var act = async () => await grain.UpdateEmailAsync(oldEmail, invalidNewEmail!, orgId, userId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Given: an email registered in two different organizations
    // When: updating the email for one organization only
    // Then: the other organization's mapping should remain unchanged
    [Fact]
    public async Task UpdateEmailAsync_ShouldPreserveOtherOrgMappings()
    {
        // Arrange
        var grain = GetGrain();
        var email = $"preserve-{Guid.NewGuid()}@example.com";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var newEmail = $"updated-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, org1Id, user1Id);
        await grain.RegisterEmailAsync(email, org2Id, user2Id);

        // Act - update only for org1
        await grain.UpdateEmailAsync(email, newEmail, org1Id, user1Id);

        // Assert - org2 mapping should still exist
        var oldMappings = await grain.FindByEmailAsync(email);
        oldMappings.Should().ContainSingle();
        oldMappings[0].OrganizationId.Should().Be(org2Id);

        var newMappings = await grain.FindByEmailAsync(newEmail);
        newMappings.Should().ContainSingle();
        newMappings[0].OrganizationId.Should().Be(org1Id);
    }

    // Given: an email registered with uppercase characters
    // When: updating with the lowercase version of the old email
    // Then: the old mapping should be removed and the new one created
    [Fact]
    public async Task UpdateEmailAsync_ShouldHandleCaseInsensitivity()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldEmail = $"OLD-CASE-{Guid.NewGuid()}@EXAMPLE.COM";
        var newEmail = $"NEW-CASE-{Guid.NewGuid()}@EXAMPLE.COM";

        await grain.RegisterEmailAsync(oldEmail, orgId, userId);

        // Act
        await grain.UpdateEmailAsync(oldEmail.ToLower(), newEmail, orgId, userId);

        // Assert
        var oldMappings = await grain.FindByEmailAsync(oldEmail);
        oldMappings.Should().BeEmpty();

        var newMappings = await grain.FindByEmailAsync(newEmail.ToLower());
        newMappings.Should().ContainSingle();
    }

    // Given: a registered email for a user
    // When: updating to the same email address
    // Then: the single mapping should remain (idempotent operation)
    [Fact]
    public async Task UpdateEmailAsync_ToSameEmail_ShouldBeIdempotent()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"same-email-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId);

        // Act - update to the same email
        await grain.UpdateEmailAsync(email, email, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
        mappings[0].UserId.Should().Be(userId);
    }

    #endregion

    #region EmailUserMapping Record Tests

    // Given: two EmailUserMapping records with identical organization and user IDs
    // When: comparing for equality
    // Then: they should be equal with matching hash codes
    [Fact]
    public void EmailUserMapping_ShouldSupportValueEquality()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mapping1 = new EmailUserMapping(orgId, userId);
        var mapping2 = new EmailUserMapping(orgId, userId);

        // Assert
        mapping1.Should().Be(mapping2);
        mapping1.GetHashCode().Should().Be(mapping2.GetHashCode());
    }

    // Given: two EmailUserMapping records with different organization and user IDs
    // When: comparing for equality
    // Then: they should not be equal
    [Fact]
    public void EmailUserMapping_DifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var mapping1 = new EmailUserMapping(Guid.NewGuid(), Guid.NewGuid());
        var mapping2 = new EmailUserMapping(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        mapping1.Should().NotBe(mapping2);
    }

    #endregion

    #region Concurrent Access Tests

    // Given: a global email lookup
    // When: 10 different emails are registered concurrently for the same organization
    // Then: all registrations should succeed without error
    [Fact]
    public async Task RegisterEmailAsync_ConcurrentDifferentEmails_ShouldSucceed()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var baseEmail = Guid.NewGuid().ToString();

        var tasks = Enumerable.Range(0, 10).Select(i =>
            grain.RegisterEmailAsync($"concurrent-{baseEmail}-{i}@example.com", orgId, Guid.NewGuid()));

        // Act & Assert - all should succeed
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    // Given: a user with a registered email
    // When: the email is updated multiple times sequentially
    // Then: only the final email should be registered with all previous ones removed
    [Fact]
    public async Task RegisterEmailAsync_SequentialUpdates_ShouldMaintainConsistency()
    {
        // Arrange
        var grain = GetGrain();
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var baseEmail = $"sequential-{Guid.NewGuid()}";

        // Act - register, then update multiple times
        await grain.RegisterEmailAsync($"{baseEmail}-1@example.com", orgId, userId);
        await grain.UpdateEmailAsync($"{baseEmail}-1@example.com", $"{baseEmail}-2@example.com", orgId, userId);
        await grain.UpdateEmailAsync($"{baseEmail}-2@example.com", $"{baseEmail}-3@example.com", orgId, userId);

        // Assert - only the final email should be registered
        var mappings1 = await grain.FindByEmailAsync($"{baseEmail}-1@example.com");
        var mappings2 = await grain.FindByEmailAsync($"{baseEmail}-2@example.com");
        var mappings3 = await grain.FindByEmailAsync($"{baseEmail}-3@example.com");

        mappings1.Should().BeEmpty();
        mappings2.Should().BeEmpty();
        mappings3.Should().ContainSingle();
        mappings3[0].UserId.Should().Be(userId);
    }

    #endregion
}
