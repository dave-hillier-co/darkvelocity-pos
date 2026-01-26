import { apiClient } from './client'

export interface Ingredient {
  id: string
  code: string
  name: string
  unitOfMeasure: string
  category: string
  storageType: 'ambient' | 'chilled' | 'frozen'
  reorderLevel: number
  reorderQuantity: number
  isActive: boolean
  _links: {
    self: { href: string }
    stock: { href: string }
  }
}

export interface StockBatch {
  id: string
  ingredientId: string
  locationId: string
  deliveryId: string
  initialQuantity: number
  remainingQuantity: number
  unitCost: number
  receivedAt: string
  expiryDate: string | null
  status: 'active' | 'exhausted' | 'expired'
  _links: {
    self: { href: string }
    ingredient: { href: string }
  }
}

export interface StockLevel {
  ingredientId: string
  ingredientCode: string
  ingredientName: string
  locationId: string
  currentStock: number
  unitOfMeasure: string
  reorderLevel: number
  averageCost: number
  batchCount: number
  _links: {
    self: { href: string }
    batches: { href: string }
  }
}

export interface WasteRecord {
  id: string
  locationId: string
  ingredientId: string
  quantity: number
  unitCost: number
  totalCost: number
  reason: string
  recordedAt: string
  recordedByUserId: string
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

export async function getIngredients(): Promise<HalCollection<Ingredient>> {
  return apiClient.get('/api/ingredients')
}

export async function getIngredient(ingredientId: string): Promise<Ingredient> {
  return apiClient.get(`/api/ingredients/${ingredientId}`)
}

export async function createIngredient(data: {
  code: string
  name: string
  unitOfMeasure: string
  category: string
  storageType: string
  reorderLevel?: number
  reorderQuantity?: number
}): Promise<Ingredient> {
  return apiClient.post('/api/ingredients', data)
}

export async function updateIngredient(ingredientId: string, data: {
  code?: string
  name?: string
  unitOfMeasure?: string
  category?: string
  storageType?: string
  reorderLevel?: number
  reorderQuantity?: number
  isActive?: boolean
}): Promise<Ingredient> {
  return apiClient.put(`/api/ingredients/${ingredientId}`, data)
}

export async function deleteIngredient(ingredientId: string): Promise<void> {
  return apiClient.delete(`/api/ingredients/${ingredientId}`)
}

export async function getStockLevels(locationId: string): Promise<HalCollection<StockLevel>> {
  return apiClient.get(`/api/locations/${locationId}/stock`)
}

export async function getStockBatches(locationId: string, ingredientId: string): Promise<HalCollection<StockBatch>> {
  return apiClient.get(`/api/locations/${locationId}/stock/${ingredientId}/batches`)
}

export async function adjustStock(locationId: string, ingredientId: string, data: {
  quantityAdjustment: number
  reason: string
}): Promise<void> {
  return apiClient.post(`/api/locations/${locationId}/stock/${ingredientId}/adjust`, data)
}

export async function recordWaste(locationId: string, data: {
  ingredientId: string
  quantity: number
  reason: string
}): Promise<WasteRecord> {
  return apiClient.post(`/api/locations/${locationId}/waste`, data)
}

export async function getWasteRecords(locationId: string, startDate?: string, endDate?: string): Promise<HalCollection<WasteRecord>> {
  let url = `/api/locations/${locationId}/waste`
  const params = new URLSearchParams()
  if (startDate) params.append('startDate', startDate)
  if (endDate) params.append('endDate', endDate)
  if (params.toString()) url += `?${params}`
  return apiClient.get(url)
}
