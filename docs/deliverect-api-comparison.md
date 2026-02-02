# Deliverect API vs DarkVelocity POS API Comparison

This document compares the Deliverect integration APIs with the current DarkVelocity POS API structure to identify gaps, alignment opportunities, and integration requirements.

## Executive Summary

| Aspect | Deliverect | DarkVelocity POS |
|--------|------------|------------------|
| **Architecture** | REST API with webhooks | Orleans Grains + Minimal APIs |
| **Auth** | OAuth2 (client_credentials) + HMAC | OAuth2 + Device Flow + JWT |
| **Response Format** | Plain JSON | HAL+JSON |
| **Order Flow** | Push-based (webhooks) | Event-sourced (Orleans streams) |
| **Multi-tenancy** | Account → Location | Organization → Site |

---

## 1. Authentication Comparison

### Deliverect Authentication
| Method | Description |
|--------|-------------|
| **OAuth2 M2M** | `client_id` + `client_secret` for machine-to-machine |
| **HMAC** | Request signing for webhook verification |
| **IP Whitelisting** | Additional security layer |
| **Scopes** | `genericPOS`, `store`, `genericKDS`, etc. |

### DarkVelocity Authentication
| Method | Description |
|--------|-------------|
| **OAuth2** | Google/Microsoft social login |
| **Device Flow** | RFC 8628 for headless devices (POS terminals) |
| **PIN Auth** | Staff login on authenticated devices |
| **JWT** | Access + Refresh tokens with claims |

### Gap Analysis
| Gap | Impact | Recommendation |
|-----|--------|----------------|
| No M2M auth in DarkVelocity | Cannot integrate as Deliverect POS partner | Add `client_credentials` grant |
| No HMAC webhook verification | Cannot verify Deliverect webhook authenticity | Implement HMAC signature validation |
| No API key scopes | Cannot restrict partner access | Add scope-based authorization |

---

## 2. Order Management Comparison

### Deliverect Order Model
```json
{
  "_id": "order_123",
  "channelOrderId": "UBER-456",
  "channelOrderDisplayId": "#1234",
  "status": 20,
  "orderType": 1,
  "channel": 7,
  "account": "acc_123",
  "location": "loc_456",
  "customer": {
    "name": "John Doe",
    "phoneNumber": "+1234567890",
    "email": "john@example.com"
  },
  "deliveryAddress": {
    "street": "123 Main St",
    "postalCode": "12345",
    "city": "Anytown",
    "country": "US"
  },
  "items": [{
    "plu": "BURGER-001",
    "name": "Classic Burger",
    "quantity": 2,
    "price": 1299,
    "subItems": [{
      "plu": "MOD-CHEESE",
      "name": "Extra Cheese",
      "quantity": 1,
      "price": 150
    }]
  }],
  "payment": {
    "amount": 2898,
    "type": 1,
    "due": 0
  },
  "deliveryCost": 499,
  "tip": 200,
  "pickupTime": "2026-02-01T12:30:00Z",
  "orderIsAlreadyPaid": true
}
```

### DarkVelocity External Order Model
```csharp
ExternalOrderState {
    ExternalOrderId: Guid,
    PlatformOrderId: string,
    PlatformOrderNumber: string,
    Status: ExternalOrderStatus,
    OrderType: ExternalOrderType,
    DeliveryPlatformId: Guid,
    LocationId: Guid,
    Customer: {
        Name: string,
        Phone: string,
        Email: string
    },
    Items: [{
        ExternalItemId: string,
        Name: string,
        Quantity: int,
        UnitPrice: decimal,
        Modifiers: [...]
    }],
    Subtotal: decimal,
    DeliveryFee: decimal,
    ServiceFee: decimal,
    Tax: decimal,
    Tip: decimal,
    Total: decimal,
    Currency: string
}
```

### Field Mapping

