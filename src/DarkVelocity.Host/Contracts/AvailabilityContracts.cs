namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Booking Settings Request DTOs
// ============================================================================

public record UpdateBookingSettingsRequest(
    TimeOnly? DefaultOpenTime = null,
    TimeOnly? DefaultCloseTime = null,
    TimeSpan? DefaultDuration = null,
    TimeSpan? SlotInterval = null,
    int? MaxPartySizeOnline = null,
    int? MaxBookingsPerSlot = null,
    int? AdvanceBookingDays = null,
    bool? RequireDeposit = null,
    decimal? DepositAmount = null);

// ============================================================================
// Customer Preferences Request DTOs
// ============================================================================

public record UpdateCustomerPreferencesRequest(
    List<string>? DietaryRestrictions = null,
    List<string>? Allergens = null,
    string? SeatingPreference = null,
    string? Notes = null);

public record AddTagRequest(string Tag);
