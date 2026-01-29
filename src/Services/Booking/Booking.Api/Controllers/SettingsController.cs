using DarkVelocity.Booking.Api.Data;
using DarkVelocity.Booking.Api.Dtos;
using DarkVelocity.Booking.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/booking-settings")]
public class BookingSettingsController : ControllerBase
{
    private readonly BookingDbContext _context;

    public BookingSettingsController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<BookingSettingsDto>> Get(Guid locationId)
    {
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            // Return default settings if none exist
            settings = CreateDefaultSettings(locationId);
            _context.BookingSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/booking-settings");

        return Ok(dto);
    }

    [HttpPut]
    public async Task<ActionResult<BookingSettingsDto>> Update(
        Guid locationId,
        [FromBody] UpdateBookingSettingsRequest request)
    {
        var settings = await _context.BookingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            settings = CreateDefaultSettings(locationId);
            _context.BookingSettings.Add(settings);
        }

        // Booking window
        if (request.BookingWindowDays.HasValue) settings.BookingWindowDays = request.BookingWindowDays.Value;
        if (request.MinAdvanceHours.HasValue) settings.MinAdvanceHours = request.MinAdvanceHours.Value;
        if (request.MaxAdvanceHours.HasValue) settings.MaxAdvanceHours = request.MaxAdvanceHours;

        // Party size
        if (request.MinPartySize.HasValue) settings.MinPartySize = request.MinPartySize.Value;
        if (request.MaxOnlinePartySize.HasValue) settings.MaxOnlinePartySize = request.MaxOnlinePartySize.Value;
        if (request.MaxPartySize.HasValue) settings.MaxPartySize = request.MaxPartySize.Value;

        // Duration
        if (request.DefaultDurationMinutes.HasValue) settings.DefaultDurationMinutes = request.DefaultDurationMinutes.Value;
        if (request.MinDurationMinutes.HasValue) settings.MinDurationMinutes = request.MinDurationMinutes.Value;
        if (request.MaxDurationMinutes.HasValue) settings.MaxDurationMinutes = request.MaxDurationMinutes.Value;

        // Buffer times
        if (request.TableTurnBufferMinutes.HasValue) settings.TableTurnBufferMinutes = request.TableTurnBufferMinutes.Value;
        if (request.NoShowGracePeriodMinutes.HasValue) settings.NoShowGracePeriodMinutes = request.NoShowGracePeriodMinutes.Value;

        // Confirmation
        if (request.RequireConfirmation.HasValue) settings.RequireConfirmation = request.RequireConfirmation.Value;
        if (request.ConfirmationReminderHours.HasValue) settings.ConfirmationReminderHours = request.ConfirmationReminderHours.Value;
        if (request.AutoConfirmOnlineBookings.HasValue) settings.AutoConfirmOnlineBookings = request.AutoConfirmOnlineBookings.Value;

        // Online booking
        if (request.OnlineBookingEnabled.HasValue) settings.OnlineBookingEnabled = request.OnlineBookingEnabled.Value;
        if (request.ShowAvailableTables.HasValue) settings.ShowAvailableTables = request.ShowAvailableTables.Value;
        if (request.AllowTableSelection.HasValue) settings.AllowTableSelection = request.AllowTableSelection.Value;
        if (request.RequirePhone.HasValue) settings.RequirePhone = request.RequirePhone.Value;
        if (request.RequireEmail.HasValue) settings.RequireEmail = request.RequireEmail.Value;

        // Cancellation
        if (request.FreeCancellationHours.HasValue) settings.FreeCancellationHours = request.FreeCancellationHours.Value;
        if (request.AllowOnlineCancellation.HasValue) settings.AllowOnlineCancellation = request.AllowOnlineCancellation.Value;

        // Waitlist
        if (request.WaitlistEnabled.HasValue) settings.WaitlistEnabled = request.WaitlistEnabled.Value;
        if (request.MaxWaitlistSize.HasValue) settings.MaxWaitlistSize = request.MaxWaitlistSize.Value;
        if (request.WaitlistOfferExpiryMinutes.HasValue) settings.WaitlistOfferExpiryMinutes = request.WaitlistOfferExpiryMinutes.Value;

        // Display
        if (request.Timezone != null) settings.Timezone = request.Timezone;
        if (request.TermsAndConditions != null) settings.TermsAndConditions = request.TermsAndConditions;
        if (request.ConfirmationMessage != null) settings.ConfirmationMessage = request.ConfirmationMessage;
        if (request.CancellationPolicyText != null) settings.CancellationPolicyText = request.CancellationPolicyText;

