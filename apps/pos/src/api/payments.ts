import { apiClient } from './client'

const API_BASE = '/api/locations'

export interface PaymentMethod {
  id: string
  name: string
  methodType: 'cash' | 'card' | 'voucher' | 'other'
  isActive: boolean
  requiresConfirmation: boolean
}

export interface CreatePaymentRequest {
  orderId: string
  paymentMethodId: string
  amount: number
  tipAmount?: number
  receivedAmount?: number
  reference?: string
}

export interface Payment {
  id: string
  orderId: string
  paymentMethodId: string
  paymentMethodName: string
  amount: number
  tipAmount: number
  receivedAmount: number
  changeAmount: number
  status: 'pending' | 'completed' | 'failed' | 'refunded'
  stripePaymentIntentId?: string
  createdAt: string
}

export interface Receipt {
  id: string
  paymentId: string
  receiptNumber: string
  receiptData: {
    header: string[]
    lines: Array<{
      name: string
      quantity: number
      unitPrice: number
      total: number
    }>
    subtotal: number
    tax: number
    total: number
    footer: string[]
  }
  printedAt?: string
}

export async function getPaymentMethods(locationId: string): Promise<PaymentMethod[]> {
  const response = await apiClient.get<{ _embedded?: { items: PaymentMethod[] } }>(
    `${API_BASE}/${locationId}/payment-methods`
  )
  return response._embedded?.items ?? []
}

export async function createPayment(
  locationId: string,
  request: CreatePaymentRequest
): Promise<Payment> {
  return apiClient.post<Payment>(`${API_BASE}/${locationId}/payments`, request)
}

export async function getPayment(locationId: string, paymentId: string): Promise<Payment> {
  return apiClient.get<Payment>(`${API_BASE}/${locationId}/payments/${paymentId}`)
}

export async function getReceipt(paymentId: string): Promise<Receipt> {
  return apiClient.get<Receipt>(`/api/payments/${paymentId}/receipt`)
}

export async function printReceipt(paymentId: string, printerId: string): Promise<void> {
  await apiClient.post(`/api/payments/${paymentId}/print`, { printerId })
}

// Stripe integration
export interface CreatePaymentIntentRequest {
  amount: number
  currency?: string
  orderId: string
}

export interface PaymentIntent {
  clientSecret: string
  paymentIntentId: string
}

export async function createPaymentIntent(
  request: CreatePaymentIntentRequest
): Promise<PaymentIntent> {
  return apiClient.post<PaymentIntent>('/api/stripe/create-payment-intent', request)
}
