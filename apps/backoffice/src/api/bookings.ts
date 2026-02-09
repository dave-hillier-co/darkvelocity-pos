import { apiClient } from './client'
import type { HalResource } from '../types'

export interface GuestInfo {
  name: string
  phone?: string
  email?: string
}

export interface Booking extends HalResource {
  id: string
  guest: GuestInfo
  requestedTime: string
  confirmedTime?: string
  partySize: number
  duration?: string
  status: BookingStatus
  specialRequests?: string
  occasion?: string
  source: string
  externalRef?: string
  customerId?: string
  confirmationCode: string
  arrivedAt?: string
  seatedAt?: string
  completedAt?: string
  cancelledAt?: string
  cancelReason?: string
}

export type BookingStatus =
  | 'Requested'
  | 'PendingDeposit'
  | 'Confirmed'
  | 'Arrived'
  | 'Seated'
  | 'Completed'
  | 'NoShow'
  | 'Cancelled'

export async function requestBooking(data: {
  guest: GuestInfo
  requestedTime: string
  partySize: number
  duration?: string
  specialRequests?: string
  occasion?: string
  source?: string
  externalRef?: string
  customerId?: string
}): Promise<{ id: string; confirmationCode: string; createdAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/bookings')
  return apiClient.post(endpoint, data)
}

export async function getBooking(bookingId: string): Promise<Booking> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}`)
  return apiClient.get(endpoint)
}

export async function confirmBooking(bookingId: string, data?: {
  confirmedTime?: string
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}/confirm`)
  return apiClient.post(endpoint, data ?? {})
}

export async function cancelBooking(bookingId: string, data: {
  reason: string
  cancelledBy: string
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}/cancel`)
  return apiClient.post(endpoint, data)
}

export async function checkinBooking(bookingId: string, data: {
  checkedInBy: string
}): Promise<{ arrivedAt: string } & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}/checkin`)
  return apiClient.post(endpoint, data)
}

export async function seatBooking(bookingId: string, data: {
  tableId: string
  tableNumber: string
  seatedBy: string
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}/seat`)
  return apiClient.post(endpoint, data)
}

export async function completeBooking(bookingId: string, data?: {
  orderId?: string
}): Promise<HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/bookings/${bookingId}/complete`)
  return apiClient.post(endpoint, data ?? {})
}
