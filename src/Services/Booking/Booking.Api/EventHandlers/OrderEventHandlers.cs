using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Booking.Api.EventHandlers;

/// <summary>
/// Handles OrderCreated events to automatically link orders to bookings
/// when they share the same table assignment.
/// </summary>
public class OrderCreatedHandler : IEventHandler<OrderCreated>
{
    private readonly BookingDbContext _context;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(
        BookingDbContext context,
        IEventBus eventBus,
        ILogger<OrderCreatedHandler> logger)
    {
        _context = context;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling OrderCreated event for order {OrderId} at location {LocationId}",
            @event.OrderId,
            @event.LocationId);

        // If order doesn't have a table assignment, we can't link it to a booking
        if (!@event.TableId.HasValue)
        {
            _logger.LogDebug(
                "Order {OrderId} has no table assignment, skipping booking link",
                @event.OrderId);
            return;
        }

        // Find a matching booking for the same table that is currently seated
        // The booking should be:
        // - At the same location
        // - For the same table
        // - Currently seated (status = "seated")
        // - Today's date
        // - Not already linked to another order
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var booking = await _context.Bookings
            .Where(b => b.LocationId == @event.LocationId)
            .Where(b => b.TableId == @event.TableId)
            .Where(b => b.Status == "seated")
            .Where(b => b.BookingDate == today)
            .Where(b => b.OrderId == null)
            .OrderByDescending(b => b.SeatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking == null)
        {
            _logger.LogDebug(
                "No matching seated booking found for table {TableId} at location {LocationId}",
                @event.TableId,
                @event.LocationId);
            return;
        }

        // Link the order to the booking
        booking.OrderId = @event.OrderId;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Linked order {OrderId} to booking {BookingId} (ref: {BookingReference})",
            @event.OrderId,
            booking.Id,
            booking.BookingReference);

        // Publish event for the booking-order link
        await _eventBus.PublishAsync(new BookingLinkedToOrder(
            BookingId: booking.Id,
            LocationId: booking.LocationId,
            BookingReference: booking.BookingReference,
            OrderId: @event.OrderId
        ), cancellationToken);
    }
}
