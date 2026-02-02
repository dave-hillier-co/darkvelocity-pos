using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for fuzzy matching vendor item descriptions to internal ingredients.
/// </summary>
public interface IFuzzyMatchingService
{
    /// <summary>
    /// Normalize a description for matching (lowercase, remove punctuation, etc.).
    /// </summary>
    string Normalize(string description);

    /// <summary>
    /// Extract significant tokens from a description.
    /// </summary>
    IReadOnlyList<string> Tokenize(string description);

    /// <summary>
    /// Calculate similarity between two strings (0.0 to 1.0).
    /// </summary>
    decimal CalculateSimilarity(string a, string b);

    /// <summary>
    /// Calculate token overlap score between description and pattern.
    /// </summary>
    decimal CalculateTokenScore(IReadOnlyList<string> descriptionTokens, IReadOnlyList<string> patternTokens);

    /// <summary>
    /// Find best matches from learned patterns.
    /// </summary>
    IReadOnlyList<PatternMatch> FindPatternMatches(
        string description,
        IEnumerable<LearnedPattern> patterns,
        decimal minConfidence = 0.5m,
        int maxResults = 5);

    /// <summary>
    /// Find best matches from candidate ingredients using fuzzy matching.
    /// </summary>
    IReadOnlyList<MappingSuggestion> FindIngredientMatches(
        string vendorDescription,
        IEnumerable<IngredientInfo> candidates,
        decimal minConfidence = 0.3m,
        int maxResults = 5);

    /// <summary>
    /// Expand common abbreviations in receipt text.
    /// </summary>
    string ExpandAbbreviations(string description);
}

/// <summary>
/// Result of pattern matching.
/// </summary>
public record PatternMatch(
    LearnedPattern Pattern,
    decimal Score,
    string MatchReason);

/// <summary>
/// Default implementation of fuzzy matching.
/// </summary>
public class FuzzyMatchingService : IFuzzyMatchingService
{
    // Common abbreviations found on receipts
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Proteins
        ["CHKN"] = "CHICKEN",
        ["BF"] = "BEEF",
        ["GRD"] = "GROUND",
        ["GRND"] = "GROUND",
        ["BRST"] = "BREAST",
        ["THGH"] = "THIGH",
        ["WNG"] = "WING",
        ["WNGS"] = "WINGS",
        ["SALMN"] = "SALMON",
        ["SHRMP"] = "SHRIMP",
        ["PRK"] = "PORK",
        ["SAUS"] = "SAUSAGE",
        ["BCN"] = "BACON",

        // Dairy
        ["MLK"] = "MILK",
        ["BTTR"] = "BUTTER",
        ["CHSE"] = "CHEESE",
        ["CHS"] = "CHEESE",
        ["YOG"] = "YOGURT",
        ["YGRT"] = "YOGURT",
        ["CRM"] = "CREAM",

        // Produce
        ["ORG"] = "ORGANIC",
        ["ORGNC"] = "ORGANIC",
        ["VEG"] = "VEGETABLE",
        ["FRT"] = "FRUIT",
        ["LG"] = "LARGE",
        ["SM"] = "SMALL",
        ["MED"] = "MEDIUM",
        ["GRN"] = "GREEN",
        ["RED"] = "RED",
        ["YEL"] = "YELLOW",
        ["WHT"] = "WHITE",
        ["BRN"] = "BROWN",
        ["PTTO"] = "POTATO",
        ["TOMS"] = "TOMATOES",
        ["TOM"] = "TOMATO",
        ["LET"] = "LETTUCE",
        ["LETC"] = "LETTUCE",
        ["CUC"] = "CUCUMBER",
        ["ONIN"] = "ONION",
        ["ONJN"] = "ONION",
        ["GRLC"] = "GARLIC",
        ["PEPS"] = "PEPPERS",
        ["PEP"] = "PEPPER",
        ["MUSHRM"] = "MUSHROOM",
        ["MUSH"] = "MUSHROOM",
        ["BRCLI"] = "BROCCOLI",
        ["BROC"] = "BROCCOLI",
        ["CRRT"] = "CARROT",
        ["CARR"] = "CARROT",
        ["APPL"] = "APPLE",
        ["BAN"] = "BANANA",
        ["STRW"] = "STRAWBERRY",
        ["STRWB"] = "STRAWBERRY",
        ["BLUB"] = "BLUEBERRY",
        ["RASP"] = "RASPBERRY",

