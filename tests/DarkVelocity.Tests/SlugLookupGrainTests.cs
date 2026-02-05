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
