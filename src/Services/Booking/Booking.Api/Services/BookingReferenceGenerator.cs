using DarkVelocity.Booking.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Booking.Api.Services;

public interface IBookingReferenceGenerator
{
    Task<string> GenerateAsync(Guid locationId);
}

public class BookingReferenceGenerator : IBookingReferenceGenerator
{
    private readonly BookingDbContext _context;
    private static readonly char[] AlphanumericChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public BookingReferenceGenerator(BookingDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(Guid locationId)
    {
        var year = DateTime.UtcNow.Year;
        string reference;
        var attempts = 0;
        const int maxAttempts = 10;

        do
        {
            var randomPart = GenerateRandomPart(6);
            reference = $"BK-{year}-{randomPart}";
            attempts++;

            if (attempts >= maxAttempts)
            {
                // Fall back to GUID-based reference
                reference = $"BK-{year}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
                break;
            }
        }
        while (await _context.Bookings.AnyAsync(b =>
            b.LocationId == locationId &&
            b.BookingReference == reference));

        return reference;
    }

    private static string GenerateRandomPart(int length)
    {
        var random = Random.Shared;
        var chars = new char[length];

        for (var i = 0; i < length; i++)
        {
            chars[i] = AlphanumericChars[random.Next(AlphanumericChars.Length)];
        }

        return new string(chars);
    }
}
