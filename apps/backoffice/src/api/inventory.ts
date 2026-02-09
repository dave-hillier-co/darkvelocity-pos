import { apiClient } from './client'
import type { HalResource } from '../types'

// Types matching backend contracts
export interface InventoryState {
  ingredientId: string
  ingredientName: string
  sku: string
  unit: string
  category: string
  currentQuantity: number
  reorderPoint?: number
  parLevel?: number
  lastReceivedAt?: string
  lastConsumedAt?: string
}

export interface InventoryLevelInfo {
  currentQuantity: number
  reorderPoint?: number
  parLevel?: number
  isLow: boolean
  isCritical: boolean
}

export interface StockBatch {
  id: string
  batchNumber: string
  quantity: number
  remainingQuantity: number
  unitCost: number
  expiryDate?: string
  supplierId?: string
  deliveryId?: string
  location?: string
  receivedAt: string
}

// Request types matching backend
export interface InitializeInventoryRequest {
  ingredientId: string
  ingredientName: string
  sku: string
  unit: string
  category: string
  reorderPoint?: number
  parLevel?: number
}

export interface ReceiveBatchRequest {
  batchNumber: string
  quantity: number
  unitCost: number
  expiryDate?: string
  supplierId?: string
  deliveryId?: string
  location?: string
  notes?: string
  receivedBy?: string
}

export interface ConsumeStockRequest {
  quantity: number
  reason: string
  orderId?: string
  performedBy?: string
}

export interface AdjustInventoryRequest {
  newQuantity: number
  reason: string
  adjustedBy: string
  approvedBy?: string
}

// API functions - inventory is at site level
export async function initializeInventory(request: InitializeInventoryRequest): Promise<{ ingredientId: string; ingredientName: string } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/inventory')
  return apiClient.post(endpoint, request)
}

export async function getInventoryItem(ingredientId: string): Promise<InventoryState & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/inventory/${ingredientId}`)
  return apiClient.get(endpoint)
}

export async function receiveBatch(ingredientId: string, request: ReceiveBatchRequest): Promise<StockBatch & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/inventory/${ingredientId}/receive`)
  return apiClient.post(endpoint, request)
}

export async function consumeStock(ingredientId: string, request: ConsumeStockRequest): Promise<{ consumed: number; remaining: number } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/inventory/${ingredientId}/consume`)
  return apiClient.post(endpoint, request)
}

export async function adjustInventory(ingredientId: string, request: AdjustInventoryRequest): Promise<InventoryLevelInfo & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/inventory/${ingredientId}/adjust`)
  return apiClient.post(endpoint, request)
}

export async function getInventoryLevel(ingredientId: string): Promise<InventoryLevelInfo & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/inventory/${ingredientId}/level`)
  return apiClient.get(endpoint)
}

export interface InventorySearchFilter {
  query?: string
  category?: string
  belowReorderPoint?: boolean
}

export async function searchInventory(filter: InventorySearchFilter = {}): Promise<{ items: InventoryState[] }> {
  const endpoint = apiClient.buildOrgSitePath('/inventory/batch/search')
  return apiClient.post(endpoint, filter)
}
