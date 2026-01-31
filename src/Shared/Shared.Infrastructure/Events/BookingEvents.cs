namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Published when a new booking is created.
/// </summary>
public sealed record BookingCreated(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    string GuestName,
    int PartySize,
    DateOnly BookingDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? TableId,
    Guid? TableCombinationId,
    string Source,
    Guid? CreatedByUserId
) : IntegrationEvent
{
    public override string EventType => "booking.booking.created";
}

/// <summary>
/// Published when a booking is confirmed.
/// </summary>
public sealed record BookingConfirmed(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    string ConfirmationMethod,
    DateTime ConfirmedAt
) : IntegrationEvent
{
    public override string EventType => "booking.booking.confirmed";
}

/// <summary>
/// Published when a booking is cancelled.
/// </summary>
public sealed record BookingCancelled(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    string? Reason,
    Guid? CancelledByUserId,
    DateTime CancelledAt
) : IntegrationEvent
{
    public override string EventType => "booking.booking.cancelled";
}

/// <summary>
/// Published when a booking is marked as no-show.
/// </summary>
public sealed record BookingNoShow(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    Guid? MarkedByUserId,
    DateTime MarkedAt
) : IntegrationEvent
{
    public override string EventType => "booking.booking.no_show";
}

/// <summary>
/// Published when guests are seated for their booking.
/// </summary>
public sealed record GuestSeated(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    Guid? TableId,
    Guid? TableCombinationId,
    int PartySize,
    DateTime SeatedAt
) : IntegrationEvent
{
    public override string EventType => "booking.guest.seated";
}

/// <summary>
/// Published when guests depart after their booking.
/// </summary>
public sealed record GuestDeparted(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    Guid? OrderId,
    DateTime CompletedAt
) : IntegrationEvent
{
    public override string EventType => "booking.guest.departed";
}

/// <summary>
/// Published when a booking is linked to a POS order.
/// </summary>
public sealed record BookingLinkedToOrder(
    Guid BookingId,
    Guid LocationId,
    string BookingReference,
    Guid OrderId
) : IntegrationEvent
{
    public override string EventType => "booking.booking.linked_to_order";
}
