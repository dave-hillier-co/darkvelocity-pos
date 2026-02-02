import { apiClient } from './client'
import type { HalResource } from '../types'

// Payment method enum matching backend
export type PaymentMethodType = 'Cash' | 'Card' | 'GiftCard' | 'StoreCredit' | 'External'

// Card info matching backend
export interface CardInfo {
  lastFour: string
  brand: string
  expiryMonth: number
  expiryYear: number
  cardholderName?: string
}

// Request types matching backend contracts
export interface InitiatePaymentRequest {
  orderId: string
  method: PaymentMethodType
  amount: number
  cashierId: string
  customerId?: string
  drawerId?: string
}

export interface CompleteCashRequest {
  amountTendered: number
  tipAmount?: number
}

export interface CompleteCardRequest {
  gatewayReference: string
  authorizationCode: string
  cardInfo: CardInfo
  gatewayName: string
  tipAmount?: number
}

export interface VoidPaymentRequest {
  voidedBy: string
  reason: string
}

export interface RefundPaymentRequest {
  amount: number
  reason: string
  issuedBy: string
}

// Response types
export interface InitiatePaymentResult {
  id: string
  createdAt: string
}

export interface CashPaymentResult {
  totalAmount: number
  amountTendered: number
  changeAmount: number
  tipAmount: number
}

export interface CardPaymentResult {
  totalAmount: number
  authorizationCode: string
  lastFour: string
  tipAmount: number
}

export interface RefundResult {
  refundId: string
  amount: number
  issuedAt: string
}

export interface PaymentState {
  id: string
  orderId: string
  method: PaymentMethodType
  amount: number
  tipAmount: number
  totalAmount: number
  status: 'Pending' | 'Completed' | 'Voided' | 'Refunded' | 'PartiallyRefunded'
  cashierId: string
  customerId?: string
  cardInfo?: CardInfo
  createdAt: string
  completedAt?: string
}

// API functions
export async function initiatePayment(request: InitiatePaymentRequest): Promise<InitiatePaymentResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath('/payments')
  return apiClient.post<InitiatePaymentResult & HalResource>(endpoint, request)
}

export async function getPayment(paymentId: string): Promise<PaymentState & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/payments/${paymentId}`)
  return apiClient.get<PaymentState & HalResource>(endpoint)
}

export async function completeCashPayment(paymentId: string, request: CompleteCashRequest): Promise<CashPaymentResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/payments/${paymentId}/complete-cash`)
  return apiClient.post<CashPaymentResult & HalResource>(endpoint, request)
}

export async function completeCardPayment(paymentId: string, request: CompleteCardRequest): Promise<CardPaymentResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/payments/${paymentId}/complete-card`)
  return apiClient.post<CardPaymentResult & HalResource>(endpoint, request)
}

export async function voidPayment(paymentId: string, request: VoidPaymentRequest): Promise<{ message: string }> {
  const endpoint = apiClient.buildOrgSitePath(`/payments/${paymentId}/void`)
  return apiClient.post(endpoint, request)
}

export async function refundPayment(paymentId: string, request: RefundPaymentRequest): Promise<RefundResult & HalResource> {
  const endpoint = apiClient.buildOrgSitePath(`/payments/${paymentId}/refund`)
  return apiClient.post<RefundResult & HalResource>(endpoint, request)
}
