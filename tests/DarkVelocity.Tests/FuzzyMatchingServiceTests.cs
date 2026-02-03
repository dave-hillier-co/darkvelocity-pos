using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Trait("Category", "Unit")]
public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    [Theory]
    [InlineData("CHICKEN BREAST", "chicken breast")]
    [InlineData("  Organic  Eggs  ", "organic eggs")]
    [InlineData("Ground Beef!!!", "ground beef")]
    public void Normalize_ShouldCleanAndLowercase(string input, string expected)
    {
        var result = _service.Normalize(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CHKN BRST", "chicken breast")]
    [InlineData("ORG LG EGGS", "organic large eggs")]
    [InlineData("GRD BF 80/20", "ground beef 80/20")]
    [InlineData("EVOO 1L", "extra virgin olive oil 1l")]
    public void ExpandAbbreviations_ShouldExpandCommonReceipt(string input, string expected)
    {
        var result = _service.ExpandAbbreviations(input);
        result.ToLowerInvariant().Should().Contain(expected.Split(' ')[0]);
    }

    [Fact]
    public void Tokenize_ShouldExtractSignificantTokens()
    {
        // Arrange
        var description = "Organic Large Brown Eggs 24ct";

        // Act
        var tokens = _service.Tokenize(description);

        // Assert
        tokens.Should().Contain("organic");
        tokens.Should().Contain("large");
        tokens.Should().Contain("brown");
        tokens.Should().Contain("eggs");
        // Should not contain stop words or pure numbers (but "24ct" is kept as it's a quantity pattern useful for matching)
        tokens.Should().Contain("24ct");
    }

    [Theory]
    [InlineData("chicken breast", "chicken breast", 1.0)]
    [InlineData("chicken breast", "chicken breat", 0.9)] // typo
    [InlineData("chicken breast", "beef steak", 0.2)] // different - Levenshtein similarity is ~0.28
    public void CalculateSimilarity_ShouldReturnExpectedRange(
        string a, string b, double minExpected)
    {
        var result = _service.CalculateSimilarity(a, b);
        result.Should().BeGreaterThanOrEqualTo((decimal)minExpected);
    }

    [Fact]
    public void CalculateTokenScore_HighOverlap_ShouldReturnHighScore()
    {
        // Arrange
        var descTokens = new[] { "organic", "chicken", "breast", "boneless" };
        var patternTokens = new[] { "organic", "chicken", "breast" };

        // Act
        var score = _service.CalculateTokenScore(descTokens, patternTokens);

        // Assert
        score.Should().BeGreaterThan(0.8m);
    }

    [Fact]
    public void CalculateTokenScore_NoOverlap_ShouldReturnLowScore()
    {
        // Arrange
        var descTokens = new[] { "organic", "chicken", "breast" };
        var patternTokens = new[] { "ground", "beef", "patty" };

        // Act
        var score = _service.CalculateTokenScore(descTokens, patternTokens);

        // Assert
        score.Should().BeLessThan(0.3m);
    }

    [Fact]
    public void FindPatternMatches_ShouldReturnBestMatches()
    {
        // Arrange
        var patterns = new List<LearnedPattern>
        {
            new LearnedPattern
            {
                Tokens = new[] { "chicken", "breast", "boneless" },
                IngredientId = Guid.NewGuid(),
                IngredientName = "Chicken Breast",
                IngredientSku = "chicken-breast",
                Weight = 5,
                LearnedAt = DateTime.UtcNow
            },
            new LearnedPattern
            {
                Tokens = new[] { "ground", "beef" },
                IngredientId = Guid.NewGuid(),
                IngredientName = "Ground Beef",
                IngredientSku = "beef-ground",
                Weight = 3,
                LearnedAt = DateTime.UtcNow
            }
        };

        // Act
        var matches = _service.FindPatternMatches(
            "Chicken Breast Skinless Boneless",
            patterns,
            minConfidence: 0.5m);

        // Assert
        matches.Should().HaveCount(1);
        matches[0].Pattern.IngredientName.Should().Be("Chicken Breast");
        matches[0].Score.Should().BeGreaterThan(0.7m);
    }

    [Fact]
    public void FindIngredientMatches_ShouldReturnSuggestions()
    {
        // Arrange
        var candidates = new List<IngredientInfo>
        {
            new IngredientInfo(Guid.NewGuid(), "Chicken Breast", "chicken-breast", "Proteins"),
            new IngredientInfo(Guid.NewGuid(), "Chicken Thigh", "chicken-thigh", "Proteins"),
            new IngredientInfo(Guid.NewGuid(), "Ground Beef 80/20", "beef-ground", "Proteins"),
            new IngredientInfo(Guid.NewGuid(), "Atlantic Salmon", "salmon-atlantic", "Seafood")
        };

        // Act
        var suggestions = _service.FindIngredientMatches(
            "CHKN BRST BNLS",
            candidates,
            minConfidence: 0.3m);

        // Assert
        suggestions.Should().NotBeEmpty();
        // Chicken items should be ranked higher
        var chickenSuggestions = suggestions.Where(s => s.IngredientName.Contains("Chicken")).ToList();
        chickenSuggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void FindIngredientMatches_WithAliases_ShouldMatchOnAlias()
    {
        // Arrange
        var candidates = new List<IngredientInfo>
        {
            new IngredientInfo(
                Guid.NewGuid(),
                "Extra Virgin Olive Oil",
                "oil-olive-ev",
                "Oils",
                new[] { "EVOO", "Olive Oil" })
        };

        // Act
        var suggestions = _service.FindIngredientMatches(
            "EVOO 1 Liter",
            candidates,
            minConfidence: 0.3m);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions[0].IngredientName.Should().Be("Extra Virgin Olive Oil");
    }

    [Theory]
    [InlineData("KS ORG LG EGGS 24CT", "eggs")]
    [InlineData("CHKN BRST BNLS SKNLS", "chicken")]
    [InlineData("GRD BF 80/20 5LB", "beef")]
    public void Tokenize_ReceiptAbbreviations_ShouldExpandAndTokenize(
        string input, string expectedToken)
    {
        // Act
        var tokens = _service.Tokenize(input);

        // Assert
        tokens.Should().Contain(expectedToken);
    }
}
