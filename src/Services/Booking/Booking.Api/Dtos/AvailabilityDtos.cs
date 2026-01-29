using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Booking.Api.Dtos;

public class TimeSlotDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int IntervalMinutes { get; set; }
    public int? MaxBookings { get; set; }
    public int? MaxCovers { get; set; }
    public int TurnTimeMinutes { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }
    public Guid? FloorPlanId { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DateOverrideDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public string OverrideType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? MaxBookings { get; set; }
    public int? MaxCovers { get; set; }
    public bool DisableOnlineBooking { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepositPolicyDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MinPartySize { get; set; }
    public int? MaxPartySize { get; set; }
    public string DepositType { get; set; } = string.Empty;
    public decimal? AmountPerPerson { get; set; }
    public decimal? FlatAmount { get; set; }
    public decimal? PercentageRate { get; set; }
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int RefundableUntilHours { get; set; }
    public decimal RefundPercentage { get; set; }
    public bool ForfeitsOnNoShow { get; set; }
    public string? ApplicableDays { get; set; }
    public TimeOnly? ApplicableFromTime { get; set; }
    public TimeOnly? ApplicableToTime { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingSettingsDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }

    // Booking window
    public int BookingWindowDays { get; set; }
    public int MinAdvanceHours { get; set; }
    public int? MaxAdvanceHours { get; set; }

    // Party size
    public int MinPartySize { get; set; }
    public int MaxOnlinePartySize { get; set; }
    public int MaxPartySize { get; set; }

    // Duration
    public int DefaultDurationMinutes { get; set; }
    public int MinDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }

    // Buffer times
    public int TableTurnBufferMinutes { get; set; }
    public int NoShowGracePeriodMinutes { get; set; }

    // Confirmation
    public bool RequireConfirmation { get; set; }
    public int ConfirmationReminderHours { get; set; }
    public bool AutoConfirmOnlineBookings { get; set; }

    // Online booking
    public bool OnlineBookingEnabled { get; set; }
    public bool ShowAvailableTables { get; set; }
    public bool AllowTableSelection { get; set; }
    public bool RequirePhone { get; set; }
    public bool RequireEmail { get; set; }

    // Cancellation
    public int FreeCancellationHours { get; set; }
    public bool AllowOnlineCancellation { get; set; }

    // Waitlist
    public bool WaitlistEnabled { get; set; }
    public int MaxWaitlistSize { get; set; }
    public int WaitlistOfferExpiryMinutes { get; set; }

    // Display
    public string Timezone { get; set; } = string.Empty;
    public string? TermsAndConditions { get; set; }
    public string? ConfirmationMessage { get; set; }
    public string? CancellationPolicyText { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AvailabilityResponseDto : HalResource
{
    public DateOnly Date { get; set; }
    public int PartySize { get; set; }
    public Guid? FloorPlanId { get; set; }
    public List<AvailableSlotDto> Slots { get; set; } = new();
    public bool HasAvailability => Slots.Any();
}

public class AvailableSlotDto
{
    public TimeOnly Time { get; set; }
    public int AvailableTableCount { get; set; }
    public string? SlotName { get; set; }
}

public class AvailableTableDto : HalResource
{
    public Guid Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int MinCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public string? FloorPlanName { get; set; }
    public string Shape { get; set; } = string.Empty;
}

public class DepositRequirementDto : HalResource
{
    public bool DepositRequired { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? PolicyName { get; set; }
    public int? RefundableUntilHours { get; set; }
    public decimal? RefundPercentage { get; set; }
    public bool ForfeitsOnNoShow { get; set; }
}

// Request DTOs

public record CreateTimeSlotRequest(
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int IntervalMinutes = 15,
    int? MaxBookings = null,
    int? MaxCovers = null,
    int TurnTimeMinutes = 90,
    string? Name = null,
    Guid? FloorPlanId = null,
    int Priority = 0);

public record UpdateTimeSlotRequest(
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int? IntervalMinutes = null,
    int? MaxBookings = null,
    int? MaxCovers = null,
    int? TurnTimeMinutes = null,
    string? Name = null,
    bool? IsActive = null,
    Guid? FloorPlanId = null,
    int? Priority = null);

public record CreateDateOverrideRequest(
    DateOnly Date,
    string OverrideType,
    string Name,
    string? Description = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int? MaxBookings = null,
    int? MaxCovers = null,
    bool DisableOnlineBooking = false,
    string? Notes = null);

public record UpdateDateOverrideRequest(
    string? OverrideType = null,
    string? Name = null,
    string? Description = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int? MaxBookings = null,
    int? MaxCovers = null,
    bool? DisableOnlineBooking = null,
    string? Notes = null);

public record CreateDepositPolicyRequest(
    string Name,
    string DepositType,
    int MinPartySize = 1,
    int? MaxPartySize = null,
    decimal? AmountPerPerson = null,
    decimal? FlatAmount = null,
    decimal? PercentageRate = null,
    decimal? MinimumAmount = null,
    decimal? MaximumAmount = null,
    string CurrencyCode = "GBP",
    int RefundableUntilHours = 24,
    decimal RefundPercentage = 100,
    bool ForfeitsOnNoShow = true,
    string? ApplicableDays = null,
    TimeOnly? ApplicableFromTime = null,
    TimeOnly? ApplicableToTime = null,
    int Priority = 0,
    string? Description = null);

public record UpdateDepositPolicyRequest(
    string? Name = null,
    string? Description = null,
    int? MinPartySize = null,
    int? MaxPartySize = null,
    string? DepositType = null,
    decimal? AmountPerPerson = null,
    decimal? FlatAmount = null,
    decimal? PercentageRate = null,
    decimal? MinimumAmount = null,
    decimal? MaximumAmount = null,
    int? RefundableUntilHours = null,
    decimal? RefundPercentage = null,
    bool? ForfeitsOnNoShow = null,
    string? ApplicableDays = null,
    TimeOnly? ApplicableFromTime = null,
    TimeOnly? ApplicableToTime = null,
    bool? IsActive = null,
    int? Priority = null);

public record UpdateBookingSettingsRequest(
    int? BookingWindowDays = null,
    int? MinAdvanceHours = null,
    int? MaxAdvanceHours = null,
    int? MinPartySize = null,
    int? MaxOnlinePartySize = null,
    int? MaxPartySize = null,
    int? DefaultDurationMinutes = null,
    int? MinDurationMinutes = null,
    int? MaxDurationMinutes = null,
    int? TableTurnBufferMinutes = null,
    int? NoShowGracePeriodMinutes = null,
    bool? RequireConfirmation = null,
    int? ConfirmationReminderHours = null,
    bool? AutoConfirmOnlineBookings = null,
    bool? OnlineBookingEnabled = null,
    bool? ShowAvailableTables = null,
    bool? AllowTableSelection = null,
    bool? RequirePhone = null,
    bool? RequireEmail = null,
    int? FreeCancellationHours = null,
    bool? AllowOnlineCancellation = null,
    bool? WaitlistEnabled = null,
    int? MaxWaitlistSize = null,
    int? WaitlistOfferExpiryMinutes = null,
    string? Timezone = null,
    string? TermsAndConditions = null,
    string? ConfirmationMessage = null,
    string? CancellationPolicyText = null);

public record CheckAvailabilityRequest(
    DateOnly Date,
    int PartySize,
    Guid? FloorPlanId = null);

public record GetAvailableTablesRequest(
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    int PartySize,
    Guid? FloorPlanId = null);
