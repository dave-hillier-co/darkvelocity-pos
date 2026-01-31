namespace DarkVelocity.Shared.Infrastructure.Hal;

public sealed record HalLink(
    string Href,
    string? Title = null,
    string? Type = null,
    bool? Templated = null
);
