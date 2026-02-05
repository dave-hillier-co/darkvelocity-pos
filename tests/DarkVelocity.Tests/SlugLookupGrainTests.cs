using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SlugLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SlugLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a slug that has not been reserved by any organization
    // When: checking slug availability
    // Then: it should be available
    [Fact]
    public async Task IsSlugAvailableAsync_WhenNotReserved_ShouldReturnTrue()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var uniqueSlug = $"available-slug-{Guid.NewGuid():N}";

        // Act
        var isAvailable = await grain.IsSlugAvailableAsync(uniqueSlug);

        // Assert
        isAvailable.Should().BeTrue();
    }

    // Given: a system-reserved slug such as "admin"
    // When: checking slug availability
    // Then: it should be unavailable
    [Fact]
    public async Task IsSlugAvailableAsync_WhenSystemSlug_ShouldReturnFalse()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());

        // Act
        var isAvailable = await grain.IsSlugAvailableAsync("admin");

        // Assert
        isAvailable.Should().BeFalse();
    }

    // Given: an available slug and an organization
    // When: the slug is reserved for the organization
    // Then: the reservation should succeed and the slug should no longer be available
    [Fact]
    public async Task ReserveSlugAsync_ShouldReserveSlug()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var slug = $"my-company-{Guid.NewGuid():N}";

        // Act
        var reserved = await grain.ReserveSlugAsync(slug, orgId);

        // Assert
        reserved.Should().BeTrue();

        var isAvailable = await grain.IsSlugAvailableAsync(slug);
        isAvailable.Should().BeFalse();
    }

    // Given: a slug already reserved by one organization
    // When: a different organization tries to reserve the same slug
    // Then: the reservation should fail
    [Fact]
    public async Task ReserveSlugAsync_WhenAlreadyReserved_ShouldReturnFalse()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var slug = $"taken-slug-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug, orgId1);

        // Act
        var reserved = await grain.ReserveSlugAsync(slug, orgId2);

        // Assert
        reserved.Should().BeFalse();
    }

    // Given: a system-reserved slug such as "api"
    // When: an organization tries to reserve it
    // Then: the reservation should fail
    [Fact]
    public async Task ReserveSlugAsync_SystemSlug_ShouldReturnFalse()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();

        // Act
        var reserved = await grain.ReserveSlugAsync("api", orgId);

        // Assert
        reserved.Should().BeFalse();
    }

    // Given: a slug reserved by an organization
    // When: looking up the organization by slug
    // Then: it should return the correct organization ID
    [Fact]
    public async Task GetOrganizationBySlugAsync_WhenReserved_ShouldReturnOrganizationId()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var slug = $"lookup-test-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug, orgId);

        // Act
        var result = await grain.GetOrganizationBySlugAsync(slug);

        // Assert
        result.Should().NotBeNull();
        result!.OrganizationId.Should().Be(orgId);
    }

    // Given: a slug that has never been reserved
    // When: looking up the organization by slug
    // Then: it should return null
    [Fact]
    public async Task GetOrganizationBySlugAsync_WhenNotReserved_ShouldReturnNull()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var nonexistentSlug = $"nonexistent-{Guid.NewGuid():N}";

        // Act
        var result = await grain.GetOrganizationBySlugAsync(nonexistentSlug);

        // Assert
        result.Should().BeNull();
    }

    // Given: a slug reserved by an organization
    // When: the owning organization releases the slug
    // Then: the slug should become available again
    [Fact]
    public async Task ReleaseSlugAsync_ShouldReleaseSlug()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var slug = $"release-test-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug, orgId);

        // Act
        await grain.ReleaseSlugAsync(slug, orgId);

        // Assert
        var isAvailable = await grain.IsSlugAvailableAsync(slug);
        isAvailable.Should().BeTrue();
    }

    // Given: a slug reserved by one organization
    // When: a different organization attempts to release it
    // Then: the slug should remain reserved by the original owner
    [Fact]
    public async Task ReleaseSlugAsync_WhenWrongOrgId_ShouldNotRelease()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var wrongOrgId = Guid.NewGuid();
        var slug = $"wrong-org-test-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug, orgId);

        // Act
        await grain.ReleaseSlugAsync(slug, wrongOrgId);

        // Assert
        var isAvailable = await grain.IsSlugAvailableAsync(slug);
        isAvailable.Should().BeFalse(); // Should still be reserved
    }

    // Given: an organization with a reserved slug
    // When: the slug is changed to a new value
    // Then: the old slug should be released and the new slug should be reserved
    [Fact]
    public async Task ChangeSlugAsync_ShouldChangeSlugAndRecordHistory()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var oldSlug = $"old-slug-{Guid.NewGuid():N}";
        var newSlug = $"new-slug-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(oldSlug, orgId);

        // Act
        var changed = await grain.ChangeSlugAsync(oldSlug, newSlug, orgId);

        // Assert
        changed.Should().BeTrue();

        // Old slug should be available
        var oldAvailable = await grain.IsSlugAvailableAsync(oldSlug);
        oldAvailable.Should().BeTrue();

        // New slug should be reserved
        var newAvailable = await grain.IsSlugAvailableAsync(newSlug);
        newAvailable.Should().BeFalse();

        // Organization should be found with new slug
        var result = await grain.GetOrganizationBySlugAsync(newSlug);
        result!.OrganizationId.Should().Be(orgId);
    }

    // Given: two organizations each with their own slug
    // When: one organization tries to change its slug to the other's slug
    // Then: the change should fail because the target slug is already taken
    [Fact]
    public async Task ChangeSlugAsync_WhenNewSlugTaken_ShouldReturnFalse()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var slug1 = $"slug-one-{Guid.NewGuid():N}";
        var slug2 = $"slug-two-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug1, orgId1);
        await grain.ReserveSlugAsync(slug2, orgId2);

        // Act - try to change slug1 to slug2 which is already taken
        var changed = await grain.ChangeSlugAsync(slug1, slug2, orgId1);

        // Assert
        changed.Should().BeFalse();
    }

    // Given: a slug reserved by one organization
    // When: a different organization tries to change it
    // Then: the change should fail because the requesting organization does not own the slug
    [Fact]
    public async Task ChangeSlugAsync_WhenOldSlugNotOwned_ShouldReturnFalse()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var oldSlug = $"owned-slug-{Guid.NewGuid():N}";
        var newSlug = $"new-slug-attempt-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(oldSlug, orgId1);

        // Act - try to change with wrong org ID
        var changed = await grain.ChangeSlugAsync(oldSlug, newSlug, orgId2);

        // Assert
        changed.Should().BeFalse();
    }

    // Given: known system-reserved slugs
    // When: checking if various slugs are system slugs
    // Then: reserved slugs should return true and custom slugs should return false
    [Fact]
    public async Task IsSystemSlugAsync_ShouldIdentifySystemSlugs()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());

        // Act & Assert
        (await grain.IsSystemSlugAsync("admin")).Should().BeTrue();
        (await grain.IsSystemSlugAsync("api")).Should().BeTrue();
        (await grain.IsSystemSlugAsync("login")).Should().BeTrue();
        (await grain.IsSystemSlugAsync("ADMIN")).Should().BeTrue(); // Case insensitive
        (await grain.IsSystemSlugAsync("my-company")).Should().BeFalse();
    }

    // Given: a slug reserved with mixed-case characters
    // When: availability is checked with different casings
    // Then: slug lookups should be case-insensitive
    [Fact]
    public async Task SlugNormalization_ShouldBeCaseInsensitive()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISlugLookupGrain>(GrainKeys.SlugLookup());
        var orgId = Guid.NewGuid();
        var slug = $"MixedCase-{Guid.NewGuid():N}";

        await grain.ReserveSlugAsync(slug, orgId);

        // Act & Assert
        var lowerCaseAvailable = await grain.IsSlugAvailableAsync(slug.ToLowerInvariant());
        lowerCaseAvailable.Should().BeFalse();

        var upperCaseAvailable = await grain.IsSlugAvailableAsync(slug.ToUpperInvariant());
        upperCaseAvailable.Should().BeFalse();

        var result = await grain.GetOrganizationBySlugAsync(slug.ToUpperInvariant());
        result!.OrganizationId.Should().Be(orgId);
    }
}
