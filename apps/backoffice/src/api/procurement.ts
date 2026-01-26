import { apiClient } from './client'

export interface Supplier {
  id: string
  code: string
  name: string
  contactEmail: string
  contactPhone: string
  address: string
  paymentTermsDays: number
  leadTimeDays: number
  isActive: boolean
  _links: {
    self: { href: string }
    ingredients: { href: string }
    orders: { href: string }
  }
}

export interface SupplierIngredient {
  supplierId: string
  ingredientId: string
  ingredientName: string
  supplierProductCode: string
  packSize: number
  packUnit: string
  lastKnownPrice: number
  _links: {
    self: { href: string }
  }
}

export interface PurchaseOrder {
  id: string
  orderNumber: string
  supplierId: string
  supplierName: string
  locationId: string
  status: 'draft' | 'submitted' | 'partially_received' | 'received' | 'cancelled'
  expectedDeliveryDate: string
  orderTotal: number
  lineCount: number
  createdAt: string
  _links: {
    self: { href: string }
    supplier: { href: string }
    lines: { href: string }
  }
}

export interface PurchaseOrderLine {
  id: string
  purchaseOrderId: string
  ingredientId: string
  ingredientName: string
  quantityOrdered: number
  quantityReceived: number
  unitPrice: number
  lineTotal: number
  _links: {
    self: { href: string }
    ingredient: { href: string }
  }
}

export interface Delivery {
  id: string
  deliveryNumber: string
  supplierId: string
  supplierName: string
  purchaseOrderId: string | null
  purchaseOrderNumber: string | null
  locationId: string
  status: 'pending' | 'accepted' | 'rejected'
  totalValue: number
  hasDiscrepancies: boolean
  lineCount: number
  receivedAt: string
  _links: {
    self: { href: string }
    supplier: { href: string }
    purchaseOrder?: { href: string }
    lines: { href: string }
  }
}

export interface DeliveryLine {
  id: string
  deliveryId: string
  ingredientId: string
  ingredientName: string
  quantityReceived: number
  unitCost: number
  lineCost: number
  batchNumber: string | null
  expiryDate: string | null
  _links: {
    self: { href: string }
    ingredient: { href: string }
  }
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

// Suppliers
export async function getSuppliers(): Promise<HalCollection<Supplier>> {
  return apiClient.get('/api/suppliers')
}

export async function getSupplier(supplierId: string): Promise<Supplier> {
  return apiClient.get(`/api/suppliers/${supplierId}`)
}

export async function createSupplier(data: {
  code: string
  name: string
  contactEmail?: string
  contactPhone?: string
  address?: string
  paymentTermsDays?: number
  leadTimeDays?: number
}): Promise<Supplier> {
  return apiClient.post('/api/suppliers', data)
}

export async function updateSupplier(supplierId: string, data: {
  code?: string
  name?: string
  contactEmail?: string
  contactPhone?: string
  address?: string
  paymentTermsDays?: number
  leadTimeDays?: number
  isActive?: boolean
}): Promise<Supplier> {
  return apiClient.put(`/api/suppliers/${supplierId}`, data)
}

export async function deleteSupplier(supplierId: string): Promise<void> {
  return apiClient.delete(`/api/suppliers/${supplierId}`)
}

export async function getSupplierIngredients(supplierId: string): Promise<HalCollection<SupplierIngredient>> {
  return apiClient.get(`/api/suppliers/${supplierId}/ingredients`)
}

// Purchase Orders
export async function getPurchaseOrders(locationId?: string, status?: string): Promise<HalCollection<PurchaseOrder>> {
  let url = '/api/purchase-orders'
  const params = new URLSearchParams()
  if (locationId) params.append('locationId', locationId)
  if (status) params.append('status', status)
  if (params.toString()) url += `?${params}`
  return apiClient.get(url)
}

export async function getPurchaseOrder(orderId: string): Promise<PurchaseOrder> {
  return apiClient.get(`/api/purchase-orders/${orderId}`)
}

export async function createPurchaseOrder(data: {
  supplierId: string
  locationId: string
  expectedDeliveryDate?: string
}): Promise<PurchaseOrder> {
  return apiClient.post('/api/purchase-orders', data)
}

export async function addPurchaseOrderLine(orderId: string, data: {
  ingredientId: string
  quantity: number
  unitPrice: number
}): Promise<PurchaseOrderLine> {
  return apiClient.post(`/api/purchase-orders/${orderId}/lines`, data)
}

export async function updatePurchaseOrderLine(orderId: string, lineId: string, data: {
  quantity?: number
  unitPrice?: number
}): Promise<PurchaseOrderLine> {
  return apiClient.put(`/api/purchase-orders/${orderId}/lines/${lineId}`, data)
}

export async function removePurchaseOrderLine(orderId: string, lineId: string): Promise<void> {
  return apiClient.delete(`/api/purchase-orders/${orderId}/lines/${lineId}`)
}

export async function submitPurchaseOrder(orderId: string): Promise<PurchaseOrder> {
  return apiClient.post(`/api/purchase-orders/${orderId}/submit`)
}

export async function cancelPurchaseOrder(orderId: string): Promise<PurchaseOrder> {
  return apiClient.post(`/api/purchase-orders/${orderId}/cancel`)
}

// Deliveries
export async function getDeliveries(locationId?: string, status?: string): Promise<HalCollection<Delivery>> {
  let url = '/api/deliveries'
  const params = new URLSearchParams()
  if (locationId) params.append('locationId', locationId)
  if (status) params.append('status', status)
  if (params.toString()) url += `?${params}`
  return apiClient.get(url)
}

export async function getDelivery(deliveryId: string): Promise<Delivery> {
  return apiClient.get(`/api/deliveries/${deliveryId}`)
}

export async function createDeliveryFromPO(purchaseOrderId: string): Promise<Delivery> {
  return apiClient.post('/api/deliveries', { purchaseOrderId })
}

export async function createAdHocDelivery(data: {
  supplierId: string
  locationId: string
}): Promise<Delivery> {
  return apiClient.post('/api/deliveries/ad-hoc', data)
}

export async function addDeliveryLine(deliveryId: string, data: {
  ingredientId: string
  quantityReceived: number
  unitCost: number
  batchNumber?: string
  expiryDate?: string
}): Promise<DeliveryLine> {
  return apiClient.post(`/api/deliveries/${deliveryId}/lines`, data)
}

export async function acceptDelivery(deliveryId: string): Promise<Delivery> {
  return apiClient.post(`/api/deliveries/${deliveryId}/accept`)
}

export async function rejectDelivery(deliveryId: string, reason: string): Promise<Delivery> {
  return apiClient.post(`/api/deliveries/${deliveryId}/reject`, { reason })
}
