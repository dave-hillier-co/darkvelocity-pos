import { useEffect } from 'react'
import { useProcurement } from '../contexts/ProcurementContext'

export default function SuppliersPage() {
  const { suppliers, isLoading, error, loadSuppliers } = useProcurement()

  useEffect(() => {
    loadSuppliers()
  }, [])

  return (
    <>
      <hgroup>
        <h1>Suppliers</h1>
        <p>Manage your suppliers and procurement</p>
      </hgroup>

      {error && (
        <article style={{ background: 'var(--pico-del-color)', padding: '1rem', marginBottom: '1rem' }}>
          <p>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search suppliers..."
          style={{ maxWidth: '300px' }}
        />
        <button>Add Supplier</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Code</th>
            <th>Name</th>
            <th>Contact Email</th>
            <th>Payment Terms</th>
            <th>Lead Time</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {suppliers.map((supplier) => (
            <tr key={supplier.id}>
              <td><code>{supplier.code}</code></td>
              <td>{supplier.name}</td>
              <td>{supplier.contactEmail}</td>
              <td>{supplier.paymentTermsDays} days</td>
              <td>{supplier.leadTimeDays} day{supplier.leadTimeDays > 1 ? 's' : ''}</td>
              <td>
                <span className={`badge ${supplier.isActive ? 'badge-success' : 'badge-danger'}`}>
                  {supplier.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <button className="secondary outline" style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>
                  Edit
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && suppliers.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No suppliers found
        </p>
      )}
    </>
  )
}
