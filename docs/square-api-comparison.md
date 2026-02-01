# Square API vs DarkVelocity POS API Comparison

This document compares the Square API with the DarkVelocity POS API to identify gaps, alignment opportunities, and architectural differences.

## Executive Summary

| Aspect | Square API | DarkVelocity POS API |
|--------|------------|---------------------|
| **API Style** | REST, JSON | REST, HAL+JSON |
| **Authentication** | OAuth 2.0 | OAuth 2.0, Device Flow (RFC 8628), PIN |
| **Versioning** | URL path (`/v2/`) | Not versioned (planned) |
| **Maturity** | Production-ready, comprehensive | Early stage, most endpoints disabled |

---

## API Domain Coverage

### Square API Domains

| Domain | Square API | DarkVelocity Status | Notes |
|--------|------------|---------------------|-------|
| **Locations** | ✅ Full | ⚠️ Disabled | Square: sites/stores; DV: Organizations + Sites |
| **Catalog** | ✅ Full | ⚠️ Disabled | Square: items, variations, modifiers, categories; DV: Menu API |
| **Orders** | ✅ Full | ⚠️ Disabled | Core POS functionality |
| **Payments** | ✅ Full | ⚠️ Disabled | Payment processing |
| **Checkout** | ✅ Full | ❌ Not planned | Hosted checkout pages |
| **Inventory** | ✅ Full | ⚠️ Disabled | Stock tracking |
| **Customers** | ✅ Full | ⚠️ Disabled | Customer profiles |
| **Team/Labor** | ✅ Full | ⚠️ Disabled | Employee management |
| **Devices** | ✅ Terminal API | ✅ Active | Device management |
| **OAuth** | ✅ Full | ✅ Active | Authentication |

---

## Detailed API Comparison

### 1. Locations / Sites

#### Square Locations API
```
GET    /v2/locations                    # List all locations
POST   /v2/locations                    # Create location
GET    /v2/locations/{location_id}      # Get location
PUT    /v2/locations/{location_id}      # Update location
```

**Square Location Object:**
```json
{
  "id": "LOCATION_ID",
  "name": "Main Store",
  "address": { "address_line_1": "...", "locality": "...", "country": "US" },
  "timezone": "America/Los_Angeles",
  "currency": "USD",
  "business_hours": { "periods": [...] },
  "status": "ACTIVE",
  "merchant_id": "MERCHANT_ID"
}
```

#### DarkVelocity Sites (Disabled)
```
POST   /api/locations/organizations         # Create organization
GET    /api/locations/organizations/{orgId} # Get organization
POST   /api/locations/sites                 # Create site
GET    /api/locations/sites/{orgId}/{siteId} # Get site
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Multi-tenant hierarchy | Merchant → Locations | Organization → Sites |
| Timezone support | ✅ | ✅ |
| Currency per location | ✅ | ✅ |
| Business hours | ✅ | ❌ Not implemented |
| Address/Coordinates | ✅ | ⚠️ Planned |
| Tax jurisdiction | ⚠️ Via tax settings | ✅ Per site |

**Gap:** DarkVelocity needs business hours support for sites.

---

### 2. Catalog / Menu

#### Square Catalog API
```
GET    /v2/catalog/list                     # List catalog objects
POST   /v2/catalog/search                   # Search with filters
POST   /v2/catalog/object                   # Upsert single object
POST   /v2/catalog/batch-upsert             # Batch upsert
POST   /v2/catalog/batch-retrieve           # Batch retrieve
POST   /v2/catalog/batch-delete             # Batch delete
POST   /v2/catalog/update-item-modifier-lists  # Update modifiers
GET    /v2/catalog/object/{object_id}       # Get by ID
DELETE /v2/catalog/object/{object_id}       # Delete by ID
POST   /v2/catalog/search-catalog-items     # Search items specifically
```

**Square CatalogObject Types:**
- `ITEM` - Base product
- `ITEM_VARIATION` - Specific variant with price/SKU
- `CATEGORY` - Grouping for items
- `MODIFIER_LIST` - Set of modifiers (e.g., sizes, add-ons)
- `MODIFIER` - Individual modifier option
- `DISCOUNT` - Discount definitions
- `TAX` - Tax definitions
- `IMAGE` - Product images

**Square Catalog Item Structure:**
```json
{
  "type": "ITEM",
  "id": "ITEM_ID",
  "item_data": {
    "name": "Coffee",
    "description": "Fresh brewed",
    "category_id": "CATEGORY_ID",
    "variations": [
      {
        "type": "ITEM_VARIATION",
        "id": "VAR_SMALL",
        "item_variation_data": {
          "name": "Small",
          "pricing_type": "FIXED_PRICING",
          "price_money": { "amount": 250, "currency": "USD" },
          "sku": "COFFEE-SM"
        }
      }
    ],
    "modifier_list_info": [
      {
        "modifier_list_id": "MILK_OPTIONS",
        "min_selected_modifiers": 0,
        "max_selected_modifiers": 1
      }
    ]
  }
}
```

#### DarkVelocity Menu API (Disabled)
```
POST   /api/menu/items                        # Create menu item
GET    /api/menu/items/{orgId}/{menuItemId}   # Get menu item
PUT    /api/menu/items/{orgId}/{menuItemId}/price  # Update price
POST   /api/menu/categories                   # Create category
POST   /api/menu/modifiers                    # Create modifier
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Item variations | ✅ First-class | ⚠️ Implicit via modifiers |
| Modifier lists | ✅ Reusable across items | ⚠️ Per-item |
| Min/max modifier selection | ✅ | ❌ |
| Batch operations | ✅ | ❌ |
| Search/filter | ✅ Advanced | ⚠️ Basic |
| Images | ✅ Native | ❌ |
| SKU support | ✅ | ⚠️ Planned |
| Tax assignment | ✅ Per item | ⚠️ Per site |