| Deliverect Field | DarkVelocity Field | Notes |
|-----------------|-------------------|-------|
| `_id` | `ExternalOrderId` | Need to store Deliverect ID |
| `channelOrderId` | `PlatformOrderId` | Direct mapping |
| `channelOrderDisplayId` | `PlatformOrderNumber` | Direct mapping |
| `status` (int) | `ExternalOrderStatus` (enum) | Need status mapping |
| `orderType` | `ExternalOrderType` | 1=Delivery, 2=Pickup, 3=DineIn |
| `channel` | `DeliveryPlatformId` | Need channel→platform mapping |
| `account` | - | Maps to Organization |
| `location` | `LocationId` | Need location mapping |
| `customer.*` | `Customer.*` | Direct mapping |
| `deliveryAddress` | - | **Missing in DarkVelocity** |
| `items[].plu` | `Items[].ExternalItemId` | Direct mapping |
| `items[].subItems` | `Items[].Modifiers` | Direct mapping |
| `payment.amount` | `Total` | Cents vs decimal |
| `deliveryCost` | `DeliveryFee` | Direct mapping |
| `tip` | `Tip` | Direct mapping |
| `pickupTime` | - | **Missing in DarkVelocity** |
| `deliveryTime` | - | **Missing in DarkVelocity** |
| `courier` | - | **Missing in DarkVelocity** |

### Missing in DarkVelocity
- Delivery address storage
- Scheduled pickup/delivery times
- Courier tracking information
- Channel-specific display IDs
- Discount breakdown by provider
- Packaging preferences (cutlery, bags)

---

## 3. Order Status Comparison

### Deliverect Status Codes
| Code | Meaning | Description |
|------|---------|-------------|
| 2 | Received | Order received from channel |
| 10 | Pending | Awaiting POS acceptance |
| 20 | Accepted | POS accepted the order |
| 30 | Preparing | Kitchen started preparation |
| 40 | Prepared | Ready for pickup/delivery |
| 50 | Picked Up | Courier collected |
| 60 | Delivered | Order delivered |
| 90 | Finalized | Order completed |
| 95 | Auto-finalized | System auto-closed |
| 110 | Cancelled | Order cancelled |
| 120 | Failed | Order failed |

### DarkVelocity External Order Status
```csharp
public enum ExternalOrderStatus {
    Received,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}
```

### Status Mapping Required
| Deliverect | DarkVelocity | Action Needed |
|------------|--------------|---------------|
| 2, 10 | `Received` | Map both |
| 20 | `Accepted` | Direct |
| 30 | `Preparing` | Direct |
| 40 | `Ready` | Direct |
| 50 | `PickedUp` | Direct |
| 60 | `Delivered` | Direct |
| 90, 95 | - | Add `Finalized` status |
| 110 | `Cancelled` | Direct |
| 120 | `Failed` | Direct |

---

## 4. Product/Menu Sync Comparison

### Deliverect Product Sync Endpoint
```
POST /products/{accountId}
```

```json
{
  "locationId": "loc_123",
  "products": [{
    "productType": 1,
    "name": "Classic Burger",
    "plu": "BURGER-001",
    "price": 1299,
    "deliveryTax": 10,
    "takeawayTax": 10,
    "eatInTax": 20,
    "description": "Juicy beef burger",
    "imageUrl": "https://...",
    "max": 100,
    "subProducts": ["MOD-CHEESE", "MOD-BACON"],
    "productTags": [1, 5],
    "snoozed": false
  }],
  "categories": [{
    "name": "Burgers",
    "categoryId": "CAT-BURGERS"
  }]
}
```

### DarkVelocity Menu Sync
```csharp
IMenuSyncGrain {
    Task<MenuSyncSnapshot> StartAsync(StartMenuSyncCommand)
    Task RecordItemSyncedAsync(MenuItemMappingRecord)
    Task RecordItemFailedAsync(Guid menuItemId, string error)
    Task CompleteAsync()
    Task FailAsync(string error)
}
```

