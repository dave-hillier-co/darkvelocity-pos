using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain that projects customer spend into loyalty status.
/// Loyalty is derived from actual spend, ensuring consistency with accounting.
/// </summary>
public class CustomerSpendProjectionGrain : Grain, ICustomerSpendProjectionGrain
{
    private readonly IPersistentState<CustomerSpendState> _state;
    private IAsyncStream<IStreamEvent>? _spendStream;

    // Default tier configuration - can be customized per org
    private static readonly List<SpendTier> DefaultTiers =
    [
        new() { Name = "Bronze", MinSpend = 0, MaxSpend = 500, PointsMultiplier = 1.0m, PointsPerDollar = 1.0m },
        new() { Name = "Silver", MinSpend = 500, MaxSpend = 1500, PointsMultiplier = 1.25m, PointsPerDollar = 1.0m },
        new() { Name = "Gold", MinSpend = 1500, MaxSpend = 5000, PointsMultiplier = 1.5m, PointsPerDollar = 1.0m },
        new() { Name = "Platinum", MinSpend = 5000, MaxSpend = decimal.MaxValue, PointsMultiplier = 2.0m, PointsPerDollar = 1.0m }
    ];

    private List<SpendTier> _tiers = DefaultTiers;

    public CustomerSpendProjectionGrain(
        [PersistentState("customerspend", "OrleansStorage")]
        IPersistentState<CustomerSpendState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetSpendStream()
    {
        if (_spendStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, _state.State.OrganizationId.ToString());
            _spendStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _spendStream!;
    }

    public async Task InitializeAsync(Guid organizationId, Guid customerId)
    {
        if (_state.State.CustomerId != Guid.Empty)
            return; // Already initialized

        var now = DateTime.UtcNow;

        _state.State = new CustomerSpendState
        {
            CustomerId = customerId,
            OrganizationId = organizationId,
            CurrentTier = "Bronze",
            CurrentTierMultiplier = 1.0m,
            CurrentYear = now.Year,
            CurrentMonth = now.Month,
            CreatedAt = now,
            Version = 1
        };

        UpdateTierStatus();

        await _state.WriteStateAsync();
    }

    public async Task<RecordSpendResult> RecordSpendAsync(RecordSpendCommand command)
    {
        EnsureExists();
        ResetPeriodsIfNeeded();

        var oldTier = _state.State.CurrentTier;

        // Calculate points: NetSpend × PointsPerDollar × TierMultiplier
        var tier = GetCurrentTier();
        var pointsEarned = (int)Math.Floor(command.NetSpend * tier.PointsPerDollar * tier.PointsMultiplier);

        // Update spend totals
        _state.State.LifetimeSpend += command.NetSpend;
        _state.State.YearToDateSpend += command.NetSpend;
        _state.State.MonthToDateSpend += command.NetSpend;
        _state.State.LifetimeTransactions++;

        // Update points
        _state.State.TotalPointsEarned += pointsEarned;
        _state.State.AvailablePoints += pointsEarned;

        // Record transaction
        var transaction = new SpendTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = command.OrderId,
            SiteId = command.SiteId,
            NetSpend = command.NetSpend,
            GrossSpend = command.GrossSpend,
            DiscountAmount = command.DiscountAmount,
            PointsEarned = pointsEarned,
            TransactionDate = command.TransactionDate,
            RecordedAt = DateTime.UtcNow
        };

        _state.State.RecentTransactions.Insert(0, transaction);
        if (_state.State.RecentTransactions.Count > 100)
            _state.State.RecentTransactions.RemoveAt(_state.State.RecentTransactions.Count - 1);

        // Update timestamps
        _state.State.FirstTransactionAt ??= DateTime.UtcNow;
        _state.State.LastTransactionAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        // Recalculate tier
        UpdateTierStatus();

        var tierChanged = _state.State.CurrentTier != oldTier;

        await _state.WriteStateAsync();

        // Publish spend recorded event
        if (GetSpendStream() != null)
        {
            await GetSpendStream().OnNextAsync(new CustomerSpendRecordedEvent(
                _state.State.CustomerId,
                command.SiteId,
                command.OrderId,
                command.NetSpend,
                command.GrossSpend,
                command.DiscountAmount,
                0, // Tax is in gross
                command.ItemCount,
                command.TransactionDate)
            {
                OrganizationId = _state.State.OrganizationId
            });

            // Publish points earned event
            await GetSpendStream().OnNextAsync(new LoyaltyPointsEarnedEvent(
                _state.State.CustomerId,
                command.OrderId,
                command.NetSpend,
                pointsEarned,
                _state.State.AvailablePoints,
                _state.State.CurrentTier,
                _state.State.CurrentTierMultiplier)
            {
                OrganizationId = _state.State.OrganizationId
            });

            // Publish tier change event if applicable
            if (tierChanged)
            {
                await GetSpendStream().OnNextAsync(new CustomerTierChangedEvent(
                    _state.State.CustomerId,
                    oldTier,
                    _state.State.CurrentTier,
                    _state.State.LifetimeSpend,
                    _state.State.SpendToNextTier)
                {
                    OrganizationId = _state.State.OrganizationId
                });
            }
        }

        return new RecordSpendResult(
            pointsEarned,
            _state.State.AvailablePoints,
            _state.State.CurrentTier,
            tierChanged,
            tierChanged ? _state.State.CurrentTier : null);
    }

