namespace DarkVelocity.Orleans.Abstractions;

/// <summary>
/// Helper methods for consistent grain key generation.
/// All grain keys follow the pattern: "orgId:entityId" or "orgId:siteId:entityId"
/// </summary>
public static class GrainKeys
{
    /// <summary>
    /// Creates a key for an organization-level entity.
    /// </summary>
    public static string OrgEntity(Guid orgId, string entityType, Guid entityId)
        => $"{orgId}:{entityType}:{entityId}";

    /// <summary>
    /// Creates a key for a site-level entity.
    /// </summary>
    public static string SiteEntity(Guid orgId, Guid siteId, string entityType, Guid entityId)
        => $"{orgId}:{siteId}:{entityType}:{entityId}";

    /// <summary>
    /// Creates a key for an organization grain.
    /// </summary>
    public static string Organization(Guid orgId) => orgId.ToString();

    /// <summary>
    /// Creates a key for a site grain.
    /// </summary>
    public static string Site(Guid orgId, Guid siteId) => $"{orgId}:{siteId}";

    /// <summary>
    /// Creates a key for a user grain.
    /// </summary>
    public static string User(Guid orgId, Guid userId) => OrgEntity(orgId, "user", userId);

    /// <summary>
    /// Creates a key for a user group grain.
    /// </summary>
    public static string UserGroup(Guid orgId, Guid groupId) => OrgEntity(orgId, "usergroup", groupId);

    /// <summary>
    /// Creates a key for an order grain.
    /// </summary>
    public static string Order(Guid orgId, Guid siteId, Guid orderId) => SiteEntity(orgId, siteId, "order", orderId);

    /// <summary>
    /// Creates a key for a payment grain.
    /// </summary>
    public static string Payment(Guid orgId, Guid siteId, Guid paymentId) => SiteEntity(orgId, siteId, "payment", paymentId);

    /// <summary>
    /// Creates a key for a cash drawer grain.
    /// </summary>
    public static string CashDrawer(Guid orgId, Guid siteId, Guid drawerId) => SiteEntity(orgId, siteId, "drawer", drawerId);

    /// <summary>
    /// Creates a key for an inventory grain (per ingredient per site).
    /// </summary>
    public static string Inventory(Guid orgId, Guid siteId, Guid ingredientId) => SiteEntity(orgId, siteId, "inventory", ingredientId);

    /// <summary>
    /// Creates a key for a customer grain.
    /// </summary>
    public static string Customer(Guid orgId, Guid customerId) => OrgEntity(orgId, "customer", customerId);

    /// <summary>
    /// Creates a key for a loyalty program grain.
    /// </summary>
    public static string LoyaltyProgram(Guid orgId, Guid programId) => OrgEntity(orgId, "loyalty", programId);

    /// <summary>
    /// Creates a key for a booking grain.
    /// </summary>
    public static string Booking(Guid orgId, Guid siteId, Guid bookingId) => SiteEntity(orgId, siteId, "booking", bookingId);

    /// <summary>
    /// Creates a key for a waitlist grain (one per site per day).
    /// </summary>
    public static string Waitlist(Guid orgId, Guid siteId, DateOnly date) => $"{orgId}:{siteId}:waitlist:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a kitchen order (KOT) grain.
    /// </summary>
    public static string KitchenOrder(Guid orgId, Guid siteId, Guid ticketId) => SiteEntity(orgId, siteId, "kot", ticketId);

    /// <summary>
    /// Creates a key for a kitchen station grain.
    /// </summary>
    public static string KitchenStation(Guid orgId, Guid siteId, Guid stationId) => SiteEntity(orgId, siteId, "station", stationId);

    /// <summary>
    /// Creates a key for a gift card grain.
    /// </summary>
    public static string GiftCard(Guid orgId, Guid cardId) => OrgEntity(orgId, "giftcard", cardId);

    /// <summary>
    /// Creates a key for an account grain.
    /// </summary>
    public static string Account(Guid orgId, Guid accountId) => OrgEntity(orgId, "account", accountId);

    /// <summary>
    /// Creates a key for a menu grain.
    /// </summary>
    public static string Menu(Guid orgId, Guid menuId) => OrgEntity(orgId, "menu", menuId);

    /// <summary>
    /// Creates a key for a batch (settlement) grain.
    /// </summary>
    public static string Batch(Guid orgId, Guid siteId, Guid batchId) => SiteEntity(orgId, siteId, "batch", batchId);

    /// <summary>
    /// Creates a key for a booking calendar grain (one per site per day).
    /// </summary>
    public static string BookingCalendar(Guid orgId, Guid siteId, DateOnly date) => $"{orgId}:{siteId}:calendar:{date:yyyy-MM-dd}";

    /// <summary>
    /// Parses an organization-level key.
    /// </summary>
    public static (Guid OrgId, string EntityType, Guid EntityId) ParseOrgEntity(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 3)
            throw new ArgumentException($"Invalid org entity key format: {key}");
        return (Guid.Parse(parts[0]), parts[1], Guid.Parse(parts[2]));
    }

    /// <summary>
    /// Parses a site-level key.
    /// </summary>
    public static (Guid OrgId, Guid SiteId, string EntityType, Guid EntityId) ParseSiteEntity(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 4)
            throw new ArgumentException($"Invalid site entity key format: {key}");
        return (Guid.Parse(parts[0]), Guid.Parse(parts[1]), parts[2], Guid.Parse(parts[3]));
    }

    /// <summary>
    /// Extracts the organization ID from any grain key.
    /// </summary>
    public static Guid ExtractOrgId(string key)
    {
        var firstColonIndex = key.IndexOf(':');
        var orgPart = firstColonIndex > 0 ? key[..firstColonIndex] : key;
        return Guid.Parse(orgPart);
    }
}
