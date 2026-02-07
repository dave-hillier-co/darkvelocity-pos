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

    // Given: a new OAuth state with Google provider, return URL, and PKCE S256 parameters
    // When: the OAuth state is initialized
    // Then: all PKCE parameters, provider, return URL, nonce, and scope are persisted with a 10-minute expiry
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

    // Given: an OAuth state that has already been initialized
    // When: initialization is attempted again
    // Then: an error is raised indicating the state is already initialized
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

    // Given: a valid, unconsumed OAuth state for Microsoft provider with PKCE parameters
    // When: the state is validated and consumed
    // Then: validation succeeds, all parameters are returned, and the state is marked consumed
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

    // Given: an OAuth state that has already been consumed once
    // When: a second consumption attempt is made
    // Then: validation fails with a "state_already_used" error
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

    // Given: an OAuth state grain that was never initialized
    // When: validation and consumption is attempted
    // Then: validation fails with an "invalid_state" error
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

    // Given: an OAuth state initialized for the GitHub provider
    // When: the state is retrieved
    // Then: the provider and return URL are returned correctly
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

    // Given: a new authorization code with user, organization, client, redirect URI, scope, PKCE challenge, and roles
    // When: the authorization code is issued
    // Then: the code is valid and ready for exchange
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

    // Given: an authorization code that has already been issued
    // When: issuance is attempted again
    // Then: an error is raised indicating the code was already issued
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

    // Given: a valid authorization code issued for a POS client with user info, scope, nonce, and roles
    // When: the code is exchanged by the correct client
    // Then: the user's identity details including ID, organization, display name, roles, scope, and nonce are returned
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

    // Given: an authorization code issued with a PKCE S256 code challenge
    // When: the code is exchanged with the correct code verifier
    // Then: the PKCE validation passes and user info is returned
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

    // Given: an authorization code issued with a PKCE S256 code challenge
    // When: the code is exchanged with an incorrect code verifier
    // Then: the exchange is rejected and returns null
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

    // Given: an authorization code issued with a PKCE PLAIN code challenge
    // When: the code is exchanged with the matching plain-text verifier
    // Then: the PKCE validation passes and user info is returned
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

    // Given: an authorization code issued with a PKCE PLAIN code challenge
    // When: the code is exchanged with an incorrect verifier
    // Then: the exchange is rejected and returns null
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

    // Given: an authorization code issued with a PKCE S256 challenge
    // When: the code is exchanged without providing a code verifier
    // Then: the exchange is rejected because PKCE verification is required
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

    // Given: an authorization code issued for client "pos-client"
    // When: a different client attempts to exchange the code
    // Then: the exchange is rejected and returns null
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

    // Given: an authorization code that has already been exchanged once
    // When: a second exchange attempt is made
    // Then: the exchange is rejected to prevent authorization code replay
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

    // Given: an authorization code grain that was never issued
    // When: an exchange is attempted
    // Then: the exchange returns null since no code exists
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

    // Given: an authorization code that has been issued
    // When: its validity is checked
    // Then: it reports as valid
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

    // Given: an authorization code grain that was never issued
    // When: its validity is checked
    // Then: it reports as invalid
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

    // Given: an authorization code that has already been exchanged
    // When: its validity is checked
    // Then: it reports as invalid since the code has been consumed
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

    // Given: a Google external identity with user details including email, name, and avatar
    // When: the external identity is linked to an internal user
    // Then: the linked user info contains all provided identity details and a link timestamp
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

    // Given: a Microsoft external identity already linked to a user
    // When: a second link attempt is made for the same external identity
    // Then: an error is raised indicating the identity is already linked
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

    // Given: a GitHub external identity linked to a user with an email
    // When: the linked user info is retrieved
    // Then: the user ID, organization, and email are returned correctly
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

    // Given: a linked Google external identity that has been accessed once
    // When: the linked user info is retrieved a second time
    // Then: the last-used timestamp is updated to reflect the latest access
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

    // Given: an external identity grain that has never been linked
    // When: the linked user info is retrieved
    // Then: null is returned indicating no link exists
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

    // Given: a linked Google external identity with original email, name, and picture URL
    // When: all identity fields are updated with new values
    // Then: the linked user info reflects the updated email, name, and picture URL
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

    // Given: a linked external identity with original email, name, and picture URL
    // When: only the name field is updated (email and picture are null)
    // Then: only the name changes while email and picture retain their original values
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

    // Given: an external identity grain that has never been linked
    // When: an info update is attempted
    // Then: the operation completes without error and the identity remains unlinked
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

    // Given: a linked Google external identity
    // When: the identity link is removed
    // Then: the linked user info returns null indicating the link has been cleared
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

    // Given: an external identity grain that has never been linked
    // When: an unlink is attempted
    // Then: the operation completes without error
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

    // Given: an organization's OAuth lookup with no registered external IDs
    // When: a Google external ID is registered for a user
    // Then: looking up that external ID returns the correct user
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

    // Given: a Google external ID already registered to a user
    // When: the same user re-registers the same external ID
    // Then: the operation is idempotent and succeeds without error
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

    // Given: a Google external ID already linked to one user
    // When: a different user tries to register the same external ID
    // Then: an error is raised indicating the identity is already linked to another user
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

    // Given: a Microsoft external ID registered to a user in an organization
    // When: the external ID is looked up by provider and ID
    // Then: the correct user ID is returned
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

    // Given: an organization's OAuth lookup with no matching external IDs
    // When: an unknown provider and external ID are looked up
    // Then: null is returned indicating no match exists
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

    // Given: a Google external ID registered with mixed-case provider name "Google"
    // When: the lookup is performed with lowercase provider name "google"
    // Then: the user is found because provider matching is case-insensitive
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

    // Given: a GitHub external ID registered to a user
    // When: the external ID registration is removed
    // Then: looking up that external ID returns null
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

    // Given: an organization's OAuth lookup with no matching registration
    // When: an unregister is attempted for a nonexistent external ID
    // Then: the operation completes without error
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

    // Given: a user with Google, Microsoft, and GitHub external identities registered
    // When: all external IDs for that user are retrieved
    // Then: all three provider-to-external-ID mappings are returned
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

    // Given: a user with no external identity links in the organization
    // When: the user's external IDs are retrieved
    // Then: an empty dictionary is returned
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

    // Given: two users each with different external identity providers
    // When: external IDs are retrieved for the first user
    // Then: only the first user's external IDs are returned, not the second user's
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