    public async Task ReverseSpendAsync(ReverseSpendCommand command)
    {
        EnsureExists();

        var oldTier = _state.State.CurrentTier;

        // Find the original transaction
        var originalTx = _state.State.RecentTransactions
            .FirstOrDefault(t => t.OrderId == command.OrderId);

        if (originalTx != null)
        {
            // Reverse the points that were earned
            _state.State.TotalPointsEarned -= originalTx.PointsEarned;
            _state.State.AvailablePoints = Math.Max(0, _state.State.AvailablePoints - originalTx.PointsEarned);
        }

        // Reverse the spend
        _state.State.LifetimeSpend = Math.Max(0, _state.State.LifetimeSpend - command.Amount);
        _state.State.YearToDateSpend = Math.Max(0, _state.State.YearToDateSpend - command.Amount);
        _state.State.MonthToDateSpend = Math.Max(0, _state.State.MonthToDateSpend - command.Amount);

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        UpdateTierStatus();

        await _state.WriteStateAsync();

        // Publish spend reversed event
        if (GetSpendStream() != null)
        {
            await GetSpendStream().OnNextAsync(new CustomerSpendReversedEvent(
                _state.State.CustomerId,
                originalTx?.SiteId ?? Guid.Empty,
                command.OrderId,
                command.Amount,
                command.Reason)
            {
                OrganizationId = _state.State.OrganizationId
            });

            // Check for tier demotion
            if (_state.State.CurrentTier != oldTier)
            {
                await GetSpendStream().OnNextAsync(new CustomerTierChangedEvent(
                    _state.State.CustomerId,
                    oldTier,
                    _state.State.CurrentTier,
                    _state.State.LifetimeSpend,
                    _state.State.SpendToNextTier)
                {
                    OrganizationId = _state.State.OrganizationId
                });
            }
        }
    }

    public async Task<RedeemPointsResult> RedeemPointsAsync(RedeemSpendPointsCommand command)
    {
        EnsureExists();

        if (command.Points > _state.State.AvailablePoints)
            throw new InvalidOperationException("Insufficient points");

        // Calculate discount value (1 point = $0.01 by default)
        var discountValue = command.Points * 0.01m;

        _state.State.AvailablePoints -= command.Points;
        _state.State.TotalPointsRedeemed += command.Points;

        var redemption = new PointsRedemption
        {
            Id = Guid.NewGuid(),
            OrderId = command.OrderId,
            PointsRedeemed = command.Points,
            DiscountValue = discountValue,
            RewardType = command.RewardType,
            RedeemedAt = DateTime.UtcNow
        };

        _state.State.RecentRedemptions.Insert(0, redemption);
        if (_state.State.RecentRedemptions.Count > 50)
            _state.State.RecentRedemptions.RemoveAt(_state.State.RecentRedemptions.Count - 1);

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish redemption event - triggers accounting
        if (GetSpendStream() != null)
        {
            await GetSpendStream().OnNextAsync(new LoyaltyPointsRedeemedEvent(
                _state.State.CustomerId,
                command.OrderId,
                command.Points,
                discountValue,
                _state.State.AvailablePoints,
                command.RewardType)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return new RedeemPointsResult(discountValue, _state.State.AvailablePoints);
    }

    public Task<CustomerLoyaltySnapshot> GetSnapshotAsync()
    {
        EnsureExists();

        return Task.FromResult(new CustomerLoyaltySnapshot(
            _state.State.CustomerId,
            _state.State.LifetimeSpend,
            _state.State.YearToDateSpend,
            _state.State.AvailablePoints,
            _state.State.CurrentTier,
            _state.State.CurrentTierMultiplier,
            _state.State.SpendToNextTier,
            _state.State.NextTier,
            _state.State.LifetimeTransactions,
            _state.State.LastTransactionAt));
    }

    public Task<CustomerSpendState> GetStateAsync() => Task.FromResult(_state.State);

    public Task<int> GetAvailablePointsAsync() => Task.FromResult(_state.State.AvailablePoints);

    public Task<bool> HasSufficientPointsAsync(int points)
        => Task.FromResult(_state.State.AvailablePoints >= points);

    public async Task ConfigureTiersAsync(List<SpendTier> tiers)
    {
        _tiers = tiers.OrderBy(t => t.MinSpend).ToList();
        UpdateTierStatus();
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.CustomerId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.CustomerId == Guid.Empty)
            throw new InvalidOperationException("Customer spend projection not initialized");
    }

    private SpendTier GetCurrentTier()
    {
        return _tiers.LastOrDefault(t => _state.State.LifetimeSpend >= t.MinSpend)
            ?? _tiers[0];
    }

    private void UpdateTierStatus()
    {
        var tier = GetCurrentTier();
        _state.State.CurrentTier = tier.Name;
        _state.State.CurrentTierMultiplier = tier.PointsMultiplier;

        // Find next tier
        var tierIndex = _tiers.FindIndex(t => t.Name == tier.Name);
        if (tierIndex < _tiers.Count - 1)
        {
            var nextTier = _tiers[tierIndex + 1];
            _state.State.NextTier = nextTier.Name;
            _state.State.SpendToNextTier = nextTier.MinSpend - _state.State.LifetimeSpend;
        }
        else
        {
            _state.State.NextTier = null;
            _state.State.SpendToNextTier = 0;
        }
    }

    private void ResetPeriodsIfNeeded()
    {
        var now = DateTime.UtcNow;

        // Reset YTD if year changed
        if (now.Year != _state.State.CurrentYear)
        {
            _state.State.YearToDateSpend = 0;
            _state.State.CurrentYear = now.Year;
        }

        // Reset MTD if month changed
        if (now.Month != _state.State.CurrentMonth || now.Year != _state.State.CurrentYear)
        {
            _state.State.MonthToDateSpend = 0;
            _state.State.CurrentMonth = now.Month;
        }
    }
}
