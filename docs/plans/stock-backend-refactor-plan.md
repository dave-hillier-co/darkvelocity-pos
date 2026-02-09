# Stock & Inventory Backend Refactor Plan

> **Date:** February 2026
> **Scope:** Rework the inventory/procurement domain model in the Orleans backend, then enable TypeScript reducer generation from grain definitions
> **Motivation:** Align the backend with a Product-centric domain model better suited to F&B (especially beverage-heavy operations), and establish a pattern where grain transition logic can be ported to client-side reducers for offline/optimistic UI

---

## Current State

### What Exists

| Area | Status | Notes |
|------|--------|-------|
| **InventoryGrain** | Complete | Per-ingredient-per-site, FIFO batches, WAC, deficit tracking |
| **StockTakeGrain** | Complete | Blind/open counts, variance, approval workflow |
| **InventoryTransferGrain** | Complete | Full lifecycle: request → approve → ship → receive |
| **VendorItemMappingGrain** | Complete | Fuzzy matching, learned patterns, confidence scoring |
| **PurchaseDocumentGrain** | Complete | Invoice/receipt OCR capture, line extraction, mapping |
| **IngredientGrain** | Complete | Master data, allergens, unit conversions, supplier links |
| **RecipeCmsGrain** | Complete | Versioned recipes, scaling, allergen inheritance, cost rollup |
| **ReorderSuggestionGrain** | Complete | Consumption-based reorder suggestions |
| **ExpiryMonitorGrain** | Complete | Batch expiry tracking and alerts |
| **SupplierGrain** | Complete | Standalone supplier aggregate |
| **PurchaseOrderGrain** | **Spec only** | Event storming doc exists, no grain implementation |
| **DeliveryGrain** | **Spec only** | Event storming doc exists, no grain implementation |

### What's Wrong

**1. No Product concept.** `Ingredient` conflates identity, stock tracking, and procurement. "Hendrick's Gin" is an ingredient — but it's also a product you buy in 70cl and 1L bottles (different SKUs) and sell as a single, double, or part of a G&T (different sale items). There's no entity that captures "this is the same thing in different forms."

**2. No SKU / container hierarchy.** Supplier units are ad-hoc conversion factors on `IngredientSupplierLink`. There's no formal model for "a case contains 6 bottles of 75cl each." `ContainerUnpacked` exists as a domain event but unpacking logic is caller-determined, not data-driven.

**3. No purchase order lifecycle.** The event-storming doc defines a full PO state machine (Draft → Submitted → Approved → Sent → Confirmed → Received → Closed) but no grain implements it. `PurchaseDocumentGrain` handles the *receiving* side (invoice capture) but not the *ordering* side.

**4. No delivery aggregate.** Delivery receipt is handled as events on `InventoryGrain` and `PurchaseDocumentGrain`, not as a standalone lifecycle with inspection, acceptance, and discrepancy tracking.

**5. Flat locations.** `StockBatch.Location` is a free-text string. No support for hierarchical locations (e.g., `Main Bar > Back Bar > Speed Rail`) or location-scoped stock takes and reporting.

**6. SaleItem → Product link is implicit.** MenuItem references a Recipe, which references Ingredients. The chain from "what was sold" to "what was consumed from stock" passes through two indirections with no explicit Product identity connecting them.

---

## Design Principles

These carry forward from the original stock app design and align with existing DarkVelocity conventions:

