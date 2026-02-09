import { describe, it, expect } from 'vitest'
import { customerReducer, initialCustomerState, type CustomerState } from './customerReducer'
import type { Customer } from '../api/customers'

function makeCustomer(overrides: Partial<Customer> = {}): Customer {
  return {
    id: 'cust-1',
    firstName: 'Alice',
    lastName: 'Smith',
    email: 'alice@example.com',
    source: 'Direct',
    preferences: {},
    tags: [],
    createdAt: '2026-01-01T00:00:00Z',
    _links: { self: { href: '/api/orgs/org-1/customers/cust-1' } },
    ...overrides,
  }
}

describe('customerReducer', () => {
  it('handles CUSTOMERS_LOADED', () => {
    const customers = [makeCustomer(), makeCustomer({ id: 'cust-2', firstName: 'Bob' })]
    const state = customerReducer(
      { ...initialCustomerState, isLoading: true },
      { type: 'CUSTOMERS_LOADED', payload: { customers } }
    )
    expect(state.customers).toHaveLength(2)
    expect(state.isLoading).toBe(false)
  })

  it('handles CUSTOMER_SELECTED and CUSTOMER_DESELECTED', () => {
    const customer = makeCustomer()
    let state = customerReducer(initialCustomerState, { type: 'CUSTOMER_SELECTED', payload: { customer } })
    expect(state.selectedCustomer).toEqual(customer)

    state = customerReducer(state, { type: 'CUSTOMER_DESELECTED' })
    expect(state.selectedCustomer).toBeNull()
  })

  it('handles CUSTOMER_CREATED', () => {
    const customer = makeCustomer()
    const state = customerReducer(initialCustomerState, { type: 'CUSTOMER_CREATED', payload: { customer } })
    expect(state.customers).toHaveLength(1)
    expect(state.customers[0].firstName).toBe('Alice')
  })

  it('handles CUSTOMER_UPDATED in list and selected', () => {
    const customer = makeCustomer()
    const updated = makeCustomer({ firstName: 'Alicia' })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, { type: 'CUSTOMER_UPDATED', payload: { customer: updated } })
    expect(state.customers[0].firstName).toBe('Alicia')
    expect(state.selectedCustomer?.firstName).toBe('Alicia')
  })

  it('handles POINTS_EARNED on list and selected customer', () => {
    const customer = makeCustomer({
      loyalty: {
        programId: 'prog-1',
        memberNumber: 'M001',
        tierId: 'tier-1',
        tierName: 'Gold',
        pointsBalance: 100,
        lifetimePoints: 500,
      },
    })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, {
      type: 'POINTS_EARNED',
      payload: { customerId: 'cust-1', points: 50 },
    })
    expect(state.customers[0].loyalty?.pointsBalance).toBe(150)
    expect(state.customers[0].loyalty?.lifetimePoints).toBe(550)
    expect(state.selectedCustomer?.loyalty?.pointsBalance).toBe(150)
  })

  it('handles POINTS_REDEEMED', () => {
    const customer = makeCustomer({
      loyalty: {
        programId: 'prog-1',
        memberNumber: 'M001',
        tierId: 'tier-1',
        tierName: 'Gold',
        pointsBalance: 100,
        lifetimePoints: 500,
      },
    })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, {
      type: 'POINTS_REDEEMED',
      payload: { customerId: 'cust-1', points: 30 },
    })
    expect(state.customers[0].loyalty?.pointsBalance).toBe(70)
    expect(state.selectedCustomer?.loyalty?.pointsBalance).toBe(70)
  })

  it('handles TAG_ADDED', () => {
    const customer = makeCustomer({ tags: ['vip'] })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, {
      type: 'TAG_ADDED',
      payload: { customerId: 'cust-1', tag: 'regular' },
    })
    expect(state.customers[0].tags).toEqual(['vip', 'regular'])
    expect(state.selectedCustomer?.tags).toEqual(['vip', 'regular'])
  })

  it('does not duplicate tags on TAG_ADDED', () => {
    const customer = makeCustomer({ tags: ['vip'] })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, {
      type: 'TAG_ADDED',
      payload: { customerId: 'cust-1', tag: 'vip' },
    })
    expect(state.customers[0].tags).toEqual(['vip'])
  })

  it('handles TAG_REMOVED', () => {
    const customer = makeCustomer({ tags: ['vip', 'regular'] })
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const state = customerReducer(initial, {
      type: 'TAG_REMOVED',
      payload: { customerId: 'cust-1', tag: 'vip' },
    })
    expect(state.customers[0].tags).toEqual(['regular'])
    expect(state.selectedCustomer?.tags).toEqual(['regular'])
  })

  it('handles PREFERENCES_UPDATED', () => {
    const customer = makeCustomer()
    const initial: CustomerState = {
      ...initialCustomerState,
      customers: [customer],
      selectedCustomer: customer,
    }
    const preferences = { dietaryRestrictions: ['vegetarian'], allergens: ['nuts'] }
    const state = customerReducer(initial, {
      type: 'PREFERENCES_UPDATED',
      payload: { customerId: 'cust-1', preferences },
    })
    expect(state.customers[0].preferences.dietaryRestrictions).toEqual(['vegetarian'])
    expect(state.selectedCustomer?.preferences.allergens).toEqual(['nuts'])
  })

  it('handles LOADING_FAILED', () => {
    const state = customerReducer(
      { ...initialCustomerState, isLoading: true },
      { type: 'LOADING_FAILED', payload: { error: 'Failed to fetch' } }
    )
    expect(state.isLoading).toBe(false)
    expect(state.error).toBe('Failed to fetch')
  })
})