### Gap Analysis
| Feature | Deliverect | DarkVelocity | Gap |
|---------|------------|--------------|-----|
| Push menu to channel | Yes | Partial | Need outbound sync |
| Pull menu from channel | Yes | No | Need import capability |
| Product snooze/availability | Yes | Unknown | Check inventory integration |
| Multi-tax rates | Yes (delivery/takeaway/eatIn) | Unknown | May need tax context |
| Product tags/allergens | Yes | Unknown | Check menu metadata |
| Image URLs | Yes | Unknown | Check asset management |

---

## 5. Webhook Comparison

### Deliverect Webhooks (POS receives)
| Webhook | Trigger | DarkVelocity Equivalent |
|---------|---------|------------------------|
| Order Notification | New order from channel | `IExternalOrderGrain.CreateAsync` |
| Order Cancellation | Channel cancels order | `IExternalOrderGrain.CancelAsync` |
| Sync Products | Channel requests menu | Need to implement |
| Sync Tables | Channel requests tables | Not applicable |
| Tax Calculation | Dynamic tax request | Not implemented |

### Deliverect Webhooks (POS sends)
| Webhook | Trigger | DarkVelocity Equivalent |
|---------|---------|------------------------|
| Order Status Update | Status changes | Need HTTP callback |
| Register POS | Initial connection | Need registration flow |

### DarkVelocity Webhook System
```csharp
IWebhookEndpointState {
    EndpointId: Guid,
    MerchantId: Guid,
    Url: string,
    EnabledEvents: List<string>,  // ["*", "order.*"]
    Secret: string                 // HMAC signing
}
```

### Required Implementation
1. **Inbound webhook handler** for Deliverect order notifications
2. **Outbound webhook sender** for status updates to Deliverect
3. **HMAC validation** for Deliverect webhook security
4. **Menu sync responder** for channel product requests

---

## 6. Grain Interface vs Deliverect Functionality Comparison

This section maps Deliverect API capabilities to existing DarkVelocity Orleans grain interfaces.

### 6.1 Delivery Platform Management

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Connect to channel | `IDeliveryPlatformGrain` | `ConnectAsync(ConnectDeliveryPlatformCommand)` | ✅ Exists |
| Disconnect channel | `IDeliveryPlatformGrain` | `DisconnectAsync()` | ✅ Exists |
| Pause/Resume orders | `IDeliveryPlatformGrain` | `PauseAsync()` / `ResumeAsync()` | ✅ Exists |
| Map locations to stores | `IDeliveryPlatformGrain` | `AddLocationMappingAsync(PlatformLocationMapping)` | ✅ Exists |
| Track order stats | `IDeliveryPlatformGrain` | `RecordOrderAsync(decimal)` | ✅ Exists |
| Store API credentials | `IDeliveryPlatformGrain` | `ApiCredentialsEncrypted` in command | ✅ Exists |
| Webhook secret storage | `IDeliveryPlatformGrain` | `WebhookSecret` in command | ✅ Exists |

**Gap:** No method to validate incoming webhooks using stored secret.

