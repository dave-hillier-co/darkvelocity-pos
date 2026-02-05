using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for managing alerts at site level.
/// </summary>
public class AlertGrain : Grain, IAlertGrain
{
    private readonly IPersistentState<AlertState> _state;
    private IAsyncStream<IStreamEvent>? _alertStream;

    public AlertGrain(
        [PersistentState("alerts", "OrleansStorage")]
        IPersistentState<AlertState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetAlertStream()
    {
        if (_alertStream == null && _state.State.OrgId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, _state.State.OrgId.ToString());
            _alertStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _alertStream!;
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
            Metadata = command.Metadata
        };

        _state.State.Alerts.Add(alertRecord);
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish alert triggered event for notifications and integrations
        await GetAlertStream().OnNextAsync(new AlertTriggeredEvent(
            alertId,
            _state.State.SiteId,
            command.Type.ToString(),
            command.Severity.ToString(),
            command.Title,
            command.Message,
            alertRecord.Metadata ?? new Dictionary<string, string>())
        {
            OrganizationId = _state.State.OrgId
        });

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
        // No-op when called without metrics - kept for backward compatibility
        // Rule evaluation requires a MetricsSnapshot to evaluate against
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Alert>> EvaluateRulesAsync(MetricsSnapshot metrics)
    {
        EnsureInitialized();

        var triggeredAlerts = new List<Alert>();
        var now = DateTime.UtcNow;

        foreach (var rule in _state.State.Rules.Where(r => r.IsEnabled))
        {
            // Check if we're still in cooldown for this rule
            if (rule.CooldownPeriod.HasValue &&
                _state.State.RuleLastTriggered.TryGetValue(rule.RuleId, out var lastTriggered))
            {
                if (now - lastTriggered < rule.CooldownPeriod.Value)
                    continue; // Still in cooldown
            }

            // Evaluate the rule against the metrics
            var evaluationResult = EvaluateRule(rule, metrics);

            if (evaluationResult.Triggered)
            {
                // Create the alert
                var alertCommand = new CreateAlertCommand(
                    Type: rule.Type,
                    Severity: rule.DefaultSeverity,
                    Title: GenerateAlertTitle(rule, metrics, evaluationResult),
                    Message: GenerateAlertMessage(rule, metrics, evaluationResult),
                    EntityId: metrics.EntityId,
                    EntityType: metrics.EntityType,
                    Metadata: BuildAlertMetadata(rule, metrics, evaluationResult));

                var alert = await CreateAlertAsync(alertCommand);
                triggeredAlerts.Add(alert);

                // Record the last triggered time for cooldown
                _state.State.RuleLastTriggered[rule.RuleId] = now;
            }
        }

        if (triggeredAlerts.Count > 0)
        {
            await _state.WriteStateAsync();
        }

        return triggeredAlerts;
    }

    private static RuleEvaluationResult EvaluateRule(AlertRuleRecord rule, MetricsSnapshot metrics)
    {
        // Get the primary metric value
        if (!metrics.Metrics.TryGetValue(rule.Metric, out var primaryValue))
        {
            return new RuleEvaluationResult
            {
                Rule = ToAlertRule(rule),
                Triggered = false,
                Message = $"Metric '{rule.Metric}' not found in snapshot"
            };
        }

        // Get threshold - may be dynamic from secondary metric
        var threshold = rule.Threshold;
        if (rule.SecondaryMetric != null && metrics.Metrics.TryGetValue(rule.SecondaryMetric, out var secondaryValue))
        {
            // For some operators, the threshold comes from the secondary metric
            if (rule.Operator == ComparisonOperator.ChangedBy)
            {
                // For ChangedBy, threshold is the change amount, compare against secondary
                var change = primaryValue - secondaryValue;
                return new RuleEvaluationResult
                {
                    Rule = ToAlertRule(rule),
                    Triggered = rule.Threshold < 0 ? change <= rule.Threshold : change >= rule.Threshold,
                    ActualValue = change,
                    ThresholdValue = rule.Threshold,
                    Message = $"Value changed from {secondaryValue:N2} to {primaryValue:N2} (change: {change:N2})"
                };
            }
            else
            {
                // Secondary value is the dynamic threshold (e.g., ReorderPoint)
                threshold = secondaryValue;
            }
        }

        // Evaluate based on operator
        var triggered = rule.Operator switch
        {
            ComparisonOperator.GreaterThan => primaryValue > threshold,
            ComparisonOperator.GreaterThanOrEqual => primaryValue >= threshold,
            ComparisonOperator.LessThan => primaryValue < threshold,
            ComparisonOperator.LessThanOrEqual => primaryValue <= threshold,
            ComparisonOperator.Equal => primaryValue == threshold,
            ComparisonOperator.NotEqual => primaryValue != threshold,
            _ => false
        };

        return new RuleEvaluationResult
        {
            Rule = ToAlertRule(rule),
            Triggered = triggered,
            ActualValue = primaryValue,
            ThresholdValue = threshold,
            Message = $"{rule.Metric}: {primaryValue:N2} {GetOperatorSymbol(rule.Operator)} {threshold:N2}"
        };
    }

    private static string GetOperatorSymbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.Equal => "==",
        ComparisonOperator.NotEqual => "!=",
        ComparisonOperator.ChangedBy => "changed by",
        _ => "?"
    };

