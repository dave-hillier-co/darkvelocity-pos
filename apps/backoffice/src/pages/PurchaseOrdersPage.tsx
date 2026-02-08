import { useState } from 'react'

interface PurchaseOrder {
  id: string
  orderNumber: string
  supplierName: string
  status: 'draft' | 'submitted' | 'partially_received' | 'received' | 'cancelled'
  lineCount: number
  orderTotal: number
  expectedDeliveryDate: string
  createdAt: string
}

const sampleOrders: PurchaseOrder[] = [
  { id: '1', orderNumber: 'PO-2026-0001', supplierName: 'Fresh Foods Ltd', status: 'submitted', lineCount: 5, orderTotal: 450.00, expectedDeliveryDate: '2026-01-28', createdAt: '2026-01-25' },
  { id: '2', orderNumber: 'PO-2026-0002', supplierName: 'Quality Meats', status: 'draft', lineCount: 3, orderTotal: 280.00, expectedDeliveryDate: '2026-01-30', createdAt: '2026-01-26' },
  { id: '3', orderNumber: 'PO-2025-0089', supplierName: 'Beverage Distributors', status: 'received', lineCount: 8, orderTotal: 620.00, expectedDeliveryDate: '2026-01-20', createdAt: '2026-01-18' },
  { id: '4', orderNumber: 'PO-2025-0088', supplierName: 'Fresh Foods Ltd', status: 'partially_received', lineCount: 6, orderTotal: 380.00, expectedDeliveryDate: '2026-01-22', createdAt: '2026-01-19' },
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

function getStatusBadgeClass(status: PurchaseOrder['status']): string {
  switch (status) {
    case 'draft': return 'badge-warning'
    case 'submitted': return 'badge-success'
    case 'partially_received': return 'badge-warning'
    case 'received': return 'badge-success'
    case 'cancelled': return 'badge-danger'
    default: return ''
  }
}

function getStatusLabel(status: PurchaseOrder['status']): string {
  switch (status) {
    case 'draft': return 'Draft'
    case 'submitted': return 'Submitted'
    case 'partially_received': return 'Partial'
    case 'received': return 'Received'
    case 'cancelled': return 'Cancelled'
    default: return status
  }
}

export default function PurchaseOrdersPage() {
  const [statusFilter, setStatusFilter] = useState<string>('all')

  const filteredOrders = statusFilter === 'all'
    ? sampleOrders
    : sampleOrders.filter((order) => order.status === statusFilter)

  return (
    <>
      <hgroup>
        <h1>Purchase Orders</h1>
        <p>Create and manage supplier orders</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button
            className={statusFilter === 'all' ? '' : 'outline'}
            onClick={() => setStatusFilter('all')}
          >
            All
          </button>
          <button
            className={statusFilter === 'draft' ? '' : 'outline'}
            onClick={() => setStatusFilter('draft')}
          >
            Draft
          </button>
          <button
            className={statusFilter === 'submitted' ? '' : 'outline'}
            onClick={() => setStatusFilter('submitted')}
          >
            Submitted
          </button>
          <button
            className={statusFilter === 'received' ? '' : 'outline'}
            onClick={() => setStatusFilter('received')}
          >
            Received
          </button>
        </div>
        <button>New Purchase Order</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Order #</th>
            <th>Supplier</th>
            <th>Status</th>
            <th>Lines</th>
            <th>Total</th>
            <th>Expected</th>
            <th>Created</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredOrders.map((order) => (
            <tr key={order.id}>
              <td><strong>{order.orderNumber}</strong></td>
              <td>{order.supplierName}</td>
              <td>
                <span className={`badge ${getStatusBadgeClass(order.status)}`}>
                  {getStatusLabel(order.status)}
                </span>
              </td>
              <td>{order.lineCount}</td>
              <td>{formatCurrency(order.orderTotal)}</td>
              <td>{formatDate(order.expectedDeliveryDate)}</td>
              <td>{formatDate(order.createdAt)}</td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    View
                  </button>
                  {order.status === 'submitted' && (
                    <button className="outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                      Receive
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {filteredOrders.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No purchase orders found
        </p>
      )}
    </>
  )
}
