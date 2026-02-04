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
    /// Creates a key for a ledger grain.
    /// Format: org:{orgId}:ledger:{ownerType}:{ownerId}
    /// </summary>
    public static string Ledger(Guid orgId, string ownerType, string ownerId)
        => $"org:{orgId}:ledger:{ownerType}:{ownerId}";

    /// <summary>
    /// Creates a key for a ledger grain with a Guid owner ID.
    /// </summary>
    public static string Ledger(Guid orgId, string ownerType, Guid ownerId)
        => Ledger(orgId, ownerType, ownerId.ToString());

    /// <summary>
    /// Creates a key for an inventory ledger (site-scoped).
    /// Format: org:{orgId}:ledger:inventory:{siteId}:{ingredientId}
    /// </summary>
    public static string InventoryLedger(Guid orgId, Guid siteId, Guid ingredientId)
        => $"org:{orgId}:ledger:inventory:{siteId}:{ingredientId}";

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
    /// Creates a key for a role grain.
    /// </summary>
    public static string Role(Guid orgId, Guid roleId) => OrgEntity(orgId, "role", roleId);

    /// <summary>
    /// Creates a key for a schedule grain (weekly rota).
    /// </summary>
    public static string Schedule(Guid orgId, Guid siteId, DateOnly weekStartDate)
        => $"{orgId}:{siteId}:schedule:{weekStartDate:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a time entry grain.
    /// </summary>
    public static string TimeEntry(Guid orgId, Guid timeEntryId) => OrgEntity(orgId, "timeentry", timeEntryId);

    /// <summary>
    /// Creates a key for a tip pool grain.
    /// </summary>
    public static string TipPool(Guid orgId, Guid siteId, DateOnly date, string poolName)
        => $"{orgId}:{siteId}:tippool:{date:yyyy-MM-dd}:{poolName}";

    /// <summary>
    /// Creates a key for a payroll period grain.
    /// </summary>
    public static string PayrollPeriod(Guid orgId, Guid siteId, DateOnly periodStart)
        => $"{orgId}:{siteId}:payroll:{periodStart:yyyy-MM-dd}";

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
    /// Creates a key for an OAuth state grain (CSRF protection).
    /// Key is the state parameter.
    /// </summary>
    public static string OAuthState(string state) => $"oauthstate:{state}";

    /// <summary>
    /// Creates a key for an external identity grain.
    /// Maps external OAuth identities to internal users.
    /// </summary>
    public static string ExternalIdentity(string provider, string externalId)
        => $"extid:{provider.ToLowerInvariant()}:{externalId}";

    /// <summary>
    /// Creates a key for an authorization code grain.
    /// </summary>
    public static string AuthorizationCode(string code) => $"authcode:{code}";

    /// <summary>
    /// Generates a secure random state parameter for OAuth flows.
    /// </summary>
    public static string GenerateOAuthState()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a secure random authorization code.
    /// </summary>
    public static string GenerateAuthorizationCode()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Creates a key for a user session grain.
    /// </summary>
    public static string Session(Guid orgId, Guid sessionId) => OrgEntity(orgId, "session", sessionId);

    /// <summary>
    /// Creates a key for user lookup grain (PIN to user mapping).
    /// </summary>
    public static string UserLookup(Guid orgId) => $"{orgId}:userlookup";

    /// <summary>
    /// Creates a key for OAuth lookup grain (external OAuth identity to user mapping).
    /// One per organization.
    /// </summary>
    public static string OAuthLookup(Guid orgId) => $"{orgId}:oauthlookup";

    /// <summary>
    /// Creates a key for global email lookup grain (email to organization/user mapping).
    /// Single global instance for cross-org email lookup.
    /// </summary>
    public static string EmailLookup() => "global:emaillookup";

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

    // ============================================================================
    // CMS Menu Grains
    // ============================================================================

    /// <summary>
    /// Creates a key for a menu item document grain (CMS versioned).
    /// </summary>
    public static string MenuItemDocument(Guid orgId, string documentId) => $"{orgId}:menuitemdoc:{documentId}";

    /// <summary>
    /// Creates a key for a menu category document grain (CMS versioned).
    /// </summary>
    public static string MenuCategoryDocument(Guid orgId, string documentId) => $"{orgId}:menucategorydoc:{documentId}";

    /// <summary>
    /// Creates a key for a modifier block grain.
    /// </summary>
    public static string ModifierBlock(Guid orgId, string blockId) => $"{orgId}:modifierblock:{blockId}";

    /// <summary>
    /// Creates a key for a content tag grain.
    /// </summary>
    public static string ContentTag(Guid orgId, string tagId) => $"{orgId}:contenttag:{tagId}";

    /// <summary>
    /// Creates a key for site menu overrides grain.
    /// </summary>
    public static string SiteMenuOverrides(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:menuoverrides";

    /// <summary>
    /// Creates a key for the menu content resolver grain.
    /// </summary>
    public static string MenuContentResolver(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:menuresolver";

    /// <summary>
    /// Creates a key for the menu registry grain (one per org).
    /// </summary>
    public static string MenuRegistry(Guid orgId) => $"{orgId}:menuregistry";

    // ============================================================================
    // CMS Recipe Grains
    // ============================================================================

    /// <summary>
    /// Creates a key for a recipe document grain (CMS versioned).
    /// </summary>
    public static string RecipeDocument(Guid orgId, string documentId) => $"{orgId}:recipedoc:{documentId}";

    /// <summary>
    /// Creates a key for a recipe category document grain (CMS versioned).
    /// </summary>
    public static string RecipeCategoryDocument(Guid orgId, string documentId) => $"{orgId}:recipecategorydoc:{documentId}";

    /// <summary>
    /// Creates a key for the recipe registry grain (one per org).
    /// </summary>
    public static string RecipeRegistry(Guid orgId) => $"{orgId}:reciperegistry";

    /// <summary>
    /// Creates a key for a purchase document grain (invoice or receipt).
    /// </summary>
    public static string PurchaseDocument(Guid orgId, Guid siteId, Guid documentId)
        => SiteEntity(orgId, siteId, "purchase-doc", documentId);

    /// <summary>
    /// Creates a key for a line items grain.
    /// Used for composition with OrderGrain, PurchaseDocumentGrain, etc.
    /// </summary>
    public static string LineItems(Guid orgId, string ownerType, Guid ownerId)
        => $"{orgId}:lines:{ownerType}:{ownerId}";

    /// <summary>
    /// Creates a key for a vendor item mapping grain using vendor GUID.
    /// </summary>
    public static string VendorItemMapping(Guid orgId, Guid vendorId)
        => OrgEntity(orgId, "vendor-mapping", vendorId);

    /// <summary>
    /// Creates a key for a vendor item mapping grain using vendor name/ID string.
    /// Used for retail stores (e.g., "costco", "walmart").
    /// </summary>
    public static string VendorItemMapping(Guid orgId, string vendorId)
        => $"{orgId}:vendor-mapping:{vendorId.ToLowerInvariant()}";

    /// <summary>
    /// Creates a key for an expense grain.
    /// </summary>
    public static string Expense(Guid orgId, Guid siteId, Guid expenseId)
        => SiteEntity(orgId, siteId, "expense", expenseId);

    /// <summary>
    /// Creates a key for a workflow grain.
    /// Key format: org:{orgId}:workflow:{ownerType}:{ownerId}
    /// </summary>
    public static string Workflow(Guid orgId, string ownerType, Guid ownerId)
        => $"org:{orgId}:workflow:{ownerType}:{ownerId}";

    /// <summary>
    /// Parses a workflow grain key.
    /// </summary>
    public static (Guid OrgId, string OwnerType, Guid OwnerId) ParseWorkflow(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 5 || parts[0] != "org" || parts[2] != "workflow")
            throw new ArgumentException($"Invalid workflow key format: {key}");
        return (Guid.Parse(parts[1]), parts[3], Guid.Parse(parts[4]));
    }

    /// <summary>
    /// Creates a key for an email inbox grain (one per site).
    /// </summary>
    public static string EmailInbox(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:email-inbox";

    /// <summary>
    /// Creates a key for an expense index grain (one per site).
    /// </summary>
    public static string ExpenseIndex(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:expense-index";

    // ============================================================================
    // Generic Index Grains
    // ============================================================================

    /// <summary>
    /// Creates a key for an index grain.
    /// Format: org:{orgId}:index:{indexType}:{scope}
    /// </summary>
    /// <param name="orgId">The organization ID.</param>
    /// <param name="indexType">The type of index (e.g., "orders", "expenses", "bookings").</param>
    /// <param name="scope">The scope identifier (e.g., site ID, month "2024-01").</param>
    /// <example>
    /// GrainKeys.Index(orgId, "orders", siteId.ToString())  // org:abc:index:orders:site-123
    /// GrainKeys.Index(orgId, "expenses", "2024-01")        // org:abc:index:expenses:2024-01
    /// GrainKeys.Index(orgId, "bookings", siteId.ToString()) // org:abc:index:bookings:site-456
    /// </example>
    public static string Index(Guid orgId, string indexType, string scope)
        => $"org:{orgId}:index:{indexType}:{scope}";

    /// <summary>
    /// Creates a key for an index grain scoped to a site.
    /// Convenience overload for site-scoped indexes.
    /// </summary>
    public static string Index(Guid orgId, string indexType, Guid siteId)
        => Index(orgId, indexType, siteId.ToString());

    /// <summary>
    /// Creates a key for a time-scoped index grain (e.g., monthly).
    /// </summary>
    public static string Index(Guid orgId, string indexType, int year, int month)
        => Index(orgId, indexType, $"{year:D4}-{month:D2}");

    /// <summary>
    /// Creates a key for a date-scoped index grain (daily).
    /// </summary>
    public static string Index(Guid orgId, string indexType, DateOnly date)
        => Index(orgId, indexType, date.ToString("yyyy-MM-dd"));

    /// <summary>
    /// Parses an index grain key.
    /// </summary>
    /// <returns>Tuple of (OrgId, IndexType, Scope).</returns>
    /// <exception cref="ArgumentException">If the key format is invalid.</exception>
    public static (Guid OrgId, string IndexType, string Scope) ParseIndex(string key)
    {
        var parts = key.Split(':');
        if (parts.Length < 5 || parts[0] != "org" || parts[2] != "index")
            throw new ArgumentException($"Invalid index key format: {key}");

        return (Guid.Parse(parts[1]), parts[3], string.Join(":", parts[4..]));
    }

    // ============================================================================
    // Batch Grains (Singleton per scope - used for batch operations)
    // ============================================================================

    /// <summary>
    /// Creates a key for a menu batch grain (singleton per org).
    /// </summary>
    public static string MenuBatch(Guid orgId) => $"{orgId}:menubatch";

    /// <summary>
    /// Creates a key for an inventory batch grain (singleton per site).
    /// </summary>
    public static string InventoryBatch(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:inventorybatch";

    /// <summary>
    /// Creates a key for an order batch grain (singleton per site).
    /// </summary>
    public static string OrderBatch(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:orderbatch";

    /// <summary>
    /// Creates a key for a customer batch grain (singleton per org).
    /// </summary>
    public static string CustomerBatch(Guid orgId) => $"{orgId}:customerbatch";

    /// <summary>
    /// Creates a key for a payment batch grain (singleton per site).
    /// </summary>
    public static string PaymentBatch(Guid orgId, Guid siteId) => $"{orgId}:{siteId}:paymentbatch";

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

    // ============================================================================
    // API Key Grains
    // ============================================================================

    /// <summary>
    /// Creates a key for an API key grain.
    /// </summary>
    public static string ApiKey(Guid orgId, Guid keyId) => OrgEntity(orgId, "apikey", keyId);

    /// <summary>
    /// Creates a key for an API key registry grain (one per user).
    /// </summary>
    public static string ApiKeyRegistry(Guid orgId, Guid userId) => $"{orgId}:apikeyregistry:{userId}";

    /// <summary>
    /// Creates a key for the global API key lookup grain.
    /// </summary>
    public static string ApiKeyLookup() => "global:apikeylookup";
}