### 6.2 External Order Management

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Receive order webhook | `IExternalOrderGrain` | `ReceiveAsync(ExternalOrderReceived)` | ✅ Exists |
| Accept order | `IExternalOrderGrain` | `AcceptAsync(DateTime? estimatedPickupAt)` | ✅ Exists |
| Reject order | `IExternalOrderGrain` | `RejectAsync(string reason)` | ✅ Exists |
| Mark preparing | `IExternalOrderGrain` | `SetPreparingAsync()` | ✅ Exists |
| Mark ready | `IExternalOrderGrain` | `SetReadyAsync()` | ✅ Exists |
| Mark picked up | `IExternalOrderGrain` | `SetPickedUpAsync()` | ✅ Exists |
| Mark delivered | `IExternalOrderGrain` | `SetDeliveredAsync()` | ✅ Exists |
| Cancel order | `IExternalOrderGrain` | `CancelAsync(string reason)` | ✅ Exists |
| Link to internal order | `IExternalOrderGrain` | `LinkInternalOrderAsync(Guid internalOrderId)` | ✅ Exists |
| Retry failed order | `IExternalOrderGrain` | `IncrementRetryAsync()` | ✅ Exists |
| Store delivery address | `ExternalOrderCustomer` | `DeliveryAddress` field | ⚠️ Partial (single string) |
| Scheduled pickup time | - | - | ❌ Missing |
| Scheduled delivery time | - | - | ❌ Missing |
| Courier tracking | - | - | ❌ Missing |
| ASAP vs scheduled flag | - | - | ❌ Missing |
| Channel display ID | - | - | ❌ Missing |
| Discount breakdown | - | - | ❌ Missing |
| Packaging preferences | - | - | ❌ Missing |

**Current `ExternalOrderCustomer` record:**
```csharp
public record ExternalOrderCustomer(
    string Name,
    string? Phone,
    string? DeliveryAddress);  // Single string, not structured
```

**Deliverect requires structured address:**
```json
{
  "street": "123 Main St",
  "postalCode": "12345",
  "city": "Anytown",
  "country": "US",
  "extraAddressInfo": "Apt 4B"
}
```

### 6.3 Menu Synchronization

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Start menu sync | `IMenuSyncGrain` | `StartAsync(StartMenuSyncCommand)` | ✅ Exists |
| Record item synced | `IMenuSyncGrain` | `RecordItemSyncedAsync(MenuItemMappingRecord)` | ✅ Exists |
| Record item failed | `IMenuSyncGrain` | `RecordItemFailedAsync(Guid, string)` | ✅ Exists |
| Complete sync | `IMenuSyncGrain` | `CompleteAsync()` | ✅ Exists |
| PLU → MenuItem mapping | `MenuItemMappingRecord` | `InternalMenuItemId` ↔ `PlatformItemId` | ✅ Exists |
| Price override per platform | `MenuItemMappingRecord` | `PriceOverride` | ✅ Exists |
| Availability/snooze | `MenuItemMappingRecord` | `IsAvailable` | ✅ Exists |
| Multi-tax rates | `IMenuItemGrain` | - | ❌ Missing |
| Product tags/allergens | `IMenuItemGrain` | - | ❌ Missing |
| Image URL | `IMenuItemGrain` | `ImageUrl` in `CreateMenuItemCommand` | ✅ Exists |
| Category mapping | `MenuItemMappingRecord` | `PlatformCategoryId` | ✅ Exists |

**Current `IMenuItemGrain` interface:**
```csharp
public interface IMenuItemGrain : IGrainWithStringKey
{
    Task<MenuItemSnapshot> CreateAsync(CreateMenuItemCommand command);
    Task<MenuItemSnapshot> UpdateAsync(UpdateMenuItemCommand command);
    Task DeactivateAsync();
    Task<MenuItemSnapshot> GetSnapshotAsync();
    Task<decimal> GetPriceAsync();
    Task AddModifierAsync(MenuItemModifier modifier);
    Task RemoveModifierAsync(Guid modifierId);
    Task UpdateCostAsync(decimal theoreticalCost);
}
```

**Missing for Deliverect:**
- `SetSnoozedAsync(bool snoozed, TimeSpan? duration)` - Temporarily unavailable
- `UpdateTaxRatesAsync(decimal deliveryTax, decimal takeawayTax, decimal eatInTax)`
- `AddProductTagAsync(int tagId)` / `RemoveProductTagAsync(int tagId)`

