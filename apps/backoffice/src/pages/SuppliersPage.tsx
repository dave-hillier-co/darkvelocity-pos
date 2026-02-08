import type { Supplier } from '../types'

const sampleSuppliers: Supplier[] = [
  { id: '1', code: 'FRESH01', name: 'Fresh Foods Ltd', contactEmail: 'orders@freshfoods.com', paymentTermsDays: 30, leadTimeDays: 2, isActive: true },
  { id: '2', code: 'METRO01', name: 'Metro Wholesale', contactEmail: 'sales@metrowholesale.com', paymentTermsDays: 14, leadTimeDays: 1, isActive: true },
  { id: '3', code: 'SEAFOOD', name: 'Ocean Catch Seafood', contactEmail: 'supply@oceancatch.com', paymentTermsDays: 7, leadTimeDays: 1, isActive: true },
  { id: '4', code: 'DRINKS', name: 'Beverage Distributors', contactEmail: 'orders@bevdist.com', paymentTermsDays: 30, leadTimeDays: 3, isActive: true },
]

export default function SuppliersPage() {
  return (
    <>
      <hgroup>
        <h1>Suppliers</h1>
        <p>Manage your suppliers and procurement</p>
      </hgroup>

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search suppliers..."
          style={{ maxWidth: '300px' }}
        />
        <button>Add Supplier</button>
      </div>

      <table>
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
          {sampleSuppliers.map((supplier) => (
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
    </>
  )
}
