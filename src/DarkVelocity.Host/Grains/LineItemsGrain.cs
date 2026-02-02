using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Manages ordered collections of line items.
/// Provides consistent line item handling across document types.
/// Key format: org:{orgId}:lines:{ownerType}:{ownerId}
/// </summary>
public class LineItemsGrain : Grain, ILineItemsGrain
{
    private readonly IPersistentState<LineItemsState> _state;

    public LineItemsGrain(
        [PersistentState("lineitems", "OrleansStorage")]
        IPersistentState<LineItemsState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId == Guid.Empty)
        {
            var key = this.GetPrimaryKeyString();
            var (orgId, ownerType, ownerId) = ParseKey(key);
            _state.State.OrganizationId = orgId;
            _state.State.OwnerType = ownerType;
            _state.State.OwnerId = ownerId;
            _state.State.CreatedAt = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<LineItemResult> AddAsync(
        string itemType,
        decimal quantity,
        decimal unitPrice,
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(itemType))
            throw new ArgumentException("Item type is required", nameof(itemType));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        var extendedPrice = quantity * unitPrice;
        var lineId = Guid.NewGuid();
        var index = _state.State.NextIndex++;

        var line = new LineItem
        {
            Id = lineId,
            Index = index,
            ItemType = itemType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            ExtendedPrice = extendedPrice,
            Metadata = metadata ?? [],
            IsVoided = false,
            CreatedAt = DateTime.UtcNow
        };

        _state.State.Lines.Add(line);
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        var totals = CalculateTotals();
        return new LineItemResult(lineId, index, extendedPrice, totals);
    }

    public async Task UpdateAsync(
        Guid lineId,
        decimal? quantity = null,
        decimal? unitPrice = null,
        Dictionary<string, string>? metadata = null)
    {
        var index = _state.State.Lines.FindIndex(l => l.Id == lineId);
        if (index < 0)
            throw new InvalidOperationException("Line not found");

        var line = _state.State.Lines[index];

        if (line.IsVoided)
            throw new InvalidOperationException("Cannot update a voided line");

        var newQuantity = quantity ?? line.Quantity;
        var newUnitPrice = unitPrice ?? line.UnitPrice;

        if (quantity.HasValue && quantity.Value <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        var updatedLine = line with
        {
            Quantity = newQuantity,
            UnitPrice = newUnitPrice,
            ExtendedPrice = newQuantity * newUnitPrice,
            Metadata = metadata ?? line.Metadata,
            UpdatedAt = DateTime.UtcNow
        };

        _state.State.Lines[index] = updatedLine;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task VoidAsync(Guid lineId, Guid voidedBy, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Void reason is required", nameof(reason));

        var index = _state.State.Lines.FindIndex(l => l.Id == lineId);
        if (index < 0)
            throw new InvalidOperationException("Line not found");

        var line = _state.State.Lines[index];

        if (line.IsVoided)
            throw new InvalidOperationException("Line is already voided");

        var voidedLine = line with
        {
            IsVoided = true,
            VoidedBy = voidedBy,
            VoidedAt = DateTime.UtcNow,
            VoidReason = reason,
            UpdatedAt = DateTime.UtcNow
        };

        _state.State.Lines[index] = voidedLine;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(Guid lineId)
    {
        var removed = _state.State.Lines.RemoveAll(l => l.Id == lineId);
        if (removed == 0)
            throw new InvalidOperationException("Line not found");

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<LineItem>> GetLinesAsync(bool includeVoided = false)
    {
        IEnumerable<LineItem> lines = _state.State.Lines.OrderBy(l => l.Index);

        if (!includeVoided)
            lines = lines.Where(l => !l.IsVoided);

        return Task.FromResult<IReadOnlyList<LineItem>>(lines.ToList());
    }

    public Task<LineItemTotals> GetTotalsAsync()
    {
        return Task.FromResult(CalculateTotals());
    }

    public Task<LineItemsState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public Task<bool> HasLinesAsync()
    {
        return Task.FromResult(_state.State.Lines.Count > 0);
    }

    private LineItemTotals CalculateTotals()
    {
        var activeLines = _state.State.Lines.Where(l => !l.IsVoided).ToList();
        var voidedLines = _state.State.Lines.Where(l => l.IsVoided).ToList();

        return new LineItemTotals
        {
            LineCount = activeLines.Count,
            VoidedCount = voidedLines.Count,
            TotalQuantity = activeLines.Sum(l => l.Quantity),
            Subtotal = activeLines.Sum(l => l.ExtendedPrice)
        };
    }

    private static (Guid OrgId, string OwnerType, Guid OwnerId) ParseKey(string key)
    {
        // Expected format: {orgId}:lines:{ownerType}:{ownerId}
        var parts = key.Split(':');
        if (parts.Length != 4 || parts[1] != "lines")
            throw new ArgumentException($"Invalid line items key format: {key}");

        return (Guid.Parse(parts[0]), parts[2], Guid.Parse(parts[3]));
    }
}
