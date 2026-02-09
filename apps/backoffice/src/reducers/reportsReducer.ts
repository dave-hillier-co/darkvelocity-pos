export interface DailySales {
  date: string
  grossRevenue: number
  netRevenue: number
  totalCOGS: number
  grossProfit: number
  grossMarginPercent: number
  orderCount: number
}

export interface ItemMargin {
  menuItemId: string
  menuItemName: string
  categoryName: string
  unitsSold: number
  grossRevenue: number
  totalCOGS: number
  grossProfit: number
  marginPercent: number
  targetMarginPercent: number
}

export interface CategoryMargin {
  categoryId: string
  categoryName: string
  accountingGroupId: string
  itemCount: number
  unitsSold: number
  grossRevenue: number
  totalCOGS: number
  grossProfit: number
  marginPercent: number
}

export interface CostAlert {
  id: string
  alertType: string
  recipeName: string | null
  ingredientName: string | null
  menuItemName: string | null
  changePercent: number
  isAcknowledged: boolean
  actionTaken: string | null
  createdAt: string
  acknowledgedAt: string | null
}

export interface DateRange {
  start: string
  end: string
}

export interface ReportsState {
  dailySales: DailySales[]
  itemMargins: ItemMargin[]
  categoryMargins: CategoryMargin[]
  costAlerts: CostAlert[]
  dateRange: DateRange
  isLoading: boolean
  error: string | null
}

export type ReportsAction =
  | { type: 'DAILY_SALES_LOADED'; payload: { dailySales: DailySales[] } }
  | { type: 'ITEM_MARGINS_LOADED'; payload: { itemMargins: ItemMargin[] } }
  | { type: 'CATEGORY_MARGINS_LOADED'; payload: { categoryMargins: CategoryMargin[] } }
  | { type: 'COST_ALERTS_LOADED'; payload: { costAlerts: CostAlert[] } }
  | { type: 'ALERT_ACKNOWLEDGED'; payload: { alertId: string } }
  | { type: 'DATE_RANGE_CHANGED'; payload: { start: string; end: string } }
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }

function getDefaultDateRange(): DateRange {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 30)
  return {
    start: start.toISOString().split('T')[0],
    end: end.toISOString().split('T')[0],
  }
}

export const initialReportsState: ReportsState = {
  dailySales: [],
  itemMargins: [],
  categoryMargins: [],
  costAlerts: [],
  dateRange: getDefaultDateRange(),
  isLoading: false,
  error: null,
}

export function reportsReducer(state: ReportsState, action: ReportsAction): ReportsState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'DAILY_SALES_LOADED':
      return { ...state, isLoading: false, dailySales: action.payload.dailySales }

    case 'ITEM_MARGINS_LOADED':
      return { ...state, isLoading: false, itemMargins: action.payload.itemMargins }

    case 'CATEGORY_MARGINS_LOADED':
      return { ...state, isLoading: false, categoryMargins: action.payload.categoryMargins }

    case 'COST_ALERTS_LOADED':
      return { ...state, isLoading: false, costAlerts: action.payload.costAlerts }

    case 'ALERT_ACKNOWLEDGED':
      return {
        ...state,
        isLoading: false,
        costAlerts: state.costAlerts.map((alert) =>
          alert.id === action.payload.alertId
            ? { ...alert, isAcknowledged: true, acknowledgedAt: new Date().toISOString() }
            : alert
        ),
      }

    case 'DATE_RANGE_CHANGED':
      return {
        ...state,
        dateRange: { start: action.payload.start, end: action.payload.end },
      }

    default:
      return state
  }
}
