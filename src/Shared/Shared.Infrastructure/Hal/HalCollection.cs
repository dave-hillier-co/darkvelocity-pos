using System.Text.Json.Serialization;

namespace DarkVelocity.Shared.Infrastructure.Hal;

public class HalCollection<T> : HalResource where T : HalResource
{
    [JsonPropertyName("_embedded")]
    public new HalCollectionEmbedded<T> Embedded { get; init; } = new();

    [JsonPropertyName("count")]
    public int Count => Embedded.Items.Count;

    [JsonPropertyName("total")]
    public int? Total { get; init; }

    public static HalCollection<T> Create(IEnumerable<T> items, string selfHref, int? total = null)
    {
        var collection = new HalCollection<T>
        {
            Embedded = new HalCollectionEmbedded<T> { Items = items.ToList() },
            Total = total
        };
        collection.AddSelfLink(selfHref);
        return collection;
    }
}

public class HalCollectionEmbedded<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; init; } = new();
}
