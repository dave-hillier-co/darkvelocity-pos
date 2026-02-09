import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { customerReducer, initialCustomerState, type CustomerState, type CustomerAction } from '../reducers/customerReducer'
import * as customerApi from '../api/customers'
import type { Customer, CustomerPreferences } from '../api/customers'

interface CustomerContextValue extends CustomerState {
  loadCustomers: (customers: Customer[]) => void
  selectCustomer: (customer: Customer) => void
  deselectCustomer: () => void
  createCustomer: (data: Parameters<typeof customerApi.createCustomer>[0]) => Promise<void>
  updateCustomer: (customerId: string, data: Parameters<typeof customerApi.updateCustomer>[1]) => Promise<void>
  enrollLoyalty: (customerId: string, data: Parameters<typeof customerApi.enrollLoyalty>[1]) => Promise<void>
  earnPoints: (customerId: string, data: Parameters<typeof customerApi.earnPoints>[1]) => Promise<void>
  redeemPoints: (customerId: string, data: Parameters<typeof customerApi.redeemPoints>[1]) => Promise<void>
  addTag: (customerId: string, tag: string) => Promise<void>
  removeTag: (customerId: string, tag: string) => Promise<void>
  updatePreferences: (customerId: string, preferences: Partial<CustomerPreferences>) => Promise<void>
  dispatch: React.Dispatch<CustomerAction>
}

const CustomerContext = createContext<CustomerContextValue | null>(null)

export function CustomerProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(customerReducer, initialCustomerState)

  function loadCustomers(customers: Customer[]) {
    dispatch({ type: 'CUSTOMERS_LOADED', payload: { customers } })
  }

  function selectCustomer(customer: Customer) {
    dispatch({ type: 'CUSTOMER_SELECTED', payload: { customer } })
  }

  function deselectCustomer() {
    dispatch({ type: 'CUSTOMER_DESELECTED' })
  }

  async function createCustomer(data: Parameters<typeof customerApi.createCustomer>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await customerApi.createCustomer(data)
      const customer = await customerApi.getCustomer(result.id)
      dispatch({ type: 'CUSTOMER_CREATED', payload: { customer } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function updateCustomer(customerId: string, data: Parameters<typeof customerApi.updateCustomer>[1]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const customer = await customerApi.updateCustomer(customerId, data)
      dispatch({ type: 'CUSTOMER_UPDATED', payload: { customer } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function enrollLoyalty(customerId: string, data: Parameters<typeof customerApi.enrollLoyalty>[1]) {
    try {
      await customerApi.enrollLoyalty(customerId, data)
      dispatch({ type: 'LOYALTY_ENROLLED', payload: { customerId, programId: data.programId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function earnPoints(customerId: string, data: Parameters<typeof customerApi.earnPoints>[1]) {
    try {
      await customerApi.earnPoints(customerId, data)
      dispatch({ type: 'POINTS_EARNED', payload: { customerId, points: data.points } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function redeemPoints(customerId: string, data: Parameters<typeof customerApi.redeemPoints>[1]) {
    try {
      await customerApi.redeemPoints(customerId, data)
      dispatch({ type: 'POINTS_REDEEMED', payload: { customerId, points: data.points } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function addTag(customerId: string, tag: string) {
    try {
      await customerApi.addTag(customerId, tag)
      dispatch({ type: 'TAG_ADDED', payload: { customerId, tag } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function removeTag(customerId: string, tag: string) {
    try {
      await customerApi.removeTag(customerId, tag)
      dispatch({ type: 'TAG_REMOVED', payload: { customerId, tag } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function updatePreferences(customerId: string, preferences: Partial<CustomerPreferences>) {
    try {
      await customerApi.updatePreferences(customerId, preferences)
      const fullPrefs = await customerApi.getPreferences(customerId)
      dispatch({ type: 'PREFERENCES_UPDATED', payload: { customerId, preferences: fullPrefs } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <CustomerContext.Provider
      value={{
        ...state,
        loadCustomers,
        selectCustomer,
        deselectCustomer,
        createCustomer,
        updateCustomer,
        enrollLoyalty,
        earnPoints,
        redeemPoints,
        addTag,
        removeTag,
        updatePreferences,
        dispatch,
      }}
    >
      {children}
    </CustomerContext.Provider>
  )
}

export function useCustomers() {
  const context = useContext(CustomerContext)
  if (!context) {
    throw new Error('useCustomers must be used within a CustomerProvider')
  }
  return context
}
