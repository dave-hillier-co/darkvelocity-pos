import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useBookings } from '../contexts/BookingContext'
import type { BookingStatus } from '../api/bookings'

function getStatusBadge(status: BookingStatus) {
  switch (status) {
    case 'Confirmed':
      return { className: 'badge badge-success', label: 'Confirmed' }
    case 'Arrived':
      return { className: 'badge badge-success', label: 'Arrived' }
    case 'Seated':
      return { className: 'badge badge-success', label: 'Seated' }
    case 'Completed':
      return { className: 'badge badge-success', label: 'Completed' }
    case 'Cancelled':
      return { className: 'badge badge-danger', label: 'Cancelled' }
    case 'NoShow':
      return { className: 'badge badge-danger', label: 'No Show' }
    case 'Requested':
      return { className: 'badge badge-warning', label: 'Requested' }
    case 'PendingDeposit':
      return { className: 'badge badge-warning', label: 'Pending Deposit' }
    default:
      return { className: 'badge', label: status }
  }
}

export default function BookingsPage() {
  const navigate = useNavigate()
  const { bookings, isLoading, error, confirmBooking, cancelBooking, checkinBooking } = useBookings()
  const [statusFilter, setStatusFilter] = useState<string>('all')

  const filteredBookings = statusFilter === 'all'
    ? bookings
    : bookings.filter((b) => b.status === statusFilter)

  return (
    <>
      <hgroup>
        <h1>Bookings</h1>
        <p>Manage reservations and table assignments</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          style={{ maxWidth: '200px' }}
          aria-label="Filter by status"
        >
          <option value="all">All statuses</option>
          <option value="Requested">Requested</option>
          <option value="Confirmed">Confirmed</option>
          <option value="Arrived">Arrived</option>
          <option value="Seated">Seated</option>
          <option value="Completed">Completed</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <nav>
          <ul>
            <li><button className="secondary outline" onClick={() => navigate('/bookings/floor-plans')}>Floor Plans</button></li>
            <li><button>New Booking</button></li>
          </ul>
        </nav>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Confirmation</th>
            <th>Guest</th>
            <th>Date / Time</th>
            <th>Party Size</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredBookings.map((booking) => {
            const badge = getStatusBadge(booking.status)
            return (
              <tr key={booking.id}>
                <td><code>{booking.confirmationCode}</code></td>
                <td><strong>{booking.guest.name}</strong></td>
                <td>{new Date(booking.requestedTime).toLocaleString()}</td>
                <td>{booking.partySize}</td>
                <td><span className={badge.className}>{badge.label}</span></td>
                <td>
                  <div style={{ display: 'flex', gap: '0.25rem' }}>
                    {booking.status === 'Requested' && (
                      <button
                        className="outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => confirmBooking(booking.id)}
                      >
                        Confirm
                      </button>
                    )}
                    {booking.status === 'Confirmed' && (
                      <button
                        className="outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => checkinBooking(booking.id, 'current-user')}
                      >
                        Check In
                      </button>
                    )}
                    {(booking.status === 'Requested' || booking.status === 'Confirmed' || booking.status === 'Arrived') && (
                      <button
                        className="secondary outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => cancelBooking(booking.id, 'Cancelled from backoffice', 'current-user')}
                      >
                        Cancel
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && filteredBookings.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No bookings found
        </p>
      )}
    </>
  )
}
