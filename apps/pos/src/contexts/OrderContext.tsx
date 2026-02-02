import { createContext, useContext, useReducer, useCallback, type ReactNode } from 'react'
import type { MenuItem, OrderType } from '../types'
import { orderReducer, initialOrderState, type OrderState, type OrderAction } from '../reducers/orderReducer'
import * as orderApi from '../api/orders'
import { useAuth } from './AuthContext'

interface OrderContextValue extends OrderState {
  createOrder: (orderType: OrderType) => void
  createOrderAsync: (orderType: OrderType) => Promise<void>
  addItem: (item: MenuItem, quantity?: number) => void
  addItemAsync: (item: MenuItem, quantity?: number) => Promise<void>
  removeItem: (lineId: string) => void
  removeItemAsync: (lineId: string) => Promise<void>
  changeQuantity: (lineId: string, quantity: number) => void
  applyDiscount: (lineId: string, amount: number, reason?: string) => void
  selectLine: (lineId: string | null) => void
  clearOrder: () => void
  completePayment: (paymentId: string) => void
  setKeypadValue: (value: string) => void
  enterEditMode: () => void
  exitEditMode: () => void
  toggleLine: (lineId: string) => void
  removeSelectedLines: () => void
  clearUnsentItems: () => void
  applyOrderDiscount: (amount: number, reason?: string) => void
  sendOrder: () => void
  sendOrderAsync: () => Promise<void>
  voidOrderAsync: (reason: string) => Promise<void>
  dispatch: React.Dispatch<OrderAction>
}

const OrderContext = createContext<OrderContextValue | null>(null)

