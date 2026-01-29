using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/bookings")]
public class BookingsController : ControllerBase
{
    private readonly BookingDbContext _context;
    private readonly IBookingReferenceGenerator _referenceGenerator;
    private readonly IAvailabilityService _availabilityService;

    public BookingsController(
        BookingDbContext context,
        IBookingReferenceGenerator referenceGenerator,
        IAvailabilityService availabilityService)
    {
        _context = context;
        _referenceGenerator = referenceGenerator;
        _availabilityService = availabilityService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<BookingSummaryDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? tableId = null,
        [FromQuery] Guid? tableCombinationId = null,
        [FromQuery] string? guestName = null,
        [FromQuery] string? guestPhone = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.Deposits)
            .Where(b => b.LocationId == locationId);

        if (date.HasValue)
            query = query.Where(b => b.BookingDate == date.Value);
        else
        {
            if (fromDate.HasValue)
                query = query.Where(b => b.BookingDate >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(b => b.BookingDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(b => b.Status == status);

        if (tableId.HasValue)
            query = query.Where(b => b.TableId == tableId.Value);

        if (tableCombinationId.HasValue)
            query = query.Where(b => b.TableCombinationId == tableCombinationId.Value);

        if (!string.IsNullOrEmpty(guestName))
            query = query.Where(b => b.GuestName.Contains(guestName));

        if (!string.IsNullOrEmpty(guestPhone))
            query = query.Where(b => b.GuestPhone != null && b.GuestPhone.Contains(guestPhone));

        var total = await query.CountAsync();

        var bookings = await query
            .OrderBy(b => b.BookingDate)
            .ThenBy(b => b.StartTime)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = bookings.Select(b => new BookingSummaryDto
        {
            Id = b.Id,
            BookingReference = b.BookingReference,
            GuestName = b.GuestName,
            PartySize = b.PartySize,
            BookingDate = b.BookingDate,
            StartTime = b.StartTime,
            Status = b.Status,
            TableNumber = b.Table?.TableNumber,
            IsVip = b.IsVip,
            HasDeposit = b.Deposits.Any(d => d.Status == "paid")
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/locations/{locationId}/bookings/{dto.Id}");
        }

        return Ok(HalCollection<BookingSummaryDto>.Create(
            dtos,
            $"/api/locations/{locationId}/bookings",
            total
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingDto>> GetById(Guid locationId, Guid id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpGet("reference/{reference}")]
    public async Task<ActionResult<BookingDto>> GetByReference(Guid locationId, string reference)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.BookingReference == reference && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(
        Guid locationId,
        [FromBody] CreateBookingRequest request)
    {
        // Validate table or combination
        if (request.TableId.HasValue)
        {
            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == request.TableId.Value && t.LocationId == locationId);

            if (table == null)
                return BadRequest(new { message = "Table not found" });

            if (!table.IsActive)
                return BadRequest(new { message = "Table is not active" });

            if (request.PartySize > table.MaxCapacity)
                return BadRequest(new { message = "Party size exceeds table capacity" });
        }
        else if (request.TableCombinationId.HasValue)
        {
            var combination = await _context.TableCombinations
                .FirstOrDefaultAsync(c => c.Id == request.TableCombinationId.Value && c.LocationId == locationId);

            if (combination == null)
                return BadRequest(new { message = "Table combination not found" });

            if (!combination.IsActive)
                return BadRequest(new { message = "Table combination is not active" });

            if (request.PartySize > combination.CombinedCapacity)
                return BadRequest(new { message = "Party size exceeds combination capacity" });
        }

        // Get settings for duration
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        var duration = request.DurationMinutes ?? settings?.DefaultDurationMinutes ?? 90;

        var reference = await _referenceGenerator.GenerateAsync(locationId);

        var booking = new Entities.Booking
        {
            LocationId = locationId,
            BookingReference = reference,
            TableId = request.TableId,
            TableCombinationId = request.TableCombinationId,
            GuestName = request.GuestName,
            GuestEmail = request.GuestEmail,
            GuestPhone = request.GuestPhone,
            PartySize = request.PartySize,
            SpecialRequests = request.SpecialRequests,
            InternalNotes = request.InternalNotes,
            BookingDate = request.BookingDate,
            StartTime = request.StartTime,
            EndTime = request.StartTime.Add(TimeSpan.FromMinutes(duration)),
            DurationMinutes = duration,
            Status = "pending",
            Source = request.Source,
            ExternalReference = request.ExternalReference,
            IsVip = request.IsVip,
            Tags = request.Tags,
            Occasion = request.Occasion,
            CreatedByUserId = request.CreatedByUserId
        };

        // Auto-confirm online bookings if configured
        if (settings?.AutoConfirmOnlineBookings == true && request.Source == "web")
        {
            booking.Status = "confirmed";
            booking.IsConfirmed = true;
            booking.ConfirmedAt = DateTime.UtcNow;
            booking.ConfirmationMethod = "auto";
        }

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Reload with related data
        await _context.Entry(booking).Reference(b => b.Table).LoadAsync();
        await _context.Entry(booking).Reference(b => b.TableCombination).LoadAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return CreatedAtAction(nameof(GetById), new { locationId, id = booking.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<BookingDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateBookingRequest request)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status == "completed" || booking.Status == "cancelled" || booking.Status == "no_show")
            return BadRequest(new { message = "Cannot modify a closed booking" });

        if (request.GuestName != null) booking.GuestName = request.GuestName;
        if (request.GuestEmail != null) booking.GuestEmail = request.GuestEmail;
        if (request.GuestPhone != null) booking.GuestPhone = request.GuestPhone;
        if (request.PartySize.HasValue) booking.PartySize = request.PartySize.Value;
        if (request.SpecialRequests != null) booking.SpecialRequests = request.SpecialRequests;
        if (request.InternalNotes != null) booking.InternalNotes = request.InternalNotes;
        if (request.IsVip.HasValue) booking.IsVip = request.IsVip.Value;
        if (request.Tags != null) booking.Tags = request.Tags;
        if (request.Occasion != null) booking.Occasion = request.Occasion;

        // Handle table change
        if (request.TableId.HasValue && request.TableId != booking.TableId)
        {
            var table = await _context.Tables
                .FirstOrDefaultAsync(t => t.Id == request.TableId.Value && t.LocationId == locationId);

            if (table == null)
                return BadRequest(new { message = "Table not found" });

            booking.TableId = request.TableId;
            booking.TableCombinationId = null;

            await _context.Entry(booking).Reference(b => b.Table).LoadAsync();
        }
        else if (request.TableCombinationId.HasValue && request.TableCombinationId != booking.TableCombinationId)
        {
            var combination = await _context.TableCombinations
                .FirstOrDefaultAsync(c => c.Id == request.TableCombinationId.Value && c.LocationId == locationId);

            if (combination == null)
                return BadRequest(new { message = "Table combination not found" });

            booking.TableCombinationId = request.TableCombinationId;
            booking.TableId = null;

            await _context.Entry(booking).Reference(b => b.TableCombination).LoadAsync();
        }

        // Handle date/time change
        if (request.BookingDate.HasValue || request.StartTime.HasValue || request.DurationMinutes.HasValue)
        {
            var newDate = request.BookingDate ?? booking.BookingDate;
            var newStartTime = request.StartTime ?? booking.StartTime;
            var newDuration = request.DurationMinutes ?? booking.DurationMinutes;

            booking.BookingDate = newDate;
            booking.StartTime = newStartTime;
            booking.DurationMinutes = newDuration;
            booking.EndTime = newStartTime.Add(TimeSpan.FromMinutes(newDuration));
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<BookingDto>> Confirm(
        Guid locationId,
        Guid id,
        [FromBody] ConfirmBookingRequest request)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status != "pending")
            return BadRequest(new { message = "Booking is not pending" });

        booking.Status = "confirmed";
        booking.IsConfirmed = true;
        booking.ConfirmedAt = DateTime.UtcNow;
        booking.ConfirmationMethod = request.ConfirmationMethod;

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/arrive")]
    public async Task<ActionResult<BookingDto>> Arrive(Guid locationId, Guid id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status != "confirmed" && booking.Status != "pending")
            return BadRequest(new { message = "Booking must be confirmed or pending to mark as arrived" });

        booking.ArrivedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/seat")]
    public async Task<ActionResult<BookingDto>> Seat(
        Guid locationId,
        Guid id,
        [FromBody] SeatBookingRequest? request = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status == "completed" || booking.Status == "cancelled" || booking.Status == "no_show")
            return BadRequest(new { message = "Cannot seat a closed booking" });

        // Optionally change table when seating
        if (request?.TableId.HasValue == true && request.TableId != booking.TableId)
        {
            booking.TableId = request.TableId;
            booking.TableCombinationId = null;

            // Update table status
            var newTable = await _context.Tables.FindAsync(request.TableId.Value);
            if (newTable != null)
            {
                newTable.Status = "occupied";
            }
        }
        else if (request?.TableCombinationId.HasValue == true && request.TableCombinationId != booking.TableCombinationId)
        {
            booking.TableCombinationId = request.TableCombinationId;
            booking.TableId = null;
        }

        // Update current table status
        if (booking.TableId.HasValue)
        {
            var table = await _context.Tables.FindAsync(booking.TableId.Value);
            if (table != null)
            {
                table.Status = "occupied";
            }
        }

        booking.Status = "seated";
        booking.SeatedAt = DateTime.UtcNow;

        if (!booking.ArrivedAt.HasValue)
            booking.ArrivedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Reload table
        await _context.Entry(booking).Reference(b => b.Table).LoadAsync();
        await _context.Entry(booking).Reference(b => b.TableCombination).LoadAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/link-order")]
    public async Task<ActionResult<BookingDto>> LinkOrder(
        Guid locationId,
        Guid id,
        [FromBody] LinkOrderRequest request)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        booking.OrderId = request.OrderId;

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);
        AddActionLinks(dto, locationId, booking);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BookingDto>> Complete(
        Guid locationId,
        Guid id,
        [FromBody] CompleteBookingRequest? request = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status == "completed")
            return BadRequest(new { message = "Booking is already completed" });

        if (booking.Status == "cancelled" || booking.Status == "no_show")
            return BadRequest(new { message = "Cannot complete a cancelled or no-show booking" });

        // Free up the table
        if (booking.TableId.HasValue)
        {
            var table = await _context.Tables.FindAsync(booking.TableId.Value);
            if (table != null)
            {
                table.Status = "available";
            }
        }

        booking.Status = "completed";
        booking.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(
        Guid locationId,
        Guid id,
        [FromBody] CancelBookingRequest request)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status == "completed" || booking.Status == "cancelled" || booking.Status == "no_show")
            return BadRequest(new { message = "Cannot cancel a closed booking" });

        // Free up the table if it was marked as reserved
        if (booking.TableId.HasValue && booking.Status == "seated")
        {
            var table = await _context.Tables.FindAsync(booking.TableId.Value);
            if (table != null)
            {
                table.Status = "available";
            }
        }

        booking.Status = "cancelled";
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancelledByUserId = request.CancelledByUserId;
        booking.CancellationReason = request.Reason;

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<BookingDto>> MarkNoShow(
        Guid locationId,
        Guid id,
        [FromBody] MarkNoShowRequest request)
    {
        var booking = await _context.Bookings
            .Include(b => b.Table)
            .Include(b => b.TableCombination)
            .Include(b => b.Deposits)
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        if (booking.Status == "completed" || booking.Status == "cancelled" || booking.Status == "no_show" || booking.Status == "seated")
            return BadRequest(new { message = "Cannot mark as no-show" });

        booking.Status = "no_show";
        booking.MarkedNoShowAt = DateTime.UtcNow;
        booking.MarkedNoShowByUserId = request.MarkedByUserId;

        // Forfeit deposits if policy requires
        foreach (var deposit in booking.Deposits.Where(d => d.Status == "paid"))
        {
            var policy = await _context.DepositPolicies
                .FirstOrDefaultAsync(p => p.LocationId == locationId && p.IsActive);

            if (policy?.ForfeitsOnNoShow == true)
            {
                deposit.Status = "forfeited";
                deposit.ForfeitedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(booking);
        AddLinks(dto, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.LocationId == locationId);

        if (booking == null)
            return NotFound();

        // Only allow deleting cancelled bookings
        if (booking.Status != "cancelled")
            return BadRequest(new { message = "Only cancelled bookings can be deleted" });

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static BookingDto MapToDto(Entities.Booking booking)
    {
        var dto = new BookingDto
        {
            Id = booking.Id,
            LocationId = booking.LocationId,
            BookingReference = booking.BookingReference,
            TableId = booking.TableId,
            TableNumber = booking.Table?.TableNumber,
            TableCombinationId = booking.TableCombinationId,
            TableCombinationName = booking.TableCombination?.Name,
            GuestName = booking.GuestName,
            GuestEmail = booking.GuestEmail,
            GuestPhone = booking.GuestPhone,
            PartySize = booking.PartySize,
            SpecialRequests = booking.SpecialRequests,
            InternalNotes = booking.InternalNotes,
            BookingDate = booking.BookingDate,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            DurationMinutes = booking.DurationMinutes,
            Status = booking.Status,
            Source = booking.Source,
            ExternalReference = booking.ExternalReference,
            IsConfirmed = booking.IsConfirmed,
            ConfirmedAt = booking.ConfirmedAt,
            ConfirmationMethod = booking.ConfirmationMethod,
            ArrivedAt = booking.ArrivedAt,
            SeatedAt = booking.SeatedAt,
            CompletedAt = booking.CompletedAt,
            CancelledAt = booking.CancelledAt,
            CancellationReason = booking.CancellationReason,
            MarkedNoShowAt = booking.MarkedNoShowAt,
            OrderId = booking.OrderId,
            IsVip = booking.IsVip,
            Tags = booking.Tags,
            Occasion = booking.Occasion,
            CreatedByUserId = booking.CreatedByUserId,
            CreatedAt = booking.CreatedAt,
            Deposits = booking.Deposits.Select(d => new BookingDepositDto
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
            }).ToList(),
            TotalDepositAmount = booking.Deposits.Sum(d => d.Amount),
            PaidDepositAmount = booking.Deposits.Where(d => d.Status == "paid").Sum(d => d.Amount)
        };

        return dto;
    }

    private static void AddLinks(BookingDto dto, Guid locationId)
    {
        dto.AddSelfLink($"/api/locations/{locationId}/bookings/{dto.Id}");
        dto.AddLink("deposits", $"/api/locations/{locationId}/bookings/{dto.Id}/deposits");

        if (dto.TableId.HasValue)
            dto.AddLink("table", $"/api/locations/{locationId}/tables/{dto.TableId}");

        if (dto.TableCombinationId.HasValue)
            dto.AddLink("tableCombination", $"/api/locations/{locationId}/table-combinations/{dto.TableCombinationId}");
    }

    private static void AddActionLinks(BookingDto dto, Guid locationId, Entities.Booking booking)
    {
        var baseUrl = $"/api/locations/{locationId}/bookings/{dto.Id}";

        switch (booking.Status)
        {
            case "pending":
                dto.AddLink("confirm", $"{baseUrl}/confirm");
                dto.AddLink("seat", $"{baseUrl}/seat");
                dto.AddLink("cancel", $"{baseUrl}/cancel");
                break;
            case "confirmed":
                dto.AddLink("arrive", $"{baseUrl}/arrive");
                dto.AddLink("seat", $"{baseUrl}/seat");
                dto.AddLink("cancel", $"{baseUrl}/cancel");
                dto.AddLink("no-show", $"{baseUrl}/no-show");
                break;
            case "seated":
                dto.AddLink("complete", $"{baseUrl}/complete");
                dto.AddLink("link-order", $"{baseUrl}/link-order");
                break;
        }
    }
}
