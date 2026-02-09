import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useEmployees } from '../contexts/EmployeeContext'

function getStatusBadge(isClockedIn: boolean) {
  if (isClockedIn) return { className: 'badge badge-success', label: 'Clocked In' }
  return { className: 'badge badge-danger', label: 'Off Duty' }
}

export default function EmployeesPage() {
  const navigate = useNavigate()
  const { employees, isLoading, error } = useEmployees()
  const [searchTerm, setSearchTerm] = useState('')

  const filteredEmployees = employees.filter((emp) =>
    `${emp.firstName} ${emp.lastName}`.toLowerCase().includes(searchTerm.toLowerCase())
    || emp.employeeNumber.toLowerCase().includes(searchTerm.toLowerCase())
  )

  return (
    <>
      <hgroup>
        <h1>Employees</h1>
        <p>Manage staff, roles, and attendance</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'space-between' }}>
        <input
          type="search"
          placeholder="Search employees..."
          style={{ maxWidth: '300px' }}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          aria-label="Search employees"
        />
        <button onClick={() => navigate('/employees/new')}>Add Employee</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Employee #</th>
            <th>Name</th>
            <th>Email</th>
            <th>Type</th>
            <th>Roles</th>
            <th>Status</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {filteredEmployees.map((emp) => {
            const badge = getStatusBadge(emp.isClockedIn)
            return (
              <tr key={emp.id}>
                <td><code>{emp.employeeNumber}</code></td>
                <td><strong>{emp.firstName} {emp.lastName}</strong></td>
                <td>{emp.email}</td>
                <td>{emp.employmentType}</td>
                <td>{emp.roles.map(r => r.roleName).join(', ') || '-'}</td>
                <td><span className={badge.className}>{badge.label}</span></td>
                <td>
                  <button
                    className="secondary outline"
                    style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                    onClick={() => navigate(`/employees/${emp.id}`)}
                  >
                    View
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && filteredEmployees.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No employees found
        </p>
      )}
    </>
  )
}
