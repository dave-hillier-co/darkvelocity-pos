using System.Security.Cryptography;
using DarkVelocity.GiftCards.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.GiftCards.Api.Services;

public interface ICardNumberGenerator
{
    Task<string> GenerateAsync(string prefix);
    string GeneratePin();
    string HashPin(string pin);
    bool VerifyPin(string pin, string hash);
}

public class CardNumberGenerator : ICardNumberGenerator
{
    private readonly GiftCardsDbContext _context;
    private const int CardNumberLength = 16;
    private const int PinLength = 4;
    private const int MaxAttempts = 10;

    public CardNumberGenerator(GiftCardsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generates a unique card number with the given prefix
    /// Format: PREFIX + random digits to make 16-19 digits total, with Luhn check digit
    /// </summary>
    public async Task<string> GenerateAsync(string prefix)
    {
        var normalizedPrefix = prefix.ToUpperInvariant().Replace("-", "").Replace(" ", "");

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var cardNumber = GenerateCardNumber(normalizedPrefix);

            // Check for uniqueness
            var exists = await _context.GiftCards.AnyAsync(g => g.CardNumber == cardNumber);
            if (!exists)
            {
                return cardNumber;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique card number after multiple attempts");
    }

    /// <summary>
    /// Generates a random PIN
    /// </summary>
    public string GeneratePin()
    {
        var bytes = new byte[PinLength];
        RandomNumberGenerator.Fill(bytes);

        var pin = "";
        for (var i = 0; i < PinLength; i++)
        {
            pin += (bytes[i] % 10).ToString();
        }
        return pin;
    }

    /// <summary>
    /// Hashes a PIN for secure storage
    /// </summary>
    public string HashPin(string pin)
    {
        // Use a simple hash with salt for PIN storage
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 10000, HashAlgorithmName.SHA256, 32);

        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a PIN against its hash
    /// </summary>
    public bool VerifyPin(string pin, string hash)
    {
        try
        {
            var parts = hash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = Convert.FromBase64String(parts[1]);

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 10000, HashAlgorithmName.SHA256, 32);

            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateCardNumber(string prefix)
    {
        // Generate random digits to fill the card number
        var randomLength = CardNumberLength - prefix.Length - 1; // -1 for Luhn check digit
        if (randomLength < 8) randomLength = 8; // Ensure minimum randomness

        var bytes = new byte[randomLength];
        RandomNumberGenerator.Fill(bytes);

        var cardNumber = prefix;
        for (var i = 0; i < randomLength; i++)
        {
            cardNumber += (bytes[i] % 10).ToString();
        }

        // Add Luhn check digit
        var checkDigit = CalculateLuhnCheckDigit(cardNumber);
        return cardNumber + checkDigit;
    }

    private static int CalculateLuhnCheckDigit(string number)
    {
        var sum = 0;
        var alternate = true;

        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return (10 - (sum % 10)) % 10;
    }
}

public static class CardNumberHelper
{
    /// <summary>
    /// Masks a card number for display, showing only the last 4 digits
    /// </summary>
    public static string MaskCardNumber(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return cardNumber;

        return new string('*', cardNumber.Length - 4) + cardNumber[^4..];
    }

    /// <summary>
    /// Validates a card number using the Luhn algorithm
    /// </summary>
    public static bool ValidateLuhn(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber))
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = cardNumber.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(cardNumber[i]))
                return false;

            var digit = cardNumber[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
