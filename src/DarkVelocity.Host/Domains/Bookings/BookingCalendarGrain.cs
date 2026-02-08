using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Enhanced booking calendar grain with day/week views and availability calculation.
/// </summary>
public class BookingCalendarGrainImpl : Grain, IBookingCalendarGrain
{
    private readonly IPersistentState<BookingCalendarState> _state;
    private readonly IGrainFactory _grainFactory;

    public BookingCalendarGrainImpl(
        [PersistentState("bookingcalendar", "OrleansStorage")]
        IPersistentState<BookingCalendarState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date)
    {
        if (_state.State.SiteId != Guid.Empty)
            return;

        _state.State = new BookingCalendarState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            Date = date,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<BookingCalendarState> GetStateAsync() => Task.FromResult(_state.State);

    public async Task AddBookingAsync(AddBookingToCalendarCommand command)
    {
        EnsureExists();

        if (_state.State.Bookings.Any(b => b.BookingId == command.BookingId))
            return;

        var reference = new BookingReference
        {
            BookingId = command.BookingId,
            ConfirmationCode = command.ConfirmationCode,
            Time = command.Time,
            PartySize = command.PartySize,
            GuestName = command.GuestName,
            Status = command.Status,
            Duration = command.Duration
        };

        _state.State.Bookings.Add(reference);
        _state.State.TotalCovers += command.PartySize;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UpdateBookingAsync(UpdateBookingInCalendarCommand command)
    {
        EnsureExists();

        var index = _state.State.Bookings.FindIndex(b => b.BookingId == command.BookingId);
        if (index < 0)
            throw new InvalidOperationException("Booking not found in calendar");

        var existing = _state.State.Bookings[index];
        var oldPartySize = existing.PartySize;

        _state.State.Bookings[index] = existing with
        {
            Status = command.Status ?? existing.Status,
            Time = command.Time ?? existing.Time,
            PartySize = command.PartySize ?? existing.PartySize,
            TableId = command.TableId ?? existing.TableId,
            TableNumber = command.TableNumber ?? existing.TableNumber
        };

        if (command.PartySize.HasValue)
        {
            _state.State.TotalCovers = _state.State.TotalCovers - oldPartySize + command.PartySize.Value;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveBookingAsync(Guid bookingId)
    {
        EnsureExists();

        var booking = _state.State.Bookings.FirstOrDefault(b => b.BookingId == bookingId);
        if (booking != null)
        {
            _state.State.Bookings.Remove(booking);
            _state.State.TotalCovers -= booking.PartySize;
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<BookingReference>> GetBookingsAsync(BookingStatus? status = null)
    {
        var bookings = status.HasValue
            ? _state.State.Bookings.Where(b => b.Status == status.Value).ToList()
            : _state.State.Bookings;

        return Task.FromResult<IReadOnlyList<BookingReference>>(
            bookings.OrderBy(b => b.Time).ToList());
    }

    public Task<IReadOnlyList<BookingReference>> GetBookingsByTimeRangeAsync(TimeOnly start, TimeOnly end)
    {
        var bookings = _state.State.Bookings
            .Where(b => b.Time >= start && b.Time <= end)
            .OrderBy(b => b.Time)
            .ToList();

        return Task.FromResult<IReadOnlyList<BookingReference>>(bookings);
    }

    public Task<int> GetCoverCountAsync() => Task.FromResult(_state.State.TotalCovers);

    public Task<int> GetBookingCountAsync(BookingStatus? status = null)
    {
        var count = status.HasValue
            ? _state.State.Bookings.Count(b => b.Status == status.Value)
            : _state.State.Bookings.Count;

        return Task.FromResult(count);
    }

    public Task<CalendarDayView> GetDayViewAsync(TimeSpan? slotDuration = null)
    {
        EnsureExists();

        var duration = slotDuration ?? TimeSpan.FromHours(1);
        var slots = new List<CalendarBookingSlot>();

        // Generate slots for the day (11:00 to 22:00 by default)
        var startTime = new TimeOnly(11, 0);
        var endTime = new TimeOnly(22, 0);
        var currentTime = startTime;

        while (currentTime < endTime)
        {
            var slotEnd = currentTime.Add(duration);
            if (slotEnd > endTime) slotEnd = endTime;

            var slotBookings = _state.State.Bookings
                .Where(b => b.Time >= currentTime && b.Time < slotEnd)
                .OrderBy(b => b.Time)
                .ToList();

            slots.Add(new CalendarBookingSlot
            {
                StartTime = currentTime,
                EndTime = slotEnd,
                BookingCount = slotBookings.Count,
                CoverCount = slotBookings.Sum(b => b.PartySize),
                Bookings = slotBookings
            });

            currentTime = slotEnd;
        }

        var dayView = new CalendarDayView
        {
            Date = _state.State.Date,
            Slots = slots,
            TotalBookings = _state.State.Bookings.Count,
            TotalCovers = _state.State.TotalCovers,
            ConfirmedBookings = _state.State.Bookings.Count(b => b.Status == BookingStatus.Confirmed),
            SeatedBookings = _state.State.Bookings.Count(b => b.Status == BookingStatus.Seated),
            NoShowCount = _state.State.Bookings.Count(b => b.Status == BookingStatus.NoShow)
        };

        return Task.FromResult(dayView);
    }

    public async Task<IReadOnlyList<AvailableTimeSlot>> GetAvailabilityAsync(GetCalendarAvailabilityQuery query)
    {
        EnsureExists();

        var slots = new List<AvailableTimeSlot>();
        var settingsGrain = _grainFactory.GetGrain<IBookingSettingsGrain>(
            GrainKeys.BookingSettings(_state.State.OrganizationId, _state.State.SiteId));

        if (!await settingsGrain.ExistsAsync())
            await settingsGrain.InitializeAsync(_state.State.OrganizationId, _state.State.SiteId);

        var settings = await settingsGrain.GetStateAsync();
        var interval = settings.SlotInterval;
        var duration = query.RequestedDuration ?? settings.DefaultDuration;
        var turnoverBuffer = settings.TurnoverBuffer;

        // Check blocked dates
        if (settings.BlockedDates.Contains(query.Date))
        {
            return GenerateUnavailableSlots(settings.DefaultOpenTime, settings.DefaultCloseTime, interval, duration);
        }

        // Check party size
        if (query.PartySize > settings.MaxPartySizeOnline)
        {
            return GenerateUnavailableSlots(settings.DefaultOpenTime, settings.DefaultCloseTime, interval, duration);
        }

        // Try to get table recommendations from optimizer
        var optimizerGrain = _grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
            GrainKeys.TableAssignmentOptimizer(_state.State.OrganizationId, _state.State.SiteId));
        var hasOptimizer = await optimizerGrain.ExistsAsync();

        var currentTime = settings.DefaultOpenTime;
        var closeTime = settings.DefaultCloseTime;

        while (currentTime < closeTime)
        {
            // Count bookings that overlap this time slot (considering duration + turnover buffer)
            var overlappingBookings = _state.State.Bookings.Count(b =>
            {
                var bookingDuration = b.Duration ?? duration;
                var bookingEnd = b.Time.Add(bookingDuration).Add(turnoverBuffer);
                return b.Time <= currentTime && bookingEnd > currentTime;
            });

            var slotAvailableByCount = overlappingBookings < settings.MaxBookingsPerSlot;
            var availableCapacity = Math.Max(0, settings.MaxBookingsPerSlot - overlappingBookings);

            // Get table suggestions from optimizer if available
            var suggestedTables = new List<TableSuggestion>();
            var hasAvailableTables = true;

            if (hasOptimizer)
            {
                var bookingDateTime = query.Date.ToDateTime(currentTime);
                var recommendations = await optimizerGrain.GetRecommendationsAsync(new TableAssignmentRequest(
                    BookingId: Guid.Empty,
                    PartySize: query.PartySize,
                    BookingTime: bookingDateTime,
                    Duration: duration));

                if (recommendations.Success && recommendations.Recommendations.Count > 0)
                {
                    // Filter out tables already booked at this time
                    var bookedTableIds = _state.State.Bookings
                        .Where(b =>
                        {
                            var bookingDuration = b.Duration ?? duration;
                            var bookingEnd = b.Time.Add(bookingDuration).Add(turnoverBuffer);
                            return b.Time <= currentTime && bookingEnd > currentTime && b.TableId.HasValue;
                        })
                        .Select(b => b.TableId!.Value)
                        .ToHashSet();

                    suggestedTables = recommendations.Recommendations
                        .Where(r => !bookedTableIds.Contains(r.TableId))
                        .Select(r => new TableSuggestion
                        {
                            TableId = r.TableId,
                            TableNumber = r.TableNumber,
                            Capacity = r.Capacity,
                            IsCombination = r.RequiresCombination,
                            CombinedTableIds = r.CombinedTableIds,
                            Score = r.Score
                        })
                        .ToList();

                    hasAvailableTables = suggestedTables.Count > 0;
                    availableCapacity = suggestedTables.Count;
                }
                else
                {
                    hasAvailableTables = false;
                }
            }

            var isAvailable = slotAvailableByCount && hasAvailableTables;

            slots.Add(new AvailableTimeSlot
            {
                Time = currentTime,
                IsAvailable = isAvailable,
                AvailableCapacity = availableCapacity,
                SuggestedTables = suggestedTables,
                EstimatedDuration = duration
            });

            currentTime = currentTime.Add(interval);
        }

        return slots;
    }

    private static IReadOnlyList<AvailableTimeSlot> GenerateUnavailableSlots(
        TimeOnly openTime, TimeOnly closeTime, TimeSpan interval, TimeSpan duration)
    {
        var slots = new List<AvailableTimeSlot>();
        var currentTime = openTime;
        while (currentTime < closeTime)
        {
            slots.Add(new AvailableTimeSlot
            {
                Time = currentTime,
                IsAvailable = false,
                AvailableCapacity = 0,
                SuggestedTables = [],
                EstimatedDuration = duration
            });
            currentTime = currentTime.Add(interval);
        }
        return slots;
    }

    public Task<IReadOnlyList<TableAllocation>> GetTableAllocationsAsync()
    {
        EnsureExists();

        var allocations = _state.State.Bookings
            .Where(b => b.TableId.HasValue)
            .GroupBy(b => b.TableId!.Value)
            .Select(g => new TableAllocation
            {
                TableId = g.Key,
                TableNumber = g.First().TableNumber ?? "",
                Bookings = g.OrderBy(b => b.Time).ToList()
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TableAllocation>>(allocations);
    }

    public async Task SetTableAllocationAsync(Guid bookingId, Guid tableId, string tableNumber)
    {
        EnsureExists();

        var index = _state.State.Bookings.FindIndex(b => b.BookingId == bookingId);
        if (index >= 0)
        {
            _state.State.Bookings[index] = _state.State.Bookings[index] with
            {
                TableId = tableId,
                TableNumber = tableNumber
            };
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.SiteId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Booking calendar not initialized");
    }
}

/// <summary>
/// Week calendar grain for aggregate views.
/// </summary>
public class WeekCalendarGrain : Grain, IWeekCalendarGrain
{
    private readonly IGrainFactory _grainFactory;

    public WeekCalendarGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<CalendarWeekView> GetWeekViewAsync(Guid orgId, Guid siteId, DateOnly startDate)
    {
        var days = new List<CalendarDaySummary>();
        var totalBookings = 0;
        var totalCovers = 0;

        for (int i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);
            var calendarGrain = _grainFactory.GetGrain<IBookingCalendarGrain>(
                GrainKeys.BookingCalendar(orgId, siteId, date));

            if (await calendarGrain.ExistsAsync())
            {
                var dayView = await calendarGrain.GetDayViewAsync();
                days.Add(new CalendarDaySummary
                {
                    Date = date,
                    BookingCount = dayView.TotalBookings,
                    CoverCount = dayView.TotalCovers,
                    AvailableSlots = dayView.Slots.Count(s => s.Bookings.Count == 0),
                    IsClosed = false
                });
                totalBookings += dayView.TotalBookings;
                totalCovers += dayView.TotalCovers;
            }
            else
            {
                days.Add(new CalendarDaySummary
                {
                    Date = date,
                    BookingCount = 0,
                    CoverCount = 0,
                    AvailableSlots = 0,
                    IsClosed = false
                });
            }
        }

        return new CalendarWeekView
        {
            StartDate = startDate,
            EndDate = startDate.AddDays(6),
            Days = days,
            TotalBookings = totalBookings,
            TotalCovers = totalCovers
        };
    }
}
