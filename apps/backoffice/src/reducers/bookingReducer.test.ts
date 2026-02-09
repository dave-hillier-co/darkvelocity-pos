import { describe, it, expect } from 'vitest'
import { bookingReducer, initialBookingState, type BookingState } from './bookingReducer'
import type { Booking } from '../api/bookings'
import type { Table } from '../api/tables'

function makeBooking(overrides: Partial<Booking> = {}): Booking {
  return {
    id: 'book-1',
    guest: { name: 'John Smith', phone: '+1234567890' },
    requestedTime: '2026-02-08T19:00:00Z',
    partySize: 4,
    status: 'Requested',
    source: 'Direct',
    confirmationCode: 'ABC123',
    _links: { self: { href: '/api/orgs/org-1/sites/site-1/bookings/book-1' } },
    ...overrides,
  }
}

function makeTable(overrides: Partial<Table> = {}): Table {
  return {
    id: 'table-1',
    number: '1',
    minCapacity: 2,
    maxCapacity: 4,
    shape: 'Square',
    status: 'Available',
    isCombinable: true,
    sortOrder: 1,
    _links: { self: { href: '/api/orgs/org-1/sites/site-1/tables/table-1' } },
    ...overrides,
  }
}

describe('bookingReducer', () => {
  it('handles BOOKINGS_LOADED', () => {
    const bookings = [makeBooking(), makeBooking({ id: 'book-2' })]
    const state = bookingReducer(
      { ...initialBookingState, isLoading: true },
      { type: 'BOOKINGS_LOADED', payload: { bookings } }
    )
    expect(state.bookings).toHaveLength(2)
    expect(state.isLoading).toBe(false)
  })

  it('handles BOOKING_SELECTED and BOOKING_DESELECTED', () => {
    const booking = makeBooking()
    let state = bookingReducer(initialBookingState, { type: 'BOOKING_SELECTED', payload: { booking } })
    expect(state.selectedBooking).toEqual(booking)

    state = bookingReducer(state, { type: 'BOOKING_DESELECTED' })
    expect(state.selectedBooking).toBeNull()
  })

  it('handles BOOKING_REQUESTED', () => {
    const booking = makeBooking()
    const state = bookingReducer(initialBookingState, { type: 'BOOKING_REQUESTED', payload: { booking } })
    expect(state.bookings).toHaveLength(1)
    expect(state.bookings[0].confirmationCode).toBe('ABC123')
  })

  it('handles BOOKING_CONFIRMED', () => {
    const booking = makeBooking()
    const initial: BookingState = {
      ...initialBookingState,
      bookings: [booking],
      selectedBooking: booking,
    }
    const state = bookingReducer(initial, { type: 'BOOKING_CONFIRMED', payload: { bookingId: 'book-1' } })
    expect(state.bookings[0].status).toBe('Confirmed')
    expect(state.selectedBooking?.status).toBe('Confirmed')
  })

  it('handles BOOKING_CANCELLED with reason', () => {
    const booking = makeBooking({ status: 'Confirmed' })
    const initial: BookingState = {
      ...initialBookingState,
      bookings: [booking],
      selectedBooking: booking,
    }
    const state = bookingReducer(initial, {
      type: 'BOOKING_CANCELLED',
      payload: { bookingId: 'book-1', reason: 'Customer requested' },
    })
    expect(state.bookings[0].status).toBe('Cancelled')
    expect(state.bookings[0].cancelReason).toBe('Customer requested')
    expect(state.selectedBooking?.cancelReason).toBe('Customer requested')
  })

  it('handles BOOKING_CHECKED_IN', () => {
    const booking = makeBooking({ status: 'Confirmed' })
    const initial: BookingState = { ...initialBookingState, bookings: [booking], selectedBooking: booking }
    const arrivedAt = '2026-02-08T19:05:00Z'
    const state = bookingReducer(initial, {
      type: 'BOOKING_CHECKED_IN',
      payload: { bookingId: 'book-1', arrivedAt },
    })
    expect(state.bookings[0].status).toBe('Arrived')
    expect(state.bookings[0].arrivedAt).toBe(arrivedAt)
    expect(state.selectedBooking?.arrivedAt).toBe(arrivedAt)
  })

  it('handles BOOKING_SEATED', () => {
    const booking = makeBooking({ status: 'Arrived' })
    const initial: BookingState = { ...initialBookingState, bookings: [booking], selectedBooking: booking }
    const state = bookingReducer(initial, {
      type: 'BOOKING_SEATED',
      payload: { bookingId: 'book-1', tableId: 'table-1' },
    })
    expect(state.bookings[0].status).toBe('Seated')
    expect(state.selectedBooking?.status).toBe('Seated')
  })

  it('handles BOOKING_COMPLETED', () => {
    const booking = makeBooking({ status: 'Seated' })
    const initial: BookingState = { ...initialBookingState, bookings: [booking], selectedBooking: booking }
    const state = bookingReducer(initial, { type: 'BOOKING_COMPLETED', payload: { bookingId: 'book-1' } })
    expect(state.bookings[0].status).toBe('Completed')
    expect(state.selectedBooking?.status).toBe('Completed')
  })

  it('handles TABLES_LOADED', () => {
    const tables = [makeTable(), makeTable({ id: 'table-2', number: '2' })]
    const state = bookingReducer(initialBookingState, { type: 'TABLES_LOADED', payload: { tables } })
    expect(state.tables).toHaveLength(2)
  })

  it('handles TABLE_STATUS_CHANGED', () => {
    const table = makeTable()
    const initial: BookingState = { ...initialBookingState, tables: [table] }
    const state = bookingReducer(initial, {
      type: 'TABLE_STATUS_CHANGED',
      payload: { tableId: 'table-1', status: 'Occupied' },
    })
    expect(state.tables[0].status).toBe('Occupied')
  })

  it('handles FLOOR_PLANS_LOADED', () => {
    const floorPlans = [{
      id: 'fp-1',
      name: 'Main Floor',
      isDefault: true,
      isActive: true,
      width: 800,
      height: 600,
      tableIds: [],
      sections: [],
      createdAt: '2026-01-01T00:00:00Z',
      _links: { self: { href: '/floor-plans/fp-1' } },
    }]
    const state = bookingReducer(initialBookingState, { type: 'FLOOR_PLANS_LOADED', payload: { floorPlans } })
    expect(state.floorPlans).toHaveLength(1)
    expect(state.floorPlans[0].name).toBe('Main Floor')
  })

  it('handles LOADING_FAILED', () => {
    const state = bookingReducer(
      { ...initialBookingState, isLoading: true },
      { type: 'LOADING_FAILED', payload: { error: 'Network error' } }
    )
    expect(state.isLoading).toBe(false)
    expect(state.error).toBe('Network error')
  })
})
