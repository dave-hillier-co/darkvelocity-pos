import { apiClient } from './client'
import type { HalCollection, HalResource } from '../types'

export type ChannelStatus = 'Active' | 'Paused' | 'Error' | 'Disconnected'
export type DeliveryPlatformType = 'UberEats' | 'DoorDash' | 'JustEat' | 'Deliveroo' | 'GrubHub' | 'Custom'
export type IntegrationType = 'Direct' | 'Deliverect' | 'Otter' | 'Custom'

export interface ChannelLocation {
  locationId: string
  externalStoreId: string
  isActive: boolean
  menuId?: string
  operatingHoursOverride?: string
}

export interface Channel extends HalResource {
  channelId: string
  platformType: string
  integrationType: string
  name: string
  status: ChannelStatus
  externalChannelId?: string
  connectedAt?: string
  lastSyncAt?: string
  lastOrderAt?: string
  lastHeartbeatAt?: string
  totalOrdersToday: number
  totalRevenueToday: number
  lastErrorMessage?: string
  locations: ChannelLocation[]
}

export async function listChannels(): Promise<Channel[]> {
  try {
    const endpoint = apiClient.buildOrgPath('/channels')
    const response = await apiClient.get<HalCollection<Channel>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getChannel(channelId: string): Promise<Channel> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}`)
  return apiClient.get(endpoint)
}

export async function connectChannel(data: {
  platformType: DeliveryPlatformType
  integrationType: IntegrationType
  name: string
  apiCredentialsEncrypted?: string
  webhookSecret?: string
  externalChannelId?: string
  settings?: string
}): Promise<Channel> {
  const endpoint = apiClient.buildOrgPath('/channels')
  return apiClient.post(endpoint, data)
}

export async function updateChannel(channelId: string, data: {
  name?: string
  status?: ChannelStatus
  apiCredentialsEncrypted?: string
  webhookSecret?: string
  settings?: string
}): Promise<Channel> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}`)
  return apiClient.patch(endpoint, data)
}

export async function disconnectChannel(channelId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}`)
  return apiClient.delete(endpoint)
}

export async function pauseOrders(channelId: string, reason?: string): Promise<Channel> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/pause`)
  return apiClient.post(endpoint, { reason })
}

export async function resumeOrders(channelId: string): Promise<Channel> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/resume`)
  return apiClient.post(endpoint)
}

export async function getLocations(channelId: string): Promise<ChannelLocation[]> {
  try {
    const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/locations`)
    const response = await apiClient.get<HalCollection<ChannelLocation>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function addLocation(channelId: string, data: {
  locationId: string
  externalStoreId: string
  isActive?: boolean
  menuId?: string
  operatingHoursOverride?: string
}): Promise<ChannelLocation & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/locations`)
  return apiClient.post(endpoint, data)
}

export async function removeLocation(channelId: string, locationId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/locations/${locationId}`)
  return apiClient.delete(endpoint)
}

export async function triggerMenuSync(channelId: string, locationId?: string): Promise<HalResource> {
  const endpoint = apiClient.buildOrgPath(`/channels/${channelId}/menu-sync`)
  return apiClient.post(endpoint, locationId ? { locationId } : {})
}
