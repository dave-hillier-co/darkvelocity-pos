import { apiClient } from './client'
import type { Order, OrderLine } from '../types'

const API_BASE = '/api/locations'

export interface CreateOrderRequest {
  orderType: Order['orderType']
  userId?: string
}

export interface AddLineRequest {
  menuItemId: string
  itemName: string
  quantity: number
  unitPrice: number
}

export interface OrderResponse extends Order {
  _links: Record<string, { href: string }>
}

export async function createOrder(
  locationId: string,
  request: CreateOrderRequest
): Promise<Order> {
  return apiClient.post<OrderResponse>(`${API_BASE}/${locationId}/orders`, request)
}

export async function getOrder(locationId: string, orderId: string): Promise<Order> {
  return apiClient.get<OrderResponse>(`${API_BASE}/${locationId}/orders/${orderId}`)
}

export async function addOrderLine(
  locationId: string,
  orderId: string,
  request: AddLineRequest
): Promise<OrderLine> {
  return apiClient.post<OrderLine>(
    `${API_BASE}/${locationId}/orders/${orderId}/lines`,
    request
  )
}

export async function updateOrderLine(
  locationId: string,
  orderId: string,
  lineId: string,
  updates: Partial<Pick<OrderLine, 'quantity' | 'discountAmount'>>
): Promise<OrderLine> {
  return apiClient.patch<OrderLine>(
    `${API_BASE}/${locationId}/orders/${orderId}/lines/${lineId}`,
    updates
  )
}

export async function removeOrderLine(
  locationId: string,
  orderId: string,
  lineId: string
): Promise<void> {
  await apiClient.delete(`${API_BASE}/${locationId}/orders/${orderId}/lines/${lineId}`)
}

export async function sendOrder(locationId: string, orderId: string): Promise<Order> {
  return apiClient.post<OrderResponse>(
    `${API_BASE}/${locationId}/orders/${orderId}/send`,
    {}
  )
}

export async function voidOrder(
  locationId: string,
  orderId: string,
  reason: string
): Promise<Order> {
  return apiClient.post<OrderResponse>(
    `${API_BASE}/${locationId}/orders/${orderId}/void`,
    { reason }
  )
}

export async function completeOrder(locationId: string, orderId: string): Promise<Order> {
  return apiClient.post<OrderResponse>(
    `${API_BASE}/${locationId}/orders/${orderId}/complete`,
    {}
  )
}
