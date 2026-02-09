import { useBookings } from '../contexts/BookingContext'

export default function FloorPlansPage() {
  const { floorPlans, tables, isLoading, error } = useBookings()

  return (
    <>
      <hgroup>
        <h1>Floor Plans</h1>
        <p>Manage dining room layouts and table assignments</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button>Add Floor Plan</button>
      </div>

      {floorPlans.length === 0 && !isLoading && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No floor plans configured
        </p>
      )}

      {floorPlans.map((plan) => (
        <article key={plan.id} aria-busy={isLoading}>
          <header>
            <hgroup>
              <h3>{plan.name}</h3>
              <p>
                {plan.width} x {plan.height}
                {plan.isDefault && <span className="badge badge-success" style={{ marginLeft: '0.5rem' }}>Default</span>}
                {!plan.isActive && <span className="badge badge-danger" style={{ marginLeft: '0.5rem' }}>Inactive</span>}
              </p>
            </hgroup>
          </header>

          <h4>Tables ({plan.tableIds.length})</h4>
          {plan.tableIds.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Number</th>
                  <th>Name</th>
                  <th>Capacity</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {plan.tableIds.map((tableId) => {
                  const table = tables.find((t) => t.id === tableId)
                  if (!table) return null
                  return (
                    <tr key={tableId}>
                      <td>{table.number}</td>
                      <td>{table.name ?? '-'}</td>
                      <td>{table.minCapacity}-{table.maxCapacity}</td>
                      <td>
                        <span className={
                          table.status === 'Available' ? 'badge badge-success' :
                          table.status === 'Occupied' ? 'badge badge-danger' :
                          'badge badge-warning'
                        }>
                          {table.status}
                        </span>
                      </td>
                      <td>
                        <button
                          className="secondary outline"
                          style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          ) : (
            <p style={{ color: 'var(--pico-muted-color)' }}>No tables assigned to this floor plan</p>
          )}

          {plan.sections.length > 0 && (
            <>
              <h4>Sections</h4>
              <ul>
                {plan.sections.map((section) => (
                  <li key={section.id}>
                    <strong>{section.name}</strong> - {section.tableIds.length} table{section.tableIds.length !== 1 ? 's' : ''}
                  </li>
                ))}
              </ul>
            </>
          )}
        </article>
      ))}
    </>
  )
}
