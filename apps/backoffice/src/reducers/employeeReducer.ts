import type { Employee, EmployeeRole } from '../api/employees'

export type EmployeeAction =
  | { type: 'EMPLOYEES_LOADED'; payload: { employees: Employee[] } }
  | { type: 'EMPLOYEE_SELECTED'; payload: { employee: Employee } }
  | { type: 'EMPLOYEE_CREATED'; payload: { employee: Employee } }
  | { type: 'EMPLOYEE_UPDATED'; payload: { employee: Employee } }
  | { type: 'EMPLOYEE_DESELECTED' }
  | { type: 'ROLE_ASSIGNED'; payload: { employeeId: string; role: EmployeeRole } }
  | { type: 'ROLE_REMOVED'; payload: { employeeId: string; roleId: string } }
  | { type: 'CLOCKED_IN'; payload: { employeeId: string } }
  | { type: 'CLOCKED_OUT'; payload: { employeeId: string } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

export interface EmployeeState {
  employees: Employee[]
  selectedEmployee: Employee | null
  isLoading: boolean
  error: string | null
}

export const initialEmployeeState: EmployeeState = {
  employees: [],
  selectedEmployee: null,
  isLoading: false,
  error: null,
}

export function employeeReducer(state: EmployeeState, action: EmployeeAction): EmployeeState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'EMPLOYEES_LOADED':
      return { ...state, employees: action.payload.employees, isLoading: false, error: null }

    case 'EMPLOYEE_SELECTED':
      return { ...state, selectedEmployee: action.payload.employee }

    case 'EMPLOYEE_DESELECTED':
      return { ...state, selectedEmployee: null }

    case 'EMPLOYEE_CREATED': {
      return {
        ...state,
        employees: [...state.employees, action.payload.employee],
        isLoading: false,
      }
    }

    case 'EMPLOYEE_UPDATED': {
      const updated = action.payload.employee
      return {
        ...state,
        employees: state.employees.map(e => e.id === updated.id ? updated : e),
        selectedEmployee: state.selectedEmployee?.id === updated.id ? updated : state.selectedEmployee,
        isLoading: false,
      }
    }

    case 'ROLE_ASSIGNED': {
      const { employeeId, role } = action.payload
      return {
        ...state,
        employees: state.employees.map(e =>
          e.id === employeeId
            ? { ...e, roles: [...e.roles.filter(r => r.roleId !== role.roleId), role] }
            : e
        ),
        selectedEmployee: state.selectedEmployee?.id === employeeId
          ? { ...state.selectedEmployee, roles: [...state.selectedEmployee.roles.filter(r => r.roleId !== role.roleId), role] }
          : state.selectedEmployee,
      }
    }

    case 'ROLE_REMOVED': {
      const { employeeId, roleId } = action.payload
      return {
        ...state,
        employees: state.employees.map(e =>
          e.id === employeeId
            ? { ...e, roles: e.roles.filter(r => r.roleId !== roleId) }
            : e
        ),
        selectedEmployee: state.selectedEmployee?.id === employeeId
          ? { ...state.selectedEmployee, roles: state.selectedEmployee.roles.filter(r => r.roleId !== roleId) }
          : state.selectedEmployee,
      }
    }

    case 'CLOCKED_IN': {
      const { employeeId } = action.payload
      return {
        ...state,
        employees: state.employees.map(e =>
          e.id === employeeId ? { ...e, isClockedIn: true } : e
        ),
        selectedEmployee: state.selectedEmployee?.id === employeeId
          ? { ...state.selectedEmployee, isClockedIn: true }
          : state.selectedEmployee,
      }
    }

    case 'CLOCKED_OUT': {
      const { employeeId } = action.payload
      return {
        ...state,
        employees: state.employees.map(e =>
          e.id === employeeId ? { ...e, isClockedIn: false } : e
        ),
        selectedEmployee: state.selectedEmployee?.id === employeeId
          ? { ...state.selectedEmployee, isClockedIn: false }
          : state.selectedEmployee,
      }
    }

    default:
      return state
  }
}
