using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Represents a user's organization membership for email lookup.
/// </summary>
[GenerateSerializer]
public sealed record EmailUserMapping(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid UserId);

/// <summary>
/// State for global email to organization/user mapping.
/// Key format: normalized email -> List of (OrgId, UserId)
/// </summary>
[GenerateSerializer]
public sealed class EmailLookupState
{
    [Id(0)] public Dictionary<string, List<EmailUserMapping>> EmailToUsers { get; set; } = [];
}

/// <summary>
/// Global singleton grain for mapping email addresses to organizations and users.
/// Used during OAuth login to find which organizations a user belongs to.
/// Key: "global:emaillookup" (single instance)
/// </summary>
public interface IEmailLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers an email address for a user in an organization.
    /// Called when a user is created or their email is updated.
    /// </summary>
    Task RegisterEmailAsync(string email, Guid orgId, Guid userId);

    /// <summary>
    /// Finds all organizations that have a user with this email address.
    /// Returns a list of (OrgId, UserId) tuples.
    /// </summary>
    Task<IReadOnlyList<EmailUserMapping>> FindByEmailAsync(string email);

    /// <summary>
    /// Unregisters an email for a specific organization.
    /// Called when a user is deactivated or their email changes.
    /// </summary>
    Task UnregisterEmailAsync(string email, Guid orgId);

    /// <summary>
    /// Updates the email for an existing user (removes old, adds new).
    /// </summary>
    Task UpdateEmailAsync(string oldEmail, string newEmail, Guid orgId, Guid userId);
}

public class EmailLookupGrain : Grain, IEmailLookupGrain
{
    private readonly IPersistentState<EmailLookupState> _state;

    public EmailLookupGrain(
        [PersistentState("emaillookup", "OrleansStorage")]
        IPersistentState<EmailLookupState> state)
    {
        _state = state;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public async Task RegisterEmailAsync(string email, Guid orgId, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = NormalizeEmail(email);
        var mapping = new EmailUserMapping(orgId, userId);

        if (!_state.State.EmailToUsers.TryGetValue(normalizedEmail, out var mappings))
        {
            mappings = [];
            _state.State.EmailToUsers[normalizedEmail] = mappings;
        }

        // Check if already registered for this org
        var existing = mappings.FirstOrDefault(m => m.OrganizationId == orgId);
        if (existing != null)
        {
            if (existing.UserId != userId)
            {
                // Email is already registered to a different user in this org
                throw new InvalidOperationException(
                    $"Email {email} is already registered to a different user in this organization");
            }
            return; // Already registered to the same user
        }

        mappings.Add(mapping);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<EmailUserMapping>> FindByEmailAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = NormalizeEmail(email);
        if (_state.State.EmailToUsers.TryGetValue(normalizedEmail, out var mappings))
        {
            return Task.FromResult<IReadOnlyList<EmailUserMapping>>(mappings);
        }
        return Task.FromResult<IReadOnlyList<EmailUserMapping>>([]);
    }

    public async Task UnregisterEmailAsync(string email, Guid orgId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = NormalizeEmail(email);
        if (!_state.State.EmailToUsers.TryGetValue(normalizedEmail, out var mappings))
        {
            return;
        }

        var removed = mappings.RemoveAll(m => m.OrganizationId == orgId);
        if (removed > 0)
        {
            if (mappings.Count == 0)
            {
                _state.State.EmailToUsers.Remove(normalizedEmail);
            }
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateEmailAsync(string oldEmail, string newEmail, Guid orgId, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);

        if (!string.IsNullOrWhiteSpace(oldEmail))
        {
            await UnregisterEmailAsync(oldEmail, orgId);
        }
        await RegisterEmailAsync(newEmail, orgId, userId);
    }
}
