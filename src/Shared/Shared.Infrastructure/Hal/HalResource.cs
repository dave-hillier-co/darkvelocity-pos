using System.Text.Json.Serialization;

namespace DarkVelocity.Shared.Infrastructure.Hal;

public abstract class HalResource
{
    [JsonPropertyName("_links")]
    public Dictionary<string, HalLink> Links { get; init; } = new();

    [JsonPropertyName("_embedded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Embedded { get; init; }

    public void AddLink(string rel, string href, string? title = null)
    {
        Links[rel] = new HalLink(href, title);
    }

    public void AddSelfLink(string href)
    {
        AddLink("self", href);
    }
}