export function OrderProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(orderReducer, initialOrderState)
  const { user } = useAuth()

  // Sync: Create order on backend
  const createOrderAsync = useCallback(async (orderType: OrderType) => {
    try {
      const result = await orderApi.createOrder({
        createdBy: user?.id || 'anonymous',
        type: orderType,
      })
      dispatch({
        type: 'ORDER_CREATED',
        payload: {
          orderType,
          orderId: result.id,
          orderNumber: result.orderNumber,
        },
      })
    } catch (error) {
      console.error('Failed to create order on server:', error)
      // Fall back to local order creation
      dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
    }
  }, [user])

  // Local: Create order locally (for offline mode)
  function createOrder(orderType: OrderType) {
    dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
  }

  // Sync: Add item and sync with backend
  const addItemAsync = useCallback(async (item: MenuItem, quantity?: number) => {
    if (!state.order) {
      await createOrderAsync('DirectSale')
    }

    const qty = quantity ?? (state.keypadValue ? parseInt(state.keypadValue, 10) : 1)
    const finalQty = isNaN(qty) ? 1 : qty

    // Update local state first for responsiveness
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity: finalQty } })

    if (state.keypadValue) {
      dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value: '' } })
    }

    // Sync with backend if order exists
    if (state.order?.id) {
      try {
        await orderApi.addOrderLine(state.order.id, {
          menuItemId: item.id,
          name: item.name,
          quantity: finalQty,
          unitPrice: item.price,
        })
      } catch (error) {
        console.error('Failed to add item on server:', error)
        // Local state is already updated, continue working offline
      }
    }
  }, [state.order, state.keypadValue, createOrderAsync])

  // Local: Add item locally
  function addItem(item: MenuItem, quantity?: number) {
    if (!state.order) {
      createOrder('DirectSale')
    }
    const qty = quantity ?? (state.keypadValue ? parseInt(state.keypadValue, 10) : 1)
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity: isNaN(qty) ? 1 : qty } })
    if (state.keypadValue) {
      dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value: '' } })
    }
  }

  // Sync: Remove item from backend
  const removeItemAsync = useCallback(async (lineId: string) => {
    dispatch({ type: 'ITEM_REMOVED', payload: { lineId } })

    if (state.order?.id) {
      try {
        await orderApi.removeOrderLine(state.order.id, lineId)
      } catch (error) {
        console.error('Failed to remove item on server:', error)
      }
    }
  }, [state.order])

  function removeItem(lineId: string) {
    dispatch({ type: 'ITEM_REMOVED', payload: { lineId } })
  }

  function changeQuantity(lineId: string, quantity: number) {
    dispatch({ type: 'QUANTITY_CHANGED', payload: { lineId, quantity } })
  }

  function applyDiscount(lineId: string, amount: number, reason?: string) {
    dispatch({ type: 'DISCOUNT_APPLIED', payload: { lineId, amount, reason } })
  }

  function selectLine(lineId: string | null) {
    dispatch({ type: 'LINE_SELECTED', payload: { lineId } })
  }

  function clearOrder() {
    dispatch({ type: 'ORDER_CLEARED' })
  }

  function completePayment(paymentId: string) {
    dispatch({ type: 'ORDER_PAID', payload: { paymentId } })
  }

  function setKeypadValue(value: string) {
    dispatch({ type: 'KEYPAD_VALUE_CHANGED', payload: { value } })
  }

  function enterEditMode() {
    dispatch({ type: 'EDIT_MODE_ENTERED' })
  }

  function exitEditMode() {
    dispatch({ type: 'EDIT_MODE_EXITED' })
  }

  function toggleLine(lineId: string) {
    dispatch({ type: 'LINE_TOGGLED', payload: { lineId } })
  }

  function removeSelectedLines() {
    if (state.selectedLineIds.length > 0) {
      dispatch({ type: 'LINES_REMOVED', payload: { lineIds: state.selectedLineIds } })
    }
  }

  function clearUnsentItems() {
    dispatch({ type: 'UNSENT_ITEMS_CLEARED' })
  }

  function applyOrderDiscount(amount: number, reason?: string) {
    dispatch({ type: 'ORDER_DISCOUNT_APPLIED', payload: { amount, reason } })
  }

  // Local: Send order (mark items as sent locally)
  function sendOrder() {
    dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
  }

  // Sync: Send order to kitchen via backend
  const sendOrderAsync = useCallback(async () => {
    if (!state.order?.id) {
      // No server order, just update locally
      dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
      return
    }

    try {
      const result = await orderApi.sendOrder(state.order.id, {
        sentBy: user?.id || 'anonymous',
      })
      dispatch({
        type: 'ORDER_SENT',
        payload: { sentAt: result.sentAt, status: 'Sent' },
      })
    } catch (error) {
      console.error('Failed to send order to server:', error)
      // Fall back to local update
      dispatch({ type: 'ORDER_SENT', payload: { sentAt: new Date().toISOString() } })
    }
  }, [state.order, user])

  // Sync: Void order on backend
  const voidOrderAsync = useCallback(async (reason: string) => {
    if (!state.order?.id) {
      dispatch({ type: 'ORDER_CLEARED' })
      return
    }

    try {
      await orderApi.voidOrder(state.order.id, {
        voidedBy: user?.id || 'anonymous',
        reason,
      })
      dispatch({ type: 'STATUS_CHANGED', payload: { status: 'Voided' } })
    } catch (error) {
      console.error('Failed to void order on server:', error)
      dispatch({ type: 'STATUS_CHANGED', payload: { status: 'Voided' } })
    }
  }, [state.order, user])

  return (
    <OrderContext.Provider
      value={{
        ...state,
        createOrder,
        createOrderAsync,
        addItem,
        addItemAsync,
        removeItem,
        removeItemAsync,
        changeQuantity,
        applyDiscount,
        selectLine,
        clearOrder,
        completePayment,
        setKeypadValue,
        enterEditMode,
        exitEditMode,
        toggleLine,
        removeSelectedLines,
        clearUnsentItems,
        applyOrderDiscount,
        sendOrder,
        sendOrderAsync,
        voidOrderAsync,
        dispatch,
      }}
    >
      {children}
    </OrderContext.Provider>
  )
}

export function useOrder() {
  const context = useContext(OrderContext)
  if (!context) {
    throw new Error('useOrder must be used within an OrderProvider')
  }
  return context
}
