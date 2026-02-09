import { createContext, useContext, useReducer, type ReactNode } from 'react'
import {
  reportsReducer,
  initialReportsState,
  type ReportsState,
  type ReportsAction,
} from '../reducers/reportsReducer'
import * as reportsApi from '../api/reports'

interface ReportsContextValue extends ReportsState {
  loadDailySales: () => Promise<void>
  loadItemMargins: (categoryId?: string) => Promise<void>
  loadCategoryMargins: () => Promise<void>
  loadCostAlerts: () => Promise<void>
  acknowledgeCostAlert: (alertId: string) => Promise<void>
  setDateRange: (start: string, end: string) => void
  dispatch: React.Dispatch<ReportsAction>
}

const ReportsContext = createContext<ReportsContextValue | null>(null)

export function ReportsProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reportsReducer, initialReportsState)

  async function loadDailySales() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await reportsApi.getDailySalesCOGS(
        state.dateRange.start,
        state.dateRange.end
      )
      dispatch({
        type: 'DAILY_SALES_LOADED',
        payload: { dailySales: result._embedded.items },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load daily sales' },
      })
    }
  }

  async function loadItemMargins(categoryId?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await reportsApi.getItemMargins(
        state.dateRange.start,
        state.dateRange.end,
        categoryId
      )
      dispatch({
        type: 'ITEM_MARGINS_LOADED',
        payload: { itemMargins: result._embedded.items },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load item margins' },
      })
    }
  }

  async function loadCategoryMargins() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await reportsApi.getCategoryMargins(
        state.dateRange.start,
        state.dateRange.end
      )
      dispatch({
        type: 'CATEGORY_MARGINS_LOADED',
        payload: { categoryMargins: result._embedded.items },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load category margins' },
      })
    }
  }

  async function loadCostAlerts() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await reportsApi.getCostAlerts()
      dispatch({
        type: 'COST_ALERTS_LOADED',
        payload: { costAlerts: result.alerts },
      })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to load cost alerts' },
      })
    }
  }

  async function acknowledgeCostAlert(alertId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await reportsApi.acknowledgeCostAlert(alertId)
      dispatch({ type: 'ALERT_ACKNOWLEDGED', payload: { alertId } })
    } catch (error) {
      dispatch({
        type: 'LOADING_FAILED',
        payload: { error: error instanceof Error ? error.message : 'Failed to acknowledge alert' },
      })
    }
  }

  function setDateRange(start: string, end: string) {
    dispatch({ type: 'DATE_RANGE_CHANGED', payload: { start, end } })
  }

  return (
    <ReportsContext.Provider
      value={{
        ...state,
        loadDailySales,
        loadItemMargins,
        loadCategoryMargins,
        loadCostAlerts,
        acknowledgeCostAlert,
        setDateRange,
        dispatch,
      }}
    >
      {children}
    </ReportsContext.Provider>
  )
}

export function useReports() {
  const context = useContext(ReportsContext)
  if (!context) {
    throw new Error('useReports must be used within a ReportsProvider')
  }
  return context
}
