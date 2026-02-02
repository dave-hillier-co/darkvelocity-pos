// Tenant context
export interface TenantContext {
  orgId: string
  siteId: string
}

// Auth types - matches PinLoginResponse from backend
export interface User {
  id: string
  displayName: string
}

export interface PinLoginResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  userId: string
  displayName: string
}

// Legacy LoginResponse for compatibility
export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: User
}

// Menu types - matches backend MenuItemGrain snapshots
export interface MenuItem {
  id: string
  name: string
  price: number
  categoryId: string
  accountingGroupId?: string
  recipeId?: string
  description?: string
  imageUrl?: string
  sku?: string
  isActive: boolean
  trackInventory: boolean
}

export interface MenuCategory {
  id: string
  name: string
  displayOrder: number
  color?: string
  description?: string
  itemCount?: number
}

// Order types - matches backend OrderGrain state
export type OrderType = 'DirectSale' | 'TableService' | 'Tab' | 'Delivery' | 'TakeOut'
export type OrderStatus = 'Open' | 'Sent' | 'InProgress' | 'Ready' | 'Closed' | 'Voided'

export interface OrderLineModifier {
  id: string
  name: string
  price: number
}

export interface OrderLine {
  id: string
  menuItemId: string
  itemName: string
  quantity: number
  unitPrice: number
  notes?: string
  modifiers?: OrderLineModifier[]
  discountAmount: number
  discountReason?: string
  lineTotal: number
  sentAt?: string
}

export interface OrderDiscount {
  id: string
  name: string
  type: 'Percentage' | 'FixedAmount'
  value: number
  appliedBy: string
  reason?: string
}

export interface Order {
  id: string
  orderNumber: string
  type: OrderType
  status: OrderStatus
  tableId?: string
  tableNumber?: string
  customerId?: string
  guestCount?: number
  lines: OrderLine[]
  discounts: OrderDiscount[]
  subtotal: number
  taxTotal: number
  discountTotal: number
  grandTotal: number
  createdAt: string
  sentAt?: string
  closedAt?: string
}

// Backend request/response types
export interface CreateOrderRequest {
  createdBy: string
  type: OrderType
  tableId?: string
  tableNumber?: string
  customerId?: string
  guestCount?: number
}

export interface AddLineRequest {
  menuItemId: string
  name: string
  quantity: number
  unitPrice: number
  notes?: string
  modifiers?: OrderLineModifier[]
}

export interface SendOrderRequest {
  sentBy: string
}

export interface CloseOrderRequest {
  closedBy: string
}

export interface VoidOrderRequest {
  voidedBy: string
  reason: string
}

export interface ApplyDiscountRequest {
  name: string
  type: 'Percentage' | 'FixedAmount'
  value: number
  appliedBy: string
  discountId?: string
  reason?: string
  approvedBy?: string
}

// HAL types
export interface HalLink {
  href: string
  title?: string
}

export interface HalResource {
  _links: Record<string, HalLink>
}

export interface HalCollection<T> extends HalResource {
  _embedded: {
    items: T[]
  }
  count: number
  total?: number
}
