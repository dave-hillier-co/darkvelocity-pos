import { describe, it, expect } from 'vitest'
import { employeeReducer, initialEmployeeState, type EmployeeState } from './employeeReducer'
import type { Employee } from '../api/employees'

function makeEmployee(overrides: Partial<Employee> = {}): Employee {
  return {
    id: 'emp-1',
    userId: 'user-1',
    defaultSiteId: 'site-1',
    employeeNumber: 'E001',
    firstName: 'Jane',
    lastName: 'Doe',
    email: 'jane@example.com',
    employmentType: 'FullTime',
    isClockedIn: false,
    roles: [],
    _links: { self: { href: '/api/orgs/org-1/employees/emp-1' } },
    ...overrides,
  }
}

describe('employeeReducer', () => {
  it('returns initial state for unknown action', () => {
    const state = employeeReducer(initialEmployeeState, { type: 'LOADING_STARTED' })
    expect(state.isLoading).toBe(true)
  })

  it('handles EMPLOYEES_LOADED', () => {
    const employees = [makeEmployee(), makeEmployee({ id: 'emp-2', firstName: 'John' })]
    const state = employeeReducer(
      { ...initialEmployeeState, isLoading: true },
      { type: 'EMPLOYEES_LOADED', payload: { employees } }
    )
    expect(state.employees).toHaveLength(2)
    expect(state.isLoading).toBe(false)
    expect(state.error).toBeNull()
  })

  it('handles EMPLOYEE_SELECTED and EMPLOYEE_DESELECTED', () => {
    const employee = makeEmployee()
    let state = employeeReducer(initialEmployeeState, { type: 'EMPLOYEE_SELECTED', payload: { employee } })
    expect(state.selectedEmployee).toEqual(employee)

    state = employeeReducer(state, { type: 'EMPLOYEE_DESELECTED' })
    expect(state.selectedEmployee).toBeNull()
  })

  it('handles EMPLOYEE_CREATED', () => {
    const employee = makeEmployee()
    const state = employeeReducer(initialEmployeeState, { type: 'EMPLOYEE_CREATED', payload: { employee } })
    expect(state.employees).toHaveLength(1)
    expect(state.employees[0].id).toBe('emp-1')
  })

  it('handles EMPLOYEE_UPDATED in list and selected', () => {
    const employee = makeEmployee()
    const updated = makeEmployee({ firstName: 'Janet' })
    const initial: EmployeeState = {
      ...initialEmployeeState,
      employees: [employee],
      selectedEmployee: employee,
    }
    const state = employeeReducer(initial, { type: 'EMPLOYEE_UPDATED', payload: { employee: updated } })
    expect(state.employees[0].firstName).toBe('Janet')
    expect(state.selectedEmployee?.firstName).toBe('Janet')
  })

  it('handles ROLE_ASSIGNED', () => {
    const employee = makeEmployee()
    const initial: EmployeeState = {
      ...initialEmployeeState,
      employees: [employee],
      selectedEmployee: employee,
    }
    const role = { roleId: 'role-1', roleName: 'Server', department: 'FOH', isPrimary: true }
    const state = employeeReducer(initial, {
      type: 'ROLE_ASSIGNED',
      payload: { employeeId: 'emp-1', role },
    })
    expect(state.employees[0].roles).toHaveLength(1)
    expect(state.employees[0].roles[0].roleName).toBe('Server')
    expect(state.selectedEmployee?.roles).toHaveLength(1)
  })

  it('handles ROLE_REMOVED', () => {
    const employee = makeEmployee({
      roles: [{ roleId: 'role-1', roleName: 'Server', department: 'FOH', isPrimary: true }],
    })
    const initial: EmployeeState = {
      ...initialEmployeeState,
      employees: [employee],
      selectedEmployee: employee,
    }
    const state = employeeReducer(initial, {
      type: 'ROLE_REMOVED',
      payload: { employeeId: 'emp-1', roleId: 'role-1' },
    })
    expect(state.employees[0].roles).toHaveLength(0)
    expect(state.selectedEmployee?.roles).toHaveLength(0)
  })

  it('handles CLOCKED_IN', () => {
    const employee = makeEmployee()
    const initial: EmployeeState = {
      ...initialEmployeeState,
      employees: [employee],
      selectedEmployee: employee,
    }
    const state = employeeReducer(initial, { type: 'CLOCKED_IN', payload: { employeeId: 'emp-1' } })
    expect(state.employees[0].isClockedIn).toBe(true)
    expect(state.selectedEmployee?.isClockedIn).toBe(true)
  })

  it('handles CLOCKED_OUT', () => {
    const employee = makeEmployee({ isClockedIn: true })
    const initial: EmployeeState = {
      ...initialEmployeeState,
      employees: [employee],
      selectedEmployee: employee,
    }
    const state = employeeReducer(initial, { type: 'CLOCKED_OUT', payload: { employeeId: 'emp-1' } })
    expect(state.employees[0].isClockedIn).toBe(false)
    expect(state.selectedEmployee?.isClockedIn).toBe(false)
  })

  it('handles LOADING_FAILED', () => {
    const state = employeeReducer(
      { ...initialEmployeeState, isLoading: true },
      { type: 'LOADING_FAILED', payload: { error: 'Network error' } }
    )
    expect(state.isLoading).toBe(false)
    expect(state.error).toBe('Network error')
  })
})
