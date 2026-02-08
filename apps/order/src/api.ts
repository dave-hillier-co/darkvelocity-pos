import type { OrderingLink, GuestSession, CartModifier, OrderResult } from './types.ts'

const API_BASE = import.meta.env.VITE_API_URL || ''

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  }

  // Add org/site context headers for session endpoints
  const orgId = sessionStorage.getItem('orgId')
  const siteId = sessionStorage.getItem('siteId')
  if (orgId) headers['X-Org-Id'] = orgId
  if (siteId) headers['X-Site-Id'] = siteId

  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: { ...headers, ...(options.headers as Record<string, string>) },
  })

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Request failed' }))
    throw new Error(error.error_description || error.message || `HTTP ${response.status}`)
  }

  if (response.status === 204) return undefined as T
  return response.json()
}

export async function getOrderingMenu(orgId: string, siteId: string, linkCode: string): Promise<OrderingLink> {
  const data = await request<OrderingLink & { _links: Record<string, { href: string }> }>(
    `/api/public/ordering/${orgId}/${siteId}/${linkCode}`
  )
  // Store context for subsequent session calls
  sessionStorage.setItem('orgId', orgId)
  sessionStorage.setItem('siteId', siteId)
  return data
}

export async function startSession(orgId: string, siteId: string, linkCode: string): Promise<GuestSession> {
  return request<GuestSession>(
    `/api/public/ordering/${orgId}/${siteId}/${linkCode}/sessions`,
    { method: 'POST' }
  )
}

export async function addToCart(
  sessionId: string,
  menuItemId: string,
  name: string,
  quantity: number,
  unitPrice: number,
  notes?: string,
  modifiers?: CartModifier[]
): Promise<GuestSession> {
  return request<GuestSession>(
    `/api/public/ordering/sessions/${sessionId}/cart`,
    {
      method: 'POST',
      body: JSON.stringify({ menuItemId, name, quantity, unitPrice, notes, modifiers }),
    }
  )
}

export async function updateCartItem(
  sessionId: string,
  cartItemId: string,
  quantity?: number,
  notes?: string
): Promise<GuestSession> {
  return request<GuestSession>(
    `/api/public/ordering/sessions/${sessionId}/cart/${cartItemId}`,
    {
      method: 'PATCH',
      body: JSON.stringify({ quantity, notes }),
    }
  )
}

export async function removeFromCart(sessionId: string, cartItemId: string): Promise<GuestSession> {
  return request<GuestSession>(
    `/api/public/ordering/sessions/${sessionId}/cart/${cartItemId}`,
    { method: 'DELETE' }
  )
}

export async function submitOrder(
  sessionId: string,
  guestName?: string,
  guestPhone?: string
): Promise<OrderResult> {
  return request<OrderResult>(
    `/api/public/ordering/sessions/${sessionId}/submit`,
    {
      method: 'POST',
      body: JSON.stringify({ guestName, guestPhone }),
    }
  )
}

export async function getSessionStatus(sessionId: string): Promise<{
  sessionId: string
  sessionStatus: string
  orderId?: string
  orderNumber?: string
  orderStatus?: string
  cartTotal: number
  guestName?: string
  tableNumber?: string
}> {
  return request(`/api/public/ordering/sessions/${sessionId}/status`)
}
