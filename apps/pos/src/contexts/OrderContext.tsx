import { createContext, useContext, useReducer, type ReactNode } from 'react'
import type { Order, MenuItem } from '../types'
import { orderReducer, initialOrderState, type OrderState, type OrderAction } from '../reducers/orderReducer'

interface OrderContextValue extends OrderState {
  createOrder: (orderType: Order['orderType']) => void
  addItem: (item: MenuItem, quantity?: number) => void
  removeItem: (lineId: string) => void
  changeQuantity: (lineId: string, quantity: number) => void
  applyDiscount: (lineId: string, amount: number, reason?: string) => void
  selectLine: (lineId: string | null) => void
  clearOrder: () => void
  completePayment: (paymentId: string) => void
  dispatch: React.Dispatch<OrderAction>
}

const OrderContext = createContext<OrderContextValue | null>(null)

export function OrderProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(orderReducer, initialOrderState)

  function createOrder(orderType: Order['orderType']) {
    dispatch({ type: 'ORDER_CREATED', payload: { orderType } })
  }

  function addItem(item: MenuItem, quantity = 1) {
    if (!state.order) {
      createOrder('direct_sale')
    }
    dispatch({ type: 'ITEM_ADDED', payload: { item, quantity } })
  }

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

  return (
    <OrderContext.Provider
      value={{
        ...state,
        createOrder,
        addItem,
        removeItem,
        changeQuantity,
        applyDiscount,
        selectLine,
        clearOrder,
        completePayment,
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
