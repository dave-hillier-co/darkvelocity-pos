using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Tests;

// ============================================================================
// OAuthStateGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OAuthStateGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OAuthStateGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateStateWithPkceParams()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));
        var codeChallenge = ComputeS256Challenge("test-verifier");

        var request = new OAuthStateRequest(
            Provider: "google",
            ReturnUrl: "https://example.com/callback",
            CodeChallenge: codeChallenge,
            CodeChallengeMethod: "S256",
            ClientId: "test-client",
            Nonce: "random-nonce",
            Scope: "openid profile email");

        // Act
        await grain.InitializeAsync(request);

        // Assert
        var savedState = await grain.GetStateAsync();
        savedState.Initialized.Should().BeTrue();
        savedState.Provider.Should().Be("google");
        savedState.ReturnUrl.Should().Be("https://example.com/callback");
        savedState.CodeChallenge.Should().Be(codeChallenge);
        savedState.CodeChallengeMethod.Should().Be("S256");
        savedState.ClientId.Should().Be("test-client");
        savedState.Nonce.Should().Be("random-nonce");
        savedState.Scope.Should().Be("openid profile email");
        savedState.Consumed.Should().BeFalse();
        savedState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        savedState.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyInitialized_ShouldThrow()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));

        var request = new OAuthStateRequest(
            Provider: "google",
            ReturnUrl: "https://example.com/callback");

        await grain.InitializeAsync(request);

        // Act
        var act = () => grain.InitializeAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("OAuth state already initialized");
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_ShouldValidateAndMarkConsumed()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));

        var request = new OAuthStateRequest(
            Provider: "microsoft",
            ReturnUrl: "https://example.com/callback",
            CodeChallenge: "challenge123",
            CodeChallengeMethod: "S256",
            ClientId: "client-123",
            Nonce: "nonce-456",
            Scope: "openid");

        await grain.InitializeAsync(request);

        // Act
        var result = await grain.ValidateAndConsumeAsync();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Provider.Should().Be("microsoft");
        result.ReturnUrl.Should().Be("https://example.com/callback");
        result.CodeChallenge.Should().Be("challenge123");
        result.CodeChallengeMethod.Should().Be("S256");
        result.ClientId.Should().Be("client-123");
        result.Nonce.Should().Be("nonce-456");
        result.Scope.Should().Be("openid");
        result.Error.Should().BeNull();

        // Verify state is marked as consumed
        var savedState = await grain.GetStateAsync();
        savedState.Consumed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenAlreadyConsumed_ShouldFail()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));

        await grain.InitializeAsync(new OAuthStateRequest(
            Provider: "google",
            ReturnUrl: "https://example.com/callback"));

        await grain.ValidateAndConsumeAsync(); // First consumption

        // Act
        var result = await grain.ValidateAndConsumeAsync(); // Second attempt

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("state_already_used");
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenNotInitialized_ShouldFail()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));

        // Act (without initializing)
        var result = await grain.ValidateAndConsumeAsync();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("invalid_state");
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var state = GrainKeys.GenerateOAuthState();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));

        await grain.InitializeAsync(new OAuthStateRequest(
            Provider: "github",
            ReturnUrl: "https://example.com/auth"));

        // Act
        var savedState = await grain.GetStateAsync();

        // Assert
        savedState.Provider.Should().Be("github");
        savedState.ReturnUrl.Should().Be("https://example.com/auth");
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

