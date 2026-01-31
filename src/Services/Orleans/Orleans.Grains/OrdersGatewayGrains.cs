using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

// ============================================================================
// Delivery Platform Grain
// ============================================================================

/// <summary>
/// Grain for delivery platform management.
/// Manages third-party delivery platform integrations.
/// </summary>
public class DeliveryPlatformGrain : Grain, IDeliveryPlatformGrain
{
    private readonly IPersistentState<DeliveryPlatformState> _state;

    public DeliveryPlatformGrain(
        [PersistentState("deliveryPlatform", "OrleansStorage")]
        IPersistentState<DeliveryPlatformState> state)
    {
        _state = state;
    }

    public async Task<DeliveryPlatformSnapshot> ConnectAsync(ConnectDeliveryPlatformCommand command)
    {
        if (_state.State.DeliveryPlatformId != Guid.Empty)
            throw new InvalidOperationException("Delivery platform already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var platformId = Guid.Parse(parts[2]);

        _state.State = new DeliveryPlatformState
        {
            OrgId = orgId,
            DeliveryPlatformId = platformId,
            PlatformType = command.PlatformType,
            Name = command.Name,
            Status = DeliveryPlatformStatus.Active,
            ApiCredentialsEncrypted = command.ApiCredentialsEncrypted,
            WebhookSecret = command.WebhookSecret,
            MerchantId = command.MerchantId,
            Settings = command.Settings,
            ConnectedAt = DateTime.UtcNow,
            TodayDate = DateTime.UtcNow.Date,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<DeliveryPlatformSnapshot> UpdateAsync(UpdateDeliveryPlatformCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Status.HasValue) _state.State.Status = command.Status.Value;
        if (command.ApiCredentialsEncrypted != null) _state.State.ApiCredentialsEncrypted = command.ApiCredentialsEncrypted;
        if (command.WebhookSecret != null) _state.State.WebhookSecret = command.WebhookSecret;
        if (command.Settings != null) _state.State.Settings = command.Settings;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DisconnectAsync()
    {
        EnsureInitialized();
        _state.State.Status = DeliveryPlatformStatus.Disconnected;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task PauseAsync()
    {
        EnsureInitialized();
        _state.State.Status = DeliveryPlatformStatus.Paused;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        EnsureInitialized();
        _state.State.Status = DeliveryPlatformStatus.Active;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddLocationMappingAsync(PlatformLocationMapping mapping)
    {
        EnsureInitialized();

        var existing = _state.State.Locations.FirstOrDefault(l => l.LocationId == mapping.LocationId);
        if (existing != null)
        {
            existing.PlatformStoreId = mapping.PlatformStoreId;
            existing.IsActive = mapping.IsActive;
            existing.OperatingHoursOverride = mapping.OperatingHoursOverride;
        }
        else
        {
            _state.State.Locations.Add(new PlatformLocationState
            {
                LocationId = mapping.LocationId,
                PlatformStoreId = mapping.PlatformStoreId,
                IsActive = mapping.IsActive,
                OperatingHoursOverride = mapping.OperatingHoursOverride
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveLocationMappingAsync(Guid locationId)
    {
        EnsureInitialized();
        _state.State.Locations.RemoveAll(l => l.LocationId == locationId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<DeliveryPlatformSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordOrderAsync(decimal orderTotal)
    {
        EnsureInitialized();
        ResetDailyCountersIfNeeded();

        _state.State.TotalOrdersToday++;
        _state.State.TotalRevenueToday += orderTotal;
        _state.State.LastOrderAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordSyncAsync()
    {
        EnsureInitialized();
        _state.State.LastSyncAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void ResetDailyCountersIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_state.State.TodayDate != today)
        {
            _state.State.TodayDate = today;
            _state.State.TotalOrdersToday = 0;
            _state.State.TotalRevenueToday = 0;
        }
    }

    private DeliveryPlatformSnapshot CreateSnapshot()
    {
        ResetDailyCountersIfNeeded();

        return new DeliveryPlatformSnapshot(
            DeliveryPlatformId: _state.State.DeliveryPlatformId,
            PlatformType: _state.State.PlatformType,
            Name: _state.State.Name,
            Status: _state.State.Status,
            MerchantId: _state.State.MerchantId,
            ConnectedAt: _state.State.ConnectedAt,
            LastSyncAt: _state.State.LastSyncAt,
            LastOrderAt: _state.State.LastOrderAt,
            Locations: _state.State.Locations.Select(l => new PlatformLocationMapping(
                LocationId: l.LocationId,
                PlatformStoreId: l.PlatformStoreId,
                IsActive: l.IsActive,
                OperatingHoursOverride: l.OperatingHoursOverride)).ToList(),
            TotalOrdersToday: _state.State.TotalOrdersToday,
            TotalRevenueToday: _state.State.TotalRevenueToday);
    }

    private void EnsureInitialized()
    {
        if (_state.State.DeliveryPlatformId == Guid.Empty)
            throw new InvalidOperationException("Delivery platform grain not initialized");
    }
}

// ============================================================================
// External Order Grain
// ============================================================================

/// <summary>
/// Grain for external order management.
/// Manages orders from third-party delivery platforms.
/// </summary>
public class ExternalOrderGrain : Grain, IExternalOrderGrain
{
    private readonly IPersistentState<ExternalOrderState> _state;

    public ExternalOrderGrain(
        [PersistentState("externalOrder", "OrleansStorage")]
        IPersistentState<ExternalOrderState> state)
    {
        _state = state;
    }

    public async Task<ExternalOrderSnapshot> CreateAsync(CreateExternalOrderCommand command)
    {
        if (_state.State.ExternalOrderId != Guid.Empty)
            throw new InvalidOperationException("External order already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var orderId = Guid.Parse(parts[2]);

        _state.State = new ExternalOrderState
        {
            OrgId = orgId,
            ExternalOrderId = orderId,
            LocationId = command.LocationId,
            DeliveryPlatformId = command.DeliveryPlatformId,
            PlatformOrderId = command.PlatformOrderId,
            PlatformOrderNumber = command.PlatformOrderNumber,
            Status = ExternalOrderStatus.Pending,
            OrderType = command.OrderType,
            PlacedAt = command.PlacedAt,
            Customer = new ExternalOrderCustomerState
            {
                Name = command.Customer.Name,
                Phone = command.Customer.Phone,
                DeliveryAddress = command.Customer.DeliveryAddress
            },
            Items = command.Items.Select(i => new ExternalOrderItemState
            {
                PlatformItemId = i.PlatformItemId,
                InternalMenuItemId = i.InternalMenuItemId,
                Name = i.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                SpecialInstructions = i.SpecialInstructions,
                Modifiers = i.Modifiers?.Select(m => new ExternalOrderModifierState
                {
                    Name = m.Name,
                    Price = m.Price
                }).ToList() ?? []
            }).ToList(),
            Subtotal = command.Subtotal,
            DeliveryFee = command.DeliveryFee,
            ServiceFee = command.ServiceFee,
            Tax = command.Tax,
            Tip = command.Tip,
            Total = command.Total,
            Currency = command.Currency,
            SpecialInstructions = command.SpecialInstructions,
            PlatformRawPayload = command.PlatformRawPayload,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<ExternalOrderSnapshot> AcceptAsync(DateTime? estimatedPickupAt)
    {
        EnsureInitialized();

        if (_state.State.Status != ExternalOrderStatus.Pending)
            throw new InvalidOperationException("Order is not pending");

        _state.State.Status = ExternalOrderStatus.Accepted;
        _state.State.AcceptedAt = DateTime.UtcNow;
        _state.State.EstimatedPickupAt = estimatedPickupAt;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<ExternalOrderSnapshot> RejectAsync(string reason)
    {
        EnsureInitialized();

        if (_state.State.Status != ExternalOrderStatus.Pending)
            throw new InvalidOperationException("Order is not pending");

        _state.State.Status = ExternalOrderStatus.Rejected;
        _state.State.ErrorMessage = reason;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task SetPreparingAsync()
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.Preparing;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetReadyAsync()
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.Ready;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetPickedUpAsync()
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.PickedUp;
        _state.State.ActualPickupAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetDeliveredAsync()
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.Delivered;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.Cancelled;
        _state.State.ErrorMessage = reason;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task LinkInternalOrderAsync(Guid internalOrderId)
    {
        EnsureInitialized();
        _state.State.InternalOrderId = internalOrderId;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<ExternalOrderSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task MarkFailedAsync(string errorMessage)
    {
        EnsureInitialized();
        _state.State.Status = ExternalOrderStatus.Failed;
        _state.State.ErrorMessage = errorMessage;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task IncrementRetryAsync()
    {
        EnsureInitialized();
        _state.State.RetryCount++;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private ExternalOrderSnapshot CreateSnapshot()
    {
        return new ExternalOrderSnapshot(
            ExternalOrderId: _state.State.ExternalOrderId,
            LocationId: _state.State.LocationId,
            DeliveryPlatformId: _state.State.DeliveryPlatformId,
            PlatformOrderId: _state.State.PlatformOrderId,
            PlatformOrderNumber: _state.State.PlatformOrderNumber,
            InternalOrderId: _state.State.InternalOrderId,
            Status: _state.State.Status,
            OrderType: _state.State.OrderType,
            PlacedAt: _state.State.PlacedAt,
            AcceptedAt: _state.State.AcceptedAt,
            EstimatedPickupAt: _state.State.EstimatedPickupAt,
            ActualPickupAt: _state.State.ActualPickupAt,
            Customer: new ExternalOrderCustomer(
                Name: _state.State.Customer.Name,
                Phone: _state.State.Customer.Phone,
                DeliveryAddress: _state.State.Customer.DeliveryAddress),
            Items: _state.State.Items.Select(i => new ExternalOrderItem(
                PlatformItemId: i.PlatformItemId,
                InternalMenuItemId: i.InternalMenuItemId,
                Name: i.Name,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice,
                TotalPrice: i.TotalPrice,
                SpecialInstructions: i.SpecialInstructions,
                Modifiers: i.Modifiers.Select(m => new ExternalOrderModifier(
                    Name: m.Name,
                    Price: m.Price)).ToList())).ToList(),
            Subtotal: _state.State.Subtotal,
            DeliveryFee: _state.State.DeliveryFee,
            ServiceFee: _state.State.ServiceFee,
            Tax: _state.State.Tax,
            Tip: _state.State.Tip,
            Total: _state.State.Total,
            Currency: _state.State.Currency,
            SpecialInstructions: _state.State.SpecialInstructions,
            ErrorMessage: _state.State.ErrorMessage,
            RetryCount: _state.State.RetryCount);
    }

    private void EnsureInitialized()
    {
        if (_state.State.ExternalOrderId == Guid.Empty)
            throw new InvalidOperationException("External order grain not initialized");
    }
}

// ============================================================================
// Menu Sync Grain
// ============================================================================

/// <summary>
/// Grain for menu sync management.
/// Manages menu synchronization with delivery platforms.
/// </summary>
public class MenuSyncGrain : Grain, IMenuSyncGrain
{
    private readonly IPersistentState<MenuSyncState> _state;

    public MenuSyncGrain(
        [PersistentState("menuSync", "OrleansStorage")]
        IPersistentState<MenuSyncState> state)
    {
        _state = state;
    }

    public async Task<MenuSyncSnapshot> StartAsync(StartMenuSyncCommand command)
    {
        if (_state.State.MenuSyncId != Guid.Empty)
            throw new InvalidOperationException("Menu sync already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var syncId = Guid.Parse(parts[2]);

        _state.State = new MenuSyncState
        {
            OrgId = orgId,
            MenuSyncId = syncId,
            DeliveryPlatformId = command.DeliveryPlatformId,
            LocationId = command.LocationId,
            Status = MenuSyncStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task RecordItemSyncedAsync(MenuItemMappingRecord mapping)
    {
        EnsureInitialized();

        _state.State.Mappings.Add(new MenuItemMappingState
        {
            InternalMenuItemId = mapping.InternalMenuItemId,
            PlatformItemId = mapping.PlatformItemId,
            PlatformCategoryId = mapping.PlatformCategoryId,
            PriceOverride = mapping.PriceOverride,
            IsAvailable = mapping.IsAvailable
        });

        _state.State.ItemsSynced++;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordItemFailedAsync(Guid menuItemId, string error)
    {
        EnsureInitialized();

        _state.State.ItemsFailed++;
        _state.State.Errors.Add($"Item {menuItemId}: {error}");
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync()
    {
        EnsureInitialized();

        _state.State.Status = MenuSyncStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task FailAsync(string error)
    {
        EnsureInitialized();

        _state.State.Status = MenuSyncStatus.Failed;
        _state.State.Errors.Add(error);
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<MenuSyncSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    private MenuSyncSnapshot CreateSnapshot()
    {
        return new MenuSyncSnapshot(
            MenuSyncId: _state.State.MenuSyncId,
            DeliveryPlatformId: _state.State.DeliveryPlatformId,
            LocationId: _state.State.LocationId,
            Status: _state.State.Status,
            StartedAt: _state.State.StartedAt,
            CompletedAt: _state.State.CompletedAt,
            ItemsSynced: _state.State.ItemsSynced,
            ItemsFailed: _state.State.ItemsFailed,
            Errors: _state.State.Errors);
    }

    private void EnsureInitialized()
    {
        if (_state.State.MenuSyncId == Guid.Empty)
            throw new InvalidOperationException("Menu sync grain not initialized");
    }
}

// ============================================================================
// Platform Payout Grain
// ============================================================================

/// <summary>
/// Grain for platform payout management.
/// Manages payouts from delivery platforms.
/// </summary>
public class PlatformPayoutGrain : Grain, IPlatformPayoutGrain
{
    private readonly IPersistentState<PlatformPayoutState> _state;

    public PlatformPayoutGrain(
        [PersistentState("platformPayout", "OrleansStorage")]
        IPersistentState<PlatformPayoutState> state)
    {
        _state = state;
    }

    public async Task<PayoutSnapshot> CreateAsync(CreatePayoutCommand command)
    {
        if (_state.State.PayoutId != Guid.Empty)
            throw new InvalidOperationException("Payout already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var payoutId = Guid.Parse(parts[2]);

        _state.State = new PlatformPayoutState
        {
            OrgId = orgId,
            PayoutId = payoutId,
            DeliveryPlatformId = command.DeliveryPlatformId,
            LocationId = command.LocationId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            GrossAmount = command.GrossAmount,
            PlatformFees = command.PlatformFees,
            NetAmount = command.NetAmount,
            Currency = command.Currency,
            Status = PayoutStatus.Pending,
            PayoutReference = command.PayoutReference,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task SetProcessingAsync()
    {
        EnsureInitialized();
        _state.State.Status = PayoutStatus.Processing;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime processedAt)
    {
        EnsureInitialized();
        _state.State.Status = PayoutStatus.Completed;
        _state.State.ProcessedAt = processedAt;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task FailAsync(string reason)
    {
        EnsureInitialized();
        _state.State.Status = PayoutStatus.Failed;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<PayoutSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    private PayoutSnapshot CreateSnapshot()
    {
        return new PayoutSnapshot(
            PayoutId: _state.State.PayoutId,
            DeliveryPlatformId: _state.State.DeliveryPlatformId,
            LocationId: _state.State.LocationId,
            PeriodStart: _state.State.PeriodStart,
            PeriodEnd: _state.State.PeriodEnd,
            GrossAmount: _state.State.GrossAmount,
            PlatformFees: _state.State.PlatformFees,
            NetAmount: _state.State.NetAmount,
            Currency: _state.State.Currency,
            Status: _state.State.Status,
            PayoutReference: _state.State.PayoutReference,
            ProcessedAt: _state.State.ProcessedAt);
    }

    private void EnsureInitialized()
    {
        if (_state.State.PayoutId == Guid.Empty)
            throw new InvalidOperationException("Platform payout grain not initialized");
    }
}