    private static string GenerateAlertTitle(AlertRuleRecord rule, MetricsSnapshot metrics, RuleEvaluationResult result)
    {
        var entityName = metrics.EntityName ?? "Unknown";
        return rule.Type switch
        {
            AlertType.LowStock => $"Low Stock: {entityName}",
            AlertType.OutOfStock => $"Out of Stock: {entityName}",
            AlertType.NegativeStock => $"Negative Stock: {entityName}",
            AlertType.ExpiryRisk => $"Expiry Risk: {entityName}",
            AlertType.GPDropped => $"GP% Dropped",
            AlertType.HighVariance => $"High Cost Variance",
            AlertType.SupplierPriceSpike => $"Price Spike: {entityName}",
            AlertType.HighWaste => $"High Waste",
            AlertType.HighVoidRate => $"High Void Rate",
            _ => rule.Name
        };
    }

    private static string GenerateAlertMessage(AlertRuleRecord rule, MetricsSnapshot metrics, RuleEvaluationResult result)
    {
        var entityName = metrics.EntityName ?? "Unknown";
        return rule.Type switch
        {
            AlertType.LowStock => $"{entityName} is below reorder point. Current: {result.ActualValue:N2}, Reorder Point: {result.ThresholdValue:N2}",
            AlertType.OutOfStock => $"{entityName} is out of stock.",
            AlertType.NegativeStock => $"{entityName} has negative stock quantity: {result.ActualValue:N2}",
            AlertType.ExpiryRisk => $"{entityName} expires in {result.ActualValue:N0} days",
            AlertType.GPDropped => $"Gross profit dropped by {Math.Abs(result.ActualValue ?? 0):N1}% vs last week",
            AlertType.HighVariance => $"Actual vs theoretical cost variance is {result.ActualValue:N1}%",
            AlertType.SupplierPriceSpike => $"{entityName} price increased by {result.ActualValue:N1}%",
            AlertType.HighWaste => $"Waste percentage is {result.ActualValue:N1}%",
            AlertType.HighVoidRate => $"Void rate is {result.ActualValue:N1}%",
            _ => result.Message ?? rule.Description
        };
    }

    private static Dictionary<string, string> BuildAlertMetadata(AlertRuleRecord rule, MetricsSnapshot metrics, RuleEvaluationResult result)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ruleId"] = rule.RuleId.ToString(),
            ["ruleName"] = rule.Name,
            ["metric"] = rule.Metric,
            ["actualValue"] = result.ActualValue?.ToString("N2") ?? "N/A",
            ["thresholdValue"] = result.ThresholdValue?.ToString("N2") ?? "N/A",
            ["operator"] = rule.Operator.ToString()
        };

        if (metrics.EntityName != null)
            metadata["entityName"] = metrics.EntityName;

        if (metrics.Context != null)
        {
            foreach (var kv in metrics.Context)
            {
                metadata[kv.Key] = kv.Value;
            }
        }

        return metadata;
    }

    private static AlertRule ToAlertRule(AlertRuleRecord r) => new()
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
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = r.CooldownPeriod
    };

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
            Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
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

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.SiteId != Guid.Empty);
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
            Metadata = record.Metadata
        };
    }
}
