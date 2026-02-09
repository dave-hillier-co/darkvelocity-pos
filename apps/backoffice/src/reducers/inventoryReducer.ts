import type { InventoryState as InventoryItemState } from '../api/inventory'

export type InventoryAction =
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }
  | { type: 'INVENTORY_LOADED'; payload: { items: InventoryItemState[] } }
  | { type: 'ITEM_SELECTED'; payload: { item: InventoryItemState } }
  | { type: 'ITEM_DESELECTED' }
  | { type: 'BATCH_RECEIVED'; payload: { ingredientId: string; newQuantity: number } }
  | { type: 'STOCK_CONSUMED'; payload: { ingredientId: string; consumed: number; remaining: number } }
  | { type: 'INVENTORY_ADJUSTED'; payload: { ingredientId: string; newQuantity: number } }

export interface InventoryReducerState {
  items: InventoryItemState[]
  selectedItem: InventoryItemState | null
  isLoading: boolean
  error: string | null
}

export const initialInventoryState: InventoryReducerState = {
  items: [],
  selectedItem: null,
  isLoading: false,
  error: null,
}

export function inventoryReducer(state: InventoryReducerState, action: InventoryAction): InventoryReducerState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'INVENTORY_LOADED':
      return { ...state, isLoading: false, items: action.payload.items }

    case 'ITEM_SELECTED':
      return { ...state, selectedItem: action.payload.item }

    case 'ITEM_DESELECTED':
      return { ...state, selectedItem: null }

    case 'BATCH_RECEIVED': {
      const { ingredientId, newQuantity } = action.payload
      const updatedItems = state.items.map((item) =>
        item.ingredientId === ingredientId
          ? { ...item, currentQuantity: newQuantity, lastReceivedAt: new Date().toISOString() }
          : item
      )
      return {
        ...state,
        isLoading: false,
        items: updatedItems,
        selectedItem: state.selectedItem?.ingredientId === ingredientId
          ? { ...state.selectedItem, currentQuantity: newQuantity, lastReceivedAt: new Date().toISOString() }
          : state.selectedItem,
      }
    }

    case 'STOCK_CONSUMED': {
      const { ingredientId, remaining } = action.payload
      const updatedItems = state.items.map((item) =>
        item.ingredientId === ingredientId
          ? { ...item, currentQuantity: remaining, lastConsumedAt: new Date().toISOString() }
          : item
      )
      return {
        ...state,
        isLoading: false,
        items: updatedItems,
        selectedItem: state.selectedItem?.ingredientId === ingredientId
          ? { ...state.selectedItem, currentQuantity: remaining, lastConsumedAt: new Date().toISOString() }
          : state.selectedItem,
      }
    }

    case 'INVENTORY_ADJUSTED': {
      const { ingredientId, newQuantity } = action.payload
      const updatedItems = state.items.map((item) =>
        item.ingredientId === ingredientId
          ? { ...item, currentQuantity: newQuantity }
          : item
      )
      return {
        ...state,
        isLoading: false,
        items: updatedItems,
        selectedItem: state.selectedItem?.ingredientId === ingredientId
          ? { ...state.selectedItem, currentQuantity: newQuantity }
          : state.selectedItem,
      }
    }

    default:
      return state
  }
}
