import type { Booking, BookingStatus } from '../api/bookings'
import type { Table } from '../api/tables'
import type { FloorPlan } from '../api/floorPlans'

export type BookingAction =
  | { type: 'BOOKINGS_LOADED'; payload: { bookings: Booking[] } }
  | { type: 'BOOKING_SELECTED'; payload: { booking: Booking } }
  | { type: 'BOOKING_DESELECTED' }
  | { type: 'BOOKING_REQUESTED'; payload: { booking: Booking } }
  | { type: 'BOOKING_CONFIRMED'; payload: { bookingId: string } }
  | { type: 'BOOKING_CANCELLED'; payload: { bookingId: string; reason: string } }
  | { type: 'BOOKING_CHECKED_IN'; payload: { bookingId: string; arrivedAt: string } }
  | { type: 'BOOKING_SEATED'; payload: { bookingId: string; tableId: string } }
  | { type: 'BOOKING_COMPLETED'; payload: { bookingId: string } }
  | { type: 'TABLES_LOADED'; payload: { tables: Table[] } }
  | { type: 'TABLE_STATUS_CHANGED'; payload: { tableId: string; status: string } }
  | { type: 'FLOOR_PLANS_LOADED'; payload: { floorPlans: FloorPlan[] } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

export interface BookingState {
  bookings: Booking[]
  selectedBooking: Booking | null
  tables: Table[]
  floorPlans: FloorPlan[]
  isLoading: boolean
  error: string | null
}

export const initialBookingState: BookingState = {
  bookings: [],
  selectedBooking: null,
  tables: [],
  floorPlans: [],
  isLoading: false,
  error: null,
}

function updateBookingStatus(bookings: Booking[], bookingId: string, status: BookingStatus, extra: Partial<Booking> = {}): Booking[] {
  return bookings.map(b =>
    b.id === bookingId ? { ...b, status, ...extra } : b
  )
}

export function bookingReducer(state: BookingState, action: BookingAction): BookingState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'BOOKINGS_LOADED':
      return { ...state, bookings: action.payload.bookings, isLoading: false, error: null }

    case 'BOOKING_SELECTED':
      return { ...state, selectedBooking: action.payload.booking }

    case 'BOOKING_DESELECTED':
      return { ...state, selectedBooking: null }

    case 'BOOKING_REQUESTED':
      return {
        ...state,
        bookings: [...state.bookings, action.payload.booking],
        isLoading: false,
      }

    case 'BOOKING_CONFIRMED': {
      const bookings = updateBookingStatus(state.bookings, action.payload.bookingId, 'Confirmed')
      return {
        ...state,
        bookings,
        selectedBooking: state.selectedBooking?.id === action.payload.bookingId
          ? { ...state.selectedBooking, status: 'Confirmed' }
          : state.selectedBooking,
      }
    }

    case 'BOOKING_CANCELLED': {
      const { bookingId, reason } = action.payload
      const bookings = updateBookingStatus(state.bookings, bookingId, 'Cancelled', { cancelReason: reason })
      return {
        ...state,
        bookings,
        selectedBooking: state.selectedBooking?.id === bookingId
          ? { ...state.selectedBooking, status: 'Cancelled', cancelReason: reason }
          : state.selectedBooking,
      }
    }

    case 'BOOKING_CHECKED_IN': {
      const { bookingId, arrivedAt } = action.payload
      const bookings = updateBookingStatus(state.bookings, bookingId, 'Arrived', { arrivedAt })
      return {
        ...state,
        bookings,
        selectedBooking: state.selectedBooking?.id === bookingId
          ? { ...state.selectedBooking, status: 'Arrived', arrivedAt }
          : state.selectedBooking,
      }
    }

    case 'BOOKING_SEATED': {
      const { bookingId } = action.payload
      const bookings = updateBookingStatus(state.bookings, bookingId, 'Seated')
      return {
        ...state,
        bookings,
        selectedBooking: state.selectedBooking?.id === bookingId
          ? { ...state.selectedBooking, status: 'Seated' }
          : state.selectedBooking,
      }
    }

    case 'BOOKING_COMPLETED': {
      const { bookingId } = action.payload
      const bookings = updateBookingStatus(state.bookings, bookingId, 'Completed')
      return {
        ...state,
        bookings,
        selectedBooking: state.selectedBooking?.id === bookingId
          ? { ...state.selectedBooking, status: 'Completed' }
          : state.selectedBooking,
      }
    }

    case 'TABLES_LOADED':
      return { ...state, tables: action.payload.tables, isLoading: false }

    case 'TABLE_STATUS_CHANGED': {
      const { tableId, status } = action.payload
      return {
        ...state,
        tables: state.tables.map(t =>
          t.id === tableId ? { ...t, status: status as Table['status'] } : t
        ),
      }
    }

    case 'FLOOR_PLANS_LOADED':
      return { ...state, floorPlans: action.payload.floorPlans, isLoading: false }

    default:
      return state
  }
}