### 6.4 Kitchen Display System (KDS)

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Create kitchen ticket | `IKitchenTicketGrain` | `CreateAsync(CreateKitchenTicketCommand)` | ✅ Exists |
| Add items to ticket | `IKitchenTicketGrain` | `AddItemAsync(AddTicketItemCommand)` | ✅ Exists |
| Start preparation | `IKitchenTicketGrain` | `StartAsync()` | ✅ Exists |
| Bump ticket (complete) | `IKitchenTicketGrain` | `BumpAsync(Guid bumpedBy)` | ✅ Exists |
| Rush/VIP priority | `IKitchenTicketGrain` | `MarkRushAsync()` / `MarkVipAsync()` | ✅ Exists |
| Station routing | `IKitchenStationGrain` | `ReceiveTicketAsync(Guid ticketId)` | ✅ Exists |
| Prep time tracking | `IKitchenTicketGrain` | `GetTimingsAsync()` | ✅ Exists |

**KDS integration is well-aligned with Deliverect's KDS API scope.**

### 6.5 Inventory Integration

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Update stock levels | `IInventoryGrain` | `AdjustQuantityAsync(AdjustQuantityCommand)` | ✅ Exists |
| Check availability | `IInventoryGrain` | `HasSufficientStockAsync(decimal)` | ✅ Exists |
| Consume for order | `IInventoryGrain` | `ConsumeForOrderAsync(Guid, decimal, Guid?)` | ✅ Exists |
| Batch/lot tracking | `IInventoryGrain` | `ReceiveBatchAsync(ReceiveBatchCommand)` | ✅ Exists |
| Expiry tracking | `IInventoryGrain` | `WriteOffExpiredBatchesAsync(Guid)` | ✅ Exists |
| Stock level alerts | `IInventoryGrain` | `GetStockLevelAsync()` returns `StockLevel` enum | ✅ Exists |

**Inventory grain is feature-complete for Deliverect integration.**

### 6.6 Store/Location Management

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Get store info | `ISiteGrain` | `GetStateAsync()` | ✅ Exists |
| Open/close store | `ISiteGrain` | `OpenAsync()` / `CloseAsync()` | ✅ Exists |
| Temporary closure | `ISiteGrain` | `CloseTemporarilyAsync(string reason)` | ✅ Exists |
| Operating hours | `ISiteGrain` | `OperatingHours` in `UpdateSiteCommand` | ✅ Exists |
| Address/timezone | `ISiteGrain` | `Address`, `Timezone` in `CreateSiteCommand` | ✅ Exists |
| Currency | `ISiteGrain` | `Currency` in `CreateSiteCommand` | ✅ Exists |

**Store management is well-aligned.**

### 6.7 Payment/Payout Management

| Deliverect Capability | DarkVelocity Grain | Grain Method | Status |
|----------------------|-------------------|--------------|--------|
| Platform payouts | `IPlatformPayoutGrain` | `ReceiveAsync(PayoutReceived)` | ✅ Exists |
| Payout status tracking | `IPlatformPayoutGrain` | `SetProcessingAsync()`, `CompleteAsync()` | ✅ Exists |
| Fee breakdown | `PayoutReceived` | `GrossAmount`, `PlatformFees`, `NetAmount` | ✅ Exists |
| Period tracking | `PayoutReceived` | `PeriodStart`, `PeriodEnd` | ✅ Exists |

### 6.8 Webhooks (Outbound to Deliverect)

| Deliverect Requirement | DarkVelocity Grain | Status |
|-----------------------|-------------------|--------|
| Webhook endpoint storage | `IWebhookEndpointGrain` | ✅ Exists |
| Event subscription | `EnabledEvents` list | ✅ Exists |
| HMAC signing | `Secret` field | ✅ Exists |
| Delivery tracking | `RecentDeliveries` | ✅ Exists |
| Retry on failure | - | ⚠️ Need implementation |

**Missing:** Outbound HTTP client to POST status updates to Deliverect's API.

---

## 7. Gap Summary by Priority

### Critical Gaps (Block Integration)
| Gap | Affected Grain | Required Change |
|-----|---------------|-----------------|
| No outbound status callback | `IExternalOrderGrain` | Add `NotifyPlatformAsync()` or event subscriber |
| No HMAC validation for inbound webhooks | New | Add webhook validation middleware |
| No M2M authentication | Auth system | Add `client_credentials` OAuth grant |

