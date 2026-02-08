using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Registry grain for listing and looking up ordering links by short code.
/// One per site.
/// </summary>
public class OrderingLinkRegistryGrain : Grain, IOrderingLinkRegistryGrain
{
    private readonly IPersistentState<OrderingLinkRegistryState> _state;

    public OrderingLinkRegistryGrain(
        [PersistentState("orderingLinkRegistry", "OrleansStorage")]
        IPersistentState<OrderingLinkRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterLinkAsync(OrderingLinkSummary summary)
    {
        EnsureInitialized();

        // Remove existing entry with the same ID if re-registering
        _state.State.Links.RemoveAll(l => l.LinkId == summary.LinkId);

        _state.State.Links.Add(new OrderingLinkRegistryEntry
        {
            LinkId = summary.LinkId,
            Name = summary.Name,
            Type = summary.Type,
            ShortCode = summary.ShortCode,
            IsActive = summary.IsActive,
            TableId = summary.TableId,
            TableNumber = summary.TableNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task UpdateLinkAsync(Guid linkId, string? name, bool? isActive)
    {
        EnsureInitialized();

        var entry = _state.State.Links.FirstOrDefault(l => l.LinkId == linkId);
        if (entry == null) return;

        if (name != null) entry.Name = name;
        if (isActive.HasValue) entry.IsActive = isActive.Value;

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<OrderingLinkSummary>> GetLinksAsync(bool includeInactive = false)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<IReadOnlyList<OrderingLinkSummary>>([]);

        var links = _state.State.Links.AsEnumerable();

        if (!includeInactive)
            links = links.Where(l => l.IsActive);

        return Task.FromResult<IReadOnlyList<OrderingLinkSummary>>(
            links.Select(l => new OrderingLinkSummary(
                LinkId: l.LinkId,
                Name: l.Name,
                Type: l.Type,
                ShortCode: l.ShortCode,
                IsActive: l.IsActive,
                TableId: l.TableId,
                TableNumber: l.TableNumber
            )).ToList());
    }

    public Task<OrderingLinkSummary?> FindByShortCodeAsync(string shortCode)
    {
        if (!_state.State.IsCreated)
            return Task.FromResult<OrderingLinkSummary?>(null);

        var entry = _state.State.Links.FirstOrDefault(l =>
            l.ShortCode.Equals(shortCode, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            return Task.FromResult<OrderingLinkSummary?>(null);

        return Task.FromResult<OrderingLinkSummary?>(new OrderingLinkSummary(
            LinkId: entry.LinkId,
            Name: entry.Name,
            Type: entry.Type,
            ShortCode: entry.ShortCode,
            IsActive: entry.IsActive,
            TableId: entry.TableId,
            TableNumber: entry.TableNumber));
    }

    private void EnsureInitialized()
    {
        if (_state.State.IsCreated) return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        _state.State.OrganizationId = Guid.Parse(parts[0]);
        _state.State.SiteId = Guid.Parse(parts[1]);
        _state.State.IsCreated = true;
    }
}
