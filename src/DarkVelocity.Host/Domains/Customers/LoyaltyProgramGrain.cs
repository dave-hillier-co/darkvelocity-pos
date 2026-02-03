using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class LoyaltyProgramGrain : Grain, ILoyaltyProgramGrain
{
    private readonly IPersistentState<LoyaltyProgramState> _state;

    public LoyaltyProgramGrain(
        [PersistentState("loyaltyprogram", "OrleansStorage")]
        IPersistentState<LoyaltyProgramState> state)
    {
        _state = state;
    }

    public async Task<LoyaltyProgramCreatedResult> CreateAsync(CreateLoyaltyProgramCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Loyalty program already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, programId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new LoyaltyProgramState
        {
            Id = programId,
            OrganizationId = command.OrganizationId,
            Name = command.Name,
            Description = command.Description,
            Status = ProgramStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        return new LoyaltyProgramCreatedResult(programId, command.Name, _state.State.CreatedAt);
    }

    public Task<LoyaltyProgramState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(string? name, string? description)
    {
        EnsureExists();

        if (name != null) _state.State.Name = name;
        if (description != null) _state.State.Description = description;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        EnsureExists();

        if (_state.State.Status == ProgramStatus.Active)
            throw new InvalidOperationException("Program is already active");

        if (_state.State.Status == ProgramStatus.Archived)
            throw new InvalidOperationException("Cannot activate archived program");

        // Validate program has at least one earning rule and one tier
        if (_state.State.EarningRules.Count == 0)
            throw new InvalidOperationException("Program must have at least one earning rule");

        if (_state.State.Tiers.Count == 0)
            throw new InvalidOperationException("Program must have at least one tier");

        _state.State.Status = ProgramStatus.Active;
        _state.State.ActivatedAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task PauseAsync()
    {
        EnsureExists();

        if (_state.State.Status != ProgramStatus.Active)
            throw new InvalidOperationException($"Cannot pause program: {_state.State.Status}");

        _state.State.Status = ProgramStatus.Paused;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        if (_state.State.Status == ProgramStatus.Archived)
            throw new InvalidOperationException("Program is already archived");

        _state.State.Status = ProgramStatus.Archived;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<EarningRuleResult> AddEarningRuleAsync(AddEarningRuleCommand command)
    {
        EnsureExists();

        var ruleId = Guid.NewGuid();
        var rule = new EarningRule
        {
            Id = ruleId,
            Name = command.Name,
            Type = command.Type,
            PointsPerDollar = command.PointsPerDollar,
            PointsPerVisit = command.PointsPerVisit,
            BonusMultiplier = command.BonusMultiplier,
            ApplicableDays = command.ApplicableDays,
            MinimumSpend = command.MinimumSpend,
            IsActive = true
        };

        _state.State.EarningRules.Add(rule);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new EarningRuleResult(ruleId);
    }

    public async Task UpdateEarningRuleAsync(Guid ruleId, bool isActive)
    {
        EnsureExists();

        var index = _state.State.EarningRules.FindIndex(r => r.Id == ruleId);
        if (index < 0)
            throw new InvalidOperationException("Earning rule not found");

        _state.State.EarningRules[index] = _state.State.EarningRules[index] with { IsActive = isActive };
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveEarningRuleAsync(Guid ruleId)
    {
        EnsureExists();

        var removed = _state.State.EarningRules.RemoveAll(r => r.Id == ruleId);
        if (removed == 0)
            throw new InvalidOperationException("Earning rule not found");

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<TierResult> AddTierAsync(AddTierCommand command)
    {
        EnsureExists();

        if (_state.State.Tiers.Any(t => t.Level == command.Level))
            throw new InvalidOperationException($"Tier with level {command.Level} already exists");

        var tierId = Guid.NewGuid();
        var tier = new LoyaltyTier
        {
            Id = tierId,
            Name = command.Name,
            Level = command.Level,
            PointsRequired = command.PointsRequired,
            Benefits = command.Benefits ?? [],
            EarningMultiplier = command.EarningMultiplier,
            MaintenancePoints = command.MaintenancePoints,
            GracePeriodDays = command.GracePeriodDays,
            Color = command.Color
        };

        _state.State.Tiers.Add(tier);
        _state.State.Tiers = _state.State.Tiers.OrderBy(t => t.Level).ToList();
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new TierResult(tierId);
    }

    public async Task UpdateTierAsync(Guid tierId, int? pointsRequired, List<TierBenefit>? benefits)
    {
        EnsureExists();

        var index = _state.State.Tiers.FindIndex(t => t.Id == tierId);
        if (index < 0)
            throw new InvalidOperationException("Tier not found");

        var tier = _state.State.Tiers[index];
        _state.State.Tiers[index] = tier with
        {
            PointsRequired = pointsRequired ?? tier.PointsRequired,
            Benefits = benefits ?? tier.Benefits
        };

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveTierAsync(Guid tierId)
    {
        EnsureExists();

        var removed = _state.State.Tiers.RemoveAll(t => t.Id == tierId);
        if (removed == 0)
            throw new InvalidOperationException("Tier not found");

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<LoyaltyTier?> GetTierByLevelAsync(int level)
        => Task.FromResult(_state.State.Tiers.FirstOrDefault(t => t.Level == level));

    public Task<LoyaltyTier?> GetNextTierAsync(int currentLevel)
        => Task.FromResult(_state.State.Tiers.Where(t => t.Level > currentLevel).OrderBy(t => t.Level).FirstOrDefault());

    public async Task<RewardDefinitionResult> AddRewardAsync(AddRewardCommand command)
    {
        EnsureExists();

        var rewardId = Guid.NewGuid();
        var reward = new RewardDefinition
        {
            Id = rewardId,
            Name = command.Name,
            Description = command.Description,
            Type = command.Type,
            PointsCost = command.PointsCost,
            DiscountValue = command.DiscountValue,
            DiscountType = command.DiscountType,
            FreeItemId = command.FreeItemId,
            MinimumTierLevel = command.MinimumTierLevel,
            LimitPerCustomer = command.LimitPerCustomer,
            LimitPeriod = command.LimitPeriod,
            ValidDays = command.ValidDays,
            IsActive = true
        };

        _state.State.Rewards.Add(reward);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new RewardDefinitionResult(rewardId);
    }

    public async Task UpdateRewardAsync(Guid rewardId, int? pointsCost, bool? isActive)
    {
        EnsureExists();

        var index = _state.State.Rewards.FindIndex(r => r.Id == rewardId);
        if (index < 0)
            throw new InvalidOperationException("Reward not found");

        var reward = _state.State.Rewards[index];
        _state.State.Rewards[index] = reward with
        {
            PointsCost = pointsCost ?? reward.PointsCost,
            IsActive = isActive ?? reward.IsActive
        };

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveRewardAsync(Guid rewardId)
    {
        EnsureExists();

        var removed = _state.State.Rewards.RemoveAll(r => r.Id == rewardId);
        if (removed == 0)
            throw new InvalidOperationException("Reward not found");

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<RewardDefinition>> GetAvailableRewardsAsync(int tierLevel)
        => Task.FromResult<IReadOnlyList<RewardDefinition>>(
            _state.State.Rewards
                .Where(r => r.IsActive && (r.MinimumTierLevel == null || r.MinimumTierLevel <= tierLevel))
                .ToList());

    public async Task ConfigurePointsExpiryAsync(ConfigurePointsExpiryCommand command)
    {
        EnsureExists();

        _state.State.PointsExpiry = new PointsExpiryConfig
        {
            Enabled = command.Enabled,
            ExpiryMonths = command.ExpiryMonths,
            WarningDays = command.WarningDays
        };

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task ConfigureReferralProgramAsync(ConfigureReferralCommand command)
    {
        EnsureExists();

        _state.State.ReferralProgram = new ReferralConfig
        {
            Enabled = command.Enabled,
            ReferrerPoints = command.ReferrerPoints,
            RefereePoints = command.RefereePoints,
            MinimumQualifyingSpend = command.MinimumQualifyingSpend
        };

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SetTermsAndConditionsAsync(string terms)
    {
        EnsureExists();

        _state.State.TermsAndConditions = terms;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<PointsCalculation> CalculatePointsAsync(decimal spendAmount, int customerTierLevel, Guid siteId, DateTime timestamp)
    {
        EnsureExists();

        // Find the best matching earning rule
        var applicableRules = _state.State.EarningRules
            .Where(r => r.IsActive)
            .Where(r => r.ApplicableDays == null || r.ApplicableDays.Contains(timestamp.DayOfWeek))
            .Where(r => r.MinimumSpend == null || spendAmount >= r.MinimumSpend)
            .ToList();

        if (applicableRules.Count == 0)
            return Task.FromResult(new PointsCalculation(0, 1m, 0, null));

        // Find the primary earning rule (PerDollar takes precedence)
        var primaryRule = applicableRules.FirstOrDefault(r => r.Type == EarningType.PerDollar)
            ?? applicableRules.First();

        int basePoints = primaryRule.Type switch
        {
            EarningType.PerDollar => (int)(spendAmount * (primaryRule.PointsPerDollar ?? 1)),
            EarningType.PerVisit => primaryRule.PointsPerVisit ?? 0,
            _ => 0
        };

        // Get tier multiplier
        var tier = _state.State.Tiers.FirstOrDefault(t => t.Level == customerTierLevel);
        var tierMultiplier = tier?.EarningMultiplier ?? 1m;

        // Check for bonus rules (like bonus days)
        var bonusRule = applicableRules.FirstOrDefault(r => r.Type == EarningType.BonusDay && r.BonusMultiplier != null);
        var bonusMultiplier = bonusRule?.BonusMultiplier ?? 1m;

        var totalMultiplier = tierMultiplier * bonusMultiplier;
        var totalPoints = (int)(basePoints * totalMultiplier);

        return Task.FromResult(new PointsCalculation(basePoints, totalMultiplier, totalPoints, primaryRule.Id));
    }

    public async Task IncrementEnrollmentsAsync()
    {
        EnsureExists();

        _state.State.TotalEnrollments++;
        _state.State.ActiveMembers++;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task DecrementActiveMembersAsync()
    {
        EnsureExists();

        if (_state.State.ActiveMembers > 0)
        {
            _state.State.ActiveMembers--;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordPointsIssuedAsync(int points)
    {
        EnsureExists();

        _state.State.TotalPointsIssued += points;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordPointsRedeemedAsync(int points)
    {
        EnsureExists();

        _state.State.TotalPointsRedeemed += points;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<bool> IsActiveAsync() => Task.FromResult(_state.State.Status == ProgramStatus.Active);

    public Task<IReadOnlyList<EarningRule>> GetEarningRulesAsync()
        => Task.FromResult<IReadOnlyList<EarningRule>>(_state.State.EarningRules.Where(r => r.IsActive).ToList());

    public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync()
        => Task.FromResult<IReadOnlyList<LoyaltyTier>>(_state.State.Tiers.OrderBy(t => t.Level).ToList());

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Loyalty program does not exist");
    }
}