**Gaps:**
1. DarkVelocity needs item variations as first-class concept
2. Missing batch upsert/retrieve operations
3. No modifier list reusability
4. No catalog search/filter endpoints

---

### 3. Orders

#### Square Orders API
```
POST   /v2/orders                      # Create order
POST   /v2/orders/batch-retrieve       # Batch retrieve
POST   /v2/orders/search               # Search orders
GET    /v2/orders/{order_id}           # Get order
PUT    /v2/orders/{order_id}           # Update order
POST   /v2/orders/{order_id}/pay       # Pay for order
POST   /v2/orders/calculate            # Calculate totals (preview)
POST   /v2/orders/clone                # Clone an order
```

**Square Order Object:**
```json
{
  "idempotency_key": "unique-key",
  "order": {
    "location_id": "LOCATION_ID",
    "reference_id": "my-order-001",
    "customer_id": "CUSTOMER_ID",
    "line_items": [
      {
        "quantity": "1",
        "catalog_object_id": "ITEM_VAR_ID",
        "modifiers": [
          { "catalog_object_id": "MOD_ID" }
        ],
        "applied_discounts": [
          { "discount_uid": "discount-1" }
        ]
      }
    ],
    "taxes": [
      {
        "uid": "tax-1",
        "name": "Sales Tax",
        "percentage": "8.5",
        "scope": "ORDER"
      }
    ],
    "discounts": [
      {
        "uid": "discount-1",
        "name": "10% Off",
        "percentage": "10",
        "scope": "LINE_ITEM"
      }
    ],
    "fulfillments": [
      {
        "type": "PICKUP",
        "state": "PROPOSED",
        "pickup_details": {
          "pickup_at": "2024-01-15T12:00:00Z"
        }
      }
    ],
    "state": "OPEN"
  }
}
```

**Square Order States:**
- `DRAFT` - Not finalized
- `OPEN` - Active, accepting modifications
- `COMPLETED` - Fully paid
- `CANCELED` - Canceled

