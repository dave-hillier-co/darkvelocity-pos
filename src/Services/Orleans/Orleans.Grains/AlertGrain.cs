using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

/// <summary>
/// Grain for managing alerts at site level.
/// </summary>
public class AlertGrain : Grain, IAlertGrain
{
    private readonly IPersistentState<AlertState> _state;

    public AlertGrain(
        [PersistentState("alerts", "OrleansStorage")]
        IPersistentState<AlertState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid orgId, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new AlertState
        {
            OrgId = orgId,
            SiteId = siteId,
            Version = 1
        };

        // Initialize default rules
        foreach (var rule in AlertRules.All)
        {
            _state.State.Rules.Add(new AlertRuleRecord
            {
                RuleId = rule.RuleId,
                Type = rule.Type,
                Name = rule.Name,
                Description = rule.Description,
                IsEnabled = rule.IsEnabled,
                DefaultSeverity = rule.DefaultSeverity,
                Metric = rule.Condition.Metric,
                Operator = rule.Condition.Operator,
                Threshold = rule.Condition.Threshold,
                SecondaryMetric = rule.Condition.SecondaryMetric,
                SecondaryThreshold = rule.Condition.SecondaryThreshold,
                CooldownPeriod = rule.CooldownPeriod
            });
        }

        await _state.WriteStateAsync();
    }

    public async Task<Alert> CreateAlertAsync(CreateAlertCommand command)
    {
        EnsureInitialized();

        var alertId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var alertRecord = new AlertRecord
        {
            AlertId = alertId,
            Type = command.Type,
            Severity = command.Severity,
            Title = command.Title,
            Message = command.Message,
            EntityId = command.EntityId,
            EntityType = command.EntityType,
            TriggeredAt = now,
            Status = AlertStatus.Active,
            Metadata = command.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString() ?? string.Empty)
        };

