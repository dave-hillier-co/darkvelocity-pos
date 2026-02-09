import type { Customer, CustomerPreferences } from '../api/customers'

export type CustomerAction =
  | { type: 'CUSTOMERS_LOADED'; payload: { customers: Customer[] } }
  | { type: 'CUSTOMER_SELECTED'; payload: { customer: Customer } }
  | { type: 'CUSTOMER_CREATED'; payload: { customer: Customer } }
  | { type: 'CUSTOMER_UPDATED'; payload: { customer: Customer } }
  | { type: 'CUSTOMER_DESELECTED' }
  | { type: 'LOYALTY_ENROLLED'; payload: { customerId: string; programId: string } }
  | { type: 'POINTS_EARNED'; payload: { customerId: string; points: number } }
  | { type: 'POINTS_REDEEMED'; payload: { customerId: string; points: number } }
  | { type: 'TAG_ADDED'; payload: { customerId: string; tag: string } }
  | { type: 'TAG_REMOVED'; payload: { customerId: string; tag: string } }
  | { type: 'PREFERENCES_UPDATED'; payload: { customerId: string; preferences: CustomerPreferences } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

export interface CustomerState {
  customers: Customer[]
  selectedCustomer: Customer | null
  isLoading: boolean
  error: string | null
}

export const initialCustomerState: CustomerState = {
  customers: [],
  selectedCustomer: null,
  isLoading: false,
  error: null,
}

export function customerReducer(state: CustomerState, action: CustomerAction): CustomerState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'CUSTOMERS_LOADED':
      return { ...state, customers: action.payload.customers, isLoading: false, error: null }

    case 'CUSTOMER_SELECTED':
      return { ...state, selectedCustomer: action.payload.customer }

    case 'CUSTOMER_DESELECTED':
      return { ...state, selectedCustomer: null }

    case 'CUSTOMER_CREATED':
      return {
        ...state,
        customers: [...state.customers, action.payload.customer],
        isLoading: false,
      }

    case 'CUSTOMER_UPDATED': {
      const updated = action.payload.customer
      return {
        ...state,
        customers: state.customers.map(c => c.id === updated.id ? updated : c),
        selectedCustomer: state.selectedCustomer?.id === updated.id ? updated : state.selectedCustomer,
        isLoading: false,
      }
    }

    case 'LOYALTY_ENROLLED': {
      const { customerId, programId } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId
            ? { ...c, loyalty: { ...c.loyalty!, programId, pointsBalance: 0, lifetimePoints: 0 } }
            : c
        ),
      }
    }

    case 'POINTS_EARNED': {
      const { customerId, points } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId && c.loyalty
            ? {
                ...c,
                loyalty: {
                  ...c.loyalty,
                  pointsBalance: c.loyalty.pointsBalance + points,
                  lifetimePoints: c.loyalty.lifetimePoints + points,
                },
              }
            : c
        ),
        selectedCustomer: state.selectedCustomer?.id === customerId && state.selectedCustomer.loyalty
          ? {
              ...state.selectedCustomer,
              loyalty: {
                ...state.selectedCustomer.loyalty,
                pointsBalance: state.selectedCustomer.loyalty.pointsBalance + points,
                lifetimePoints: state.selectedCustomer.loyalty.lifetimePoints + points,
              },
            }
          : state.selectedCustomer,
      }
    }

    case 'POINTS_REDEEMED': {
      const { customerId, points } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId && c.loyalty
            ? {
                ...c,
                loyalty: {
                  ...c.loyalty,
                  pointsBalance: c.loyalty.pointsBalance - points,
                },
              }
            : c
        ),
        selectedCustomer: state.selectedCustomer?.id === customerId && state.selectedCustomer.loyalty
          ? {
              ...state.selectedCustomer,
              loyalty: {
                ...state.selectedCustomer.loyalty,
                pointsBalance: state.selectedCustomer.loyalty.pointsBalance - points,
              },
            }
          : state.selectedCustomer,
      }
    }

    case 'TAG_ADDED': {
      const { customerId, tag } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId && !c.tags.includes(tag)
            ? { ...c, tags: [...c.tags, tag] }
            : c
        ),
        selectedCustomer: state.selectedCustomer?.id === customerId && !state.selectedCustomer.tags.includes(tag)
          ? { ...state.selectedCustomer, tags: [...state.selectedCustomer.tags, tag] }
          : state.selectedCustomer,
      }
    }

    case 'TAG_REMOVED': {
      const { customerId, tag } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId
            ? { ...c, tags: c.tags.filter(t => t !== tag) }
            : c
        ),
        selectedCustomer: state.selectedCustomer?.id === customerId
          ? { ...state.selectedCustomer, tags: state.selectedCustomer.tags.filter(t => t !== tag) }
          : state.selectedCustomer,
      }
    }

    case 'PREFERENCES_UPDATED': {
      const { customerId, preferences } = action.payload
      return {
        ...state,
        customers: state.customers.map(c =>
          c.id === customerId ? { ...c, preferences } : c
        ),
        selectedCustomer: state.selectedCustomer?.id === customerId
          ? { ...state.selectedCustomer, preferences }
          : state.selectedCustomer,
      }
    }

    default:
      return state
  }
}
