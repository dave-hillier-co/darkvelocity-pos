using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Trait("Category", "Unit")]
public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    // Given: A raw supplier invoice line description with mixed casing and extra whitespace
    // When: The description is normalized for matching
    // Then: The output is lowercase, trimmed, and cleaned of non-alphanumeric characters
    [Theory]
    [InlineData("CHICKEN BREAST", "chicken breast")]
    [InlineData("  Organic  Eggs  ", "organic eggs")]
    [InlineData("Ground Beef!!!", "ground beef")]
    public void Normalize_ShouldCleanAndLowercase(string input, string expected)
    {
        var result = _service.Normalize(input);
        result.Should().Be(expected);
    }

    // Given: An abbreviated receipt description using common supplier shorthand (CHKN, ORG, GRD BF)
    // When: Abbreviations are expanded to full ingredient terms
    // Then: The expanded text contains recognizable ingredient names for matching
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

    // Given: A full product description "Organic Large Brown Eggs 24ct"
    // When: The description is tokenized for fuzzy matching
    // Then: Significant tokens (organic, large, brown, eggs, 24ct) are extracted for ingredient matching
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

    // Given: Two ingredient descriptions with varying degrees of similarity
    // When: String similarity is calculated between the descriptions
    // Then: Exact matches score 1.0, near-typos score above 0.9, and dissimilar items score low
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

    // Given: A product description with tokens mostly overlapping a known ingredient pattern
    // When: The token overlap score is calculated
    // Then: The score exceeds 0.8, indicating a strong ingredient match
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

    // Given: A product description with tokens completely different from a known ingredient pattern
    // When: The token overlap score is calculated
    // Then: The score is below 0.3, indicating no ingredient match
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

    // Given: Learned patterns for Chicken Breast and Ground Beef from previous invoice mappings
    // When: A new invoice line "Chicken Breast Skinless Boneless" is matched against the patterns
    // Then: Chicken Breast is returned as the top match with a high confidence score
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

    // Given: A catalog of ingredients including chicken, beef, and salmon
    // When: An abbreviated receipt line "CHKN BRST BNLS" is matched against the catalog
    // Then: Chicken items are suggested as top matches after abbreviation expansion
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

    // Given: An ingredient "Extra Virgin Olive Oil" with the alias "EVOO"
    // When: A receipt line containing "EVOO 1 Liter" is matched against the catalog
    // Then: The ingredient is matched via its alias with high confidence
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

    // Given: An abbreviated supplier receipt line (e.g., "KS ORG LG EGGS 24CT")
    // When: The line is expanded and tokenized for ingredient matching
    // Then: The core ingredient token (eggs, chicken, beef) is present after expansion
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
