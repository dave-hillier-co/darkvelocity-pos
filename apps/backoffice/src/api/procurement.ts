import { apiClient } from './client'
import type { HalCollection } from '../types'
import type { PurchaseDocument, Delivery } from '../reducers/procurementReducer'

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

// Suppliers (org-scoped)
export async function getSuppliers(): Promise<HalCollection<Supplier>> {
  return apiClient.get(apiClient.buildOrgPath('/suppliers'))
}

export async function getSupplier(supplierId: string): Promise<Supplier> {
  return apiClient.get(apiClient.buildOrgPath(`/suppliers/${supplierId}`))
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
  return apiClient.post(apiClient.buildOrgPath('/suppliers'), data)
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
  return apiClient.put(apiClient.buildOrgPath(`/suppliers/${supplierId}`), data)
}

export async function deleteSupplier(supplierId: string): Promise<void> {
  return apiClient.delete(apiClient.buildOrgPath(`/suppliers/${supplierId}`))
}

export async function getSupplierIngredients(supplierId: string): Promise<HalCollection<SupplierIngredient>> {
  return apiClient.get(apiClient.buildOrgPath(`/suppliers/${supplierId}/ingredients`))
}

// Purchase Documents (site-scoped)
export async function getPurchaseDocuments(): Promise<HalCollection<PurchaseDocument>> {
  return apiClient.get(apiClient.buildOrgSitePath('/purchases'))
}

export async function getPurchaseDocument(documentId: string): Promise<PurchaseDocument> {
  return apiClient.get(apiClient.buildOrgSitePath(`/purchases/${documentId}`))
}

export async function uploadPurchaseDocument(file: File, type?: string): Promise<PurchaseDocument> {
  const formData = new FormData()
  formData.append('file', file)
  const params = new URLSearchParams()
  if (type) params.append('type', type)
  const query = params.toString() ? `?${params}` : ''
  // Upload uses FormData, so we bypass the JSON client for this endpoint
  const url = apiClient.buildOrgSitePath(`/purchases${query}`)
  return apiClient.post(url, formData)
}

export async function confirmPurchaseDocument(documentId: string, data: {
  confirmedBy: string
  vendorId?: string
  vendorName?: string
  documentDate?: string
  currency?: string
}): Promise<PurchaseDocument> {
  return apiClient.post(apiClient.buildOrgSitePath(`/purchases/${documentId}/confirm`), data)
}

export async function rejectPurchaseDocument(documentId: string, rejectedBy: string, reason: string): Promise<void> {
  return apiClient.delete(apiClient.buildOrgSitePath(`/purchases/${documentId}`))
}

export async function processPurchaseDocument(documentId: string): Promise<PurchaseDocument> {
  return apiClient.post(apiClient.buildOrgSitePath(`/purchases/${documentId}/process`))
}

// Deliveries (site-scoped)
export async function getDeliveries(status?: string): Promise<HalCollection<Delivery>> {
  let path = '/purchases'
  const params = new URLSearchParams()
  if (status) params.append('status', status)
  if (params.toString()) path += `?${params}`
  return apiClient.get(apiClient.buildOrgSitePath(path))
}

export async function getDelivery(deliveryId: string): Promise<Delivery> {
  return apiClient.get(apiClient.buildOrgSitePath(`/purchases/${deliveryId}`))
}

export async function acceptDelivery(deliveryId: string): Promise<Delivery> {
  return apiClient.post(apiClient.buildOrgSitePath(`/purchases/${deliveryId}/confirm`), {})
}

export async function rejectDelivery(deliveryId: string, reason: string): Promise<Delivery> {
  return apiClient.delete(apiClient.buildOrgSitePath(`/purchases/${deliveryId}`)) as Promise<Delivery>
}

// Vendor Mappings (org-scoped)
export async function getVendorMappings(vendorId: string): Promise<unknown> {
  return apiClient.get(apiClient.buildOrgPath(`/vendors/${vendorId}/mappings`))
}

export async function getVendorMappingItems(vendorId: string): Promise<HalCollection<unknown>> {
  return apiClient.get(apiClient.buildOrgPath(`/vendors/${vendorId}/mappings/items`))
}

export async function setVendorMapping(vendorId: string, description: string, data: {
  ingredientId: string
  ingredientName: string
  ingredientSku?: string
  setBy: string
  vendorProductCode?: string
  expectedUnitPrice?: number
  unit?: string
}): Promise<unknown> {
  return apiClient.put(
    apiClient.buildOrgPath(`/vendors/${vendorId}/mappings/items/${encodeURIComponent(description)}`),
    data
  )
}
