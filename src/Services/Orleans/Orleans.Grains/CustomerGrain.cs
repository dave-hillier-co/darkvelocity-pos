using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

public class CustomerGrain : Grain, ICustomerGrain
{
    private readonly IPersistentState<CustomerState> _state;

    public CustomerGrain(
        [PersistentState("customer", "OrleansStorage")]
        IPersistentState<CustomerState> state)
    {
        _state = state;
    }

    public async Task<CustomerCreatedResult> CreateAsync(CreateCustomerCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Customer already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, customerId) = GrainKeys.ParseOrgEntity(key);

        _state.State = new CustomerState
        {
            Id = customerId,
            OrganizationId = command.OrganizationId,
            FirstName = command.FirstName,
            LastName = command.LastName,
            DisplayName = $"{command.FirstName} {command.LastName}".Trim(),
            Contact = new ContactInfo { Email = command.Email, Phone = command.Phone },
            Source = command.Source,
            Status = CustomerStatus.Active,
            Stats = new CustomerStats { Segment = CustomerSegment.New },
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return new CustomerCreatedResult(customerId, _state.State.DisplayName, _state.State.CreatedAt);
    }

    public Task<CustomerState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(UpdateCustomerCommand command)
    {
        EnsureExists();

        if (command.FirstName != null) _state.State.FirstName = command.FirstName;
        if (command.LastName != null) _state.State.LastName = command.LastName;
        if (command.FirstName != null || command.LastName != null)
            _state.State.DisplayName = $"{_state.State.FirstName} {_state.State.LastName}".Trim();

        if (command.Email != null || command.Phone != null)
            _state.State.Contact = _state.State.Contact with
            {
                Email = command.Email ?? _state.State.Contact.Email,
                Phone = command.Phone ?? _state.State.Contact.Phone
            };

        if (command.DateOfBirth != null) _state.State.DateOfBirth = command.DateOfBirth;
        if (command.Preferences != null) _state.State.Preferences = command.Preferences;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddTagAsync(string tag)
    {
        EnsureExists();
        if (!_state.State.Tags.Contains(tag))
        {
            _state.State.Tags.Add(tag);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTagAsync(string tag)
    {
        EnsureExists();
        if (_state.State.Tags.Remove(tag))
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddNoteAsync(string content, Guid createdBy)
    {
        EnsureExists();
        _state.State.Notes.Add(new CustomerNote
        {
            Id = Guid.NewGuid(),
            Content = content,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        });
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task EnrollInLoyaltyAsync(EnrollLoyaltyCommand command)
    {
        EnsureExists();
        if (_state.State.Loyalty != null)
            throw new InvalidOperationException("Customer already enrolled in loyalty");

        _state.State.Loyalty = new LoyaltyStatus
        {
            EnrolledAt = DateTime.UtcNow,
            ProgramId = command.ProgramId,
            MemberNumber = command.MemberNumber,
            TierId = command.InitialTierId,
            TierName = command.TierName,
            PointsBalance = 0,
            LifetimePoints = 0,
            YtdPoints = 0,
            PointsToNextTier = 0
        };
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<PointsResult> EarnPointsAsync(EarnPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        _state.State.Loyalty = _state.State.Loyalty! with
        {
            PointsBalance = _state.State.Loyalty.PointsBalance + command.Points,
            LifetimePoints = _state.State.Loyalty.LifetimePoints + command.Points,
            YtdPoints = _state.State.Loyalty.YtdPoints + command.Points
        };

        _state.State.Stats = _state.State.Stats with { TotalSpend = _state.State.Stats.TotalSpend + (command.SpendAmount ?? 0) };
        _state.State.Version++;
        await _state.WriteStateAsync();

        return new PointsResult(_state.State.Loyalty.PointsBalance, _state.State.Loyalty.LifetimePoints);
    }

    public async Task<PointsResult> RedeemPointsAsync(RedeemPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        if (_state.State.Loyalty!.PointsBalance < command.Points)
            throw new InvalidOperationException("Insufficient points");

        _state.State.Loyalty = _state.State.Loyalty with
        {
            PointsBalance = _state.State.Loyalty.PointsBalance - command.Points
        };

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new PointsResult(_state.State.Loyalty.PointsBalance, _state.State.Loyalty.LifetimePoints);
    }

    public async Task<PointsResult> AdjustPointsAsync(AdjustPointsCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var newBalance = _state.State.Loyalty!.PointsBalance + command.Points;
        if (newBalance < 0)
            throw new InvalidOperationException("Adjustment would result in negative balance");

        _state.State.Loyalty = _state.State.Loyalty with { PointsBalance = newBalance };
        if (command.Points > 0)
            _state.State.Loyalty = _state.State.Loyalty with { LifetimePoints = _state.State.Loyalty.LifetimePoints + command.Points };

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new PointsResult(_state.State.Loyalty.PointsBalance, _state.State.Loyalty.LifetimePoints);
    }

    public async Task ExpirePointsAsync(int points, DateTime expiryDate)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        _state.State.Loyalty = _state.State.Loyalty! with
        {
            PointsBalance = Math.Max(0, _state.State.Loyalty.PointsBalance - points)
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task PromoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        _state.State.Loyalty = _state.State.Loyalty! with
        {
            TierId = newTierId,
            TierName = tierName,
            PointsToNextTier = pointsToNextTier
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DemoteTierAsync(Guid newTierId, string tierName, int pointsToNextTier)
    {
        await PromoteTierAsync(newTierId, tierName, pointsToNextTier);
    }

    public async Task<RewardResult> IssueRewardAsync(IssueRewardCommand command)
    {
        EnsureExists();
        EnsureLoyaltyEnrolled();

        var reward = new CustomerReward
        {
            Id = Guid.NewGuid(),
            RewardDefinitionId = command.RewardDefinitionId,
            Name = command.RewardName,
            Status = RewardStatus.Available,
            PointsSpent = command.PointsCost,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = command.ExpiresAt
        };

        _state.State.Rewards.Add(reward);

        if (command.PointsCost > 0)
        {
            _state.State.Loyalty = _state.State.Loyalty! with
            {
                PointsBalance = _state.State.Loyalty.PointsBalance - command.PointsCost
            };
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new RewardResult(reward.Id, reward.ExpiresAt);
    }

    public async Task RedeemRewardAsync(RedeemRewardCommand command)
    {
        EnsureExists();

        var reward = _state.State.Rewards.FirstOrDefault(r => r.Id == command.RewardId)
            ?? throw new InvalidOperationException("Reward not found");

        if (reward.Status != RewardStatus.Available)
            throw new InvalidOperationException($"Reward is not available: {reward.Status}");

        if (reward.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Reward has expired");

        var index = _state.State.Rewards.FindIndex(r => r.Id == command.RewardId);
        _state.State.Rewards[index] = reward with
        {
            Status = RewardStatus.Redeemed,
            RedeemedAt = DateTime.UtcNow,
            RedemptionOrderId = command.OrderId,
            RedemptionSiteId = command.SiteId
        };

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ExpireRewardsAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var expired = _state.State.Rewards
            .Where(r => r.Status == RewardStatus.Available && r.ExpiresAt < now)
            .ToList();

        foreach (var reward in expired)
        {
            var index = _state.State.Rewards.FindIndex(r => r.Id == reward.Id);
            _state.State.Rewards[index] = reward with { Status = RewardStatus.Expired };
        }

        if (expired.Any())
        {
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordVisitAsync(RecordVisitCommand command)
    {
        EnsureExists();

        _state.State.Stats = _state.State.Stats with
        {
            TotalVisits = _state.State.Stats.TotalVisits + 1,
            TotalSpend = _state.State.Stats.TotalSpend + command.SpendAmount,
            AverageCheck = (_state.State.Stats.TotalSpend + command.SpendAmount) / (_state.State.Stats.TotalVisits + 1),
            LastVisitSiteId = command.SiteId,
            DaysSinceLastVisit = 0
        };

        _state.State.LastVisitAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetReferralCodeAsync(string code)
    {
        EnsureExists();
        _state.State.ReferralCode = code;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetReferredByAsync(Guid referrerId)
    {
        EnsureExists();
        _state.State.ReferredBy = referrerId;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task IncrementReferralCountAsync()
    {
        EnsureExists();
        _state.State.SuccessfulReferrals++;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task MergeFromAsync(Guid sourceCustomerId)
    {
        EnsureExists();
        _state.State.MergedFrom.Add(sourceCustomerId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.Status = CustomerStatus.Inactive;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AnonymizeAsync()
    {
        EnsureExists();
        _state.State.FirstName = "REDACTED";
        _state.State.LastName = "REDACTED";
        _state.State.DisplayName = "REDACTED";
        _state.State.Contact = new ContactInfo();
        _state.State.DateOfBirth = null;
        _state.State.Status = CustomerStatus.Inactive;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<bool> IsLoyaltyMemberAsync() => Task.FromResult(_state.State.Loyalty != null);
    public Task<int> GetPointsBalanceAsync() => Task.FromResult(_state.State.Loyalty?.PointsBalance ?? 0);
    public Task<IReadOnlyList<CustomerReward>> GetAvailableRewardsAsync()
        => Task.FromResult<IReadOnlyList<CustomerReward>>(_state.State.Rewards.Where(r => r.Status == RewardStatus.Available && r.ExpiresAt > DateTime.UtcNow).ToList());

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Customer does not exist");
    }

    private void EnsureLoyaltyEnrolled()
    {
        if (_state.State.Loyalty == null)
            throw new InvalidOperationException("Customer not enrolled in loyalty program");
    }
}