### High Priority Gaps (Feature Parity)
| Gap | Affected Grain | Required Change |
|-----|---------------|-----------------|
| Structured delivery address | `ExternalOrderCustomer` | Change `DeliveryAddress` from `string` to record |
| Scheduled pickup/delivery times | `IExternalOrderGrain` | Add `ScheduledPickupAt`, `ScheduledDeliveryAt` |
| Courier tracking | `IExternalOrderGrain` | Add `CourierInfo` record and methods |
| Discount provider tracking | `IExternalOrderGrain` | Add `Discounts` with restaurant/channel attribution |
| Menu item snooze | `IMenuItemGrain` | Add `SetSnoozedAsync(bool, TimeSpan?)` |

### Medium Priority Gaps (Nice to Have)
| Gap | Affected Grain | Required Change |
|-----|---------------|-----------------|
| Product tags/allergens | `IMenuItemGrain` | Add `ProductTags` collection |
| Multi-context tax rates | `IMenuItemGrain` | Add delivery/takeaway/eatIn tax fields |
| Packaging preferences | `IExternalOrderGrain` | Add `PackagingPreferences` record |
| ASAP delivery flag | `IExternalOrderGrain` | Add `IsAsapDelivery` boolean |

---

## 8. Integration Architecture Recommendations

### Option A: Direct Deliverect Integration
```
┌─────────────┐      ┌──────────────┐      ┌─────────────────┐
│  Deliverect │ ───▶ │   Webhook    │ ───▶ │  ExternalOrder  │
│   Channel   │      │   Handler    │      │     Grain       │
└─────────────┘      └──────────────┘      └─────────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │   Outbound   │
                     │   Notifier   │
                     └──────────────┘
                            │
                            ▼
                     ┌─────────────┐
                     │  Deliverect │
                     │  Status API │
                     └─────────────┘
```

### Option B: Adapter Pattern
```
┌─────────────┐      ┌──────────────┐      ┌─────────────────┐
│  Deliverect │ ───▶ │  Deliverect  │ ───▶ │  DarkVelocity   │
│     API     │      │   Adapter    │      │   Core Grains   │
└─────────────┘      └──────────────┘      └─────────────────┘
```

### Recommended New Components

1. **DeliverectWebhookController**
   - Handle order notifications
   - Validate HMAC signatures
   - Map to internal models

2. **DeliverectApiClient**
   - Send status updates
   - Push menu updates
   - Handle authentication

3. **DeliverectMappingService**
   - Status code mapping
   - Product PLU mapping
   - Location/channel mapping

4. **Configuration**
   ```json
   {
     "Deliverect": {
       "BaseUrl": "https://api.deliverect.com",
       "StagingUrl": "https://api.staging.deliverect.com",
       "ClientId": "...",
       "ClientSecret": "...",
       "WebhookSecret": "...",
       "TimeoutSeconds": 30
     }
   }
   ```

---

## 8. Implementation Priority

### Phase 1: Core Integration (MVP)
1. Add Deliverect webhook endpoint for order reception
2. Implement order status callback to Deliverect
3. Add HMAC signature validation
4. Create status code mapping

### Phase 2: Menu Sync
1. Implement menu push to Deliverect
2. Add product availability (snooze) support
3. Handle menu pull requests

### Phase 3: Advanced Features
1. Courier tracking integration
2. Delivery address management
3. Real-time ETA updates
4. Multi-channel reporting

---

## 9. Data Model Changes Required

