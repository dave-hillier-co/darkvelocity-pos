import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { employeeReducer, initialEmployeeState, type EmployeeState, type EmployeeAction } from '../reducers/employeeReducer'
import * as employeeApi from '../api/employees'
import type { Employee } from '../api/employees'

interface EmployeeContextValue extends EmployeeState {
  loadEmployees: (employees: Employee[]) => void
  selectEmployee: (employee: Employee) => void
  deselectEmployee: () => void
  createEmployee: (data: Parameters<typeof employeeApi.createEmployee>[0]) => Promise<void>
  updateEmployee: (employeeId: string, data: Parameters<typeof employeeApi.updateEmployee>[1]) => Promise<void>
  clockIn: (employeeId: string, siteId: string) => Promise<void>
  clockOut: (employeeId: string) => Promise<void>
  assignRole: (employeeId: string, data: Parameters<typeof employeeApi.assignRole>[1]) => Promise<void>
  removeRole: (employeeId: string, roleId: string) => Promise<void>
  dispatch: React.Dispatch<EmployeeAction>
}

const EmployeeContext = createContext<EmployeeContextValue | null>(null)

export function EmployeeProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(employeeReducer, initialEmployeeState)

  function loadEmployees(employees: Employee[]) {
    dispatch({ type: 'EMPLOYEES_LOADED', payload: { employees } })
  }

  function selectEmployee(employee: Employee) {
    dispatch({ type: 'EMPLOYEE_SELECTED', payload: { employee } })
  }

  function deselectEmployee() {
    dispatch({ type: 'EMPLOYEE_DESELECTED' })
  }

  async function createEmployee(data: Parameters<typeof employeeApi.createEmployee>[0]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await employeeApi.createEmployee(data)
      const employee = await employeeApi.getEmployee(result.id)
      dispatch({ type: 'EMPLOYEE_CREATED', payload: { employee } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function updateEmployee(employeeId: string, data: Parameters<typeof employeeApi.updateEmployee>[1]) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const employee = await employeeApi.updateEmployee(employeeId, data)
      dispatch({ type: 'EMPLOYEE_UPDATED', payload: { employee } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function clockIn(employeeId: string, siteId: string) {
    try {
      await employeeApi.clockIn(employeeId, { siteId })
      dispatch({ type: 'CLOCKED_IN', payload: { employeeId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function clockOut(employeeId: string) {
    try {
      await employeeApi.clockOut(employeeId)
      dispatch({ type: 'CLOCKED_OUT', payload: { employeeId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function assignRole(employeeId: string, data: Parameters<typeof employeeApi.assignRole>[1]) {
    try {
      await employeeApi.assignRole(employeeId, data)
      dispatch({
        type: 'ROLE_ASSIGNED',
        payload: {
          employeeId,
          role: {
            roleId: data.roleId,
            roleName: data.roleName,
            department: data.department,
            isPrimary: data.isPrimary ?? false,
            hourlyRateOverride: data.hourlyRateOverride,
          },
        },
      })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function removeRole(employeeId: string, roleId: string) {
    try {
      await employeeApi.removeRole(employeeId, roleId)
      dispatch({ type: 'ROLE_REMOVED', payload: { employeeId, roleId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <EmployeeContext.Provider
      value={{
        ...state,
        loadEmployees,
        selectEmployee,
        deselectEmployee,
        createEmployee,
        updateEmployee,
        clockIn,
        clockOut,
        assignRole,
        removeRole,
        dispatch,
      }}
    >
      {children}
    </EmployeeContext.Provider>
  )
}

export function useEmployees() {
  const context = useContext(EmployeeContext)
  if (!context) {
    throw new Error('useEmployees must be used within an EmployeeProvider')
  }
  return context
}
