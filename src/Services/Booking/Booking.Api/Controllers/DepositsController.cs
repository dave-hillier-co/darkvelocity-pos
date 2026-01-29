using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/bookings/{bookingId:guid}/deposits")]
public class DepositsController : ControllerBase
{
    private readonly BookingDbContext _context;

    public DepositsController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<BookingDepositDto>>> GetAll(
        Guid locationId,
        Guid bookingId)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.LocationId == locationId);

        if (booking == null)
            return NotFound(new { message = "Booking not found" });

        var deposits = await _context.BookingDeposits
            .Where(d => d.BookingId == bookingId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = deposits.Select(d => MapToDto(d, locationId)).ToList();

        return Ok(HalCollection<BookingDepositDto>.Create(
            dtos,
            $"/api/locations/{locationId}/bookings/{bookingId}/deposits",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingDepositDto>> GetById(
        Guid locationId,
        Guid bookingId,
        Guid id)
    {
        var deposit = await _context.BookingDeposits
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        var dto = MapToDto(deposit, locationId);
        AddDetailedLinks(dto, locationId, bookingId);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<BookingDepositDto>> Create(
        Guid locationId,
        Guid bookingId,
        [FromBody] CreateDepositRequest request)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.LocationId == locationId);

        if (booking == null)
            return NotFound(new { message = "Booking not found" });

        if (booking.Status == "completed" || booking.Status == "cancelled" || booking.Status == "no_show")
            return BadRequest(new { message = "Cannot add deposit to a closed booking" });

        var deposit = new BookingDeposit
        {
            LocationId = locationId,
            BookingId = bookingId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            Status = "pending",
            Notes = request.Notes
        };

        _context.BookingDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        var dto = MapToDto(deposit, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, bookingId, id = deposit.Id }, dto);
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<BookingDepositDto>> RecordPayment(
        Guid locationId,
        Guid bookingId,
        Guid id,
        [FromBody] RecordDepositPaymentRequest request)
    {
        var deposit = await _context.BookingDeposits
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        if (deposit.Status != "pending")
            return BadRequest(new { message = "Deposit is not pending" });

        deposit.Status = "paid";
        deposit.PaymentMethod = request.PaymentMethod;
        deposit.StripePaymentIntentId = request.StripePaymentIntentId;
        deposit.CardBrand = request.CardBrand;
        deposit.CardLastFour = request.CardLastFour;
        deposit.PaymentReference = request.PaymentReference;
        deposit.PaidAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(deposit, locationId);
        AddDetailedLinks(dto, locationId, bookingId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<BookingDepositDto>> Refund(
        Guid locationId,
        Guid bookingId,
        Guid id,
        [FromBody] RefundDepositRequest request)
    {
        var deposit = await _context.BookingDeposits
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        if (deposit.Status != "paid")
            return BadRequest(new { message = "Deposit must be paid to refund" });

        deposit.Status = "refunded";
        deposit.RefundedAt = DateTime.UtcNow;
        deposit.RefundAmount = request.Amount ?? deposit.Amount;
        deposit.RefundReason = request.Reason;
        deposit.RefundedByUserId = request.RefundedByUserId;

        await _context.SaveChangesAsync();

        var dto = MapToDto(deposit, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<BookingDepositDto>> ApplyToOrder(
        Guid locationId,
        Guid bookingId,
        Guid id,
        [FromBody] ApplyDepositToOrderRequest request)
    {
        var deposit = await _context.BookingDeposits
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        if (deposit.Status != "paid")
            return BadRequest(new { message = "Deposit must be paid to apply to order" });

        deposit.Status = "applied";
        deposit.AppliedToOrderId = request.OrderId;
        deposit.AppliedAt = DateTime.UtcNow;

        // Also update the booking's linked order
        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking != null && !booking.OrderId.HasValue)
        {
            booking.OrderId = request.OrderId;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(deposit, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/forfeit")]
    public async Task<ActionResult<BookingDepositDto>> Forfeit(
        Guid locationId,
        Guid bookingId,
        Guid id,
        [FromBody] ForfeitDepositRequest request)
    {
        var deposit = await _context.BookingDeposits
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        if (deposit.Status != "paid")
            return BadRequest(new { message = "Deposit must be paid to forfeit" });

        deposit.Status = "forfeited";
        deposit.ForfeitedAt = DateTime.UtcNow;
        deposit.RefundReason = request.Reason;

        await _context.SaveChangesAsync();

        var dto = MapToDto(deposit, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid locationId,
        Guid bookingId,
        Guid id)
    {
        var deposit = await _context.BookingDeposits
            .FirstOrDefaultAsync(d =>
                d.Id == id &&
                d.BookingId == bookingId &&
                d.LocationId == locationId);

        if (deposit == null)
            return NotFound();

        if (deposit.Status != "pending")
            return BadRequest(new { message = "Only pending deposits can be deleted" });

        _context.BookingDeposits.Remove(deposit);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static BookingDepositDto MapToDto(BookingDeposit deposit, Guid locationId)
    {
        var dto = new BookingDepositDto
        {
            Id = deposit.Id,
            BookingId = deposit.BookingId,
            Amount = deposit.Amount,
            CurrencyCode = deposit.CurrencyCode,
            Status = deposit.Status,
            PaymentMethod = deposit.PaymentMethod,
            CardBrand = deposit.CardBrand,
            CardLastFour = deposit.CardLastFour,
            PaymentReference = deposit.PaymentReference,
            PaidAt = deposit.PaidAt,
            RefundedAt = deposit.RefundedAt,
            RefundAmount = deposit.RefundAmount,
            RefundReason = deposit.RefundReason,
            AppliedToOrderId = deposit.AppliedToOrderId,
            AppliedAt = deposit.AppliedAt,
            Notes = deposit.Notes,
            CreatedAt = deposit.CreatedAt
        };

        dto.AddSelfLink($"/api/locations/{locationId}/bookings/{deposit.BookingId}/deposits/{deposit.Id}");

        return dto;
    }

    private static void AddDetailedLinks(BookingDepositDto dto, Guid locationId, Guid bookingId)
    {
        dto.AddLink("booking", $"/api/locations/{locationId}/bookings/{bookingId}");

        if (dto.Status == "pending")
        {
            dto.AddLink("pay", $"/api/locations/{locationId}/bookings/{bookingId}/deposits/{dto.Id}/pay");
        }
        else if (dto.Status == "paid")
        {
            dto.AddLink("refund", $"/api/locations/{locationId}/bookings/{bookingId}/deposits/{dto.Id}/refund");
            dto.AddLink("apply", $"/api/locations/{locationId}/bookings/{bookingId}/deposits/{dto.Id}/apply");
            dto.AddLink("forfeit", $"/api/locations/{locationId}/bookings/{bookingId}/deposits/{dto.Id}/forfeit");
        }
    }
}

// Also add a location-level deposits endpoint for reporting
[ApiController]
[Route("api/locations/{locationId:guid}/deposits")]
public class LocationDepositsController : ControllerBase
{
    private readonly BookingDbContext _context;

    public LocationDepositsController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<BookingDepositDto>>> GetAll(
        Guid locationId,
        [FromQuery] string? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.BookingDeposits
            .Include(d => d.Booking)
            .Where(d => d.LocationId == locationId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        if (fromDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) <= toDate.Value);

        var total = await query.CountAsync();

        var deposits = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = deposits.Select(d =>
        {
            var dto = new BookingDepositDto
            {
                Id = d.Id,
                BookingId = d.BookingId,
                Amount = d.Amount,
                CurrencyCode = d.CurrencyCode,
                Status = d.Status,
                PaymentMethod = d.PaymentMethod,
                CardBrand = d.CardBrand,
                CardLastFour = d.CardLastFour,
                PaymentReference = d.PaymentReference,
                PaidAt = d.PaidAt,
                RefundedAt = d.RefundedAt,
                RefundAmount = d.RefundAmount,
                RefundReason = d.RefundReason,
                AppliedToOrderId = d.AppliedToOrderId,
                AppliedAt = d.AppliedAt,
                Notes = d.Notes,
                CreatedAt = d.CreatedAt
            };
            dto.AddSelfLink($"/api/locations/{locationId}/bookings/{d.BookingId}/deposits/{d.Id}");
            dto.AddLink("booking", $"/api/locations/{locationId}/bookings/{d.BookingId}");
            return dto;
        }).ToList();

        return Ok(HalCollection<BookingDepositDto>.Create(
            dtos,
            $"/api/locations/{locationId}/deposits",
            total
        ));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary(
        Guid locationId,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var query = _context.BookingDeposits
            .Where(d => d.LocationId == locationId);

        if (fromDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(d => DateOnly.FromDateTime(d.CreatedAt) <= toDate.Value);

        var deposits = await query.ToListAsync();

        var summary = new
        {
            TotalDeposits = deposits.Count,
            TotalAmount = deposits.Sum(d => d.Amount),
            ByStatus = deposits
                .GroupBy(d => d.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(d => d.Amount)
                })
                .ToList(),
            PaidAmount = deposits.Where(d => d.Status == "paid").Sum(d => d.Amount),
            AppliedAmount = deposits.Where(d => d.Status == "applied").Sum(d => d.Amount),
            RefundedAmount = deposits.Where(d => d.Status == "refunded").Sum(d => d.RefundAmount ?? d.Amount),
            ForfeitedAmount = deposits.Where(d => d.Status == "forfeited").Sum(d => d.Amount)
        };

        return Ok(summary);
    }
}