### ExternalOrderState Additions
```csharp
// New fields needed
public string? DeliverectOrderId { get; set; }
public string? ChannelDisplayId { get; set; }
public DeliveryAddress? DeliveryAddress { get; set; }
public DateTime? ScheduledPickupTime { get; set; }
public DateTime? ScheduledDeliveryTime { get; set; }
public bool IsAsapDelivery { get; set; }
public CourierInfo? Courier { get; set; }
public List<DiscountInfo>? Discounts { get; set; }
public PackagingPreferences? Packaging { get; set; }
public string? CustomerNote { get; set; }

public record DeliveryAddress(
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ExtraInfo
);

public record CourierInfo(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Provider,
    int? Status
);

public record DiscountInfo(
    string Type,
    string Provider,  // "restaurant" or "channel"
    string Name,
    decimal Amount
);
```

### New Enum Values
```csharp
public enum ExternalOrderStatus {
    // Existing
    Received, Accepted, Rejected, Preparing, Ready,
    PickedUp, Delivered, Cancelled, Failed,
    // New
    Pending,     // Awaiting acceptance
    Finalized,   // Successfully completed
    AutoFinalized // System auto-closed
}
```

---

## 10. Multi-Channel Architecture (Future)

### 10.1 Overview

Support multiple delivery channels with a grain per integration:

```
┌─────────────────────────────────────────────────────────────┐
│                    CHANNEL INTEGRATIONS                      │
├──────────────┬──────────────┬──────────────┬────────────────┤
│ Deliverect   │  UberEats    │  Deliveroo   │ Local Website  │
│ (Aggregator) │   (Direct)   │   (Direct)   │   (Internal)   │
└──────┬───────┴──────┬───────┴──────┬───────┴───────┬────────┘
       │              │              │               │
       ▼              ▼              ▼               ▼
┌─────────────────────────────────────────────────────────────┐
│              CHANNEL ADAPTER GRAINS                          │
│  IDeliverectChannelGrain  IUberEatsChannelGrain  etc.       │
│  - HMAC validation        - OAuth2 auth                     │
│  - Status mapping         - Webhook verify                  │
│  - Multi-platform source  - Platform API client             │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼ Transforms to ExternalOrderReceived
┌─────────────────────────────────────────────────────────────┐
│              IExternalOrderGrain (existing)                  │
│              Unified order model                             │
└─────────────────────────────────────────────────────────────┘
```

### 10.2 Integration Types

```csharp
public enum IntegrationType
{
    Direct,      // UberEats, Deliveroo, JustEat (direct API)
    Aggregator,  // Deliverect, Otter (manages multiple platforms)
    Internal     // Local website, Kiosk (self-hosted)
}
```

| Platform | Type | Auth | Status Sync | Menu Sync |
|----------|------|------|-------------|-----------|
| Deliverect | Aggregator | OAuth2 M2M + HMAC | REST callback | Push |
| UberEats | Direct | OAuth2 | REST callback | Push |
| Deliveroo | Direct | API Key | REST callback | Push |
| Just Eat | Direct | API Key | REST callback | Push |
| Local Website | Internal | Internal auth | N/A (SignalR) | Read-only |

### 10.3 Channel Grain Interface

```csharp
public interface IChannelGrain : IGrainWithStringKey
{
    // Lifecycle
    Task<ChannelSnapshot> ConfigureAsync(ConfigureChannelCommand command);
    Task DeactivateAsync();
    Task ActivateAsync();

    // Order Reception
    Task<ExternalOrderSnapshot> ReceiveOrderAsync(ChannelOrderReceived order);

    // Outbound Status Updates (to platform)
    Task NotifyOrderAcceptedAsync(Guid externalOrderId, DateTime estimatedPickupAt);
    Task NotifyOrderPreparingAsync(Guid externalOrderId);
    Task NotifyOrderReadyAsync(Guid externalOrderId);
    Task NotifyOrderCancelledAsync(Guid externalOrderId, string reason);

    // Menu Operations
    Task<MenuSyncResult> SyncMenuAsync(Guid menuId);
    Task UpdateItemAvailabilityAsync(Guid menuItemId, bool isAvailable);

    // Store Operations
    Task SetStoreOpenAsync(Guid siteId);
    Task SetStoreClosedAsync(Guid siteId, string? reason);
    Task SetBusyModeAsync(Guid siteId, int additionalPrepMinutes);

    // Monitoring
    Task<ChannelSnapshot> GetSnapshotAsync();
    Task<ChannelHealthStatus> GetHealthStatusAsync();
}
```

