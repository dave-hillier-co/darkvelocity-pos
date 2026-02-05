using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Index grain for querying cost alerts within an organization.
/// Maintains a dictionary of alert summaries for efficient querying without
/// activating individual CostAlertGrain instances.
/// </summary>
public class CostAlertIndexGrain : Grain, ICostAlertIndexGrain
{
    private readonly IPersistentState<CostAlertIndexState> _state;

    public CostAlertIndexGrain(
        [PersistentState("costalertindex", "OrleansStorage")]
        IPersistentState<CostAlertIndexState> state)
    {
        _state = state;
    }

    public async Task RegisterAsync(Guid alertId, CostAlertSummary summary)
    {
        var now = DateTime.UtcNow;

        if (_state.State.CreatedAt == default)
        {
            InitializeState();
        }

        _state.State.Alerts[alertId] = summary;
        _state.State.ModifiedAt = now;
        _state.State.Version++;

        TrimIfNeeded();

        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(Guid alertId, bool isAcknowledged, CostAlertAction actionTaken, DateTime? acknowledgedAt)
    {
        if (!_state.State.Alerts.TryGetValue(alertId, out var existing))
        {
            return; // Alert not found
        }

        var updated = existing with
        {
            IsAcknowledged = isAcknowledged,
            ActionTaken = actionTaken,
            AcknowledgedAt = acknowledgedAt
        };

        _state.State.Alerts[alertId] = updated;
        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(Guid alertId)
    {
        if (_state.State.Alerts.Remove(alertId))
        {
            _state.State.ModifiedAt = DateTime.UtcNow;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<CostAlertQueryResult> QueryAsync(CostAlertQuery query)
    {
        var alerts = _state.State.Alerts.Values.AsEnumerable();

        // Apply filters
        if (query.Status.HasValue && query.Status.Value != CostAlertStatus.All)
        {
            var isAcknowledged = query.Status.Value == CostAlertStatus.Acknowledged;
            alerts = alerts.Where(a => a.IsAcknowledged == isAcknowledged);
        }

        if (query.AlertType.HasValue)
        {
            alerts = alerts.Where(a => a.AlertType == query.AlertType.Value);
        }

        if (query.FromDate.HasValue)
        {
            alerts = alerts.Where(a => a.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            alerts = alerts.Where(a => a.CreatedAt <= query.ToDate.Value);
        }

        var filteredList = alerts
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var totalCount = filteredList.Count;
        var activeCount = _state.State.Alerts.Values.Count(a => !a.IsAcknowledged);
        var acknowledgedCount = _state.State.Alerts.Values.Count(a => a.IsAcknowledged);

        var paginatedList = filteredList
            .Skip(query.Skip)
            .Take(query.Take)
            .ToList();

        return Task.FromResult(new CostAlertQueryResult(
            paginatedList,
            totalCount,
            activeCount,
            acknowledgedCount));
    }

    public Task<IReadOnlyList<Guid>> GetAllAlertIdsAsync()
    {
        var ids = _state.State.Alerts.Keys.ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(ids);
    }

    public Task<int> GetActiveCountAsync()
    {
        var count = _state.State.Alerts.Values.Count(a => !a.IsAcknowledged);
        return Task.FromResult(count);
    }

    public Task<CostAlertSummary?> GetByIdAsync(Guid alertId)
    {
        if (_state.State.Alerts.TryGetValue(alertId, out var summary))
        {
            return Task.FromResult<CostAlertSummary?>(summary);
        }
        return Task.FromResult<CostAlertSummary?>(null);
    }

    public async Task ClearAsync()
    {
        _state.State.Alerts.Clear();
        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void InitializeState()
    {
        var now = DateTime.UtcNow;
        _state.State.CreatedAt = now;
        _state.State.ModifiedAt = now;

        // Parse key: org:{orgId}:index:costalerts
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');

        if (parts.Length >= 4 && parts[0] == "org" && parts[2] == "index")
        {
            if (Guid.TryParse(parts[1], out var orgId))
            {
                _state.State.OrganizationId = orgId;
            }
        }
    }

    private void TrimIfNeeded()
    {
        var maxEntries = _state.State.MaxEntries;
        if (_state.State.Alerts.Count <= maxEntries)
        {
            return;
        }

        // Remove oldest acknowledged alerts first, then oldest active alerts
        var alertsToRemove = _state.State.Alerts
            .OrderBy(kvp => kvp.Value.IsAcknowledged ? 0 : 1) // Acknowledged first
            .ThenBy(kvp => kvp.Value.CreatedAt) // Then by age
            .Take(_state.State.Alerts.Count - maxEntries)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in alertsToRemove)
        {
            _state.State.Alerts.Remove(key);
        }
    }
}
