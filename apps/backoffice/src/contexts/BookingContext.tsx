import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { bookingReducer, initialBookingState, type BookingState, type BookingAction } from '../reducers/bookingReducer'
import * as bookingApi from '../api/bookings'
import * as tableApi from '../api/tables'
import * as floorPlanApi from '../api/floorPlans'
import type { Booking } from '../api/bookings'
import type { Table } from '../api/tables'
import type { FloorPlan } from '../api/floorPlans'

interface BookingContextValue extends BookingState {
  loadBookings: (bookings: Booking[]) => void
  loadTables: (tables: Table[]) => void
  loadFloorPlans: (floorPlans: FloorPlan[]) => void
  selectBooking: (booking: Booking) => void
  deselectBooking: () => void
  requestBooking: (data: Parameters<typeof bookingApi.requestBooking>[0]) => Promise<void>
  confirmBooking: (bookingId: string) => Promise<void>
  cancelBooking: (bookingId: string, reason: string, cancelledBy: string) => Promise<void>
  checkinBooking: (bookingId: string, checkedInBy: string) => Promise<void>
  seatBooking: (bookingId: string, tableId: string, tableNumber: string, seatedBy: string) => Promise<void>
  completeBooking: (bookingId: string) => Promise<void>
  createTable: (data: Parameters<typeof tableApi.createTable>[0]) => Promise<void>
  seatGuests: (tableId: string, data: Parameters<typeof tableApi.seatGuests>[1]) => Promise<void>
  clearTable: (tableId: string) => Promise<void>
  dispatch: React.Dispatch<BookingAction>
}

const BookingContext = createContext<BookingContextValue | null>(null)

export function BookingProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(bookingReducer, initialBookingState)

  function loadBookings(bookings: Booking[]) {
    dispatch({ type: 'BOOKINGS_LOADED', payload: { bookings } })
  }

  function loadTables(tables: Table[]) {
    dispatch({ type: 'TABLES_LOADED', payload: { tables } })
  }

  function loadFloorPlans(floorPlans: FloorPlan[]) {
    dispatch({ type: 'FLOOR_PLANS_LOADED', payload: { floorPlans } })
  }

  function selectBooking(booking: Booking) {
    dispatch({ type: 'BOOKING_SELECTED', payload: { booking } })
  }

  function deselectBooking() {
    dispatch({ type: 'BOOKING_DESELECTED' })
  }

  async function requestBooking(data: Parameters<typeof bookingApi.requestBooking>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await bookingApi.requestBooking(data)
      const booking = await bookingApi.getBooking(result.id)
      dispatch({ type: 'BOOKING_REQUESTED', payload: { booking } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function confirmBooking(bookingId: string) {
    try {
      await bookingApi.confirmBooking(bookingId)
      dispatch({ type: 'BOOKING_CONFIRMED', payload: { bookingId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function cancelBooking(bookingId: string, reason: string, cancelledBy: string) {
    try {
      await bookingApi.cancelBooking(bookingId, { reason, cancelledBy })
      dispatch({ type: 'BOOKING_CANCELLED', payload: { bookingId, reason } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function checkinBooking(bookingId: string, checkedInBy: string) {
    try {
      const result = await bookingApi.checkinBooking(bookingId, { checkedInBy })
      dispatch({ type: 'BOOKING_CHECKED_IN', payload: { bookingId, arrivedAt: result.arrivedAt } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function seatBooking(bookingId: string, tableId: string, tableNumber: string, seatedBy: string) {
    try {
      await bookingApi.seatBooking(bookingId, { tableId, tableNumber, seatedBy })
      dispatch({ type: 'BOOKING_SEATED', payload: { bookingId, tableId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function completeBooking(bookingId: string) {
    try {
      await bookingApi.completeBooking(bookingId)
      dispatch({ type: 'BOOKING_COMPLETED', payload: { bookingId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function createTable(data: Parameters<typeof tableApi.createTable>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await tableApi.createTable(data)
      const table = await tableApi.getTable(result.id)
      dispatch({ type: 'TABLES_LOADED', payload: { tables: [...state.tables, table] } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function seatGuests(tableId: string, data: Parameters<typeof tableApi.seatGuests>[1]) {
    try {
      const result = await tableApi.seatGuests(tableId, data)
      dispatch({ type: 'TABLE_STATUS_CHANGED', payload: { tableId, status: result.status } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function clearTable(tableId: string) {
    try {
      const result = await tableApi.clearTable(tableId)
      dispatch({ type: 'TABLE_STATUS_CHANGED', payload: { tableId, status: result.status } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <BookingContext.Provider
      value={{
        ...state,
        loadBookings,
        loadTables,
        loadFloorPlans,
        selectBooking,
        deselectBooking,
        requestBooking,
        confirmBooking,
        cancelBooking,
        checkinBooking,
        seatBooking,
        completeBooking,
        createTable,
        seatGuests,
        clearTable,
        dispatch,
      }}
    >
      {children}
    </BookingContext.Provider>
  )
}

export function useBookings() {
  const context = useContext(BookingContext)
  if (!context) {
    throw new Error('useBookings must be used within a BookingProvider')
  }
  return context
}
