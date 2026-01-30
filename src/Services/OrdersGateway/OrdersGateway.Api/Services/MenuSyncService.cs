using System.Text.Json;
using DarkVelocity.OrdersGateway.Api.Adapters;
using DarkVelocity.OrdersGateway.Api.Data;
using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.OrdersGateway.Api.Services;

/// <summary>
/// Service for synchronizing menus to delivery platforms.
/// </summary>
public interface IMenuSyncService
{
    Task<MenuSync> TriggerSyncAsync(Guid platformId, Guid locationId, bool fullSync = false, CancellationToken cancellationToken = default);
    Task<MenuSync?> GetSyncStatusAsync(Guid syncId, CancellationToken cancellationToken = default);
    Task<List<MenuSync>> GetSyncHistoryAsync(Guid platformId, int limit = 20, CancellationToken cancellationToken = default);
    Task UpdateItemAvailabilityAsync(Guid platformId, Guid menuItemId, bool isAvailable, CancellationToken cancellationToken = default);
}

public class MenuSyncService : IMenuSyncService
{
    private readonly OrdersGatewayDbContext _context;
    private readonly IDeliveryPlatformAdapterFactory _adapterFactory;
    private readonly ILogger<MenuSyncService> _logger;

    public MenuSyncService(
        OrdersGatewayDbContext context,
        IDeliveryPlatformAdapterFactory adapterFactory,
        ILogger<MenuSyncService> logger)
    {
        _context = context;
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public async Task<MenuSync> TriggerSyncAsync(Guid platformId, Guid locationId, bool fullSync = false, CancellationToken cancellationToken = default)
    {
        var platform = await _context.DeliveryPlatforms.FindAsync(new object[] { platformId }, cancellationToken);
        if (platform == null)
        {
            throw new ArgumentException($"Platform {platformId} not found");
        }

        var adapter = _adapterFactory.GetAdapter(platform.PlatformType);
        if (adapter == null)
        {
            throw new InvalidOperationException($"No adapter found for {platform.PlatformType}");
        }

        // Create sync record
        var sync = new MenuSync
        {
            TenantId = platform.TenantId,
            DeliveryPlatformId = platformId,
            LocationId = locationId,
            Status = MenuSyncStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            TriggeredBy = MenuSyncTrigger.Manual
        };

        _context.MenuSyncs.Add(sync);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // Get menu items from existing mappings or fetch from Menu service
            // For now, we'll use the existing mappings as a placeholder
            var mappings = await _context.MenuItemMappings
                .Where(m => m.DeliveryPlatformId == platformId)
                .ToListAsync(cancellationToken);

            // TODO: In production, this would call the Menu service to get actual menu items
            var menuItems = mappings.Select(m => new MenuItemForSync(
                m.InternalMenuItemId,
                $"Item {m.InternalMenuItemId}", // Would come from Menu service
                null,
                m.PriceOverride ?? 0,
                null,
                null,
                m.IsAvailable,
                null
            )).ToList();

            sync.ItemsTotal = menuItems.Count;

            if (menuItems.Count == 0)
            {
                sync.Status = MenuSyncStatus.Completed;
                sync.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return sync;
            }

            // Perform the sync
            var result = await adapter.SyncMenuAsync(platform, locationId, menuItems, cancellationToken);

            sync.ItemsSynced = result.ItemsSynced;
            sync.ItemsFailed = result.ItemsFailed;
            sync.Status = result.Success ? MenuSyncStatus.Completed : MenuSyncStatus.Failed;
            sync.CompletedAt = DateTime.UtcNow;

            if (result.Errors != null && result.Errors.Any())
            {
                sync.ErrorLog = JsonSerializer.Serialize(result.Errors);
            }

            // Update platform's last sync time
            platform.LastSyncAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Menu sync {SyncId} completed: {Synced} synced, {Failed} failed",
                sync.Id, sync.ItemsSynced, sync.ItemsFailed);

            return sync;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Menu sync {SyncId} failed", sync.Id);

            sync.Status = MenuSyncStatus.Failed;
            sync.CompletedAt = DateTime.UtcNow;
            sync.ErrorLog = JsonSerializer.Serialize(new[] { new MenuSyncError { ErrorMessage = ex.Message } });

            await _context.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    public async Task<MenuSync?> GetSyncStatusAsync(Guid syncId, CancellationToken cancellationToken = default)
    {
        return await _context.MenuSyncs
            .Include(s => s.DeliveryPlatform)
            .FirstOrDefaultAsync(s => s.Id == syncId, cancellationToken);
    }

    public async Task<List<MenuSync>> GetSyncHistoryAsync(Guid platformId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _context.MenuSyncs
            .Where(s => s.DeliveryPlatformId == platformId)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateItemAvailabilityAsync(Guid platformId, Guid menuItemId, bool isAvailable, CancellationToken cancellationToken = default)
    {
        var mapping = await _context.MenuItemMappings
            .Include(m => m.DeliveryPlatform)
            .FirstOrDefaultAsync(m => m.DeliveryPlatformId == platformId && m.InternalMenuItemId == menuItemId, cancellationToken);

        if (mapping == null)
        {
            _logger.LogWarning("No mapping found for menu item {MenuItemId} on platform {PlatformId}", menuItemId, platformId);
            return;
        }

        var adapter = _adapterFactory.GetAdapter(mapping.DeliveryPlatform.PlatformType);
        if (adapter == null)
        {
            _logger.LogWarning("No adapter found for {PlatformType}", mapping.DeliveryPlatform.PlatformType);
            return;
        }

        var success = await adapter.UpdateItemAvailabilityAsync(mapping.DeliveryPlatform, mapping.PlatformItemId, isAvailable, cancellationToken);

        if (success)
        {
            mapping.IsAvailable = isAvailable;
            mapping.LastSyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated availability for item {MenuItemId} on platform {PlatformId} to {IsAvailable}",
                menuItemId, platformId, isAvailable);
        }
    }
}
