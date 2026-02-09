import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useEmployees } from '../contexts/EmployeeContext'
import * as employeeApi from '../api/employees'

export default function EmployeeDetailPage() {
  const { employeeId } = useParams<{ employeeId: string }>()
  const navigate = useNavigate()
  const { selectedEmployee, selectEmployee, deselectEmployee, clockIn, clockOut, assignRole, removeRole, updateEmployee, error } = useEmployees()
  const [isLoadingDetail, setIsLoadingDetail] = useState(false)

  useEffect(() => {
    if (!employeeId) return
    setIsLoadingDetail(true)
    employeeApi.getEmployee(employeeId)
      .then((emp) => selectEmployee(emp))
      .catch(console.error)
      .finally(() => setIsLoadingDetail(false))

    return () => { deselectEmployee() }
  }, [employeeId])

  if (isLoadingDetail) {
    return <article aria-busy="true">Loading employee details...</article>
  }

  if (!selectedEmployee) {
    return (
      <article>
        <p>Employee not found</p>
        <button onClick={() => navigate('/employees')}>Back to Employees</button>
      </article>
    )
  }

  const emp = selectedEmployee

  return (
    <>
      <nav aria-label="Breadcrumb">
        <ul>
          <li><a href="#" onClick={(e) => { e.preventDefault(); navigate('/employees') }} className="secondary">Employees</a></li>
          <li>{emp.firstName} {emp.lastName}</li>
        </ul>
      </nav>

      <hgroup>
        <h1>{emp.firstName} {emp.lastName}</h1>
        <p>Employee #{emp.employeeNumber} &middot; {emp.employmentType}</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
        <article>
          <header><h3>Details</h3></header>
          <dl>
            <dt>Email</dt>
            <dd>{emp.email}</dd>
            <dt>Employment Type</dt>
            <dd>{emp.employmentType}</dd>
            {emp.hireDate && (
              <>
                <dt>Hire Date</dt>
                <dd>{emp.hireDate}</dd>
              </>
            )}
            {emp.hourlyRate !== undefined && (
              <>
                <dt>Hourly Rate</dt>
                <dd>{emp.hourlyRate}</dd>
              </>
            )}
          </dl>
        </article>

        <article>
          <header><h3>Attendance</h3></header>
          <p>
            Status:{' '}
            <span className={emp.isClockedIn ? 'badge badge-success' : 'badge badge-danger'}>
              {emp.isClockedIn ? 'Clocked In' : 'Off Duty'}
            </span>
          </p>
          {emp.isClockedIn ? (
            <button className="secondary" onClick={() => clockOut(emp.id)}>
              Clock Out
            </button>
          ) : (
            <button onClick={() => clockIn(emp.id, emp.defaultSiteId)}>
              Clock In
            </button>
          )}
        </article>
      </section>

      <section>
        <article>
          <header>
            <h3>Assigned Roles</h3>
          </header>
          {emp.roles.length === 0 ? (
            <p style={{ color: 'var(--pico-muted-color)' }}>No roles assigned</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Role</th>
                  <th>Department</th>
                  <th>Primary</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {emp.roles.map((role) => (
                  <tr key={role.roleId}>
                    <td>{role.roleName}</td>
                    <td>{role.department}</td>
                    <td>{role.isPrimary ? 'Yes' : 'No'}</td>
                    <td>
                      <button
                        className="secondary outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => removeRole(emp.id, role.roleId)}
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </article>
      </section>
    </>
  )
}
