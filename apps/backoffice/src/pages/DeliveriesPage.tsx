import { useState } from 'react'

interface Delivery {
  id: string
  deliveryNumber: string
  purchaseOrderNumber: string | null
  supplierName: string
  status: 'pending' | 'accepted' | 'rejected'
  lineCount: number
  totalValue: number
  hasDiscrepancies: boolean
  receivedAt: string
}

const sampleDeliveries: Delivery[] = [
  { id: '1', deliveryNumber: 'DEL-2026-0015', purchaseOrderNumber: 'PO-2026-0001', supplierName: 'Fresh Foods Ltd', status: 'pending', lineCount: 5, totalValue: 445.00, hasDiscrepancies: true, receivedAt: '2026-01-26' },
  { id: '2', deliveryNumber: 'DEL-2026-0014', purchaseOrderNumber: 'PO-2025-0089', supplierName: 'Beverage Distributors', status: 'accepted', lineCount: 8, totalValue: 620.00, hasDiscrepancies: false, receivedAt: '2026-01-25' },
  { id: '3', deliveryNumber: 'DEL-2026-0013', purchaseOrderNumber: null, supplierName: 'Quality Meats', status: 'accepted', lineCount: 2, totalValue: 180.00, hasDiscrepancies: false, receivedAt: '2026-01-24' },
  { id: '4', deliveryNumber: 'DEL-2026-0012', purchaseOrderNumber: 'PO-2025-0088', supplierName: 'Fresh Foods Ltd', status: 'accepted', lineCount: 4, totalValue: 280.00, hasDiscrepancies: true, receivedAt: '2026-01-22' },
]

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
  const [statusFilter, setStatusFilter] = useState<string>('all')

  const filteredDeliveries = statusFilter === 'all'
    ? sampleDeliveries
    : sampleDeliveries.filter((d) => d.status === statusFilter)

  const pendingCount = sampleDeliveries.filter((d) => d.status === 'pending').length

  return (
    <>
      <hgroup>
        <h1>Deliveries</h1>
        <p>Receive and manage supplier deliveries</p>
      </hgroup>

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

      <table>
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
          {filteredDeliveries.map((delivery) => (
            <tr key={delivery.id}>
              <td>
                <strong>{delivery.deliveryNumber}</strong>
                {delivery.hasDiscrepancies && (
                  <span style={{ color: 'var(--pico-del-color)', marginLeft: '0.5rem' }} title="Has discrepancies">
                    !
                  </span>
                )}
              </td>
              <td>{delivery.purchaseOrderNumber || <span style={{ color: 'var(--pico-muted-color)' }}>Ad-hoc</span>}</td>
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
                      <button className="outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                        Accept
                      </button>
                      <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
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

      {filteredDeliveries.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No deliveries found
        </p>
      )}
    </>
  )
}