#### DarkVelocity Orders API (Disabled)
```
POST   /api/orders/                              # Create order
GET    /api/orders/{orgId}/{orderId}             # Get order
POST   /api/orders/{orgId}/{orderId}/items       # Add item
POST   /api/orders/{orgId}/{orderId}/submit      # Submit order
POST   /api/orders/{orgId}/{orderId}/complete    # Complete order
POST   /api/orders/{orgId}/{orderId}/cancel      # Cancel order
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Order creation | ✅ Full order in one call | ✅ |
| Incremental updates | ✅ PUT with version | ⚠️ Via events |
| Order search | ✅ Rich filters | ❌ |
| Order calculate/preview | ✅ | ❌ |
| Clone order | ✅ | ❌ |
| Fulfillments | ✅ (PICKUP, SHIPMENT, DELIVERY) | ⚠️ Kitchen tickets |
| Idempotency | ✅ Required | ⚠️ Optional |
| Discounts | ✅ Line-item & order level | ⚠️ Planned |
| Tips | ✅ | ❌ |
| Service charges | ✅ | ❌ |

**Gaps:**
1. No order search endpoint
2. No order preview/calculate
3. Missing fulfillment types (delivery, shipment)
4. No service charges support

---

### 4. Payments

#### Square Payments API
```
POST   /v2/payments                    # Create payment
GET    /v2/payments/{payment_id}       # Get payment
PUT    /v2/payments/{payment_id}       # Update payment
POST   /v2/payments/{payment_id}/cancel   # Cancel payment
POST   /v2/payments/{payment_id}/complete # Complete payment
GET    /v2/payments                    # List payments
```

**Square Payment Object:**
```json
{
  "source_id": "PAYMENT_TOKEN",
  "idempotency_key": "unique-key",
  "amount_money": {
    "amount": 1000,
    "currency": "USD"
  },
  "tip_money": {
    "amount": 200,
    "currency": "USD"
  },
  "app_fee_money": {
    "amount": 50,
    "currency": "USD"
  },
  "order_id": "ORDER_ID",
  "customer_id": "CUSTOMER_ID",
  "location_id": "LOCATION_ID",
  "reference_id": "my-payment-001",
  "autocomplete": true
}
```

**Square Payment Methods:**
- Cards (credit, debit)
- Cash App Pay
- Google Pay
- Apple Pay
- Afterpay/Clearpay
- Bank transfers (ACH)
- Gift cards

#### DarkVelocity Payments API (Disabled)
```
POST   /api/payments/                              # Create payment
GET    /api/payments/{orgId}/{paymentId}           # Get payment
POST   /api/payments/{orgId}/{paymentId}/process   # Process payment
POST   /api/payments/refunds                       # Create refund
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Card payments | ✅ | ⚠️ Via gateway |
| Cash payments | ✅ | ⚠️ Planned |
| Split payments | ✅ | ⚠️ Planned |
| Tips | ✅ | ❌ |
| Refunds | ✅ Full & partial | ⚠️ Basic |
| Payment search | ✅ | ❌ |
| Delayed capture | ✅ | ❌ |
| App fees | ✅ | ❌ |

**Gaps:**
1. No native payment processing (relies on external gateway)
2. Missing tip support
3. No delayed capture
4. No payment search

---

### 5. Inventory

#### Square Inventory API
```
GET    /v2/inventory/{catalog_object_id}           # Get inventory count
POST   /v2/inventory/batch-retrieve-counts         # Batch retrieve
POST   /v2/inventory/batch-change                  # Batch adjust
POST   /v2/inventory/batch-retrieve-changes        # Get change history
POST   /v2/inventory/physical-count                # Record physical count
POST   /v2/inventory/transfer                      # Transfer between locations
```

**Square Inventory States:**
- `IN_STOCK` - Available for sale
- `SOLD` - Sold to customer
- `RETURNED_BY_CUSTOMER` - Returned
- `WASTE` - Spoiled/damaged
- `IN_TRANSIT` - Being transferred
- `NONE` - Not tracked

**Square Inventory Change:**
```json
{
  "idempotency_key": "unique-key",
  "changes": [
    {
      "type": "ADJUSTMENT",
      "adjustment": {
        "location_id": "LOCATION_ID",
        "catalog_object_id": "ITEM_VAR_ID",
        "from_state": "NONE",
        "to_state": "IN_STOCK",
        "quantity": "10",
        "occurred_at": "2024-01-15T10:00:00Z"
      }
    }
  ]
}
```