### 10.4 Local Website Channel

Unlike external platforms, the local website channel:
- No webhook validation (orders via internal API)
- No outbound status callbacks (frontend uses SignalR)
- Direct menu access (no sync needed)
- Internal customer/payment integration

```csharp
public interface ILocalWebsiteChannelGrain : IChannelGrain
{
    Task<ExternalOrderSnapshot> PlaceOrderAsync(LocalWebsiteOrderCommand command);
    Task<bool> ValidateSessionAsync(string sessionToken);
    Task<IReadOnlyList<TimeSlot>> GetAvailableTimeSlotsAsync(Guid siteId, DateOnly date);
}
```

### 10.5 Status Mapping Per Platform

Each platform has different status codes:

| Deliverect | UberEats | Deliveroo | DarkVelocity |
|------------|----------|-----------|--------------|
| 2, 10 | `created` | `placed` | `Pending` |
| 20 | `accepted` | `accepted` | `Accepted` |
| 30 | `in_progress` | `preparing` | `Preparing` |
| 40 | `ready_for_pickup` | `ready_for_collection` | `Ready` |
| 50 | `picked_up` | `collected` | `PickedUp` |
| 60, 90, 95 | `delivered` | `delivered` | `Delivered` |
| 110 | `cancelled` | `cancelled` | `Cancelled` |

### 10.6 File Structure

```
src/DarkVelocity.Host/
├── Grains/
│   ├── Channels/
│   │   ├── IChannelGrain.cs
│   │   ├── IDeliverectChannelGrain.cs
│   │   ├── IUberEatsChannelGrain.cs
│   │   ├── ILocalWebsiteChannelGrain.cs
│   │   └── [implementations]
│   └── IStatusMappingGrain.cs
├── State/
│   ├── ChannelState.cs
│   └── StatusMappingState.cs
├── Adapters/
│   ├── DeliverectAdapter.cs
│   ├── UberEatsAdapter.cs
│   └── DeliverooAdapter.cs
└── Webhooks/
    ├── DeliverectWebhookHandler.cs
    └── UberEatsWebhookHandler.cs
```

### 10.7 Grain Key Conventions

```csharp
// Channel grain
GrainKeys.Channel(orgId, DeliveryPlatformType.Deliverect, channelId)
// → "{orgId}:channel:deliverect:{channelId}"

// Status mapping
GrainKeys.StatusMapping(orgId, DeliveryPlatformType.UberEats)
// → "{orgId}:statusmapping:ubereats"
```

### 10.8 Implementation Phases

| Phase | Scope | Duration |
|-------|-------|----------|
| 1 | Base `IChannelGrain` + `IntegrationType` + status mapping | 2 weeks |
| 2 | Deliverect integration (webhook, API, HMAC) | 2 weeks |
| 3 | Direct integrations (UberEats, Deliveroo, JustEat) | 3 weeks |
| 4 | Local website channel | 1 week |
| 5 | Menu sync enhancement | 2 weeks |
| 6 | Testing & documentation | 2 weeks |

---

## 11. References

- [Deliverect Developer Hub](https://developers.deliverect.com/)
- [Deliverect POS Endpoints](https://developers.deliverect.com/reference/pos_endpoints)
- [Order Notification Webhook](https://developers.deliverect.com/reference/post-orders-webhook)
- [Order Status Update](https://developers.deliverect.com/reference/post-order-status-update)
- [API Authentication](https://developers.deliverect.com/docs/getting-started)
- [Order Flow](https://developers.deliverect.com/docs/order-flow)