        await _context.SaveChangesAsync();

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/booking-settings");

        return Ok(dto);
    }

    private static BookingSettings CreateDefaultSettings(Guid locationId)
    {
        return new BookingSettings
        {
            LocationId = locationId,
            BookingWindowDays = 30,
            MinAdvanceHours = 2,
            MinPartySize = 1,
            MaxOnlinePartySize = 8,
            MaxPartySize = 20,
            DefaultDurationMinutes = 90,
            MinDurationMinutes = 60,
            MaxDurationMinutes = 180,
            TableTurnBufferMinutes = 15,
            NoShowGracePeriodMinutes = 15,
            RequireConfirmation = true,
            ConfirmationReminderHours = 24,
            AutoConfirmOnlineBookings = true,
            OnlineBookingEnabled = true,
            ShowAvailableTables = false,
            AllowTableSelection = false,
            RequirePhone = true,
            RequireEmail = true,
            FreeCancellationHours = 24,
            AllowOnlineCancellation = true,
            WaitlistEnabled = true,
            MaxWaitlistSize = 20,
            WaitlistOfferExpiryMinutes = 15,
            Timezone = "Europe/London"
        };
    }

    private static BookingSettingsDto MapToDto(BookingSettings settings)
    {
        return new BookingSettingsDto
        {
            Id = settings.Id,
            LocationId = settings.LocationId,
            BookingWindowDays = settings.BookingWindowDays,
            MinAdvanceHours = settings.MinAdvanceHours,
            MaxAdvanceHours = settings.MaxAdvanceHours,
            MinPartySize = settings.MinPartySize,
            MaxOnlinePartySize = settings.MaxOnlinePartySize,
            MaxPartySize = settings.MaxPartySize,
            DefaultDurationMinutes = settings.DefaultDurationMinutes,
            MinDurationMinutes = settings.MinDurationMinutes,
            MaxDurationMinutes = settings.MaxDurationMinutes,
            TableTurnBufferMinutes = settings.TableTurnBufferMinutes,
            NoShowGracePeriodMinutes = settings.NoShowGracePeriodMinutes,
            RequireConfirmation = settings.RequireConfirmation,
            ConfirmationReminderHours = settings.ConfirmationReminderHours,
            AutoConfirmOnlineBookings = settings.AutoConfirmOnlineBookings,
            OnlineBookingEnabled = settings.OnlineBookingEnabled,
            ShowAvailableTables = settings.ShowAvailableTables,
            AllowTableSelection = settings.AllowTableSelection,
            RequirePhone = settings.RequirePhone,
            RequireEmail = settings.RequireEmail,
            FreeCancellationHours = settings.FreeCancellationHours,
            AllowOnlineCancellation = settings.AllowOnlineCancellation,
            WaitlistEnabled = settings.WaitlistEnabled,
            MaxWaitlistSize = settings.MaxWaitlistSize,
            WaitlistOfferExpiryMinutes = settings.WaitlistOfferExpiryMinutes,
            Timezone = settings.Timezone,
            TermsAndConditions = settings.TermsAndConditions,
            ConfirmationMessage = settings.ConfirmationMessage,
            CancellationPolicyText = settings.CancellationPolicyText,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

[ApiController]
[Route("api/locations/{locationId:guid}/deposit-policies")]
public class DepositPoliciesController : ControllerBase
{
    private readonly BookingDbContext _context;

    public DepositPoliciesController(BookingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<DepositPolicyDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.DepositPolicies
            .Where(p => p.LocationId == locationId);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var policies = await query
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.MinPartySize)
            .ToListAsync();

        var dtos = policies.Select(p => MapToDto(p, locationId)).ToList();

        return Ok(HalCollection<DepositPolicyDto>.Create(
            dtos,
            $"/api/locations/{locationId}/deposit-policies",
            dtos.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepositPolicyDto>> GetById(Guid locationId, Guid id)
    {
        var policy = await _context.DepositPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId);

        if (policy == null)
            return NotFound();

        return Ok(MapToDto(policy, locationId));
    }

    [HttpPost]
    public async Task<ActionResult<DepositPolicyDto>> Create(
        Guid locationId,
        [FromBody] CreateDepositPolicyRequest request)
    {
        var validTypes = new[] { "per_person", "flat_rate", "percentage" };
        if (!validTypes.Contains(request.DepositType))
            return BadRequest(new { message = $"Invalid deposit type. Must be one of: {string.Join(", ", validTypes)}" });

        var policy = new DepositPolicy
        {
            LocationId = locationId,
            Name = request.Name,
            Description = request.Description,
            MinPartySize = request.MinPartySize,
            MaxPartySize = request.MaxPartySize,
            DepositType = request.DepositType,
            AmountPerPerson = request.AmountPerPerson,
            FlatAmount = request.FlatAmount,
            PercentageRate = request.PercentageRate,
            MinimumAmount = request.MinimumAmount,
            MaximumAmount = request.MaximumAmount,
            CurrencyCode = request.CurrencyCode,
            RefundableUntilHours = request.RefundableUntilHours,
            RefundPercentage = request.RefundPercentage,
            ForfeitsOnNoShow = request.ForfeitsOnNoShow,
            ApplicableDays = request.ApplicableDays,
            ApplicableFromTime = request.ApplicableFromTime,
            ApplicableToTime = request.ApplicableToTime,
            Priority = request.Priority,
            IsActive = true
        };

        _context.DepositPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var dto = MapToDto(policy, locationId);

        return CreatedAtAction(nameof(GetById), new { locationId, id = policy.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<DepositPolicyDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateDepositPolicyRequest request)
    {
        var policy = await _context.DepositPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId);

        if (policy == null)
            return NotFound();

        if (request.Name != null) policy.Name = request.Name;
        if (request.Description != null) policy.Description = request.Description;
        if (request.MinPartySize.HasValue) policy.MinPartySize = request.MinPartySize.Value;
        if (request.MaxPartySize.HasValue) policy.MaxPartySize = request.MaxPartySize;
        if (request.DepositType != null) policy.DepositType = request.DepositType;
        if (request.AmountPerPerson.HasValue) policy.AmountPerPerson = request.AmountPerPerson;
        if (request.FlatAmount.HasValue) policy.FlatAmount = request.FlatAmount;
        if (request.PercentageRate.HasValue) policy.PercentageRate = request.PercentageRate;
        if (request.MinimumAmount.HasValue) policy.MinimumAmount = request.MinimumAmount;
        if (request.MaximumAmount.HasValue) policy.MaximumAmount = request.MaximumAmount;
        if (request.RefundableUntilHours.HasValue) policy.RefundableUntilHours = request.RefundableUntilHours.Value;
        if (request.RefundPercentage.HasValue) policy.RefundPercentage = request.RefundPercentage.Value;
        if (request.ForfeitsOnNoShow.HasValue) policy.ForfeitsOnNoShow = request.ForfeitsOnNoShow.Value;
        if (request.ApplicableDays != null) policy.ApplicableDays = request.ApplicableDays;
        if (request.ApplicableFromTime.HasValue) policy.ApplicableFromTime = request.ApplicableFromTime;
        if (request.ApplicableToTime.HasValue) policy.ApplicableToTime = request.ApplicableToTime;
        if (request.IsActive.HasValue) policy.IsActive = request.IsActive.Value;
        if (request.Priority.HasValue) policy.Priority = request.Priority.Value;

        await _context.SaveChangesAsync();

        return Ok(MapToDto(policy, locationId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var policy = await _context.DepositPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId);

        if (policy == null)
            return NotFound();

        _context.DepositPolicies.Remove(policy);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static DepositPolicyDto MapToDto(DepositPolicy policy, Guid locationId)
    {
        var dto = new DepositPolicyDto
        {
            Id = policy.Id,
            LocationId = policy.LocationId,
            Name = policy.Name,
            Description = policy.Description,
            MinPartySize = policy.MinPartySize,
            MaxPartySize = policy.MaxPartySize,
            DepositType = policy.DepositType,
            AmountPerPerson = policy.AmountPerPerson,
            FlatAmount = policy.FlatAmount,
            PercentageRate = policy.PercentageRate,
            MinimumAmount = policy.MinimumAmount,
            MaximumAmount = policy.MaximumAmount,
            CurrencyCode = policy.CurrencyCode,
            RefundableUntilHours = policy.RefundableUntilHours,
            RefundPercentage = policy.RefundPercentage,
            ForfeitsOnNoShow = policy.ForfeitsOnNoShow,
            ApplicableDays = policy.ApplicableDays,
            ApplicableFromTime = policy.ApplicableFromTime,
            ApplicableToTime = policy.ApplicableToTime,
            IsActive = policy.IsActive,
            Priority = policy.Priority,
            CreatedAt = policy.CreatedAt
        };

        dto.AddSelfLink($"/api/locations/{locationId}/deposit-policies/{policy.Id}");

        return dto;
    }
}
