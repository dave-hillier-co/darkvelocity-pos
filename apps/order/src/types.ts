export interface MenuCategory {
  id: string
  name: string
  displayOrder: number
  color?: string
  itemCount?: number
}

export interface MenuItem {
  id: string
  name: string
  price: number
  categoryId?: string
  description?: string
  imageUrl?: string
}

export interface MenuModifier {
  blockId: string
  name: string
  isRequired: boolean
  minSelections: number
  maxSelections: number
  options: MenuModifierOption[]
}

export interface MenuModifierOption {
  optionId: string
  name: string
  priceAdjustment: number
  isDefault: boolean
}

export interface CartItem {
  cartItemId: string
  menuItemId: string
  name: string
  quantity: number
  unitPrice: number
  notes?: string
  modifiers?: CartModifier[]
}

export interface CartModifier {
  modifierId: string
  name: string
  priceAdjustment: number
}

export interface GuestSession {
  id: string
  status: 'Active' | 'Submitted' | 'Completed' | 'Abandoned'
  type: 'TableQr' | 'TakeOut' | 'Kiosk'
  tableNumber?: string
  cartItems: CartItem[]
  cartTotal: number
  orderId?: string
  orderNumber?: string
  guestName?: string
}

export interface OrderingLink {
  linkId: string
  siteName: string
  type: 'TableQr' | 'TakeOut' | 'Kiosk'
  tableId?: string
  tableNumber?: string
  categories: MenuCategory[]
  items: MenuItem[]
}

export interface OrderResult {
  orderId: string
  orderNumber: string
  submittedAt: string
  status: string
}

export interface HalResource {
  _links: Record<string, { href: string }>
}
