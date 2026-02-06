using DarkVelocity.Host.Payments;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Trait("Category", "Unit")]
public class CardValidationServiceTests
{
    private readonly CardValidationService _service = new();

    // Given: A card number in various formats (Visa, Mastercard, Amex, Discover, invalid, empty)
    // When: The card number is validated using Luhn check and format rules
    // Then: Valid card numbers pass and invalid ones are rejected
    [Theory]
    [InlineData("4242424242424242", true)]    // Visa
    [InlineData("5555555555554444", true)]    // Mastercard
    [InlineData("378282246310005", true)]     // Amex
    [InlineData("6011111111111117", true)]    // Discover
    [InlineData("4242 4242 4242 4242", true)] // With spaces
    [InlineData("4242-4242-4242-4242", true)] // With dashes
    [InlineData("1234567890123456", false)]   // Invalid Luhn
    [InlineData("123", false)]                // Too short
    [InlineData("", false)]                   // Empty
    [InlineData("abcd1234567890", false)]     // Contains letters
    public void ValidateCardNumber_ShouldValidateCorrectly(string number, bool expected)
    {
        // Act
        var result = _service.ValidateCardNumber(number);

        // Assert
        result.Should().Be(expected);
    }

    // Given: A card number with a specific issuer prefix (Visa, Mastercard, Amex, Discover, Diners, JCB)
    // When: The card brand is detected from the number prefix
    // Then: The correct card network brand is identified
    [Theory]
    [InlineData("4242424242424242", "visa")]
    [InlineData("4000056655665556", "visa")]
    [InlineData("5555555555554444", "mastercard")]
    [InlineData("2223003122003222", "mastercard")]
    [InlineData("378282246310005", "amex")]
    [InlineData("371449635398431", "amex")]
    [InlineData("6011111111111117", "discover")]
    [InlineData("30569309025904", "diners")]
    [InlineData("3530111333300000", "jcb")]
    [InlineData("9999999999999999", "unknown")]
    public void GetCardBrand_ShouldDetectBrand(string number, string expectedBrand)
    {
        // Act
        var brand = _service.GetCardBrand(number);

        // Assert
        brand.Should().Be(expectedBrand);
    }

    // Given: A single card number used for payment
    // When: The card fingerprint is generated twice
    // Then: Both fingerprints are identical, enabling duplicate card detection
    [Fact]
    public void GenerateFingerprint_SameCard_ShouldReturnSameFingerprint()
    {
        // Arrange
        var cardNumber = "4242424242424242";

        // Act
        var fingerprint1 = _service.GenerateFingerprint(cardNumber);
        var fingerprint2 = _service.GenerateFingerprint(cardNumber);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }

    // Given: Two different card numbers (Visa and Mastercard)
    // When: Fingerprints are generated for each card
    // Then: The fingerprints differ, ensuring unique card identification
    [Fact]
    public void GenerateFingerprint_DifferentCards_ShouldReturnDifferentFingerprints()
    {
        // Act
        var fingerprint1 = _service.GenerateFingerprint("4242424242424242");
        var fingerprint2 = _service.GenerateFingerprint("5555555555554444");

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
    }

    // Given: A card number to be displayed in receipts or UI
    // When: The card number is masked for PCI compliance
    // Then: Only the last four digits remain visible with the rest replaced by asterisks
    [Theory]
    [InlineData("4242424242424242", "****4242")]
    [InlineData("5555555555554444", "****4444")]
    [InlineData("123", "***")]
    public void MaskCardNumber_ShouldMaskCorrectly(string number, string expected)
    {
        // Act
        var masked = _service.MaskCardNumber(number);

        // Assert
        masked.Should().Be(expected);
    }

    // Given: A full card number from a payment transaction
    // When: The last four digits are extracted for display
    // Then: The correct trailing digits are returned for receipt printing
    [Theory]
    [InlineData("4242424242424242", "4242")]
    [InlineData("5555555555554444", "4444")]
    [InlineData("123", "123")]
    public void GetLast4_ShouldReturnLast4Digits(string number, string expected)
    {
        // Act
        var last4 = _service.GetLast4(number);

        // Assert
        last4.Should().Be(expected);
    }

    // Given: An expiry month and year from a card payment form
    // When: The expiry date is validated against current date and format rules
    // Then: Future dates pass, past dates and invalid months are rejected
    [Theory]
    [InlineData(12, 2030, true)]   // Future expiry
    [InlineData(1, 2020, false)]   // Past expiry
    [InlineData(0, 2030, false)]   // Invalid month
    [InlineData(13, 2030, false)]  // Invalid month
    [InlineData(12, 30, true)]     // 2-digit year
    public void ValidateExpiry_ShouldValidateCorrectly(int month, int year, bool expected)
    {
        // Act
        var result = _service.ValidateExpiry(month, year);

        // Assert
        result.Should().Be(expected);
    }

    // Given: A CVC code and card brand (3 digits for standard cards, 4 for Amex)
    // When: The CVC is validated against the brand-specific length requirement
    // Then: Correctly sized numeric CVCs pass and mismatched or non-numeric ones are rejected
    [Theory]
    [InlineData("123", "visa", true)]
    [InlineData("1234", "amex", true)]
    [InlineData("12", "visa", false)]     // Too short
    [InlineData("123", "amex", false)]    // Amex needs 4 digits
    [InlineData("1234", "visa", false)]   // Non-Amex needs 3 digits
    [InlineData("abc", "visa", false)]    // Non-numeric
    public void ValidateCvc_ShouldValidateCorrectly(string cvc, string brand, bool expected)
    {
        // Act
        var result = _service.ValidateCvc(cvc, brand);

        // Assert
        result.Should().Be(expected);
    }

    // Given: A card number without BIN-level funding type data
    // When: The funding type is detected
    // Then: The card defaults to credit funding type
    [Fact]
    public void DetectFundingType_ShouldDefaultToCredit()
    {
        // Act
        var funding = _service.DetectFundingType("4242424242424242");

        // Assert
        funding.Should().Be("credit");
    }

    // Given: A valid card number formatted with spaces or dashes
    // When: The card number is validated
    // Then: Formatting characters are stripped and the card passes validation
    [Theory]
    [InlineData("4242424242424242")]
    [InlineData("4242 4242 4242 4242")]
    [InlineData("4242-4242-4242-4242")]
    public void ValidateCardNumber_ShouldHandleWhitespaceAndDashes(string number)
    {
        // Act
        var result = _service.ValidateCardNumber(number);

        // Assert
        result.Should().BeTrue();
    }

    // Given: The same card number in clean and space-formatted forms
    // When: Fingerprints are generated for both representations
    // Then: Both produce identical fingerprints regardless of formatting
    [Fact]
    public void GenerateFingerprint_WithFormattedNumber_ShouldMatchClean()
    {
        // Arrange
        var cleanNumber = "4242424242424242";
        var formattedNumber = "4242 4242 4242 4242";

        // Act
        var fingerprint1 = _service.GenerateFingerprint(cleanNumber);
        var fingerprint2 = _service.GenerateFingerprint(formattedNumber);

        // Assert
        fingerprint1.Should().Be(fingerprint2);
    }
}
