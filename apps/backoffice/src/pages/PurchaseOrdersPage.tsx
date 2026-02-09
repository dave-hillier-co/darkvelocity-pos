import { useEffect } from 'react'
import { useProcurement } from '../contexts/ProcurementContext'
import type { PurchaseDocument } from '../reducers/procurementReducer'

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

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'pending_review': return 'badge-warning'
    case 'confirmed': return 'badge-success'
    case 'processing': return 'badge-warning'
    case 'rejected': return 'badge-danger'
    default: return ''
  }
}

function getStatusLabel(status: string): string {
  switch (status) {
    case 'pending_review': return 'Pending Review'
    case 'confirmed': return 'Confirmed'
    case 'processing': return 'Processing'
    case 'rejected': return 'Rejected'
    default: return status
  }
}

export default function PurchaseOrdersPage() {
  const {
    purchaseDocuments,
    isLoading,
    error,
    statusFilter,
    loadPurchaseDocuments,
    setStatusFilter,
  } = useProcurement()

  useEffect(() => {
    loadPurchaseDocuments()
  }, [])

  const filteredDocuments = statusFilter === 'all'
    ? purchaseDocuments
    : purchaseDocuments.filter((doc: PurchaseDocument) => doc.status === statusFilter)

  return (
    <>
      <hgroup>
        <h1>Purchase Documents</h1>
        <p>Upload and manage invoices and purchase documents</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
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
            className={statusFilter === 'pending_review' ? '' : 'outline'}
            onClick={() => setStatusFilter('pending_review')}
          >
            Pending Review
          </button>
          <button
            className={statusFilter === 'confirmed' ? '' : 'outline'}
            onClick={() => setStatusFilter('confirmed')}
          >
            Confirmed
          </button>
        </div>
        <button>Upload Document</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>File</th>
            <th>Supplier</th>
            <th>Type</th>
            <th>Status</th>
            <th>Lines</th>
            <th>Total</th>
            <th>Date</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredDocuments.map((doc: PurchaseDocument) => (
            <tr key={doc.id}>
              <td><strong>{doc.fileName}</strong></td>
              <td>{doc.supplierName || <span style={{ color: 'var(--pico-muted-color)' }}>Unknown</span>}</td>
              <td>{doc.documentType}</td>
              <td>
                <span className={`badge ${getStatusBadgeClass(doc.status)}`}>
                  {getStatusLabel(doc.status)}
                </span>
              </td>
              <td>{doc.lineCount}</td>
              <td>{doc.totalAmount != null ? formatCurrency(doc.totalAmount) : '-'}</td>
              <td>{doc.documentDate ? formatDate(doc.documentDate) : formatDate(doc.createdAt)}</td>
              <td>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                    View
                  </button>
                  {doc.status === 'pending_review' && (
                    <button className="outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                      Confirm
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && filteredDocuments.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No purchase documents found
        </p>
      )}
    </>
  )
}
