using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Booking.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/waitlist")]
public class WaitlistController : ControllerBase
{
    private readonly BookingDbContext _context;
    private readonly IBookingReferenceGenerator _referenceGenerator;

    public WaitlistController(
        BookingDbContext context,
        IBookingReferenceGenerator referenceGenerator)
    {
        _context = context;
        _referenceGenerator = referenceGenerator;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<WaitlistEntryDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateOnly? date = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        var query = _context.WaitlistEntries
            .Where(w => w.LocationId == locationId);

        if (date.HasValue)
            query = query.Where(w => w.RequestedDate == date.Value);
        else
            query = query.Where(w => w.RequestedDate == DateOnly.FromDateTime(DateTime.UtcNow));

        if (!string.IsNullOrEmpty(status))
            query = query.Where(w => w.Status == status);
        else
            query = query.Where(w => w.Status == "waiting" || w.Status == "notified");

        var entries = await query
            .OrderBy(w => w.QueuePosition)
            .Take(limit)
            .ToListAsync();

        var dtos = entries.Select(w => MapToDto(w, locationId)).ToList();

        return Ok(HalCollection<WaitlistEntryDto>.Create(
            dtos,
            $"/api/locations/{locationId}/waitlist",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WaitlistEntryDto>> GetById(Guid locationId, Guid id)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        var dto = MapToDto(entry, locationId);
        AddActionLinks(dto, locationId, entry);

        return Ok(dto);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WaitlistSummaryDto>> GetSummary(
        Guid locationId,
        [FromQuery] DateOnly? date = null)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var entries = await _context.WaitlistEntries
            .Where(w => w.LocationId == locationId &&
                       w.RequestedDate == targetDate &&
                       w.Status == "waiting")
            .ToListAsync();

        var now = DateTime.UtcNow;

        var summary = new WaitlistSummaryDto
        {
            TotalWaiting = entries.Count,
            AverageWaitMinutes = entries.Any()
                ? (int)entries.Average(w => (now - w.JoinedAt).TotalMinutes)
                : null,
            LongestWaitMinutes = entries.Any()
                ? (int)entries.Max(w => (now - w.JoinedAt).TotalMinutes)
                : null,
            ByPartySize = entries
                .GroupBy(w => w.PartySize)
                .Select(g => new WaitlistByPartySizeDto
                {
                    PartySize = g.Key,
                    Count = g.Count(),
                    AverageWaitMinutes = (int)g.Average(w => (now - w.JoinedAt).TotalMinutes)
                })
                .OrderBy(x => x.PartySize)
                .ToList()
        };

        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<WaitlistEntryDto>> Create(
        Guid locationId,
        [FromBody] CreateWaitlistEntryRequest request)
    {
        // Check waitlist settings
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings?.WaitlistEnabled == false)
            return BadRequest(new { message = "Waitlist is not enabled" });

        // Get current queue position
        var maxPosition = await _context.WaitlistEntries
            .Where(w => w.LocationId == locationId &&
                       w.RequestedDate == request.RequestedDate &&
                       (w.Status == "waiting" || w.Status == "notified"))
            .MaxAsync(w => (int?)w.QueuePosition) ?? 0;

        var entry = new WaitlistEntry
        {
            LocationId = locationId,
            GuestName = request.GuestName,
            GuestPhone = request.GuestPhone,
            GuestEmail = request.GuestEmail,
            PartySize = request.PartySize,
            RequestedDate = request.RequestedDate,
            PreferredTime = request.PreferredTime,
            LatestAcceptableTime = request.LatestAcceptableTime,
            Status = "waiting",
            QueuePosition = maxPosition + 1,
            JoinedAt = DateTime.UtcNow,
            Notes = request.Notes,
            Source = request.Source,
            PreferredFloorPlanId = request.PreferredFloorPlanId,
            SeatingPreference = request.SeatingPreference
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);
        AddActionLinks(dto, locationId, entry);

        return CreatedAtAction(nameof(GetById), new { locationId, id = entry.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WaitlistEntryDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateWaitlistEntryRequest request)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status != "waiting")
            return BadRequest(new { message = "Can only update waiting entries" });

        if (request.GuestName != null) entry.GuestName = request.GuestName;
        if (request.GuestPhone != null) entry.GuestPhone = request.GuestPhone;
        if (request.GuestEmail != null) entry.GuestEmail = request.GuestEmail;
        if (request.PartySize.HasValue) entry.PartySize = request.PartySize.Value;
        if (request.PreferredTime.HasValue) entry.PreferredTime = request.PreferredTime;
        if (request.LatestAcceptableTime.HasValue) entry.LatestAcceptableTime = request.LatestAcceptableTime;
        if (request.Notes != null) entry.Notes = request.Notes;
        if (request.PreferredFloorPlanId.HasValue) entry.PreferredFloorPlanId = request.PreferredFloorPlanId;
        if (request.SeatingPreference != null) entry.SeatingPreference = request.SeatingPreference;

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);
        AddActionLinks(dto, locationId, entry);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/offer")]
    public async Task<ActionResult<WaitlistEntryDto>> OfferTable(
        Guid locationId,
        Guid id,
        [FromBody] OfferTableRequest request)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status != "waiting")
            return BadRequest(new { message = "Can only offer to waiting entries" });

        // Verify table exists
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Id == request.TableId && t.LocationId == locationId);