#### DarkVelocity Inventory API (Disabled)
```
POST   /api/inventory/items                            # Create inventory item
GET    /api/inventory/items/{orgId}/{itemId}           # Get inventory
POST   /api/inventory/items/{orgId}/{itemId}/adjust    # Adjust inventory
POST   /api/inventory/counts                           # Create count
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Inventory states | ✅ Multiple states | ⚠️ Basic |
| Batch operations | ✅ | ❌ |
| Transfer between sites | ✅ | ⚠️ Planned |
| Change history | ✅ | ⚠️ Via events |
| Physical counts | ✅ | ⚠️ Basic |
| Auto-update from orders | ✅ | ⚠️ Planned |

**Gaps:**
1. No batch inventory operations
2. Limited inventory state model
3. No transfer endpoint

---

### 6. Customers

#### Square Customers API
```
POST   /v2/customers                         # Create customer
GET    /v2/customers/{customer_id}           # Get customer
PUT    /v2/customers/{customer_id}           # Update customer
DELETE /v2/customers/{customer_id}           # Delete customer
POST   /v2/customers/search                  # Search customers
POST   /v2/customers/groups                  # Create group
POST   /v2/customers/{id}/groups/{group_id}  # Add to group
```

**Square Customer Object:**
```json
{
  "given_name": "John",
  "family_name": "Doe",
  "email_address": "john@example.com",
  "phone_number": "+1-555-555-5555",
  "address": { ... },
  "birthday": "1990-01-15",
  "reference_id": "my-customer-001",
  "note": "VIP customer",
  "preferences": {
    "email_unsubscribed": false
  }
}
```

#### DarkVelocity Customers API (Disabled)
```
POST   /api/customers/                           # Create customer
GET    /api/customers/{orgId}/{customerId}       # Get customer
PUT    /api/customers/{orgId}/{customerId}       # Update customer
POST   /api/customers/search                     # Search customers
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Basic CRUD | ✅ | ⚠️ Disabled |
| Search | ✅ Advanced | ⚠️ Basic |
| Customer groups | ✅ | ❌ |
| Preferences | ✅ | ❌ |
| Loyalty integration | ✅ | ❌ |
| Purchase history | ✅ | ⚠️ Via orders |

---

### 7. Devices / Terminal

#### Square Terminal API
```
POST   /v2/devices/codes                     # Create device code
GET    /v2/devices/codes/{id}                # Get device code
GET    /v2/devices                           # List devices
GET    /v2/devices/{device_id}               # Get device
POST   /v2/terminals/actions                 # Create terminal action
GET    /v2/terminals/actions/{action_id}     # Get action
POST   /v2/terminals/actions/search          # Search actions
```

#### DarkVelocity Device API (Active)
```
POST   /api/device/code                       # Request device code (RFC 8628)
POST   /api/device/token                      # Poll for token
POST   /api/device/authorize                  # Authorize device
POST   /api/device/deny                       # Deny device
GET    /api/devices/{orgId}/{deviceId}        # Get device info
POST   /api/devices/{orgId}/{deviceId}/heartbeat   # Send heartbeat
POST   /api/devices/{orgId}/{deviceId}/suspend     # Suspend device
POST   /api/devices/{orgId}/{deviceId}/revoke      # Revoke device
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| Device pairing | ✅ Device codes | ✅ RFC 8628 Device Flow |
| Device management | ✅ | ✅ |
| Terminal actions | ✅ (checkout, refund) | ❌ |
| Heartbeat | ❌ | ✅ |
| Device suspend/revoke | ❌ | ✅ |

**Advantage:** DarkVelocity has richer device lifecycle management.

---

## Authentication Comparison

### Square OAuth 2.0
```
# Authorization URL
GET https://connect.squareup.com/oauth2/authorize
    ?client_id=CLIENT_ID
    &scope=PAYMENTS_WRITE+ORDERS_READ
    &session=false
    &state=STATE