        // Bakery/Grains
        ["BRD"] = "BREAD",
        ["WHL"] = "WHOLE",
        ["WHT"] = "WHEAT",
        ["FLR"] = "FLOUR",
        ["FLOR"] = "FLOUR",
        ["SGR"] = "SUGAR",
        ["SUGR"] = "SUGAR",
        ["RST"] = "ROAST",

        // Other common
        ["OL"] = "OIL",
        ["OLVE"] = "OLIVE",
        ["EVOO"] = "EXTRA VIRGIN OLIVE OIL",
        ["SLT"] = "SALT",
        ["SPR"] = "SPARKLING",
        ["WTR"] = "WATER",
        ["JCE"] = "JUICE",
        ["FF"] = "FAT FREE",
        ["RF"] = "REDUCED FAT",
        ["LF"] = "LOW FAT",
        ["NS"] = "NO SALT",
        ["SWT"] = "SWEET",
        ["FRZ"] = "FROZEN",
        ["FRZN"] = "FROZEN",
        ["FRH"] = "FRESH",
        ["PKG"] = "PACKAGE",
        ["PK"] = "PACK",
        ["CT"] = "COUNT",
        ["OZ"] = "OUNCE",
        ["LB"] = "POUND",
        ["LBS"] = "POUNDS",
        ["GAL"] = "GALLON",
        ["QT"] = "QUART",
        ["PT"] = "PINT",
        ["EA"] = "EACH",
        ["PPR"] = "PAPER",
        ["PPRTWL"] = "PAPER TOWEL",
        ["PAPR"] = "PAPER",
        ["PRCHMNT"] = "PARCHMENT",
        ["KS"] = "KIRKLAND SIGNATURE",
    };

    // Stop words to filter out during tokenization
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "of", "for", "with", "in", "on", "at", "to",
        "per", "each", "pack", "pkg", "ct", "oz", "lb", "lbs", "gal", "qt", "pt",
        "approx", "about", "item", "product", "brand"
    };

    public string Normalize(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Expand abbreviations first
        var expanded = ExpandAbbreviations(description);

        // Convert to lowercase
        var normalized = expanded.ToLowerInvariant();

        // Remove special characters except spaces and hyphens
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized, @"[^\w\s-]", " ");

        // Collapse multiple spaces
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized, @"\s+", " ");

        return normalized.Trim();
    }

    public IReadOnlyList<string> Tokenize(string description)
    {
        var normalized = Normalize(description);
        if (string.IsNullOrEmpty(normalized))
            return [];

        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1) // Skip single characters
            .Where(t => !StopWords.Contains(t))
            .Where(t => !decimal.TryParse(t, out _)) // Skip pure numbers
            .Distinct()
            .ToList();

        return tokens;
    }

    public string ExpandAbbreviations(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expanded = words.Select(word =>
        {
            // Try to expand the word
            if (Abbreviations.TryGetValue(word.TrimEnd(',', '.', ';'), out var expansion))
                return expansion;
            return word;
        });

        return string.Join(" ", expanded);
    }

    public decimal CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0m;

        var normA = Normalize(a);
        var normB = Normalize(b);

        if (normA == normB)
            return 1m;

        // Calculate Levenshtein distance
        var distance = LevenshteinDistance(normA, normB);
        var maxLen = Math.Max(normA.Length, normB.Length);

        if (maxLen == 0)
            return 0m;

        // Convert distance to similarity (0 to 1)
        return Math.Max(0m, 1m - (decimal)distance / maxLen);
    }

    public decimal CalculateTokenScore(IReadOnlyList<string> descriptionTokens, IReadOnlyList<string> patternTokens)
    {
        if (descriptionTokens.Count == 0 || patternTokens.Count == 0)
            return 0m;

        var descSet = new HashSet<string>(descriptionTokens, StringComparer.OrdinalIgnoreCase);
        var patternSet = new HashSet<string>(patternTokens, StringComparer.OrdinalIgnoreCase);

        // Count exact matches
        var exactMatches = descSet.Count(d => patternSet.Contains(d));

        // Count fuzzy matches (similar tokens)
        var fuzzyMatches = 0;
        foreach (var descToken in descSet)
        {
            if (patternSet.Contains(descToken))
                continue;

            foreach (var patternToken in patternSet)
            {
                var similarity = CalculateSimilarity(descToken, patternToken);
                if (similarity >= 0.8m) // High threshold for token similarity
                {
                    fuzzyMatches++;
                    break;
                }
            }
        }

        // Score based on pattern coverage and description coverage
        var patternCoverage = (decimal)(exactMatches + fuzzyMatches * 0.8m) / patternTokens.Count;
        var descCoverage = (decimal)(exactMatches + fuzzyMatches * 0.8m) / descriptionTokens.Count;

        // Weighted average favoring pattern coverage
        return patternCoverage * 0.6m + descCoverage * 0.4m;
    }

    public IReadOnlyList<PatternMatch> FindPatternMatches(
        string description,
        IEnumerable<LearnedPattern> patterns,
        decimal minConfidence = 0.5m,
        int maxResults = 5)
    {
        var descTokens = Tokenize(description);
        if (descTokens.Count == 0)
            return [];

        var matches = new List<PatternMatch>();

        foreach (var pattern in patterns)
        {
            var tokenScore = CalculateTokenScore(descTokens, pattern.Tokens);

            // Weight by pattern's learning weight
            var weightedScore = tokenScore * (1 + Math.Min(pattern.Weight - 1, 4) * 0.05m);
            weightedScore = Math.Min(1m, weightedScore);

            if (weightedScore >= minConfidence)
            {
                var matchReason = BuildMatchReason(descTokens, pattern.Tokens);
                matches.Add(new PatternMatch(pattern, weightedScore, matchReason));
            }
        }

        return matches
            .OrderByDescending(m => m.Score)
            .ThenByDescending(m => m.Pattern.Weight)
            .Take(maxResults)
            .ToList();
    }

    public IReadOnlyList<MappingSuggestion> FindIngredientMatches(
        string vendorDescription,
        IEnumerable<IngredientInfo> candidates,
        decimal minConfidence = 0.3m,
        int maxResults = 5)
    {
        var descTokens = Tokenize(vendorDescription);
        if (descTokens.Count == 0)
            return [];

        var suggestions = new List<MappingSuggestion>();

        foreach (var ingredient in candidates)
        {
            // Check name similarity
            var nameTokens = Tokenize(ingredient.Name);
            var nameScore = CalculateTokenScore(descTokens, nameTokens);

            // Check SKU similarity (often contains abbreviated info)
            var skuScore = CalculateSimilarity(vendorDescription, ingredient.Sku) * 0.8m;

            // Check aliases if available
            decimal aliasScore = 0m;
            if (ingredient.Aliases != null)
            {
                foreach (var alias in ingredient.Aliases)
                {
                    var aliasTokens = Tokenize(alias);
                    var score = CalculateTokenScore(descTokens, aliasTokens);
                    aliasScore = Math.Max(aliasScore, score);
                }
            }

            var bestScore = Math.Max(nameScore, Math.Max(skuScore, aliasScore));
            if (bestScore >= minConfidence)
            {
                var matchReason = nameScore >= skuScore && nameScore >= aliasScore
                    ? $"Name match: {nameScore:P0}"
                    : aliasScore > skuScore
                        ? $"Alias match: {aliasScore:P0}"
                        : $"SKU match: {skuScore:P0}";

                suggestions.Add(new MappingSuggestion(
                    ingredient.Id,
                    ingredient.Name,
                    ingredient.Sku,
                    bestScore,
                    matchReason,
                    MappingMatchType.FuzzyPattern));
            }
        }

        return suggestions
            .OrderByDescending(s => s.Confidence)
            .Take(maxResults)
            .ToList();
    }

    private static string BuildMatchReason(IReadOnlyList<string> descTokens, IReadOnlyList<string> patternTokens)
    {
        var descSet = new HashSet<string>(descTokens, StringComparer.OrdinalIgnoreCase);
        var matched = patternTokens.Where(t => descSet.Contains(t)).ToList();

        if (matched.Count == 0)
            return "Fuzzy token match";

        return $"Matched: {string.Join(", ", matched)}";
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b))
            return a.Length;

        var lenA = a.Length;
        var lenB = b.Length;
        var d = new int[lenA + 1, lenB + 1];

        for (var i = 0; i <= lenA; i++)
            d[i, 0] = i;
        for (var j = 0; j <= lenB; j++)
            d[0, j] = j;

        for (var i = 1; i <= lenA; i++)
        {
            for (var j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[lenA, lenB];
    }
}
