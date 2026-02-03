namespace DarkVelocity.Host.Authorization;

/// <summary>
/// Specifies that an endpoint requires a SpiceDB permission check.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// The SpiceDB resource type (e.g., "site", "order", "payment").
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// The permission to check (e.g., "operate", "void", "manage").
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// The route parameter name containing the resource ID.
    /// If null, uses orgId for org-level resources or siteId for site-level resources.
    /// </summary>
    public string? ResourceIdParam { get; }

    /// <summary>
    /// If true, the resource ID is derived from the site (orgId:siteId format).
    /// </summary>
    public bool IsSiteScoped { get; }

    public RequirePermissionAttribute(
        string resourceType,
        string permission,
        string? resourceIdParam = null,
        bool isSiteScoped = false)
    {
        ResourceType = resourceType;
        Permission = permission;
        ResourceIdParam = resourceIdParam;
        IsSiteScoped = isSiteScoped;
    }

    /// <summary>
    /// Builds the SpiceDB resource ID from route values.
    /// </summary>
    public string BuildResourceId(RouteValueDictionary routeValues)
    {
        // If a specific resource ID param is provided, use it
        if (ResourceIdParam != null && routeValues.TryGetValue(ResourceIdParam, out var resourceId))
        {
            return resourceId?.ToString() ?? throw new InvalidOperationException(
                $"Route parameter '{ResourceIdParam}' not found");
        }

        // For site-scoped resources, build orgId:siteId format
        if (IsSiteScoped)
        {
            var orgId = routeValues["orgId"]?.ToString() ?? throw new InvalidOperationException(
                "Route parameter 'orgId' not found");
            var siteId = routeValues["siteId"]?.ToString() ?? throw new InvalidOperationException(
                "Route parameter 'siteId' not found");
            return $"{orgId}:{siteId}";
        }

        // Default to orgId for org-level resources
        return routeValues["orgId"]?.ToString() ?? throw new InvalidOperationException(
            "Route parameter 'orgId' not found");
    }
}

/// <summary>
/// Common permission constants for SpiceDB.
/// </summary>
public static class Permissions
{
    // Site-level POS operations
    public const string View = "view";
    public const string Operate = "operate";
    public const string Supervise = "supervise";
    public const string Manage = "manage";
    public const string Backoffice = "backoffice";

    // Order-specific
    public const string Create = "create";
    public const string AddLine = "add_line";
    public const string RemoveLine = "remove_line";
    public const string Send = "send";
    public const string Close = "close";
    public const string Discount = "discount";
    public const string Void = "void";

    // Payment-specific
    public const string Complete = "complete";
    public const string Refund = "refund";

    // Inventory-specific
    public const string Receive = "receive";
    public const string Consume = "consume";
    public const string Adjust = "adjust";
    public const string Transfer = "transfer";
    public const string Count = "count";

    // Table-specific
    public const string Seat = "seat";
    public const string Clear = "clear";
    public const string SetStatus = "set_status";
    public const string Update = "update";
    public const string Delete = "delete";

    // Booking-specific
    public const string Request = "request";
    public const string Checkin = "checkin";
    public const string Confirm = "confirm";
    public const string Cancel = "cancel";

    // Waitlist-specific
    public const string Add = "add";
    public const string Notify = "notify";
    public const string Remove = "remove";

    // Backoffice-specific
    public const string Upload = "upload";
    public const string Process = "process";
    public const string Submit = "submit";
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Loyalty = "loyalty";
    public const string Clock = "clock";
}

/// <summary>
/// SpiceDB resource type constants.
/// </summary>
public static class ResourceTypes
{
    public const string Organization = "organization";
    public const string Site = "site";
    public const string Order = "order";
    public const string Payment = "payment";
    public const string Inventory = "inventory";
    public const string Table = "table";
    public const string FloorPlan = "floor_plan";
    public const string Booking = "booking";
    public const string Waitlist = "waitlist";
    public const string BookingSettings = "booking_settings";
    public const string MenuCms = "menu_cms";
    public const string RecipeCms = "recipe_cms";
    public const string Employee = "employee";
    public const string Customer = "customer";
    public const string Expense = "expense";
    public const string PurchaseDocument = "purchase_document";
    public const string Channel = "channel";
    public const string Device = "device";
    public const string Webhook = "webhook";
    public const string Reporting = "reporting";
}
