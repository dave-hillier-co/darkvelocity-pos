import { createContext, useContext, useReducer, type ReactNode } from 'react'
import type { MenuCategory, MenuItem, CartItem, GuestSession } from '../types.ts'

interface OrderingState {
  categories: MenuCategory[]
  items: MenuItem[]
  linkType: 'TableQr' | 'TakeOut' | 'Kiosk' | null
  tableNumber: string | null
  sessionId: string | null
  cartItems: CartItem[]
  cartTotal: number
  orderId: string | null
  orderNumber: string | null
  status: 'loading' | 'browsing' | 'submitted' | 'error'
  error: string | null
}

type OrderingAction =
  | { type: 'MENU_LOADED'; categories: MenuCategory[]; items: MenuItem[]; linkType: string; tableNumber?: string }
  | { type: 'SESSION_STARTED'; sessionId: string }
  | { type: 'CART_UPDATED'; session: GuestSession }
  | { type: 'ORDER_SUBMITTED'; orderId: string; orderNumber: string }
  | { type: 'ERROR_OCCURRED'; error: string }

const initialState: OrderingState = {
  categories: [],
  items: [],
  linkType: null,
  tableNumber: null,
  sessionId: null,
  cartItems: [],
  cartTotal: 0,
  orderId: null,
  orderNumber: null,
  status: 'loading',
  error: null,
}

function orderingReducer(state: OrderingState, action: OrderingAction): OrderingState {
  switch (action.type) {
    case 'MENU_LOADED':
      return {
        ...state,
        categories: action.categories,
        items: action.items,
        linkType: action.linkType as OrderingState['linkType'],
        tableNumber: action.tableNumber ?? null,
        status: 'browsing',
        error: null,
      }
    case 'SESSION_STARTED':
      return {
        ...state,
        sessionId: action.sessionId,
      }
    case 'CART_UPDATED':
      return {
        ...state,
        cartItems: action.session.cartItems,
        cartTotal: action.session.cartTotal,
      }
    case 'ORDER_SUBMITTED':
      return {
        ...state,
        orderId: action.orderId,
        orderNumber: action.orderNumber,
        status: 'submitted',
      }
    case 'ERROR_OCCURRED':
      return {
        ...state,
        error: action.error,
        status: 'error',
      }
    default:
      return state
  }
}

interface OrderingContextValue extends OrderingState {
  dispatch: React.Dispatch<OrderingAction>
}

const OrderingContext = createContext<OrderingContextValue | null>(null)

export function OrderingProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(orderingReducer, initialState)

  return (
    <OrderingContext.Provider value={{ ...state, dispatch }}>
      {children}
    </OrderingContext.Provider>
  )
}

export function useOrdering(): OrderingContextValue {
  const context = useContext(OrderingContext)
  if (!context) throw new Error('useOrdering must be used within OrderingProvider')
  return context
}
