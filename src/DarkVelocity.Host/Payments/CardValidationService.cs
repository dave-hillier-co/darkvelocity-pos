using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkVelocity.Host.Payments;

public interface ICardValidationService
{
    bool ValidateCardNumber(string number);
    string GetCardBrand(string number);
    string GenerateFingerprint(string number);
    string MaskCardNumber(string number);
    string GetLast4(string number);
    bool ValidateExpiry(int month, int year);
    bool ValidateCvc(string cvc, string brand);
    string? DetectCardCountry(string number);
    string DetectFundingType(string number);
}

public class CardValidationService : ICardValidationService
{
    // Card brand patterns
    private static readonly (string Brand, Regex Pattern)[] CardPatterns =
    [
        ("visa", new Regex(@"^4[0-9]{12}(?:[0-9]{3})?$", RegexOptions.Compiled)),
        ("mastercard", new Regex(@"^5[1-5][0-9]{14}$|^2(?:2(?:2[1-9]|[3-9][0-9])|[3-6][0-9][0-9]|7(?:[01][0-9]|20))[0-9]{12}$", RegexOptions.Compiled)),
        ("amex", new Regex(@"^3[47][0-9]{13}$", RegexOptions.Compiled)),
        ("discover", new Regex(@"^6(?:011|5[0-9]{2})[0-9]{12}$", RegexOptions.Compiled)),
        ("diners", new Regex(@"^3(?:0[0-5]|[68][0-9])[0-9]{11}$", RegexOptions.Compiled)),
        ("jcb", new Regex(@"^(?:2131|1800|35\d{3})\d{11}$", RegexOptions.Compiled)),
        ("unionpay", new Regex(@"^(62|88)\d{14,17}$", RegexOptions.Compiled))
    ];

    // Test card prefixes that indicate specific types
    private static readonly Dictionary<string, string> TestCardPrefixes = new()
    {
        ["424242"] = "visa",
        ["555555"] = "mastercard",
        ["378282"] = "amex",
        ["601111"] = "discover",
        ["300569"] = "diners",
        ["353011"] = "jcb"
    };

    /// <summary>
    /// Validates a card number using the Luhn algorithm
    /// </summary>
    public bool ValidateCardNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return false;

        // Remove spaces and dashes
        var cleanNumber = CleanCardNumber(number);

        if (!Regex.IsMatch(cleanNumber, @"^\d{13,19}$"))
            return false;

        return LuhnCheck(cleanNumber);
    }

    /// <summary>
    /// Detects the card brand from the card number
    /// </summary>
    public string GetCardBrand(string number)
    {
        var cleanNumber = CleanCardNumber(number);

        // Check test cards first
        foreach (var (prefix, brand) in TestCardPrefixes)
        {
            if (cleanNumber.StartsWith(prefix))
                return brand;
        }

        // Check real card patterns
        foreach (var (brand, pattern) in CardPatterns)
        {
            if (pattern.IsMatch(cleanNumber))
                return brand;
        }

        return "unknown";
    }

    /// <summary>
    /// Generates a fingerprint for the card number (for duplicate detection)
    /// </summary>
    public string GenerateFingerprint(string number)
    {
        var cleanNumber = CleanCardNumber(number);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cleanNumber));
        return Convert.ToBase64String(bytes)[..22]; // Shortened fingerprint
    }

    /// <summary>
    /// Masks the card number showing only last 4 digits
    /// </summary>
    public string MaskCardNumber(string number)
    {
        var cleanNumber = CleanCardNumber(number);
        if (cleanNumber.Length < 4)
            return new string('*', cleanNumber.Length);

        var last4 = cleanNumber[^4..];
        return $"****{last4}";
    }

    /// <summary>
    /// Gets the last 4 digits of the card number
    /// </summary>
    public string GetLast4(string number)
    {
        var cleanNumber = CleanCardNumber(number);
        return cleanNumber.Length >= 4 ? cleanNumber[^4..] : cleanNumber;
    }

    /// <summary>
    /// Validates card expiry date
    /// </summary>
    public bool ValidateExpiry(int month, int year)
    {
        if (month < 1 || month > 12)
            return false;

        // Handle 2-digit year
        var fullYear = year < 100 ? 2000 + year : year;

        var now = DateTime.UtcNow;
        var expiry = new DateTime(fullYear, month, 1).AddMonths(1).AddDays(-1);

        return expiry >= now;
    }

    /// <summary>
    /// Validates CVC based on card brand
    /// </summary>
    public bool ValidateCvc(string cvc, string brand)
    {
        if (string.IsNullOrWhiteSpace(cvc))
            return false;

        if (!Regex.IsMatch(cvc, @"^\d+$"))
            return false;

        // Amex uses 4-digit CVC, others use 3
        var expectedLength = brand.Equals("amex", StringComparison.OrdinalIgnoreCase) ? 4 : 3;

        return cvc.Length == expectedLength;
    }

    /// <summary>
    /// Attempts to detect the card issuing country from BIN
    /// </summary>
    public string? DetectCardCountry(string number)
    {
        var cleanNumber = CleanCardNumber(number);

        // Basic BIN-based country detection
        // In production, this would use a BIN database
        if (cleanNumber.StartsWith("4") && cleanNumber.Length == 16)
        {
            // US Visa cards often start with 4
            return "US";
        }

        // Default to null if we can't determine
        return null;
    }

    /// <summary>
    /// Detects the funding type (credit, debit, prepaid)
    /// </summary>
    public string DetectFundingType(string number)
    {
        // In production, this would use a BIN database
        // For now, assume credit unless we have specific indicators
        var cleanNumber = CleanCardNumber(number);

        // Some basic heuristics for test cards
        if (cleanNumber.StartsWith("4000056655665556") || // Visa Debit
            cleanNumber.StartsWith("5200828282828210"))   // MC Debit
        {
            return "debit";
        }

        if (cleanNumber.StartsWith("4000000000003055") || // Prepaid
            cleanNumber.StartsWith("5500000000000004"))   // MC Prepaid
        {
            return "prepaid";
        }

        return "credit";
    }

    private static string CleanCardNumber(string number)
    {
        return Regex.Replace(number ?? "", @"[\s\-]", "");
    }

    private static bool LuhnCheck(string number)
    {
        var sum = 0;
        var alternate = false;

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

        return sum % 10 == 0;
    }
}
