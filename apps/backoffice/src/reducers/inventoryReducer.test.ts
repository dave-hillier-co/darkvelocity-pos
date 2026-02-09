import { describe, it, expect } from 'vitest'
import { inventoryReducer, initialInventoryState } from './inventoryReducer'
import type { InventoryReducerState, InventoryAction } from './inventoryReducer'
import type { InventoryState as InventoryItemState } from '../api/inventory'

function makeItem(overrides: Partial<InventoryItemState> = {}): InventoryItemState {
  return {
    ingredientId: 'flour-001',
    ingredientName: 'All-Purpose Flour',
    sku: 'FLR-AP-001',
    unit: 'kg',
    category: 'Dry Goods',
    currentQuantity: 50,
    reorderPoint: 10,
    parLevel: 100,
    ...overrides,
  }
}

describe('inventoryReducer', () => {
  it('returns initial state for unknown action', () => {
    const result = inventoryReducer(initialInventoryState, { type: 'UNKNOWN' } as unknown as InventoryAction)
    expect(result).toEqual(initialInventoryState)
  })

  describe('LOADING_STARTED', () => {
    it('sets isLoading true and clears error', () => {
      const state: InventoryReducerState = { ...initialInventoryState, error: 'previous error' }
      const result = inventoryReducer(state, { type: 'LOADING_STARTED' })
      expect(result.isLoading).toBe(true)
      expect(result.error).toBeNull()
    })
  })

  describe('LOADING_FAILED', () => {
    it('sets error and clears isLoading', () => {
      const state: InventoryReducerState = { ...initialInventoryState, isLoading: true }
      const result = inventoryReducer(state, { type: 'LOADING_FAILED', payload: { error: 'Network error' } })
      expect(result.isLoading).toBe(false)
      expect(result.error).toBe('Network error')
    })
  })

  describe('INVENTORY_LOADED', () => {
    it('replaces items list', () => {
      const items = [makeItem(), makeItem({ ingredientId: 'tomato-001', ingredientName: 'Tomatoes' })]
      const result = inventoryReducer(initialInventoryState, { type: 'INVENTORY_LOADED', payload: { items } })
      expect(result.items).toEqual(items)
      expect(result.isLoading).toBe(false)
    })
  })

  describe('ITEM_SELECTED', () => {
    it('sets selectedItem', () => {
      const item = makeItem()
      const result = inventoryReducer(initialInventoryState, { type: 'ITEM_SELECTED', payload: { item } })
      expect(result.selectedItem).toEqual(item)
    })
  })

  describe('ITEM_DESELECTED', () => {
    it('clears selectedItem', () => {
      const state: InventoryReducerState = { ...initialInventoryState, selectedItem: makeItem() }
      const result = inventoryReducer(state, { type: 'ITEM_DESELECTED' })
      expect(result.selectedItem).toBeNull()
    })
  })

  describe('BATCH_RECEIVED', () => {
    it('updates quantity for the item in the list', () => {
      const items = [makeItem({ currentQuantity: 50 })]
      const state: InventoryReducerState = { ...initialInventoryState, items }
      const result = inventoryReducer(state, {
        type: 'BATCH_RECEIVED',
        payload: { ingredientId: 'flour-001', newQuantity: 75 },
      })
      expect(result.items[0].currentQuantity).toBe(75)
      expect(result.items[0].lastReceivedAt).toBeDefined()
    })

    it('updates selectedItem if it matches', () => {
      const item = makeItem({ currentQuantity: 50 })
      const state: InventoryReducerState = { ...initialInventoryState, items: [item], selectedItem: item }
      const result = inventoryReducer(state, {
        type: 'BATCH_RECEIVED',
        payload: { ingredientId: 'flour-001', newQuantity: 75 },
      })
      expect(result.selectedItem?.currentQuantity).toBe(75)
    })

    it('does not update unrelated items', () => {
      const items = [makeItem(), makeItem({ ingredientId: 'tomato-001', currentQuantity: 30 })]
      const state: InventoryReducerState = { ...initialInventoryState, items }
      const result = inventoryReducer(state, {
        type: 'BATCH_RECEIVED',
        payload: { ingredientId: 'flour-001', newQuantity: 75 },
      })
      expect(result.items[1].currentQuantity).toBe(30)
    })
  })

  describe('STOCK_CONSUMED', () => {
    it('updates quantity to remaining amount', () => {
      const items = [makeItem({ currentQuantity: 50 })]
      const state: InventoryReducerState = { ...initialInventoryState, items }
      const result = inventoryReducer(state, {
        type: 'STOCK_CONSUMED',
        payload: { ingredientId: 'flour-001', consumed: 5, remaining: 45 },
      })
      expect(result.items[0].currentQuantity).toBe(45)
      expect(result.items[0].lastConsumedAt).toBeDefined()
    })

    it('allows negative stock (inventory philosophy)', () => {
      const items = [makeItem({ currentQuantity: 2 })]
      const state: InventoryReducerState = { ...initialInventoryState, items }
      const result = inventoryReducer(state, {
        type: 'STOCK_CONSUMED',
        payload: { ingredientId: 'flour-001', consumed: 5, remaining: -3 },
      })
      expect(result.items[0].currentQuantity).toBe(-3)
    })

    it('updates selectedItem if it matches', () => {
      const item = makeItem({ currentQuantity: 50 })
      const state: InventoryReducerState = { ...initialInventoryState, items: [item], selectedItem: item }
      const result = inventoryReducer(state, {
        type: 'STOCK_CONSUMED',
        payload: { ingredientId: 'flour-001', consumed: 10, remaining: 40 },
      })
      expect(result.selectedItem?.currentQuantity).toBe(40)
    })
  })

  describe('INVENTORY_ADJUSTED', () => {
    it('sets quantity to new value', () => {
      const items = [makeItem({ currentQuantity: 50 })]
      const state: InventoryReducerState = { ...initialInventoryState, items }
      const result = inventoryReducer(state, {
        type: 'INVENTORY_ADJUSTED',
        payload: { ingredientId: 'flour-001', newQuantity: 48 },
      })
      expect(result.items[0].currentQuantity).toBe(48)
    })

    it('updates selectedItem if it matches', () => {
      const item = makeItem({ currentQuantity: 50 })
      const state: InventoryReducerState = { ...initialInventoryState, items: [item], selectedItem: item }
      const result = inventoryReducer(state, {
        type: 'INVENTORY_ADJUSTED',
        payload: { ingredientId: 'flour-001', newQuantity: 48 },
      })
      expect(result.selectedItem?.currentQuantity).toBe(48)
    })
  })
})
