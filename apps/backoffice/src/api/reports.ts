import { apiClient } from './client'
import type { HalCollection } from '../types'

export interface DailySalesCOGS {
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

export interface SupplierAnalysis {
  supplierId: string
  supplierName: string
  totalSpend: number
  deliveryCount: number
  onTimeRate: number
  discrepancyCount: number
  averageLeadTime: number
}

export interface CostAlert {
  alertId: string
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

export interface CostAlertsResponse {
  totalCount: number
  activeCount: number
  acknowledgedCount: number
  alerts: CostAlert[]
  _links: { self: { href: string } }
}

// Daily Sales (site-scoped reports)
export async function getDailySalesCOGS(
  startDate: string,
  endDate: string
): Promise<HalCollection<DailySalesCOGS>> {
  return apiClient.get(
    apiClient.buildOrgSitePath(`/reports/sales/today`)
  )
}

export async function getDailySalesByDate(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/sales/${date}`))
}

export async function getDailySalesMetrics(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/sales/${date}/metrics`))
}

export async function getDailySalesGrossProfit(date: string, method?: string): Promise<unknown> {
  const params = method ? `?method=${method}` : ''
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/sales/${date}/gross-profit${params}`))
}

// Item Margins
export async function getItemMargins(
  startDate: string,
  endDate: string,
  categoryId?: string
): Promise<HalCollection<ItemMargin>> {
  const params = new URLSearchParams({ startDate, endDate })
  if (categoryId) params.append('categoryId', categoryId)
  return apiClient.get(
    apiClient.buildOrgSitePath(`/reports/sales/today`)
  )
}

// Category Margins
export async function getCategoryMargins(
  startDate: string,
  endDate: string
): Promise<HalCollection<CategoryMargin>> {
  return apiClient.get(
    apiClient.buildOrgSitePath(`/reports/sales/today`)
  )
}

// Supplier Analysis
export async function getSupplierAnalysis(
  startDate: string,
  endDate: string
): Promise<HalCollection<SupplierAnalysis>> {
  const params = new URLSearchParams({ startDate, endDate })
  return apiClient.get(
    apiClient.buildOrgSitePath(`/reports/sales/today?${params}`)
  )
}

// Cost Alerts (org-scoped)
export async function getCostAlerts(status?: string, alertType?: string): Promise<CostAlertsResponse> {
  const params = new URLSearchParams()
  if (status) params.append('status', status)
  if (alertType) params.append('alertType', alertType)
  const query = params.toString() ? `?${params}` : ''
  return apiClient.get(apiClient.buildOrgPath(`/cost-alerts${query}`))
}

export async function getCostAlert(alertId: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgPath(`/cost-alerts/${alertId}`))
}

export async function acknowledgeCostAlert(alertId: string, data?: {
  acknowledgedByUserId?: string
  notes?: string
  actionTaken?: string
}): Promise<void> {
  return apiClient.post(apiClient.buildOrgPath(`/cost-alerts/${alertId}/acknowledge`), data)
}

// Dashboard
export async function getDashboard(): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath('/reports/dashboard'))
}

export async function refreshDashboard(): Promise<void> {
  return apiClient.post(apiClient.buildOrgSitePath('/reports/dashboard/refresh'))
}

// Inventory Reports (site-scoped)
export async function getInventorySnapshot(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/inventory/${date}`))
}

export async function getInventoryHealth(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/inventory/${date}/health`))
}

// Consumption Reports (site-scoped)
export async function getConsumptionReport(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/consumption/${date}`))
}

export async function getConsumptionVariances(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/consumption/${date}/variances`))
}

// Waste Reports (site-scoped)
export async function getWasteReport(date: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/waste/${date}`))
}

// Period Reports (site-scoped)
export async function getWeeklyReport(year: number, weekNumber: number): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/weekly/${year}/${weekNumber}`))
}

export async function getMonthlyReport(year: number, month: number): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgSitePath(`/reports/monthly/${year}/${month}`))
}

// Menu Engineering (site-scoped, under costing)
export async function getMenuEngineering(fromDate?: string, toDate?: string): Promise<unknown> {
  const params = new URLSearchParams()
  if (fromDate) params.append('fromDate', fromDate)
  if (toDate) params.append('toDate', toDate)
  const query = params.toString() ? `?${params}` : ''
  return apiClient.get(apiClient.buildOrgSitePath(`/menu-engineering${query}`))
}

// Profitability (site-scoped, under costing)
export async function getProfitabilityDashboard(fromDate?: string, toDate?: string): Promise<unknown> {
  const params = new URLSearchParams()
  if (fromDate) params.append('fromDate', fromDate)
  if (toDate) params.append('toDate', toDate)
  const query = params.toString() ? `?${params}` : ''
  return apiClient.get(apiClient.buildOrgSitePath(`/profitability${query}`))
}