// ============================================================================
// AuthorizationCodeGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AuthorizationCodeGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AuthorizationCodeGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IssueAsync_ShouldCreateAuthorizationCode()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var request = new AuthorizationCodeRequest(
            UserId: userId,
            OrganizationId: orgId,
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            Scope: "openid profile",
            CodeChallenge: "challenge123",
            CodeChallengeMethod: "S256",
            Nonce: "nonce123",
            DisplayName: "Test User",
            Roles: ["admin", "manager"]);

        // Act
        await grain.IssueAsync(request);

        // Assert
        var isValid = await grain.IsValidAsync();
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task IssueAsync_WhenAlreadyIssued_ShouldThrow()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback");

        await grain.IssueAsync(request);

        // Act
        var act = () => grain.IssueAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Authorization code already issued");
    }

    [Fact]
    public async Task ExchangeAsync_WithValidCode_ShouldReturnUserInfo()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var request = new AuthorizationCodeRequest(
            UserId: userId,
            OrganizationId: orgId,
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            Scope: "openid profile",
            Nonce: "nonce123",
            DisplayName: "Test User",
            Roles: ["admin", "manager"]);

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("pos-client", null);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.OrganizationId.Should().Be(orgId);
        result.DisplayName.Should().Be("Test User");
        result.Roles.Should().BeEquivalentTo(["admin", "manager"]);
        result.Scope.Should().Be("openid profile");
        result.Nonce.Should().Be("nonce123");
    }

    [Fact]
    public async Task ExchangeAsync_WithPkceS256_ShouldValidateCodeVerifier()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var codeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var codeChallenge = ComputeS256Challenge(codeVerifier);

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            CodeChallenge: codeChallenge,
            CodeChallengeMethod: "S256");

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("pos-client", codeVerifier);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WithPkceS256_WhenVerifierInvalid_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var codeVerifier = "correct-verifier";
        var codeChallenge = ComputeS256Challenge(codeVerifier);

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            CodeChallenge: codeChallenge,
            CodeChallengeMethod: "S256");

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("pos-client", "wrong-verifier");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WithPkcePlain_ShouldValidateCodeVerifier()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var codeVerifier = "plain-text-verifier";

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            CodeChallenge: codeVerifier, // For PLAIN method, challenge equals verifier
            CodeChallengeMethod: "PLAIN");

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("pos-client", codeVerifier);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WithPkcePlain_WhenVerifierInvalid_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            CodeChallenge: "correct-verifier",
            CodeChallengeMethod: "PLAIN");

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("pos-client", "wrong-verifier");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WithPkceRequired_WhenVerifierMissing_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback",
            CodeChallenge: ComputeS256Challenge("verifier"),
            CodeChallengeMethod: "S256");

        await grain.IssueAsync(request);

        // Act - no verifier provided when PKCE is required
        var result = await grain.ExchangeAsync("pos-client", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WithWrongClientId_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback");

        await grain.IssueAsync(request);

        // Act
        var result = await grain.ExchangeAsync("different-client", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WhenAlreadyExchanged_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        var request = new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback");

        await grain.IssueAsync(request);
        await grain.ExchangeAsync("pos-client", null); // First exchange

        // Act
        var result = await grain.ExchangeAsync("pos-client", null); // Second exchange

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeAsync_WhenNotInitialized_ShouldReturnNull()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        // Act (without issuing)
        var result = await grain.ExchangeAsync("pos-client", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IsValidAsync_WhenIssued_ShouldReturnTrue()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        await grain.IssueAsync(new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback"));

        // Act
        var result = await grain.IsValidAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WhenNotInitialized_ShouldReturnFalse()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        // Act
        var result = await grain.IsValidAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenExchanged_ShouldReturnFalse()
    {
        // Arrange
        var code = GrainKeys.GenerateAuthorizationCode();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));

        await grain.IssueAsync(new AuthorizationCodeRequest(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            ClientId: "pos-client",
            RedirectUri: "https://example.com/callback"));
        await grain.ExchangeAsync("pos-client", null);

        // Act
        var result = await grain.IsValidAsync();

        // Assert
        result.Should().BeFalse();
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

// ============================================================================
// ExternalIdentityGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ExternalIdentityGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ExternalIdentityGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LinkAsync_ShouldLinkExternalIdentityToUser()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var command = new LinkExternalIdentityCommand(
            UserId: userId,
            OrganizationId: orgId,
            Provider: provider,
            ExternalId: externalId,
            Email: "test@gmail.com",
            Name: "Test User",
            PictureUrl: "https://example.com/avatar.jpg");

        // Act
        await grain.LinkAsync(command);

        // Assert
        var info = await grain.GetLinkedUserAsync();
        info.Should().NotBeNull();
        info!.UserId.Should().Be(userId);
        info.OrganizationId.Should().Be(orgId);
        info.Provider.Should().Be(provider);
        info.ExternalId.Should().Be(externalId);
        info.Email.Should().Be("test@gmail.com");
        info.Name.Should().Be("Test User");
        info.PictureUrl.Should().Be("https://example.com/avatar.jpg");
        info.LinkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LinkAsync_WhenAlreadyLinked_ShouldThrow()
    {
        // Arrange
        var provider = "microsoft";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        var command = new LinkExternalIdentityCommand(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            Provider: provider,
            ExternalId: externalId);

        await grain.LinkAsync(command);

        // Act
        var act = () => grain.LinkAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External identity already linked to a user");
    }

    [Fact]
    public async Task GetLinkedUserAsync_ShouldReturnUserInfo()
    {
        // Arrange
        var provider = "github";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await grain.LinkAsync(new LinkExternalIdentityCommand(
            UserId: userId,
            OrganizationId: orgId,
            Provider: provider,
            ExternalId: externalId,
            Email: "user@github.com"));

        // Act
        var info = await grain.GetLinkedUserAsync();

        // Assert
        info.Should().NotBeNull();
        info!.UserId.Should().Be(userId);
        info.OrganizationId.Should().Be(orgId);
        info.Email.Should().Be("user@github.com");
    }

    [Fact]
    public async Task GetLinkedUserAsync_ShouldUpdateLastUsedAt()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        await grain.LinkAsync(new LinkExternalIdentityCommand(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            Provider: provider,
            ExternalId: externalId));

        var firstCall = await grain.GetLinkedUserAsync();
        var firstLastUsedAt = firstCall!.LastUsedAt;

        // Small delay to ensure time difference
        await Task.Delay(10);

        // Act
        var secondCall = await grain.GetLinkedUserAsync();

        // Assert
        secondCall!.LastUsedAt.Should().BeOnOrAfter(firstLastUsedAt!.Value);
    }

    [Fact]
    public async Task GetLinkedUserAsync_WhenNotLinked_ShouldReturnNull()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        // Act (without linking)
        var info = await grain.GetLinkedUserAsync();

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public async Task UpdateInfoAsync_ShouldUpdateIdentityInfo()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        await grain.LinkAsync(new LinkExternalIdentityCommand(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            Provider: provider,
            ExternalId: externalId,
            Email: "old@example.com",
            Name: "Old Name",
            PictureUrl: "https://old.com/pic.jpg"));

        // Act
        await grain.UpdateInfoAsync(
            email: "new@example.com",
            name: "New Name",
            pictureUrl: "https://new.com/pic.jpg");

        // Assert
        var info = await grain.GetLinkedUserAsync();
        info!.Email.Should().Be("new@example.com");
        info.Name.Should().Be("New Name");
        info.PictureUrl.Should().Be("https://new.com/pic.jpg");
    }

    [Fact]
    public async Task UpdateInfoAsync_WithPartialUpdate_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        await grain.LinkAsync(new LinkExternalIdentityCommand(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            Provider: provider,
            ExternalId: externalId,
            Email: "original@example.com",
            Name: "Original Name",
            PictureUrl: "https://original.com/pic.jpg"));

        // Act - only update name
        await grain.UpdateInfoAsync(email: null, name: "Updated Name", pictureUrl: null);

        // Assert
        var info = await grain.GetLinkedUserAsync();
        info!.Email.Should().Be("original@example.com"); // unchanged
        info.Name.Should().Be("Updated Name"); // updated
        info.PictureUrl.Should().Be("https://original.com/pic.jpg"); // unchanged
    }

    [Fact]
    public async Task UpdateInfoAsync_WhenNotLinked_ShouldDoNothing()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        // Act (without linking)
        await grain.UpdateInfoAsync("new@example.com", "New Name", "https://pic.jpg");

        // Assert - should not throw, and still return null
        var info = await grain.GetLinkedUserAsync();
        info.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkAsync_ShouldClearLink()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        await grain.LinkAsync(new LinkExternalIdentityCommand(
            UserId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            Provider: provider,
            ExternalId: externalId));

        // Act
        await grain.UnlinkAsync();

        // Assert
        var info = await grain.GetLinkedUserAsync();
        info.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkAsync_WhenNotLinked_ShouldNotThrow()
    {
        // Arrange
        var provider = "google";
        var externalId = Guid.NewGuid().ToString();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExternalIdentityGrain>(
            GrainKeys.ExternalIdentity(provider, externalId));

        // Act (without linking)
        var act = () => grain.UnlinkAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

// ============================================================================
// OAuthLookupGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OAuthLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OAuthLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterExternalIdAsync_ShouldCreateMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        // Act
        await grain.RegisterExternalIdAsync("google", "ext123", userId);

        // Assert
        var foundUserId = await grain.FindByExternalIdAsync("google", "ext123");
        foundUserId.Should().Be(userId);
    }

    [Fact]
    public async Task RegisterExternalIdAsync_WithSameUserAgain_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("google", "ext456", userId);

        // Act - register same user again
        var act = () => grain.RegisterExternalIdAsync("google", "ext456", userId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterExternalIdAsync_WithDifferentUser_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("google", "ext789", userId1);

        // Act
        var act = () => grain.RegisterExternalIdAsync("google", "ext789", userId2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External identity google:ext789 is already linked to a different user");
    }

    [Fact]
    public async Task FindByExternalIdAsync_ShouldReturnUserId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("microsoft", "ms123", userId);

        // Act
        var foundUserId = await grain.FindByExternalIdAsync("microsoft", "ms123");

        // Assert
        foundUserId.Should().Be(userId);
    }

    [Fact]
    public async Task FindByExternalIdAsync_WhenUnknown_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        // Act
        var foundUserId = await grain.FindByExternalIdAsync("unknown", "doesnotexist");

        // Assert
        foundUserId.Should().BeNull();
    }

    [Fact]
    public async Task FindByExternalIdAsync_ShouldBeCaseInsensitiveForProvider()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("Google", "ext-case-test", userId);

        // Act
        var foundUserId = await grain.FindByExternalIdAsync("google", "ext-case-test");

        // Assert
        foundUserId.Should().Be(userId);
    }

    [Fact]
    public async Task UnregisterExternalIdAsync_ShouldRemoveMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("github", "gh123", userId);

        // Act
        await grain.UnregisterExternalIdAsync("github", "gh123");

        // Assert
        var foundUserId = await grain.FindByExternalIdAsync("github", "gh123");
        foundUserId.Should().BeNull();
    }

    [Fact]
    public async Task UnregisterExternalIdAsync_WhenNotRegistered_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        // Act
        var act = () => grain.UnregisterExternalIdAsync("nonexistent", "doesnotexist");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetExternalIdsForUserAsync_ShouldReturnAllProviders()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("google", "google-id", userId);
        await grain.RegisterExternalIdAsync("microsoft", "microsoft-id", userId);
        await grain.RegisterExternalIdAsync("github", "github-id", userId);

        // Act
        var externalIds = await grain.GetExternalIdsForUserAsync(userId);

        // Assert
        externalIds.Should().HaveCount(3);
        externalIds["google"].Should().Be("google-id");
        externalIds["microsoft"].Should().Be("microsoft-id");
        externalIds["github"].Should().Be("github-id");
    }

    [Fact]
    public async Task GetExternalIdsForUserAsync_WhenNoLinks_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        // Act
        var externalIds = await grain.GetExternalIdsForUserAsync(userId);

        // Assert
        externalIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExternalIdsForUserAsync_ShouldNotIncludeOtherUsers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOAuthLookupGrain>(GrainKeys.OAuthLookup(orgId));

        await grain.RegisterExternalIdAsync("google", "user1-google", userId1);
        await grain.RegisterExternalIdAsync("microsoft", "user2-microsoft", userId2);

        // Act
        var externalIds = await grain.GetExternalIdsForUserAsync(userId1);

        // Assert
        externalIds.Should().HaveCount(1);
        externalIds.Should().ContainKey("google");
        externalIds.Should().NotContainKey("microsoft");
    }
}