# Token exchange
POST /oauth2/token
{
  "client_id": "CLIENT_ID",
  "client_secret": "CLIENT_SECRET",
  "code": "AUTHORIZATION_CODE",
  "grant_type": "authorization_code"
}
```

### DarkVelocity Authentication

**1. OAuth (Browser-based)**
```
GET  /api/oauth/login/{provider}    # Initiate OAuth flow
GET  /api/oauth/callback            # OAuth callback
GET  /api/oauth/userinfo            # Get user info (authenticated)
```

**2. Device Authorization (RFC 8628)**
```
POST /api/device/code               # Get device + user code
POST /api/device/token              # Poll for token
POST /api/device/authorize          # User authorizes
```

**3. PIN Authentication (Staff)**
```
POST /api/auth/pin                  # Login with PIN
POST /api/auth/logout               # Logout
POST /api/auth/refresh              # Refresh token
```

**Comparison:**
| Feature | Square | DarkVelocity |
|---------|--------|--------------|
| OAuth 2.0 | ✅ | ✅ |
| Device Flow | ❌ | ✅ (RFC 8628) |
| Staff PIN login | ❌ | ✅ |
| Token refresh | ✅ | ✅ |
| Scoped permissions | ✅ | ⚠️ Role-based |

**Advantage:** DarkVelocity has more flexible auth for POS scenarios (staff PIN, device flow).

---

## API Design Pattern Comparison

### URL Structure

**Square:**
```
/v2/{resource}                      # Collection
/v2/{resource}/{id}                 # Single resource
/v2/{resource}/{id}/{sub-resource}  # Nested resources
```

**DarkVelocity:**
```
/api/{resource}/{orgId}/{entityId}              # Org-scoped resource
/api/{resource}/{orgId}/{siteId}/{entityId}     # Site-scoped resource
```

### Request/Response Format

**Square:** Standard JSON with idempotency keys
```json
{
  "idempotency_key": "unique-key-12345",
  "order": { ... }
}
```

**DarkVelocity:** HAL+JSON with hypermedia links
```json
{
  "_links": {
    "self": { "href": "/api/orders/org1/order1" },
    "items": { "href": "/api/orders/org1/order1/items" }
  },
  "id": "order1",
  "status": "open"
}
```

### Error Handling

**Square:**
```json
{
  "errors": [
    {
      "category": "INVALID_REQUEST_ERROR",
      "code": "MISSING_REQUIRED_PARAMETER",
      "detail": "Missing required parameter: location_id",
      "field": "location_id"
    }
  ]
}
```

**DarkVelocity:**
```json
{
  "error": "invalid_request",
  "error_description": "Missing required parameter: location_id"
}
```

---

## Pricing Model Comparison

### Square
- **Payments:** 2.6% + $0.10 per tap/dip/swipe; 2.9% + $0.30 online
- **Orders API (non-Square payments):** 1% per transaction
- **APIs:** Free to use

### DarkVelocity
- Self-hosted, no per-transaction fees
- Customer provides own payment gateway
- Full control over costs

---

## Recommendations

### High Priority Gaps to Address

1. **Enable Core APIs** - Orders, Payments, Catalog, Inventory APIs are disabled
2. **Order Search** - Add order search/filter endpoint
3. **Batch Operations** - Add batch create/update for catalog and inventory
4. **Order Preview** - Add calculate endpoint for order totals
5. **Item Variations** - Support variations as first-class concept

### Medium Priority

6. **Fulfillments** - Add delivery/shipment fulfillment types
7. **Tips & Service Charges** - Add support in orders and payments
8. **Customer Groups** - Add customer segmentation
9. **Inventory Transfers** - Support multi-site inventory movement
10. **Payment Search** - Add payment history search

### Consider Adopting from Square

- **Idempotency Keys** - Make mandatory for mutations
- **Batch Endpoints** - Reduce API calls for bulk operations
- **Consistent Error Format** - Structured error categories and codes
- **Calculated Fields** - Return totals in responses automatically

### DarkVelocity Advantages to Preserve

- **Device Flow Auth** - Superior device onboarding (RFC 8628)
- **Staff PIN Auth** - Essential for POS quick access
- **Event Sourcing** - Full audit trail via Orleans
- **Multi-Tenant Design** - Organization/Site hierarchy
- **HAL+JSON** - Discoverable, self-documenting API
- **No Transaction Fees** - Self-hosted model

---

## Sources

- [Square API Reference](https://developer.squareup.com/reference/square)
- [Square Orders API](https://developer.squareup.com/docs/orders-api/what-it-does)
- [Square Catalog API](https://developer.squareup.com/docs/catalog-api/what-it-does)
- [Square Payments API](https://developer.squareup.com/reference/square/payments-api)
- [Square Inventory API](https://developer.squareup.com/reference/square/inventory-api)
- [Square Locations API](https://developer.squareup.com/docs/locations-api)
- [Square Checkout API](https://developer.squareup.com/docs/checkout-api)
