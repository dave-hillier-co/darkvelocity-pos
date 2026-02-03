namespace DarkVelocity.Host.Endpoints;

/// <summary>
/// HAL+JSON helper for building hypermedia responses.
/// </summary>
public static class Hal
{
    public static object Resource(object data, Dictionary<string, object> links)
    {
        var result = new Dictionary<string, object> { ["_links"] = links };
        foreach (var prop in data.GetType().GetProperties())
        {
            var name = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            result[name] = prop.GetValue(data)!;
        }
        return result;
    }

    public static object Collection(string selfHref, IEnumerable<object> items, int count)
    {
        return new Dictionary<string, object>
        {
            ["_links"] = new { self = new { href = selfHref } },
            ["_embedded"] = new { items },
            ["count"] = count
        };
    }

    public static object Collection(
        IEnumerable<object> items,
        Dictionary<string, object> links,
        Dictionary<string, object> metadata)
    {
        var result = new Dictionary<string, object>
        {
            ["_links"] = links,
            ["_embedded"] = new { items }
        };

        foreach (var kvp in metadata)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    public static object Error(string code, string message) =>
        new { error = code, error_description = message };
}