1. **Product is the canonical identity.** A Product represents a real thing (Hendrick's Gin, plain flour, Heineken lager) independent of how it's bought or sold.

2. **SKUs are Products in containers.** A SKU = Product + packaging. Cases, kegs, bottles, bags. Containers can be recursive (a case of 6 bottles of 75cl).

3. **SaleItems are Products in portions.** Already handled by Recipe + MenuItem. No new entity needed — just an explicit link from MenuItem to Product (in addition to the existing Recipe link).

4. **Events are past tense facts.** Already the convention. No change.

5. **Negative stock is the default.** Already the convention. No change.

6. **Grains are reducers.** `TransitionState(state, event) => state` is a fold. Design events and state so the transition logic is portable to TypeScript.

7. **Composition over inheritance.** Already the convention. New grains call existing grains, not subclass them.

---

## Phase 1: Domain Model Changes (Orleans/C#)

### 1.1 Introduce ProductGrain

A new org-scoped grain representing a canonical product.

**Grain key:** `org:{orgId}:product:{productId}`

**State:**

```
ProductState
├── ProductId: Guid
├── OrgId: Guid
├── Name: string
├── Description?: string
├── BaseUnit: string (e.g., "ml", "g", "each")
├── Category: string (e.g., "Beer", "Spirits", "Dairy")
├── Tags: List<string> (e.g., ["Draft", "Porter", "Vegan"])
├── ShelfLifeDays?: int
├── StorageRequirements?: string
├── Allergens: List<AllergenDeclaration>
├── Nutrition?: NutritionInfo
├── IsActive: bool
├── CreatedAt: DateTime
```

**Events:**

| Event | Fields |
|-------|--------|
| `ProductRegistered` | ProductId, Name, BaseUnit, Category, Tags, ShelfLifeDays |
| `ProductUpdated` | Changed fields (name, description, category, tags, etc.) |
| `ProductDeactivated` | Reason |
| `ProductReactivated` | — |
| `ProductAllergensUpdated` | Allergens list |

**Relationship to existing Ingredient:**

- `IngredientGrain` retains its role for recipe composition and costing, but gains a `ProductId` reference
- Existing ingredients that represent a single product (most of them) get a 1:1 mapping
- Composite ingredients produced by sub-recipes remain ingredient-only (no product)
- Migration: add `ProductId?` to `IngredientState`, create matching Products for existing ingredients

**What Product is NOT:**

- Not a stock-tracking entity (that remains `InventoryGrain`)
- Not a recipe component (that remains `Ingredient`)
- Not a menu entry (that remains `MenuItem`)
- It's the shared identity that connects all three

### 1.2 Add SKU / Container Model

A new org-scoped grain representing a specific purchasable form of a product.

**Grain key:** `org:{orgId}:sku:{skuId}`

**State:**

```
SkuState
├── SkuId: Guid
├── OrgId: Guid
├── ProductId: Guid
├── Code: string (e.g., "HEND-GIN-70CL", "HEIN-KEG-50L")
├── Barcode?: string
├── Description: string (e.g., "Hendrick's Gin 70cl Bottle")
├── ContainerDefinition: ContainerDefinition
├── DefaultSupplierId?: Guid
├── IsActive: bool
├── CreatedAt: DateTime
```

**ContainerDefinition (value object):**

```
ContainerDefinition
├── Unit: string (e.g., "case", "keg", "bottle")
├── Quantity: decimal (e.g., 6, 50, 0.75)
├── QuantityUnit: string (e.g., "bottle", "L", "L")
├── Inner?: ContainerDefinition (recursive — for "case of 6 bottles of 75cl")
```

Resolving to base units:
- `case of 6 × bottle of 0.75L` → `4.5L` → `4500ml` if product base unit is `ml`
- The grain exposes `GetBaseUnitQuantity()` which walks the hierarchy

**Events:**

| Event | Fields |
|-------|--------|
| `SkuRegistered` | SkuId, ProductId, Code, Description, ContainerDefinition |
| `SkuUpdated` | Changed fields |
| `SkuDeactivated` | Reason |
| `SkuBarcodeAssigned` | Barcode |

**Impact on existing code:**

- `IngredientSupplierLink` gains a `SkuId` — supplier prices are per-SKU, not per-ingredient
- `ContainerUnpacked` event handler uses `ContainerDefinition` instead of caller-provided quantities
- `InventoryGrain.ReceiveBatchAsync` can accept a `SkuId` and auto-resolve quantity in base units
- `VendorItemMappingGrain` maps vendor descriptions to SKUs (not directly to ingredients)

### 1.3 Implement PurchaseOrderGrain

The event-storming doc (`docs/event-storming/06-procurement.md`) already defines the full spec. Implement it.

**Grain key:** `org:{orgId}:site:{siteId}:purchaseorder:{poId}`

**State machine:** Draft → Submitted → (PendingApproval | AutoApproved) → Approved → Sent → Confirmed → PartiallyReceived → Received → Closed. Cancellable from any state except Closed.

**Events (from event storming, adapted to grain events):**

| Event | Purpose |
|-------|---------|
| `PurchaseOrderDrafted` | Initial creation with supplier, site |
| `PurchaseOrderLineAdded` | Add SKU line with quantity, expected price |
| `PurchaseOrderLineUpdated` | Modify line |
| `PurchaseOrderLineRemoved` | Remove line |
| `PurchaseOrderSubmitted` | Submit for approval |
| `PurchaseOrderApproved` | Manual or auto approval |
| `PurchaseOrderRejected` | Rejected with reason |
| `PurchaseOrderSentToSupplier` | Transmitted |
| `PurchaseOrderConfirmedBySupplier` | Supplier acknowledgement |
| `PurchaseOrderDeliveryDateUpdated` | Expected date change |
| `PurchaseOrderPartiallyReceived` | Some lines received via delivery |
| `PurchaseOrderFullyReceived` | All lines received |
| `PurchaseOrderCancelled` | Cancelled with reason |
| `PurchaseOrderClosed` | Invoice matched, lifecycle complete |

**Key design decisions:**

- PO lines reference SKUs (not raw ingredients) — "3 cases of Hendrick's 70cl" not "3 × ingredient"
- Partial delivery: each `DeliveryGrain` acceptance updates received quantities on PO lines
- Auto-approval threshold configurable per site
- Integration with `ReorderSuggestionGrain` to auto-populate draft POs

### 1.4 Implement DeliveryGrain

A standalone grain for the delivery receipt lifecycle.

**Grain key:** `org:{orgId}:site:{siteId}:delivery:{deliveryId}`

**State machine:** Arrived → InspectionInProgress → Accepted / Rejected / PartiallyAccepted

**Events:**

| Event | Purpose |
|-------|---------|
| `DeliveryArrived` | Delivery recorded with supplier, optional PO reference |
| `DeliveryLineRecorded` | Line item with SKU, expected vs received quantity |
| `DeliveryLineInspected` | Quality inspection result per line |
| `DeliveryLineAccepted` | Line accepted, triggers batch creation |
| `DeliveryLineRejected` | Line rejected with reason |
| `DeliveryLineSubstituted` | Different product received than ordered |
| `DeliveryAccepted` | Whole delivery accepted |
| `DeliveryRejected` | Whole delivery refused |
| `DeliveryDiscrepancyRecorded` | Discrepancy noted |

**Cross-grain calls on acceptance:**

1. For each accepted line → `InventoryGrain.ReceiveBatchAsync()`
2. If linked to PO → `PurchaseOrderGrain.RecordDeliveryLineAsync()`
3. Batch creation uses `SkuGrain.GetBaseUnitQuantity()` for unit resolution

**Substitution handling:** When a different SKU is delivered than ordered, `DeliveryLineSubstituted` records the original SKU, the substitute SKU, and the quantity. The PO line for the original remains partially unfulfilled; inventory receives the substitute.

### 1.5 Hierarchical Locations

A site-scoped registry grain for location hierarchy.

**Grain key:** `org:{orgId}:site:{siteId}:locations`

**State:**

```
LocationRegistryState
├── SiteId: Guid
├── Locations: Dictionary<Guid, LocationNode>
└── RootLocationIds: List<Guid>

LocationNode
├── LocationId: Guid
├── Name: string (e.g., "Walk-In Fridge")
├── ParentId?: Guid
├── Path: string (e.g., "/Kitchen/Walk-In Fridge")
├── SortOrder: int
├── IsActive: bool
```

**Events:**

| Event | Purpose |
|-------|---------|
| `LocationAdded` | New location in hierarchy |
| `LocationRenamed` | Name change |
| `LocationMoved` | Reparented |
| `LocationDeactivated` | Soft delete |

**Impact on existing code:**

- `StockBatch.Location` changes from `string?` to `Guid? LocationId`
- `StockTakeGrain` can scope counts by location subtree
- Reporting grains can aggregate by location hierarchy

### 1.6 Wire Product into Existing Grains

| Grain | Change |
|-------|--------|
| `IngredientState` | Add `ProductId?` field |
| `IngredientGrain` | `LinkToProductAsync(Guid productId)` |
| `MenuItem` | Add optional `ProductId?` for direct-sale items (wine by the bottle) |
| `InventoryGrain` | Accept `SkuId` on receive, resolve to base units |
| `VendorItemMappingGrain` | Map to SKUs instead of (or in addition to) ingredients |
| `RecipeCmsGrain` | Recipe ingredients reference Products (via ingredient's ProductId) |

### 1.7 Align Inventory Events for Reducer Portability

Review existing `TransitionState` methods and ensure events carry **all data needed to reconstruct state** without side-channel lookups. This is critical for client-side reducers that won't have access to other grains.

Current gaps:

| Event | Missing for reducer portability |
|-------|-------------------------------|
| `StockBatchReceived` | Has all needed data |
| `StockConsumed` | Needs batch breakdown (which batches were drained, in what quantities) — currently computed in `ConsumeFifoForState` but not recorded on the event |
| `StockAdjusted` | Has all needed data |
| `StockWrittenOff` | Needs batch breakdown (same issue as StockConsumed) |
| `StockTransferredOut` | Needs batch breakdown |

**The fix:** Enrich consumption events to include `BatchConsumptionDetail[]` — the list of `(batchId, quantity, unitCost)` consumed. The grain computes FIFO server-side and records the result on the event. The client reducer then just applies the pre-computed breakdown without needing FIFO logic.

This is the single most important change for reducer portability: **move FIFO computation from the Apply method to the command handler, and record the result on the event.**

Before (FIFO in Apply):
```csharp
case StockConsumed e:
    ConsumeFifoForState(state, e.Quantity);  // client can't replicate this
    break;
```

After (FIFO result on event):
```csharp
case StockConsumed e:
    foreach (var detail in e.BatchBreakdown)
    {
        var batch = state.Batches.First(b => b.Id == detail.BatchId);
        batch.Quantity -= detail.Quantity;  // deterministic, no FIFO logic needed
    }
    break;
```

---

## Phase 2: TypeScript Type Generation

### 2.1 Build a Roslyn Source Generator (or Build Script)

Read C# source files and emit TypeScript:

**Input:** Event records, State classes, enums

**Output:**

```typescript
// Generated from InventoryEvents.cs
export type InventoryEvent =
  | { type: 'InventoryInitialized'; ingredientId: string; organizationId: string; ... }
  | { type: 'StockBatchReceived'; ingredientId: string; batchId: string; quantity: number; ... }
  | { type: 'StockConsumed'; ingredientId: string; quantity: number; batchBreakdown: BatchConsumptionDetail[]; ... }
  | ...

// Generated from InventoryState.cs
export interface InventoryState {
  ingredientId: string;
  organizationId: string;
  siteId: string;
  ingredientName: string;
  batches: StockBatch[];
  quantityOnHand: number;
  ...
}

// Generated from enums
export type StockLevel = 'OutOfStock' | 'Low' | 'Normal' | 'AbovePar';
export type BatchStatus = 'Active' | 'Exhausted' | 'Expired' | 'WrittenOff';
```

**Type mapping rules:**

| C# | TypeScript |
|----|-----------|
| `Guid` | `string` |
| `decimal` / `int` / `long` | `number` |
| `DateTime` / `DateOnly` | `string` (ISO 8601) |
| `string` | `string` |
| `bool` | `boolean` |
| `List<T>` | `T[]` |
| `Dictionary<K,V>` | `Record<K, V>` |
| `T?` (nullable) | `T \| undefined` |
| `enum` | String literal union |
| `record` / `class` | `interface` |

**Discovery convention:** Any C# record implementing `I{GrainName}Event` is an event. Any class named `{GrainName}State` is state. The generator scans `src/DarkVelocity.Host/Domains/` for these patterns.

### 2.2 Generate to a Shared Package

Output to a package consumable by both frontend apps:

```
packages/
  domain-types/
    src/
      generated/
        inventory.ts       # Events + State + Enums
        stock-take.ts
        purchase-order.ts
        delivery.ts
        product.ts
        sku.ts
      index.ts
    package.json
```

Both `apps/pos` and `apps/backoffice` depend on `@darkvelocity/domain-types`.

---

## Phase 3: TypeScript Reducers

### 3.1 Hand-Write Reducers Using Generated Types

After Phase 1's event enrichment (batch breakdown on events), the reducers become straightforward:

```typescript
import type { InventoryState, InventoryEvent, StockBatch } from '@darkvelocity/domain-types';

export function inventoryReducer(state: InventoryState, event: InventoryEvent): InventoryState {
  switch (event.type) {
    case 'StockBatchReceived': {
      const newBatch: StockBatch = {
        id: event.batchId,
        batchNumber: event.batchNumber,
        receivedDate: event.occurredAt,
        expiryDate: event.expiryDate,
        quantity: event.quantity,
        originalQuantity: event.quantity,
        unitCost: event.unitCost,
        totalCost: event.quantity * event.unitCost,
        status: 'Active',
      };
      const batches = [...state.batches, newBatch];
      return {
        ...state,
        batches,
        lastReceivedAt: event.occurredAt,
        ...recalculate(batches),
      };
    }

    case 'StockConsumed': {
      // Batch breakdown is pre-computed by the server and recorded on the event
      // No FIFO logic needed client-side
      const batches = state.batches.map(b => {
        const detail = event.batchBreakdown.find(d => d.batchId === b.id);
        if (!detail) return b;
        const newQty = b.quantity - detail.quantity;
        return { ...b, quantity: newQty, status: newQty <= 0 ? 'Exhausted' as const : b.status };
      });
      return {
        ...state,
        batches,
        lastConsumedAt: event.occurredAt,
        ...recalculate(batches),
      };
    }
    // ...
  }
}
```

The key insight from Phase 1.7: because FIFO results are on the event, the reducer is a **deterministic projection** — no business logic, just applying pre-computed deltas. This makes the reducers thin and hard to get wrong.

### 3.2 Shared Test Fixtures

Verify C# grain and TS reducer produce identical state:

1. **Export from C# tests:** After each grain test, serialize the event sequence and final state as JSON fixtures
2. **Import in TS tests:** Feed the same event sequence through the TS reducer, assert same final state
3. **CI enforcement:** Both sides must pass the same fixtures

```
tests/
  fixtures/
    inventory/
      receive-single-batch.json       # { events: [...], expectedState: {...} }
      fifo-consumption.json
      negative-stock-deficit.json
      stock-adjustment-up.json
      stock-adjustment-down.json
```

```typescript
// TS test
import fixtures from '../fixtures/inventory/fifo-consumption.json';

test('FIFO consumption matches grain', () => {
  let state = initialInventoryState();
  for (const event of fixtures.events) {
    state = inventoryReducer(state, event);
  }
  expect(state).toEqual(fixtures.expectedState);
});
```

---

## Phase 4: Live Event Streaming to Client

### 4.1 SSE Endpoint

Expose a Server-Sent Events endpoint per grain type:

```
GET /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/events?after={version}
```

- Returns event stream starting from version N
- Each SSE message is one event in the same JSON shape as the generated types
- Client connects on page load, receives live events, applies to local reducer
- On reconnect, client sends last-seen version to catch up

### 4.2 Client Integration

```typescript
function useInventoryStream(orgId: string, siteId: string, ingredientId: string) {
  const [state, dispatch] = useReducer(inventoryReducer, initialInventoryState());

  useEffect(() => {
    // Initial load: fetch snapshot
    const snapshot = await fetchInventorySnapshot(orgId, siteId, ingredientId);
    dispatch({ type: 'SnapshotLoaded', ...snapshot });

    // Live stream: SSE
    const source = new EventSource(
      `/api/orgs/${orgId}/sites/${siteId}/inventory/${ingredientId}/events?after=${snapshot.version}`
    );
    source.onmessage = (msg) => {
      const event = JSON.parse(msg.data);
      dispatch(event);
    };
    return () => source.close();
  }, [orgId, siteId, ingredientId]);

  return state;
}
```

### 4.3 Offline Support

For the POS PWA (already has sql.js):

1. Buffer events in IndexedDB when offline
2. On reconnect, replay from last-known version
3. Local-only events (optimistic) get a `pending` flag, reconciled when server confirms

---

## Implementation Order

| Step | Phase | Depends On | Scope |
|------|-------|------------|-------|
| **1** | 1.7 | — | Enrich consumption events with batch breakdown (most impactful, least risk) |
| **2** | 1.1 | — | ProductGrain (new grain, no existing code changes) |
| **3** | 1.2 | 1.1 | SkuGrain + ContainerDefinition (new grain, references Product) |
| **4** | 1.6 | 1.1, 1.2 | Wire Product/SKU into IngredientState, VendorItemMapping, InventoryGrain |
| **5** | 1.3 | 1.2 | PurchaseOrderGrain (new grain, PO lines reference SKUs) |
| **6** | 1.4 | 1.2, 1.3 | DeliveryGrain (new grain, cross-calls to Inventory and PO) |
| **7** | 1.5 | — | LocationRegistryGrain (independent, can parallel with 2-6) |
| **8** | 2.1 | 1.* | TypeScript type generator |
| **9** | 2.2 | 2.1 | Shared domain-types package |
| **10** | 3.1 | 2.2, 1.7 | TypeScript reducers |
| **11** | 3.2 | 3.1 | Shared test fixtures |
| **12** | 4.1 | 3.1 | SSE endpoint |
| **13** | 4.2 | 4.1 | Client integration |
| **14** | 4.3 | 4.2 | Offline support |

Steps 1, 2, 3, and 7 can start in parallel. Steps 5 and 6 are the largest new grains.

---

## Migration Strategy

### Ingredient → Product Migration

1. For each existing `Ingredient` that represents a real product (not a sub-recipe output):
   - Create a matching `Product` with the same name, base unit, category, allergens
   - Set `IngredientState.ProductId` to the new product ID
2. Sub-recipe outputs (`IsSubRecipeOutput = true`) remain ingredient-only
3. Existing grain keys unchanged — `IngredientGrain` keeps its identity
4. No breaking API changes — existing endpoints continue to work, new Product endpoints are additive

### IngredientSupplierLink → SKU Migration

1. For each `IngredientSupplierLink` with a distinct `SupplierUnit`:
   - Create a `Sku` with a `ContainerDefinition` derived from the unit and conversion factor
   - Example: `SupplierUnit = "case"`, `ConversionToBaseUnit = 6000` (for ml) → SKU with container `case of 6 × bottle of 1000ml`
2. Some links won't map cleanly — flag for manual review
3. `VendorItemMappingGrain` existing mappings re-pointed to SKU IDs

### Event Versioning

New events are additive — no existing events change shape. The enrichment in step 1 (batch breakdown on consumption events) is a **new field** on existing event types, defaulting to empty for historical events. This means:

- Old events replay correctly (empty breakdown, FIFO recomputed in Apply)
- New events carry the breakdown (Apply uses it directly)
- Dual-path in `TransitionState`: if breakdown present, use it; if absent, fall back to `ConsumeFifoForState`

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Product/Ingredient duplication confusion | Medium | Medium | Clear naming: Product = "what it is", Ingredient = "what goes in a recipe" |
| SKU container hierarchy too complex for simple items | Low | Low | Flat container (quantity=1, unit=base) for simple items |
| Consumption event enrichment breaks existing tests | Medium | Low | Dual-path transition; old events still work |
| Type generator doesn't handle all C# patterns | Medium | Medium | Start with inventory domain only, expand incrementally |
| SSE at scale (many clients × many grains) | Medium | High | Fan-out via Orleans streams, not per-grain SSE |

---

## Success Criteria

1. **Phase 1 complete when:** All new grains pass tests, existing inventory tests still pass, consumption events carry batch breakdown
2. **Phase 2 complete when:** `npm run generate` produces TypeScript types matching all grain events and state
3. **Phase 3 complete when:** TypeScript reducers pass the same test fixtures as C# grains
4. **Phase 4 complete when:** Backoffice app renders inventory state from SSE events through local reducers