        if (table == null)
            return BadRequest(new { message = "Table not found" });

        // Get settings for offer expiry
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        var expiryMinutes = request.OfferExpiryMinutes ?? settings?.WaitlistOfferExpiryMinutes ?? 15;

        entry.Status = "notified";
        entry.NotifiedAt = DateTime.UtcNow;
        entry.OfferExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        entry.OfferedTableId = request.TableId;
        entry.NotificationCount++;

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);
        dto.OfferedTableNumber = table.TableNumber;
        AddActionLinks(dto, locationId, entry);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<WaitlistEntryDto>> ConfirmOffer(Guid locationId, Guid id)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status != "notified")
            return BadRequest(new { message = "Entry has no pending offer" });

        if (entry.OfferExpiresAt.HasValue && DateTime.UtcNow > entry.OfferExpiresAt.Value)
        {
            entry.Status = "waiting";
            entry.OfferedTableId = null;
            entry.OfferExpiresAt = null;
            await _context.SaveChangesAsync();
            return BadRequest(new { message = "Offer has expired" });
        }

        entry.Status = "confirmed";
        entry.ConfirmedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);
        AddActionLinks(dto, locationId, entry);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<BookingDto>> ConvertToBooking(
        Guid locationId,
        Guid id,
        [FromBody] ConvertToBookingRequest? request = null)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status != "confirmed" && entry.Status != "notified")
            return BadRequest(new { message = "Entry must be confirmed or have a pending offer" });

        var tableId = request?.TableId ?? entry.OfferedTableId;

        if (!tableId.HasValue)
            return BadRequest(new { message = "Table ID is required" });

        // Get settings
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        var duration = request?.DurationMinutes ?? settings?.DefaultDurationMinutes ?? 90;
        var startTime = entry.PreferredTime ?? TimeOnly.FromDateTime(DateTime.UtcNow);

        var reference = await _referenceGenerator.GenerateAsync(locationId);

        var booking = new Entities.Booking
        {
            LocationId = locationId,
            BookingReference = reference,
            TableId = tableId,
            GuestName = entry.GuestName,
            GuestEmail = entry.GuestEmail,
            GuestPhone = entry.GuestPhone,
            PartySize = entry.PartySize,
            BookingDate = entry.RequestedDate,
            StartTime = startTime,
            EndTime = startTime.Add(TimeSpan.FromMinutes(duration)),
            DurationMinutes = duration,
            Status = "confirmed",
            Source = entry.Source == "walk_in" ? "walk_in" : "waitlist",
            IsConfirmed = true,
            ConfirmedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);

        // Update waitlist entry
        entry.Status = "seated";
        entry.SeatedAt = DateTime.UtcNow;
        entry.ConvertedToBookingId = booking.Id;
        entry.ClosedAt = DateTime.UtcNow;

        // Update table status
        var table = await _context.Tables.FindAsync(tableId.Value);
        if (table != null)
        {
            table.Status = "occupied";
        }

        // Reorder queue positions for remaining entries
        var remainingEntries = await _context.WaitlistEntries
            .Where(w => w.LocationId == locationId &&
                       w.RequestedDate == entry.RequestedDate &&
                       w.Status == "waiting" &&
                       w.QueuePosition > entry.QueuePosition)
            .ToListAsync();

        foreach (var remaining in remainingEntries)
        {
            remaining.QueuePosition--;
        }

        await _context.SaveChangesAsync();

        // Load related data
        await _context.Entry(booking).Reference(b => b.Table).LoadAsync();

        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            LocationId = booking.LocationId,
            BookingReference = booking.BookingReference,
            TableId = booking.TableId,
            TableNumber = booking.Table?.TableNumber,
            GuestName = booking.GuestName,
            GuestEmail = booking.GuestEmail,
            GuestPhone = booking.GuestPhone,
            PartySize = booking.PartySize,
            BookingDate = booking.BookingDate,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            DurationMinutes = booking.DurationMinutes,
            Status = booking.Status,
            Source = booking.Source,
            IsConfirmed = booking.IsConfirmed,
            ConfirmedAt = booking.ConfirmedAt,
            CreatedAt = booking.CreatedAt
        };

        bookingDto.AddSelfLink($"/api/locations/{locationId}/bookings/{booking.Id}");

        return Ok(bookingDto);
    }

    [HttpPost("{id:guid}/seat")]
    public async Task<ActionResult<WaitlistEntryDto>> Seat(Guid locationId, Guid id)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status != "confirmed")
            return BadRequest(new { message = "Entry must be confirmed to seat" });

        entry.Status = "seated";
        entry.SeatedAt = DateTime.UtcNow;
        entry.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<WaitlistEntryDto>> Cancel(
        Guid locationId,
        Guid id,
        [FromBody] CancelWaitlistEntryRequest? request = null)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status == "seated" || entry.Status == "cancelled" || entry.Status == "expired")
            return BadRequest(new { message = "Cannot cancel this entry" });

        var originalPosition = entry.QueuePosition;

        entry.Status = "cancelled";
        entry.ClosedAt = DateTime.UtcNow;
        if (request?.Reason != null)
            entry.Notes = $"{entry.Notes}\nCancelled: {request.Reason}".Trim();

        // Reorder queue positions
        var remainingEntries = await _context.WaitlistEntries
            .Where(w => w.LocationId == locationId &&
                       w.RequestedDate == entry.RequestedDate &&
                       w.Status == "waiting" &&
                       w.QueuePosition > originalPosition)
            .ToListAsync();

        foreach (var remaining in remainingEntries)
        {
            remaining.QueuePosition--;
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(entry, locationId);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id && w.LocationId == locationId);

        if (entry == null)
            return NotFound();

        if (entry.Status == "waiting" || entry.Status == "notified")
            return BadRequest(new { message = "Cancel the entry instead of deleting" });

        _context.WaitlistEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static WaitlistEntryDto MapToDto(WaitlistEntry entry, Guid locationId)
    {
        var dto = new WaitlistEntryDto
        {
            Id = entry.Id,
            LocationId = entry.LocationId,
            GuestName = entry.GuestName,
            GuestPhone = entry.GuestPhone,
            GuestEmail = entry.GuestEmail,
            PartySize = entry.PartySize,
            RequestedDate = entry.RequestedDate,
            PreferredTime = entry.PreferredTime,
            LatestAcceptableTime = entry.LatestAcceptableTime,
            Status = entry.Status,
            QueuePosition = entry.QueuePosition,
            EstimatedWaitMinutes = entry.EstimatedWaitMinutes,
            JoinedAt = entry.JoinedAt,
            NotifiedAt = entry.NotifiedAt,
            OfferExpiresAt = entry.OfferExpiresAt,
            ConfirmedAt = entry.ConfirmedAt,
            SeatedAt = entry.SeatedAt,
            ClosedAt = entry.ClosedAt,
            OfferedTableId = entry.OfferedTableId,
            ConvertedToBookingId = entry.ConvertedToBookingId,
            NotificationCount = entry.NotificationCount,
            Notes = entry.Notes,
            Source = entry.Source,
            PreferredFloorPlanId = entry.PreferredFloorPlanId,
            SeatingPreference = entry.SeatingPreference
        };

        dto.AddSelfLink($"/api/locations/{locationId}/waitlist/{entry.Id}");

        return dto;
    }

    private static void AddActionLinks(WaitlistEntryDto dto, Guid locationId, WaitlistEntry entry)
    {
        var baseUrl = $"/api/locations/{locationId}/waitlist/{entry.Id}";

        switch (entry.Status)
        {
            case "waiting":
                dto.AddLink("offer", $"{baseUrl}/offer");
                dto.AddLink("cancel", $"{baseUrl}/cancel");
                break;
            case "notified":
                dto.AddLink("confirm", $"{baseUrl}/confirm");
                dto.AddLink("convert", $"{baseUrl}/convert");
                dto.AddLink("cancel", $"{baseUrl}/cancel");
                break;
            case "confirmed":
                dto.AddLink("seat", $"{baseUrl}/seat");
                dto.AddLink("convert", $"{baseUrl}/convert");
                break;
        }

        if (entry.ConvertedToBookingId.HasValue)
        {
            dto.AddLink("booking", $"/api/locations/{locationId}/bookings/{entry.ConvertedToBookingId}");
        }
    }
}
