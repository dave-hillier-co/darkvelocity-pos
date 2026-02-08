using DarkVelocity.Host.State;
using Microsoft.AspNetCore.SignalR;

namespace DarkVelocity.Host.Hubs;

/// <summary>
/// SignalR hub for real-time floor plan updates.
/// Clients join a group scoped by site to receive table status changes,
/// guest arrivals, and waitlist updates.
/// </summary>
public class FloorPlanHub : Hub
{
    /// <summary>
    /// Join a site's floor plan update group.
    /// </summary>
    public async Task JoinSite(string orgId, string siteId)
    {
        var groupName = GetSiteGroup(orgId, siteId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a site's floor plan update group.
    /// </summary>
    public async Task LeaveSite(string orgId, string siteId)
    {
        var groupName = GetSiteGroup(orgId, siteId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    internal static string GetSiteGroup(string orgId, string siteId) => $"floorplan:{orgId}:{siteId}";
}

/// <summary>
/// Service for pushing floor plan events to connected clients.
/// Inject this into grains or endpoint handlers to broadcast changes.
/// </summary>
public class FloorPlanNotifier
{
    private readonly IHubContext<FloorPlanHub> _hubContext;

    public FloorPlanNotifier(IHubContext<FloorPlanHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTableStatusChanged(Guid orgId, Guid siteId, Guid tableId,
        string tableNumber, TableStatus newStatus, TableOccupancy? occupancy)
    {
        var group = FloorPlanHub.GetSiteGroup(orgId.ToString(), siteId.ToString());
        await _hubContext.Clients.Group(group).SendAsync("TableStatusChanged", new
        {
            tableId,
            tableNumber,
            status = newStatus.ToString().ToLowerInvariant(),
            occupancy = occupancy != null ? new
            {
                bookingId = occupancy.BookingId,
                orderId = occupancy.OrderId,
                guestName = occupancy.GuestName,
                guestCount = occupancy.GuestCount,
                seatedAt = occupancy.SeatedAt,
                serverId = occupancy.ServerId
            } : (object?)null,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task NotifyGuestArrived(Guid orgId, Guid siteId, Guid bookingId,
        string guestName, int partySize, string? tableNumber)
    {
        var group = FloorPlanHub.GetSiteGroup(orgId.ToString(), siteId.ToString());
        await _hubContext.Clients.Group(group).SendAsync("GuestArrived", new
        {
            bookingId,
            guestName,
            partySize,
            tableNumber,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task NotifyWaitlistChanged(Guid orgId, Guid siteId, int waitingCount)
    {
        var group = FloorPlanHub.GetSiteGroup(orgId.ToString(), siteId.ToString());
        await _hubContext.Clients.Group(group).SendAsync("WaitlistChanged", new
        {
            waitingCount,
            timestamp = DateTime.UtcNow
        });
    }
}
