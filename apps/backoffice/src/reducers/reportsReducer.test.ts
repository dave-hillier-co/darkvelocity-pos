import { describe, it, expect } from 'vitest'
import {
  reportsReducer,
  initialReportsState,
  type ReportsState,
  type DailySales,
  type ItemMargin,
  type CategoryMargin,
  type CostAlert,
} from './reportsReducer'

function makeDailySales(overrides: Partial<DailySales> = {}): DailySales {
  return {
    date: '2026-01-15',
    grossRevenue: 5000,
    netRevenue: 4200,
    totalCOGS: 1500,
    grossProfit: 2700,
    grossMarginPercent: 64.3,
    orderCount: 120,
    ...overrides,
  }
}

function makeItemMargin(overrides: Partial<ItemMargin> = {}): ItemMargin {
  return {
    menuItemId: 'item-1',
    menuItemName: 'Fish and Chips',
    categoryName: 'Mains',
    unitsSold: 45,
    grossRevenue: 675,
    totalCOGS: 200,
    grossProfit: 475,
    marginPercent: 70.4,
    targetMarginPercent: 65,
    ...overrides,
  }
}

function makeCategoryMargin(overrides: Partial<CategoryMargin> = {}): CategoryMargin {
  return {
    categoryId: 'cat-1',
    categoryName: 'Mains',
    accountingGroupId: 'ag-1',
    itemCount: 12,
    unitsSold: 300,
    grossRevenue: 4500,
    totalCOGS: 1350,
    grossProfit: 3150,
    marginPercent: 70,
    ...overrides,
  }
}

function makeCostAlert(overrides: Partial<CostAlert> = {}): CostAlert {
  return {
    id: 'alert-1',
    alertType: 'cost_increase',
    recipeName: 'Fish and Chips',
    ingredientName: 'Cod Fillet',
    menuItemName: 'Fish and Chips',
    changePercent: 15.5,
    isAcknowledged: false,
    actionTaken: null,
    createdAt: '2026-01-15T08:00:00Z',
    acknowledgedAt: null,
    ...overrides,
  }
}

describe('reportsReducer', () => {
  it('returns initial state for unknown action', () => {
    const state = reportsReducer(initialReportsState, { type: 'UNKNOWN' } as never)
    expect(state).toBe(initialReportsState)
  })

  describe('LOADING_STARTED', () => {
    it('sets isLoading to true and clears error', () => {
      const prev: ReportsState = { ...initialReportsState, error: 'old error' }
      const state = reportsReducer(prev, { type: 'LOADING_STARTED' })
      expect(state.isLoading).toBe(true)
      expect(state.error).toBeNull()
    })
  })

  describe('LOADING_FAILED', () => {
    it('sets isLoading to false and sets error', () => {
      const prev: ReportsState = { ...initialReportsState, isLoading: true }
      const state = reportsReducer(prev, {
        type: 'LOADING_FAILED',
        payload: { error: 'Server error' },
      })
      expect(state.isLoading).toBe(false)
      expect(state.error).toBe('Server error')
    })
  })

  describe('DAILY_SALES_LOADED', () => {
    it('replaces daily sales and clears loading', () => {
      const dailySales = [
        makeDailySales({ date: '2026-01-14' }),
        makeDailySales({ date: '2026-01-15' }),
      ]
      const state = reportsReducer(
        { ...initialReportsState, isLoading: true },
        { type: 'DAILY_SALES_LOADED', payload: { dailySales } }
      )
      expect(state.dailySales).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('ITEM_MARGINS_LOADED', () => {
    it('replaces item margins', () => {
      const itemMargins = [
        makeItemMargin({ menuItemId: 'item-1' }),
        makeItemMargin({ menuItemId: 'item-2', menuItemName: 'Burger' }),
      ]
      const state = reportsReducer(
        { ...initialReportsState, isLoading: true },
        { type: 'ITEM_MARGINS_LOADED', payload: { itemMargins } }
      )
      expect(state.itemMargins).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('CATEGORY_MARGINS_LOADED', () => {
    it('replaces category margins', () => {
      const categoryMargins = [makeCategoryMargin()]
      const state = reportsReducer(
        { ...initialReportsState, isLoading: true },
        { type: 'CATEGORY_MARGINS_LOADED', payload: { categoryMargins } }
      )
      expect(state.categoryMargins).toHaveLength(1)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('COST_ALERTS_LOADED', () => {
    it('replaces cost alerts', () => {
      const costAlerts = [makeCostAlert(), makeCostAlert({ id: 'alert-2' })]
      const state = reportsReducer(
        { ...initialReportsState, isLoading: true },
        { type: 'COST_ALERTS_LOADED', payload: { costAlerts } }
      )
      expect(state.costAlerts).toHaveLength(2)
      expect(state.isLoading).toBe(false)
    })
  })

  describe('ALERT_ACKNOWLEDGED', () => {
    it('marks alert as acknowledged', () => {
      const alerts = [makeCostAlert({ id: 'alert-1' }), makeCostAlert({ id: 'alert-2' })]
      const prev: ReportsState = { ...initialReportsState, costAlerts: alerts }
      const state = reportsReducer(prev, {
        type: 'ALERT_ACKNOWLEDGED',
        payload: { alertId: 'alert-1' },
      })
      expect(state.costAlerts[0].isAcknowledged).toBe(true)
      expect(state.costAlerts[0].acknowledgedAt).toBeTruthy()
      expect(state.costAlerts[1].isAcknowledged).toBe(false)
    })
  })

  describe('DATE_RANGE_CHANGED', () => {
    it('updates date range', () => {
      const state = reportsReducer(initialReportsState, {
        type: 'DATE_RANGE_CHANGED',
        payload: { start: '2026-01-01', end: '2026-01-31' },
      })
      expect(state.dateRange.start).toBe('2026-01-01')
      expect(state.dateRange.end).toBe('2026-01-31')
    })
  })

  it('initialReportsState has a default date range', () => {
    expect(initialReportsState.dateRange.start).toBeTruthy()
    expect(initialReportsState.dateRange.end).toBeTruthy()
  })
})
