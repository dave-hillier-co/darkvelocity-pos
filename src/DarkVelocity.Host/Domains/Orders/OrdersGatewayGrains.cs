using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

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
            IntegrationType: _state.State.IntegrationType,
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

    public async Task<ExternalOrderSnapshot> ReceiveAsync(ExternalOrderReceived order)
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
            LocationId = order.LocationId,
            DeliveryPlatformId = order.DeliveryPlatformId,
            PlatformOrderId = order.PlatformOrderId,
            PlatformOrderNumber = order.PlatformOrderNumber,
            ChannelDisplayId = order.ChannelDisplayId,
            Status = ExternalOrderStatus.Pending,
            OrderType = order.OrderType,
            PlacedAt = order.PlacedAt,
            ScheduledPickupAt = order.ScheduledPickupAt,
            ScheduledDeliveryAt = order.ScheduledDeliveryAt,
            IsAsapDelivery = order.IsAsapDelivery,
            Customer = new ExternalOrderCustomerState
            {
                Name = order.Customer.Name,
                Phone = order.Customer.Phone,
                Email = order.Customer.Email,
                DeliveryAddress = order.Customer.DeliveryAddress != null ? new DeliveryAddressState
                {
                    Street = order.Customer.DeliveryAddress.Street,
                    PostalCode = order.Customer.DeliveryAddress.PostalCode,
                    City = order.Customer.DeliveryAddress.City,
                    Country = order.Customer.DeliveryAddress.Country,
                    ExtraAddressInfo = order.Customer.DeliveryAddress.ExtraAddressInfo
                } : null
            },
            Courier = order.Courier != null ? new CourierInfoState
            {
                FirstName = order.Courier.FirstName,
                LastName = order.Courier.LastName,
                PhoneNumber = order.Courier.PhoneNumber,
                Provider = order.Courier.Provider,
                Status = order.Courier.Status
            } : null,
            Items = order.Items.Select(i => new ExternalOrderItemState
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
            Subtotal = order.Subtotal,
            DeliveryFee = order.DeliveryFee,
            ServiceFee = order.ServiceFee,
            Tax = order.Tax,
            Tip = order.Tip,
            Total = order.Total,
            Currency = order.Currency,
            Discounts = order.Discounts?.Select(d => new ExternalOrderDiscountState
            {
                Type = d.Type,
                Provider = d.Provider,
                Name = d.Name,
                Amount = d.Amount
            }).ToList() ?? [],
            Packaging = order.Packaging != null ? new PackagingPreferencesState
            {
                IncludeCutlery = order.Packaging.IncludeCutlery,
                IsReusable = order.Packaging.IsReusable,
                BagFee = order.Packaging.BagFee
            } : null,
            SpecialInstructions = order.SpecialInstructions,
            PlatformRawPayload = order.PlatformRawPayload,
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

    public async Task UpdateCourierAsync(CourierInfo courier)
    {
        EnsureInitialized();
        _state.State.Courier = new CourierInfoState
        {
            FirstName = courier.FirstName,
            LastName = courier.LastName,
            PhoneNumber = courier.PhoneNumber,
            Provider = courier.Provider,
            Status = courier.Status
        };
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
            ChannelDisplayId: _state.State.ChannelDisplayId,
            InternalOrderId: _state.State.InternalOrderId,
            Status: _state.State.Status,
            OrderType: _state.State.OrderType,
            PlacedAt: _state.State.PlacedAt,
            ScheduledPickupAt: _state.State.ScheduledPickupAt,
            ScheduledDeliveryAt: _state.State.ScheduledDeliveryAt,
            IsAsapDelivery: _state.State.IsAsapDelivery,
            AcceptedAt: _state.State.AcceptedAt,
            EstimatedPickupAt: _state.State.EstimatedPickupAt,
            ActualPickupAt: _state.State.ActualPickupAt,
            Customer: new ExternalOrderCustomer(
                Name: _state.State.Customer.Name,
                Phone: _state.State.Customer.Phone,
                Email: _state.State.Customer.Email,
                DeliveryAddress: _state.State.Customer.DeliveryAddress != null ? new DeliveryAddress(
                    Street: _state.State.Customer.DeliveryAddress.Street,
                    PostalCode: _state.State.Customer.DeliveryAddress.PostalCode,
                    City: _state.State.Customer.DeliveryAddress.City,
                    Country: _state.State.Customer.DeliveryAddress.Country,
                    ExtraAddressInfo: _state.State.Customer.DeliveryAddress.ExtraAddressInfo) : null),
            Courier: _state.State.Courier != null ? new CourierInfo(
                FirstName: _state.State.Courier.FirstName,
                LastName: _state.State.Courier.LastName,
                PhoneNumber: _state.State.Courier.PhoneNumber,
                Provider: _state.State.Courier.Provider,
                Status: _state.State.Courier.Status) : null,
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
            Discounts: _state.State.Discounts.Select(d => new ExternalOrderDiscount(
                Type: d.Type,
                Provider: d.Provider,
                Name: d.Name,
                Amount: d.Amount)).ToList(),
            Packaging: _state.State.Packaging != null ? new PackagingPreferences(
                IncludeCutlery: _state.State.Packaging.IncludeCutlery,
                IsReusable: _state.State.Packaging.IsReusable,
                BagFee: _state.State.Packaging.BagFee) : null,
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

    public async Task<PayoutSnapshot> ReceiveAsync(PayoutReceived payout)
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
            DeliveryPlatformId = payout.DeliveryPlatformId,
            LocationId = payout.LocationId,
            PeriodStart = payout.PeriodStart,
            PeriodEnd = payout.PeriodEnd,
            GrossAmount = payout.GrossAmount,
            PlatformFees = payout.PlatformFees,
            NetAmount = payout.NetAmount,
            Currency = payout.Currency,
            Status = PayoutStatus.Pending,
            PayoutReference = payout.PayoutReference,
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
