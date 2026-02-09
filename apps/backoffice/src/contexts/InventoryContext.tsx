import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { inventoryReducer, initialInventoryState, type InventoryReducerState, type InventoryAction } from '../reducers/inventoryReducer'
import * as inventoryApi from '../api/inventory'

interface InventoryContextValue extends InventoryReducerState {
  loadItems: (filter?: inventoryApi.InventorySearchFilter) => Promise<void>
  selectItem: (ingredientId: string) => Promise<void>
  deselectItem: () => void
  receiveBatch: (ingredientId: string, data: inventoryApi.ReceiveBatchRequest) => Promise<void>
  consumeStock: (ingredientId: string, data: inventoryApi.ConsumeStockRequest) => Promise<void>
  adjustInventory: (ingredientId: string, data: inventoryApi.AdjustInventoryRequest) => Promise<void>
  dispatch: React.Dispatch<InventoryAction>
}

const InventoryContext = createContext<InventoryContextValue | null>(null)

export function InventoryProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(inventoryReducer, initialInventoryState)

  async function loadItems(filter?: inventoryApi.InventorySearchFilter) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await inventoryApi.searchInventory(filter)
      dispatch({ type: 'INVENTORY_LOADED', payload: { items: result.items } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function selectItem(ingredientId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const item = await inventoryApi.getInventoryItem(ingredientId)
      dispatch({ type: 'ITEM_SELECTED', payload: { item } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  function deselectItem() {
    dispatch({ type: 'ITEM_DESELECTED' })
  }

  async function receiveBatch(ingredientId: string, data: inventoryApi.ReceiveBatchRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await inventoryApi.receiveBatch(ingredientId, data)
      const updated = await inventoryApi.getInventoryItem(ingredientId)
      dispatch({
        type: 'BATCH_RECEIVED',
        payload: { ingredientId, newQuantity: updated.currentQuantity },
      })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function consumeStock(ingredientId: string, data: inventoryApi.ConsumeStockRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await inventoryApi.consumeStock(ingredientId, data)
      dispatch({
        type: 'STOCK_CONSUMED',
        payload: { ingredientId, consumed: result.consumed, remaining: result.remaining },
      })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function adjustInventory(ingredientId: string, data: inventoryApi.AdjustInventoryRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await inventoryApi.adjustInventory(ingredientId, data)
      dispatch({
        type: 'INVENTORY_ADJUSTED',
        payload: { ingredientId, newQuantity: result.currentQuantity },
      })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <InventoryContext.Provider
      value={{
        ...state,
        loadItems,
        selectItem,
        deselectItem,
        receiveBatch,
        consumeStock,
        adjustInventory,
        dispatch,
      }}
    >
      {children}
    </InventoryContext.Provider>
  )
}

export function useInventory() {
  const context = useContext(InventoryContext)
  if (!context) {
    throw new Error('useInventory must be used within an InventoryProvider')
  }
  return context
}
