using DarkVelocity.Host.Payments;
using FluentAssertions;

namespace DarkVelocity.Tests;

public class CardValidationServiceTests
{
    private readonly CardValidationService _service = new();

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

    [Fact]
    public void GenerateFingerprint_DifferentCards_ShouldReturnDifferentFingerprints()
    {
        // Act
        var fingerprint1 = _service.GenerateFingerprint("4242424242424242");
        var fingerprint2 = _service.GenerateFingerprint("5555555555554444");

        // Assert
        fingerprint1.Should().NotBe(fingerprint2);
    }

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

    [Fact]
    public void DetectFundingType_ShouldDefaultToCredit()
    {
        // Act
        var funding = _service.DetectFundingType("4242424242424242");

        // Assert
        funding.Should().Be("credit");
    }

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
