using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for managing a guest ordering session.
/// Tracks the cart and creates a real order on submission.
/// </summary>
public class GuestSessionGrain : Grain, IGuestSessionGrain
{
    private readonly IPersistentState<GuestSessionState> _state;
    private readonly IGrainFactory _grainFactory;

    public GuestSessionGrain(
        [PersistentState("guestSession", "OrleansStorage")]
        IPersistentState<GuestSessionState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<GuestSessionSnapshot> StartAsync(StartGuestSessionCommand command)
    {
        if (_state.State.SessionId != Guid.Empty)
            throw new InvalidOperationException("Guest session already started");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, sessionId) = GrainKeys.ParseSiteEntity(key);

        _state.State = new GuestSessionState
        {
            SessionId = sessionId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            LinkId = command.LinkId,
            Status = GuestSessionStatus.Active,
            OrderingType = command.OrderingType,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<GuestSessionSnapshot> AddToCartAsync(AddToCartCommand command)
    {
        EnsureActive();

        var cartItem = new GuestCartItemState
        {
            CartItemId = Guid.NewGuid(),
            MenuItemId = command.MenuItemId,
            Name = command.Name,
            Quantity = command.Quantity,
            UnitPrice = command.UnitPrice,
            Notes = command.Notes,
            Modifiers = command.Modifiers?.Select(m => new GuestCartModifierState
            {
                ModifierId = m.ModifierId,
                Name = m.Name,
                PriceAdjustment = m.PriceAdjustment
            }).ToList() ?? []
        };

        _state.State.CartItems.Add(cartItem);
        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<GuestSessionSnapshot> UpdateCartItemAsync(UpdateCartItemCommand command)
    {
        EnsureActive();

        var item = _state.State.CartItems.FirstOrDefault(i => i.CartItemId == command.CartItemId)
            ?? throw new InvalidOperationException($"Cart item {command.CartItemId} not found");

        if (command.Quantity.HasValue) item.Quantity = command.Quantity.Value;
        if (command.Notes != null) item.Notes = command.Notes;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<GuestSessionSnapshot> RemoveFromCartAsync(Guid cartItemId)
    {
        EnsureActive();

        var item = _state.State.CartItems.FirstOrDefault(i => i.CartItemId == cartItemId)
            ?? throw new InvalidOperationException($"Cart item {cartItemId} not found");

        _state.State.CartItems.Remove(item);
        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<GuestOrderResult> SubmitOrderAsync(SubmitGuestOrderCommand command)
    {
        EnsureActive();

        if (_state.State.CartItems.Count == 0)
            throw new InvalidOperationException("Cannot submit an order with an empty cart");

        _state.State.GuestName = command.GuestName;
        _state.State.GuestPhone = command.GuestPhone;

        // Determine order type based on ordering link type
        var orderType = _state.State.OrderingType switch
        {
            OrderingLinkType.TableQr => OrderType.DineIn,
            OrderingLinkType.TakeOut => OrderType.TakeOut,
            OrderingLinkType.Kiosk => OrderType.DineIn,
            _ => OrderType.Online
        };

        // Create a real order via the OrderGrain
        var orderId = Guid.NewGuid();
        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(_state.State.OrganizationId, _state.State.SiteId, orderId));

        // Use a system user ID for guest orders
        var systemUserId = Guid.Empty;

        var orderResult = await orderGrain.CreateAsync(new CreateOrderCommand(
            OrganizationId: _state.State.OrganizationId,
            SiteId: _state.State.SiteId,
            CreatedBy: systemUserId,
            Type: orderType,
            TableId: _state.State.TableId,
            TableNumber: _state.State.TableNumber));

        // Add each cart item as an order line
        foreach (var cartItem in _state.State.CartItems)
        {
            var modifiers = cartItem.Modifiers.Select(m => new OrderLineModifier
            {
                ModifierId = m.ModifierId,
                Name = m.Name,
                Price = m.PriceAdjustment
            }).ToList();

            await orderGrain.AddLineAsync(new AddLineCommand(
                MenuItemId: cartItem.MenuItemId,
                Name: cartItem.Name,
                Quantity: cartItem.Quantity,
                UnitPrice: cartItem.UnitPrice,
                Notes: cartItem.Notes,
                Modifiers: modifiers.Count > 0 ? modifiers : null));
        }

        // Send the order to kitchen
        await orderGrain.SendAsync(systemUserId);

        // Update session state
        _state.State.OrderId = orderId;
        _state.State.OrderNumber = orderResult.OrderNumber;
        _state.State.Status = GuestSessionStatus.Submitted;
        _state.State.Version++;
        await _state.WriteStateAsync();

        return new GuestOrderResult(
            OrderId: orderId,
            OrderNumber: orderResult.OrderNumber,
            SubmittedAt: DateTime.UtcNow);
    }

    public Task<GuestSessionSnapshot> GetSnapshotAsync()
    {
        EnsureStarted();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<GuestSessionStatus> GetStatusAsync()
    {
        EnsureStarted();
        return Task.FromResult(_state.State.Status);
    }

    private decimal CalculateCartTotal()
    {
        return _state.State.CartItems.Sum(item =>
        {
            var modifierTotal = item.Modifiers.Sum(m => m.PriceAdjustment);
            return (item.UnitPrice + modifierTotal) * item.Quantity;
        });
    }

    private GuestSessionSnapshot CreateSnapshot() => new(
        SessionId: _state.State.SessionId,
        OrganizationId: _state.State.OrganizationId,
        SiteId: _state.State.SiteId,
        LinkId: _state.State.LinkId,
        Status: _state.State.Status,
        CartItems: _state.State.CartItems.Select(i => new GuestCartItem(
            CartItemId: i.CartItemId,
            MenuItemId: i.MenuItemId,
            Name: i.Name,
            Quantity: i.Quantity,
            UnitPrice: i.UnitPrice,
            Notes: i.Notes,
            Modifiers: i.Modifiers.Select(m => new GuestCartModifier(
                ModifierId: m.ModifierId,
                Name: m.Name,
                PriceAdjustment: m.PriceAdjustment
            )).ToList()
        )).ToList(),
        CartTotal: CalculateCartTotal(),
        OrderId: _state.State.OrderId,
        OrderNumber: _state.State.OrderNumber,
        GuestName: _state.State.GuestName,
        GuestPhone: _state.State.GuestPhone,
        OrderingType: _state.State.OrderingType,
        TableId: _state.State.TableId,
        TableNumber: _state.State.TableNumber,
        CreatedAt: _state.State.CreatedAt);

    private void EnsureStarted()
    {
        if (_state.State.SessionId == Guid.Empty)
            throw new InvalidOperationException("Guest session has not been started");
    }

    private void EnsureActive()
    {
        EnsureStarted();
        if (_state.State.Status != GuestSessionStatus.Active)
            throw new InvalidOperationException($"Guest session is not active (current status: {_state.State.Status})");
    }
}