        _state.State.Alerts.Add(alertRecord);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return ToAlert(alertRecord);
    }

    public async Task AcknowledgeAsync(AcknowledgeAlertCommand command)
    {
        EnsureInitialized();

        var alert = _state.State.Alerts.FirstOrDefault(a => a.AlertId == command.AlertId)
            ?? throw new InvalidOperationException("Alert not found");

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = command.AcknowledgedBy;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ResolveAsync(ResolveAlertCommand command)
    {
        EnsureInitialized();

        var alert = _state.State.Alerts.FirstOrDefault(a => a.AlertId == command.AlertId)
            ?? throw new InvalidOperationException("Alert not found");

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedBy = command.ResolvedBy;
        alert.ResolutionNotes = command.ResolutionNotes;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SnoozeAsync(SnoozeAlertCommand command)
    {
        EnsureInitialized();

        var alert = _state.State.Alerts.FirstOrDefault(a => a.AlertId == command.AlertId)
            ?? throw new InvalidOperationException("Alert not found");

        alert.Status = AlertStatus.Snoozed;
        alert.SnoozedUntil = DateTime.UtcNow.Add(command.Duration);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DismissAsync(DismissAlertCommand command)
    {
        EnsureInitialized();

        var alert = _state.State.Alerts.FirstOrDefault(a => a.AlertId == command.AlertId)
            ?? throw new InvalidOperationException("Alert not found");

        alert.Status = AlertStatus.Dismissed;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<Alert>> GetActiveAlertsAsync()
    {
        EnsureInitialized();

        var now = DateTime.UtcNow;
        var activeAlerts = _state.State.Alerts
            .Where(a => a.Status == AlertStatus.Active ||
                       (a.Status == AlertStatus.Snoozed && a.SnoozedUntil.HasValue && a.SnoozedUntil.Value <= now))
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.TriggeredAt)
            .Select(ToAlert)
            .ToList();

        return Task.FromResult<IReadOnlyList<Alert>>(activeAlerts);
    }

    public Task<IReadOnlyList<Alert>> GetAlertsAsync(AlertStatus? status = null, AlertType? type = null, int? limit = null)
    {
        EnsureInitialized();

        var query = _state.State.Alerts.AsEnumerable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        query = query.OrderByDescending(a => a.TriggeredAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var alerts = query.Select(ToAlert).ToList();

        return Task.FromResult<IReadOnlyList<Alert>>(alerts);
    }

    public Task<Alert?> GetAlertAsync(Guid alertId)
    {
        EnsureInitialized();

        var alert = _state.State.Alerts.FirstOrDefault(a => a.AlertId == alertId);
        return Task.FromResult(alert != null ? ToAlert(alert) : null);
    }

    public Task<int> GetActiveAlertCountAsync()
    {
        EnsureInitialized();

        var count = _state.State.Alerts.Count(a =>
            a.Status == AlertStatus.Active || a.Status == AlertStatus.Acknowledged);

        return Task.FromResult(count);
    }

    public Task<IReadOnlyDictionary<AlertType, int>> GetAlertCountsByTypeAsync()
    {
        EnsureInitialized();

        var counts = _state.State.Alerts
            .Where(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Acknowledged)
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult<IReadOnlyDictionary<AlertType, int>>(counts);
    }

    public Task EvaluateRulesAsync()
    {
        // Rule evaluation would be implemented here
        // This would check various metrics against rule thresholds
        // and create alerts as needed
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AlertRule>> GetRulesAsync()
    {
        EnsureInitialized();

        var rules = _state.State.Rules.Select(r => new AlertRule
        {
            RuleId = r.RuleId,
            Type = r.Type,
            Name = r.Name,
            Description = r.Description,
            IsEnabled = r.IsEnabled,
            DefaultSeverity = r.DefaultSeverity,
            Condition = new AlertRuleCondition
            {
                Metric = r.Metric,
                Operator = r.Operator,
                Threshold = r.Threshold,
                SecondaryMetric = r.SecondaryMetric,
                SecondaryThreshold = r.SecondaryThreshold
            },
            Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
            CooldownPeriod = r.CooldownPeriod
        }).ToList();

        return Task.FromResult<IReadOnlyList<AlertRule>>(rules);
    }

    public async Task UpdateRuleAsync(AlertRule rule)
    {
        EnsureInitialized();

        var existingRule = _state.State.Rules.FirstOrDefault(r => r.RuleId == rule.RuleId);
        if (existingRule == null)
        {
            _state.State.Rules.Add(new AlertRuleRecord
            {
                RuleId = rule.RuleId,
                Type = rule.Type,
                Name = rule.Name,
                Description = rule.Description,
                IsEnabled = rule.IsEnabled,
                DefaultSeverity = rule.DefaultSeverity,
                Metric = rule.Condition.Metric,
                Operator = rule.Condition.Operator,
                Threshold = rule.Condition.Threshold,
                SecondaryMetric = rule.Condition.SecondaryMetric,
                SecondaryThreshold = rule.Condition.SecondaryThreshold,
                CooldownPeriod = rule.CooldownPeriod
            });
        }
        else
        {
            existingRule.Name = rule.Name;
            existingRule.Description = rule.Description;
            existingRule.IsEnabled = rule.IsEnabled;
            existingRule.DefaultSeverity = rule.DefaultSeverity;
            existingRule.Metric = rule.Condition.Metric;
            existingRule.Operator = rule.Condition.Operator;
            existingRule.Threshold = rule.Condition.Threshold;
            existingRule.SecondaryMetric = rule.Condition.SecondaryMetric;
            existingRule.SecondaryThreshold = rule.Condition.SecondaryThreshold;
            existingRule.CooldownPeriod = rule.CooldownPeriod;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Alert grain not initialized");
    }

    private Alert ToAlert(AlertRecord record)
    {
        return new Alert
        {
            AlertId = record.AlertId,
            Type = record.Type,
            Severity = record.Severity,
            Title = record.Title,
            Message = record.Message,
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            EntityId = record.EntityId,
            EntityType = record.EntityType,
            TriggeredAt = record.TriggeredAt,
            Status = record.Status,
            AcknowledgedAt = record.AcknowledgedAt,
            AcknowledgedBy = record.AcknowledgedBy,
            ResolvedAt = record.ResolvedAt,
            ResolvedBy = record.ResolvedBy,
            ResolutionNotes = record.ResolutionNotes,
            SnoozedUntil = record.SnoozedUntil,
            Metadata = record.Metadata?.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
        };
    }
}
