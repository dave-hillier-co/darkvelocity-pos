using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Represents a line item on a document - the common behavior across
/// OrderLine, PurchaseDocumentLine, etc.
/// </summary>
public interface IDocumentLine
{
    /// <summary>
    /// Unique identifier for the line within its document.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The total monetary amount for this line.
    /// </summary>
    decimal LineAmount { get; }
}

/// <summary>
/// Represents state that maintains a collection of line items.
/// Implemented by OrderState, PurchaseDocumentState, etc.
/// </summary>
public interface IDocumentState<TLine> where TLine : IDocumentLine
{
    Guid OrganizationId { get; }
    List<TLine> Lines { get; }
    int Version { get; set; }
}

/// <summary>
/// Result of adding a line to a document.
/// </summary>
public record AddDocumentLineResult(
    Guid LineId,
    decimal LineAmount,
    decimal DocumentTotal);

/// <summary>
/// Result of updating a line in a document.
/// </summary>
public record UpdateDocumentLineResult(
    Guid LineId,
    decimal LineAmount,
    decimal DocumentTotal);

/// <summary>
/// Result of removing a line from a document.
/// </summary>
public record RemoveDocumentLineResult(
    Guid RemovedLineId,
    decimal DocumentTotal);

/// <summary>
/// Provides core document-with-line-items behavior: line management with total recalculation.
///
/// This abstraction unifies the common pattern found in:
/// - OrderGrain (Lines + Subtotal/GrandTotal)
/// - PurchaseDocumentGrain (Lines + Total)
/// - (Future: InvoiceGrain, QuoteGrain, etc.)
///
/// Subclasses implement domain-specific operations while delegating
/// line management to these protected methods.
/// </summary>
public abstract class DocumentGrain<TState, TLine> : Grain
    where TState : class, IDocumentState<TLine>, new()
    where TLine : class, IDocumentLine
{
    protected readonly IPersistentState<TState> State;

    protected DocumentGrain(IPersistentState<TState> state)
    {
        State = state;
    }

    /// <summary>
    /// Collection of lines on this document.
    /// </summary>
    protected IReadOnlyList<TLine> Lines => State.State.Lines;

    /// <summary>
    /// Check if this document has been initialized.
    /// </summary>
    protected abstract bool IsInitialized { get; }

    /// <summary>
    /// Returns the current document total after recalculation.
    /// </summary>
    protected abstract decimal GetDocumentTotal();

    /// <summary>
    /// Called after lines are modified to recalculate totals.
    /// Subclasses implement domain-specific calculation logic
    /// (e.g., tax, discounts, service charges for orders).
    /// </summary>
    protected abstract void RecalculateTotals();

    /// <summary>
    /// Called before a line is added. Subclasses can validate the line
    /// and throw if invalid. Default implementation does nothing.
    /// </summary>
    protected virtual void OnBeforeAddLine(TLine line)
    {
    }

    /// <summary>
    /// Called after a line is added but before persisting.
    /// Subclasses can perform additional state updates.
    /// </summary>
    protected virtual void OnAfterAddLine(TLine line)
    {
    }

    /// <summary>
    /// Called before a line is updated. Subclasses can validate the update
    /// and throw if invalid. Default implementation does nothing.
    /// </summary>
    protected virtual void OnBeforeUpdateLine(TLine existingLine, TLine updatedLine)
    {
    }

    /// <summary>
    /// Called before a line is removed. Subclasses can validate the removal
    /// and throw if invalid. Default implementation does nothing.
    /// </summary>
    protected virtual void OnBeforeRemoveLine(TLine line)
    {
    }

    /// <summary>
    /// Adds a line to the document.
    /// </summary>
    protected async Task<AddDocumentLineResult> AddLineInternalAsync(TLine line)
    {
        EnsureInitialized();

        OnBeforeAddLine(line);

        State.State.Lines.Add(line);
        RecalculateTotals();

        OnAfterAddLine(line);

        State.State.Version++;
        await State.WriteStateAsync();

        return new AddDocumentLineResult(
            line.Id,
            line.LineAmount,
            GetDocumentTotal());
    }

    /// <summary>
    /// Updates a line at the specified index with the provided updated line.
    /// </summary>
    protected async Task<UpdateDocumentLineResult> UpdateLineInternalAsync(int index, TLine updatedLine)
    {
        EnsureInitialized();

        if (index < 0 || index >= State.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Line index out of range");

        var existingLine = State.State.Lines[index];
        OnBeforeUpdateLine(existingLine, updatedLine);

        State.State.Lines[index] = updatedLine;
        RecalculateTotals();

        State.State.Version++;
        await State.WriteStateAsync();

        return new UpdateDocumentLineResult(
            updatedLine.Id,
            updatedLine.LineAmount,
            GetDocumentTotal());
    }

    /// <summary>
    /// Updates a line by ID using a transformation function.
    /// </summary>
    protected async Task<UpdateDocumentLineResult> UpdateLineInternalAsync(
        Guid lineId,
        Func<TLine, TLine> transform)
    {
        EnsureInitialized();

        var index = State.State.Lines.FindIndex(l => l.Id == lineId);
        if (index < 0)
            throw new InvalidOperationException($"Line with ID {lineId} not found");

        var existingLine = State.State.Lines[index];
        var updatedLine = transform(existingLine);

        OnBeforeUpdateLine(existingLine, updatedLine);

        State.State.Lines[index] = updatedLine;
        RecalculateTotals();

        State.State.Version++;
        await State.WriteStateAsync();

        return new UpdateDocumentLineResult(
            updatedLine.Id,
            updatedLine.LineAmount,
            GetDocumentTotal());
    }

    /// <summary>
    /// Removes a line by index.
    /// </summary>
    protected async Task<RemoveDocumentLineResult> RemoveLineInternalAsync(int index)
    {
        EnsureInitialized();

        if (index < 0 || index >= State.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Line index out of range");

        var line = State.State.Lines[index];
        OnBeforeRemoveLine(line);

        State.State.Lines.RemoveAt(index);
        RecalculateTotals();

        State.State.Version++;
        await State.WriteStateAsync();

        return new RemoveDocumentLineResult(line.Id, GetDocumentTotal());
    }

    /// <summary>
    /// Removes a line by ID.
    /// </summary>
    protected async Task<RemoveDocumentLineResult> RemoveLineInternalAsync(Guid lineId)
    {
        EnsureInitialized();

        var index = State.State.Lines.FindIndex(l => l.Id == lineId);
        if (index < 0)
            throw new InvalidOperationException($"Line with ID {lineId} not found");

        return await RemoveLineInternalAsync(index);
    }

    /// <summary>
    /// Removes all lines matching a predicate.
    /// Returns the number of lines removed.
    /// </summary>
    protected async Task<int> RemoveLinesInternalAsync(Predicate<TLine> predicate)
    {
        EnsureInitialized();

        var removed = State.State.Lines.RemoveAll(predicate);
        if (removed > 0)
        {
            RecalculateTotals();
            State.State.Version++;
            await State.WriteStateAsync();
        }

        return removed;
    }

    /// <summary>
    /// Gets a line by ID, or null if not found.
    /// </summary>
    protected TLine? GetLine(Guid lineId)
    {
        return State.State.Lines.FirstOrDefault(l => l.Id == lineId);
    }

    /// <summary>
    /// Gets a line by index.
    /// </summary>
    protected TLine GetLineAt(int index)
    {
        if (index < 0 || index >= State.State.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Line index out of range");
        return State.State.Lines[index];
    }

    /// <summary>
    /// Finds the index of a line by ID.
    /// </summary>
    protected int FindLineIndex(Guid lineId)
    {
        return State.State.Lines.FindIndex(l => l.Id == lineId);
    }

    /// <summary>
    /// Ensures the document has been initialized before operations.
    /// </summary>
    protected void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException($"{GetType().Name} has not been initialized");
    }
}
