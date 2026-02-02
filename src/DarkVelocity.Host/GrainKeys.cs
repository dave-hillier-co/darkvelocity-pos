namespace DarkVelocity.Host;

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
    /// Creates a key for an employee grain.
    /// </summary>
    public static string Employee(Guid orgId, Guid employeeId) => OrgEntity(orgId, "employee", employeeId);

    /// <summary>
    /// Creates a key for an employee grain by user ID (for lookup).
    /// </summary>
    public static string EmployeeByUser(Guid orgId, Guid userId) => $"{orgId}:employeebyuser:{userId}";

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
    /// Creates a key for a daily sales grain (one per site per day).
    /// </summary>
    public static string DailySales(Guid orgId, Guid siteId, DateOnly date) => $"{orgId}:{siteId}:sales:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a customer spend projection grain (loyalty derived from spend).
    /// </summary>
    public static string CustomerSpendProjection(Guid orgId, Guid customerId) => OrgEntity(orgId, "customerspend", customerId);

    /// <summary>
    /// Creates a key for a recipe grain.
    /// </summary>
    public static string Recipe(Guid orgId, Guid recipeId) => OrgEntity(orgId, "recipe", recipeId);

    /// <summary>
    /// Creates a key for an ingredient price grain.
    /// </summary>
    public static string IngredientPrice(Guid orgId, Guid ingredientId) => OrgEntity(orgId, "ingredientprice", ingredientId);

    /// <summary>
    /// Creates a key for a cost alert grain.
    /// </summary>
    public static string CostAlert(Guid orgId, Guid alertId) => OrgEntity(orgId, "costalert", alertId);

    /// <summary>
    /// Creates a key for a costing settings grain (per location).
    /// </summary>
    public static string CostingSettings(Guid orgId, Guid locationId) => $"{orgId}:{locationId}:costingsettings";

    /// <summary>
    /// Creates a key for a table grain.
    /// </summary>
    public static string Table(Guid orgId, Guid siteId, Guid tableId) => SiteEntity(orgId, siteId, "table", tableId);

    /// <summary>
    /// Creates a key for a floor plan grain.
    /// </summary>
    public static string FloorPlan(Guid orgId, Guid siteId, Guid floorPlanId) => SiteEntity(orgId, siteId, "floorplan", floorPlanId);

    /// <summary>
    /// Creates a key for booking settings grain (per site).
    /// </summary>
    public static string BookingSettings(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:bookingsettings";

    /// <summary>
    /// Creates a key for a shift swap request grain.
    /// </summary>
    public static string ShiftSwapRequest(Guid orgId, Guid requestId) => OrgEntity(orgId, "shiftswap", requestId);

    /// <summary>
    /// Creates a key for a time off request grain.
    /// </summary>
    public static string TimeOffRequest(Guid orgId, Guid requestId) => OrgEntity(orgId, "timeoff", requestId);

    /// <summary>
    /// Creates a key for employee availability grain.
    /// </summary>
    public static string EmployeeAvailability(Guid orgId, Guid employeeId) => OrgEntity(orgId, "availability", employeeId);

    /// <summary>
    /// Creates a key for a merchant grain (payment gateway).
    /// </summary>
    public static string Merchant(Guid orgId, Guid merchantId) => OrgEntity(orgId, "merchant", merchantId);

    /// <summary>
    /// Creates a key for a terminal grain (payment gateway).
    /// </summary>
    public static string Terminal(Guid orgId, Guid terminalId) => OrgEntity(orgId, "terminal", terminalId);

    /// <summary>
    /// Creates a key for a refund grain (payment gateway).
    /// </summary>
    public static string Refund(Guid orgId, Guid refundId) => OrgEntity(orgId, "refund", refundId);

    /// <summary>
    /// Creates a key for a webhook grain (payment gateway).
    /// </summary>
    public static string Webhook(Guid orgId, Guid webhookId) => OrgEntity(orgId, "webhook", webhookId);

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

    /// <summary>
    /// Creates a key for a daily inventory snapshot grain.
    /// </summary>
    public static string DailyInventorySnapshot(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:inventory-snapshot:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a daily consumption grain.
    /// </summary>
    public static string DailyConsumption(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a daily waste grain.
    /// </summary>
    public static string DailyWaste(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:waste:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a period aggregation grain.
    /// </summary>
    public static string PeriodAggregation(Guid orgId, Guid siteId, Grains.PeriodType periodType, int year, int periodNumber)
        => $"{orgId}:{siteId}:period:{periodType}:{year}:{periodNumber}";

    /// <summary>
    /// Creates a key for a site dashboard grain.
    /// </summary>
    public static string SiteDashboard(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:dashboard";

    /// <summary>
    /// Creates a key for an alert grain.
    /// </summary>
    public static string Alerts(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:alerts";

    /// <summary>
    /// Creates a key for a notification grain.
    /// </summary>
    public static string Notifications(Guid orgId)
        => $"{orgId}:notifications";

    /// <summary>
    /// Creates a key for a menu engineering grain.
    /// </summary>
    public static string MenuEngineering(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:menu-engineering";

    /// <summary>
    /// Creates a key for a purchase order grain.
    /// </summary>
    public static string PurchaseOrder(Guid orgId, Guid orderId) => OrgEntity(orgId, "purchaseorder", orderId);

    /// <summary>
    /// Creates a key for a supplier grain.
    /// </summary>
    public static string Supplier(Guid orgId, Guid supplierId) => OrgEntity(orgId, "supplier", supplierId);

    /// <summary>
    /// Creates a key for an inventory item grain (deprecated - use Inventory instead).
    /// </summary>
    public static string InventoryItem(Guid orgId, Guid itemId) => OrgEntity(orgId, "inventoryitem", itemId);

    /// <summary>
    /// Creates a key for an inventory count grain.
    /// </summary>
    public static string InventoryCount(Guid orgId, Guid countId) => OrgEntity(orgId, "inventorycount", countId);

    /// <summary>
    /// Creates a key for a device grain.
    /// </summary>
    public static string Device(Guid orgId, Guid deviceId) => OrgEntity(orgId, "device", deviceId);

    /// <summary>
    /// Creates a key for a daily sales report grain.
    /// </summary>
    public static string DailySalesReport(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:salesreport:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a shift grain.
    /// </summary>
    public static string Shift(Guid orgId, Guid shiftId) => OrgEntity(orgId, "shift", shiftId);

    /// <summary>
    /// Creates a key for a menu category grain.
    /// </summary>
    public static string MenuCategory(Guid orgId, Guid categoryId) => OrgEntity(orgId, "menucategory", categoryId);

    /// <summary>
    /// Creates a key for a menu item grain.
    /// </summary>
    public static string MenuItem(Guid orgId, Guid itemId) => OrgEntity(orgId, "menuitem", itemId);

    /// <summary>
    /// Creates a key for a device authorization flow grain.
    /// Key is the user code displayed to the user (e.g., "ABCD1234").
    /// </summary>
    public static string DeviceAuth(string userCode) => $"deviceauth:{userCode.ToUpperInvariant()}";

    /// <summary>
    /// Creates a key for a user session grain.
    /// </summary>
    public static string Session(Guid orgId, Guid sessionId) => OrgEntity(orgId, "session", sessionId);

    /// <summary>
    /// Creates a key for user lookup grain (PIN to user mapping).
    /// </summary>
    public static string UserLookup(Guid orgId) => $"{orgId}:userlookup";

    /// <summary>
    /// Creates a key for a channel grain.
    /// </summary>
    public static string Channel(Guid orgId, Guid channelId) => OrgEntity(orgId, "channel", channelId);

    /// <summary>
    /// Creates a key for a status mapping grain (per platform type).
    /// </summary>
    public static string StatusMapping(Guid orgId, Grains.DeliveryPlatformType platformType)
        => $"{orgId}:statusmapping:{platformType}";

    /// <summary>
    /// Creates a key for the channel registry grain (one per org).
    /// </summary>
    public static string ChannelRegistry(Guid orgId) => $"{orgId}:channelregistry";

    /// <summary>
    /// Generates a random user code for device authorization (8 alphanumeric chars).
    /// </summary>
    public static string GenerateUserCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluded I, O, 0, 1 for readability
        var random = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[8];
        random.GetBytes(bytes);
        var result = new char[8];
        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }
}