// ============================================================================
// EmailLookupGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class EmailLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public EmailLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterEmailAsync_ShouldCreateMapping()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"test-{Guid.NewGuid()}@example.com"; // Unique email for test isolation

        // Act
        await grain.RegisterEmailAsync(email, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().HaveCount(1);
        mappings[0].OrganizationId.Should().Be(orgId);
        mappings[0].UserId.Should().Be(userId);
    }

    [Fact]
    public async Task RegisterEmailAsync_WithSameUserAgain_ShouldNotThrow()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"duplicate-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId);

        // Act
        var act = () => grain.RegisterEmailAsync(email, orgId, userId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterEmailAsync_WithDifferentUserInSameOrg_ShouldThrow()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var email = $"conflict-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId1);

        // Act
        var act = () => grain.RegisterEmailAsync(email, orgId, userId2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Email {email} is already registered to a different user in this organization");
    }

    [Fact]
    public async Task FindByEmailAsync_ShouldReturnUserMappings()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"find-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, orgId, userId);

        // Act
        var mappings = await grain.FindByEmailAsync(email);

        // Assert
        mappings.Should().ContainSingle();
        mappings[0].OrganizationId.Should().Be(orgId);
        mappings[0].UserId.Should().Be(userId);
    }

    [Fact]
    public async Task FindByEmailAsync_ShouldWorkAcrossOrganizations()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var email = $"multiorg-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, org1, user1);
        await grain.RegisterEmailAsync(email, org2, user2);

        // Act
        var mappings = await grain.FindByEmailAsync(email);

        // Assert
        mappings.Should().HaveCount(2);
        mappings.Should().Contain(m => m.OrganizationId == org1 && m.UserId == user1);
        mappings.Should().Contain(m => m.OrganizationId == org2 && m.UserId == user2);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenNotRegistered_ShouldReturnEmpty()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());

        // Act
        var mappings = await grain.FindByEmailAsync("nonexistent@example.com");

        // Assert
        mappings.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterEmailAsync_ShouldRemoveMapping()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
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

    [Fact]
    public async Task UnregisterEmailAsync_ShouldOnlyRemoveSpecificOrg()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var email = $"partial-unregister-{Guid.NewGuid()}@example.com";

        await grain.RegisterEmailAsync(email, org1, user1);
        await grain.RegisterEmailAsync(email, org2, user2);

        // Act
        await grain.UnregisterEmailAsync(email, org1);

        // Assert
        var mappings = await grain.FindByEmailAsync(email);
        mappings.Should().ContainSingle();
        mappings[0].OrganizationId.Should().Be(org2);
    }

    [Fact]
    public async Task UnregisterEmailAsync_WhenNotRegistered_ShouldNotThrow()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());

        // Act
        var act = () => grain.UnregisterEmailAsync("nonexistent@example.com", Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateEmailAsync_ShouldChangeMapping()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
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

    [Fact]
    public async Task UpdateEmailAsync_WithNullOldEmail_ShouldOnlyRegisterNew()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var newEmail = $"only-new-{Guid.NewGuid()}@example.com";

        // Act
        await grain.UpdateEmailAsync(null!, newEmail, orgId, userId);

        // Assert
        var mappings = await grain.FindByEmailAsync(newEmail);
        mappings.Should().ContainSingle();
    }

    [Fact]
    public async Task EmailNormalization_ShouldBeLowercaseAndTrimmed()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var uniquePart = Guid.NewGuid().ToString();

        // Register with uppercase and spaces
        await grain.RegisterEmailAsync($"  TEST-{uniquePart}@EXAMPLE.COM  ", orgId, userId);

        // Act - search with lowercase
        var mappings = await grain.FindByEmailAsync($"test-{uniquePart}@example.com");

        // Assert
        mappings.Should().ContainSingle();
    }

    [Fact]
    public async Task EmailNormalization_ShouldMatchDifferentCasing()
    {
        // Arrange
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var uniquePart = Guid.NewGuid().ToString();

        await grain.RegisterEmailAsync($"test-{uniquePart}@example.com", orgId, userId);

        // Act - search with different casing
        var mappings = await grain.FindByEmailAsync($"TEST-{uniquePart}@EXAMPLE.COM");

        // Assert
        mappings.Should().ContainSingle();
    }
}
