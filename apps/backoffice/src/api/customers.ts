import { apiClient } from './client'
import type { HalCollection, HalResource } from '../types'

export interface Customer extends HalResource {
  id: string
  firstName: string
  lastName: string
  email?: string
  phone?: string
  dateOfBirth?: string
  source: string
  loyalty?: CustomerLoyalty
  preferences: CustomerPreferences
  tags: string[]
  createdAt: string
}

export interface CustomerLoyalty {
  programId: string
  memberNumber: string
  tierId: string
  tierName: string
  pointsBalance: number
  lifetimePoints: number
}

export interface CustomerPreferences {
  dietaryRestrictions?: string[]
  allergens?: string[]
  seatingPreference?: string
  notes?: string
}

export interface CustomerVisit {
  siteId: string
  visitedAt: string
  orderId?: string
  spendAmount?: number
}

export interface CustomerReward extends HalResource {
  id: string
  name: string
  description?: string
  pointsCost: number
  isAvailable: boolean
}

export async function createCustomer(data: {
  firstName: string
  lastName: string
  email?: string
  phone?: string
  source?: string
}): Promise<{ id: string; displayName: string; createdAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgPath('/customers')
  return apiClient.post(endpoint, data)
}

export async function getCustomer(customerId: string): Promise<Customer> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}`)
  return apiClient.get(endpoint)
}

export async function updateCustomer(customerId: string, data: {
  firstName?: string
  lastName?: string
  email?: string
  phone?: string
  dateOfBirth?: string
  preferences?: CustomerPreferences
}): Promise<Customer> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}`)
  return apiClient.patch(endpoint, data)
}

export async function enrollLoyalty(customerId: string, data: {
  programId: string
  memberNumber: string
  initialTierId: string
  tierName: string
}): Promise<{ enrolled: boolean; programId: string } & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/loyalty/enroll`)
  return apiClient.post(endpoint, data)
}

export async function earnPoints(customerId: string, data: {
  points: number
  reason: string
  orderId?: string
  siteId?: string
  spendAmount?: number
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/loyalty/earn`)
  return apiClient.post(endpoint, data)
}

export async function redeemPoints(customerId: string, data: {
  points: number
  orderId: string
  reason: string
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/loyalty/redeem`)
  return apiClient.post(endpoint, data)
}

export async function getRewards(customerId: string): Promise<CustomerReward[]> {
  try {
    const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/rewards`)
    const response = await apiClient.get<HalCollection<CustomerReward>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getVisits(customerId: string, limit?: number): Promise<CustomerVisit[]> {
  try {
    const query = limit ? `?limit=${limit}` : ''
    const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/visits${query}`)
    const response = await apiClient.get<HalCollection<CustomerVisit>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getPreferences(customerId: string): Promise<CustomerPreferences & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/preferences`)
  return apiClient.get(endpoint)
}

export async function updatePreferences(customerId: string, data: {
  dietaryRestrictions?: string[]
  allergens?: string[]
  seatingPreference?: string
  notes?: string
}): Promise<CustomerPreferences & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/preferences`)
  return apiClient.patch(endpoint, data)
}

export async function getTags(customerId: string): Promise<{ tags: string[] } & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/tags`)
  return apiClient.get(endpoint)
}

export async function addTag(customerId: string, tag: string): Promise<{ tag: string; added: boolean } & HalResource> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/tags`)
  return apiClient.post(endpoint, { tag })
}

export async function removeTag(customerId: string, tag: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/customers/${customerId}/tags/${encodeURIComponent(tag)}`)
  return apiClient.delete(endpoint)
}
