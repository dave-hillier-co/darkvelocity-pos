using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for managing ordering links (QR codes, kiosk URLs).
/// Each link has a unique short code for public access.
/// </summary>
public class OrderingLinkGrain : Grain, IOrderingLinkGrain
{
    private readonly IPersistentState<OrderingLinkState> _state;

    public OrderingLinkGrain(
        [PersistentState("orderingLink", "OrleansStorage")]
        IPersistentState<OrderingLinkState> state)
    {
        _state = state;
    }

    public async Task<OrderingLinkSnapshot> CreateAsync(CreateOrderingLinkCommand command)
    {
        if (_state.State.LinkId != Guid.Empty)
            throw new InvalidOperationException("Ordering link already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var linkId = Guid.Parse(parts[2]);

        _state.State = new OrderingLinkState
        {
            LinkId = linkId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Type = command.Type,
            Name = command.Name,
            ShortCode = GenerateShortCode(linkId),
            IsActive = true,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<OrderingLinkSnapshot> UpdateAsync(UpdateOrderingLinkCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.TableId.HasValue) _state.State.TableId = command.TableId;
        if (command.TableNumber != null) _state.State.TableNumber = command.TableNumber;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        EnsureExists();
        _state.State.IsActive = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<OrderingLinkSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.LinkId != Guid.Empty);
    }

    private OrderingLinkSnapshot CreateSnapshot() => new(
        LinkId: _state.State.LinkId,
        OrganizationId: _state.State.OrganizationId,
        SiteId: _state.State.SiteId,
        Type: _state.State.Type,
        Name: _state.State.Name,
        ShortCode: _state.State.ShortCode,
        IsActive: _state.State.IsActive,
        TableId: _state.State.TableId,
        TableNumber: _state.State.TableNumber,
        CreatedAt: _state.State.CreatedAt);

    private void EnsureExists()
    {
        if (_state.State.LinkId == Guid.Empty)
            throw new InvalidOperationException("Ordering link does not exist");
    }

    private static string GenerateShortCode(Guid linkId)
    {
        // Generate a URL-safe 8-character code from the link ID
        var bytes = linkId.ToByteArray();
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..8]
            .ToUpperInvariant();
    }
}
