import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useCustomers } from '../contexts/CustomerContext'

export default function CustomersPage() {
  const navigate = useNavigate()
  const { customers, isLoading, error } = useCustomers()
  const [searchTerm, setSearchTerm] = useState('')

  const filteredCustomers = customers.filter((cust) =>
    `${cust.firstName} ${cust.lastName}`.toLowerCase().includes(searchTerm.toLowerCase())
    || (cust.email ?? '').toLowerCase().includes(searchTerm.toLowerCase())
  )

  return (
    <>
      <hgroup>
        <h1>Customers</h1>
        <p>Manage customer profiles, loyalty, and preferences</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search customers..."
          style={{ maxWidth: '300px' }}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          aria-label="Search customers"
        />
        <button onClick={() => navigate('/customers/new')}>Add Customer</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Phone</th>
            <th>Loyalty</th>
            <th>Tags</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredCustomers.map((cust) => (
            <tr key={cust.id}>
              <td><strong>{cust.firstName} {cust.lastName}</strong></td>
              <td>{cust.email ?? '-'}</td>
              <td>{cust.phone ?? '-'}</td>
              <td>
                {cust.loyalty ? (
                  <span className="badge badge-success">
                    {cust.loyalty.tierName} ({cust.loyalty.pointsBalance} pts)
                  </span>
                ) : (
                  <span className="badge badge-warning">Not enrolled</span>
                )}
              </td>
              <td>{cust.tags.length > 0 ? cust.tags.join(', ') : '-'}</td>
              <td>
                <button
                  className="secondary outline"
                  style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                  onClick={() => navigate(`/customers/${cust.id}`)}
                >
                  View
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {!isLoading && filteredCustomers.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No customers found
        </p>
      )}
    </>
  )
}
