import { useEffect } from 'react'
import { useProcurement } from '../contexts/ProcurementContext'
import type { Delivery } from '../reducers/procurementReducer'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

function getStatusBadgeClass(status: Delivery['status']): string {
  switch (status) {
    case 'pending': return 'badge-warning'
    case 'accepted': return 'badge-success'
    case 'rejected': return 'badge-danger'
    default: return ''
  }
}

export default function DeliveriesPage() {
  const {
    deliveries,
    isLoading,
    error,
    statusFilter,
    loadDeliveries,
    acceptDelivery,
    rejectDelivery,
    setStatusFilter,
  } = useProcurement()

  useEffect(() => {
    loadDeliveries()
  }, [])

  const filteredDeliveries = statusFilter === 'all'
    ? deliveries
    : deliveries.filter((d: Delivery) => d.status === statusFilter)

  const pendingCount = deliveries.filter((d: Delivery) => d.status === 'pending').length

  return (
    <>
      <hgroup>
        <h1>Deliveries</h1>
        <p>Receive and manage supplier deliveries</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      {pendingCount > 0 && (
        <article style={{ marginBottom: '1rem', background: 'var(--pico-mark-background-color)', padding: '1rem' }}>
          <strong>{pendingCount} delivery{pendingCount > 1 ? 'ies' : ''} pending review</strong>
          <p style={{ margin: '0.5rem 0 0' }}>Check received items and accept or reject deliveries</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            className={statusFilter === 'all' ? '' : 'outline'}
            onClick={() => setStatusFilter('all')}
          >
            All
          </button>
          <button
            className={statusFilter === 'pending' ? '' : 'outline'}
            onClick={() => setStatusFilter('pending')}
          >
            Pending ({pendingCount})
          </button>
          <button
            className={statusFilter === 'accepted' ? '' : 'outline'}
            onClick={() => setStatusFilter('accepted')}
          >
            Accepted
          </button>
        </div>
        <button>Record Ad-hoc Delivery</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Delivery #</th>
            <th>PO #</th>
            <th>Supplier</th>
            <th>Status</th>
            <th>Lines</th>
            <th>Value</th>
            <th>Received</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredDeliveries.map((delivery: Delivery) => (
            <tr key={delivery.id}>
              <td>
                <strong>{delivery.deliveryNumber}</strong>
                {delivery.hasDiscrepancies && (
                  <span style={{ color: 'var(--pico-del-color)', marginLeft: '0.5rem' }} title="Has discrepancies">
                    !
                  </span>
                )}
              </td>
              <td>{delivery.purchaseOrderId || <span style={{ color: 'var(--pico-muted-color)' }}>Ad-hoc</span>}</td>
              <td>{delivery.supplierName}</td>
              <td>
                <span className={`badge ${getStatusBadgeClass(delivery.status)}`}>
                  {delivery.status.charAt(0).toUpperCase() + delivery.status.slice(1)}
                </span>
              </td>
              <td>{delivery.lineCount}</td>
              <td>{formatCurrency(delivery.totalValue)}</td>
              <td>{formatDate(delivery.receivedAt)}</td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    View
                  </button>
                  {delivery.status === 'pending' && (
                    <>
                      <button
                        className="outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => acceptDelivery(delivery.id)}
                      >
                        Accept
                      </button>
                      <button
                        className="secondary outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => rejectDelivery(delivery.id, 'Rejected from deliveries page')}
                      >
                        Reject
                      </button>
                    </>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && filteredDeliveries.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No deliveries found
        </p>
      )}
    </>
  )
}
