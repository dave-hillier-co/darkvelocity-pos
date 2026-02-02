namespace DarkVelocity.Host.Extensions;

/// <summary>
/// Extension methods for working with tag collections.
/// </summary>
public static class TagExtensions
{
    /// <summary>
    /// Attempts to add a tag to the collection if it doesn't already exist.
    /// </summary>
    /// <param name="tags">The tag collection.</param>
    /// <param name="tag">The tag to add.</param>
    /// <returns>True if the tag was added; false if it already existed.</returns>
    public static bool TryAddTag(this List<string> tags, string tag)
    {
        if (tags.Contains(tag))
            return false;

        tags.Add(tag);
        return true;
    }

    /// <summary>
    /// Checks if the collection contains the specified tag.
    /// </summary>
    /// <param name="tags">The tag collection.</param>
    /// <param name="tag">The tag to check for.</param>
    /// <returns>True if the tag exists; false otherwise.</returns>
    public static bool HasTag(this List<string> tags, string tag) => tags.Contains(tag);
}
