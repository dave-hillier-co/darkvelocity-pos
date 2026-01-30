using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.GiftCards.Api.Entities;

/// <summary>
/// Represents a visual design template for gift cards
/// </summary>
public class GiftCardDesign : BaseEntity
{
    /// <summary>
    /// Program this design belongs to
    /// </summary>
    public Guid ProgramId { get; set; }

    /// <summary>
    /// Display name for the design (e.g., "Birthday", "Thank You", "Holiday")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the design
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL to the card image/artwork
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL to a thumbnail version of the image
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Whether this is the default design for the program
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Whether this design is currently available for use
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sort order for display (lower numbers appear first)
    /// </summary>
    public int SortOrder { get; set; }

    // Navigation properties
    public GiftCardProgram? Program { get; set; }
    public ICollection<GiftCard> GiftCards { get; set; } = new List<GiftCard>();
}
