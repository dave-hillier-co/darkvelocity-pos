import { apiClient } from './client'

export interface DailySalesCOGS {
  date: string
  locationId: string
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
  id: string
  locationId: string
  menuItemId: string
  menuItemName: string
  alertType: 'margin_below_threshold' | 'cost_increase' | 'ingredient_price_change'
  currentMargin: number
  targetMargin: number
  message: string
  createdAt: string
  isAcknowledged: boolean
}

export interface HalCollection<T> {
  _embedded: {
    items: T[]
  }
  _links: {
    self: { href: string }
  }
  total: number
}

export async function getDailySalesCOGS(
  locationId: string,
  startDate: string,
  endDate: string
): Promise<HalCollection<DailySalesCOGS>> {
  return apiClient.get(
    `/api/reports/locations/${locationId}/daily-sales-cogs?startDate=${startDate}&endDate=${endDate}`
  )
}

export async function getItemMargins(
  locationId: string,
  startDate: string,
  endDate: string,
  categoryId?: string
): Promise<HalCollection<ItemMargin>> {
  let url = `/api/reports/locations/${locationId}/item-margins?startDate=${startDate}&endDate=${endDate}`
  if (categoryId) url += `&categoryId=${categoryId}`
  return apiClient.get(url)
}

export async function getCategoryMargins(
  locationId: string,
  startDate: string,
  endDate: string
): Promise<HalCollection<CategoryMargin>> {
  return apiClient.get(
    `/api/reports/locations/${locationId}/category-margins?startDate=${startDate}&endDate=${endDate}`
  )
}

export async function getSupplierAnalysis(
  locationId: string,
  startDate: string,
  endDate: string
): Promise<HalCollection<SupplierAnalysis>> {
  return apiClient.get(
    `/api/reports/locations/${locationId}/supplier-analysis?startDate=${startDate}&endDate=${endDate}`
  )
}

export async function getCostAlerts(locationId: string): Promise<HalCollection<CostAlert>> {
  return apiClient.get(`/api/locations/${locationId}/cost-alerts`)
}

export async function acknowledgeCostAlert(locationId: string, alertId: string): Promise<void> {
  return apiClient.post(`/api/locations/${locationId}/cost-alerts/${alertId}/acknowledge`)
}

export async function getSalesReport(
  locationId: string,
  startDate: string,
  endDate: string
): Promise<{
  totalRevenue: number
  totalOrders: number
  averageOrderValue: number
  topItems: { itemName: string; quantity: number; revenue: number }[]
}> {
  return apiClient.get(
    `/api/reports/locations/${locationId}/sales?startDate=${startDate}&endDate=${endDate}`
  )
}

export async function getCashDrawerReport(
  locationId: string,
  date: string
): Promise<{
  openingBalance: number
  closingBalance: number
  cashSales: number
  cashPayouts: number
  variance: number
  transactions: { time: string; type: string; amount: number; userId: string }[]
}> {
  return apiClient.get(`/api/reports/locations/${locationId}/cash-drawer?date=${date}`)
}
