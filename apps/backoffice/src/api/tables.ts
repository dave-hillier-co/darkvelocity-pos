import { apiClient } from './client'
import type { HalResource } from '../types'

export type TableStatus = 'Available' | 'Occupied' | 'Reserved' | 'Dirty' | 'Blocked' | 'OutOfService'
export type TableShape = 'Square' | 'Round' | 'Rectangle' | 'Booth' | 'Bar' | 'Custom'

export interface TablePosition {
  x: number
  y: number
  rotation?: number
  width?: number
  height?: number
}

export interface TableOccupancy {
  bookingId?: string
  orderId?: string
  guestName?: string
  guestCount: number
  serverId?: string
  seatedAt: string
}

export interface Table extends HalResource {
  id: string
  number: string
  name?: string
  minCapacity: number
  maxCapacity: number
  shape: TableShape
  status: TableStatus
  floorPlanId?: string
  sectionId?: string
  position?: TablePosition
  isCombinable: boolean
  sortOrder: number
  currentOccupancy?: TableOccupancy
}

export async function createTable(data: {
  number: string
  minCapacity?: number
  maxCapacity?: number
  name?: string
  shape?: TableShape
  floorPlanId?: string
}): Promise<{ id: string; number: string; createdAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/tables')
  return apiClient.post(endpoint, data)
}

export async function getTable(tableId: string): Promise<Table> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}`)
  return apiClient.get(endpoint)
}

export async function updateTable(tableId: string, data: {
  number?: string
  name?: string
  minCapacity?: number
  maxCapacity?: number
  shape?: TableShape
  position?: TablePosition
  isCombinable?: boolean
  sortOrder?: number
  sectionId?: string
}): Promise<Table> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}`)
  return apiClient.patch(endpoint, data)
}

export async function seatGuests(tableId: string, data: {
  bookingId?: string
  orderId?: string
  guestName?: string
  guestCount: number
  serverId?: string
}): Promise<{ status: TableStatus; occupancy: TableOccupancy } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}/seat`)
  return apiClient.post(endpoint, data)
}

export async function clearTable(tableId: string): Promise<{ status: TableStatus } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}/clear`)
  return apiClient.post(endpoint)
}

export async function setStatus(tableId: string, status: TableStatus): Promise<{ status: TableStatus } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}/status`)
  return apiClient.post(endpoint, { status })
}

export async function deleteTable(tableId: string): Promise<void> {
  const endpoint = apiClient.buildOrgSitePath(`/tables/${tableId}`)
  return apiClient.delete(endpoint)
}
