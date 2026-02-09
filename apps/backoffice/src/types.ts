// HAL+JSON types (centralized)
export interface HalLink {
  href: string
  title?: string
  templated?: boolean
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

// Document versioning types (CMS pattern)
export interface VersionInfo {
  version: number
  publishedAt: string
  publishedBy: string
  changeNote?: string
}

export interface DocumentSnapshot<T> {
  documentId: string
  currentVersion: number
  status: 'draft' | 'published' | 'archived'
  hasDraft: boolean
  content: T
  versions?: VersionInfo[]
  createdAt: string
  updatedAt: string
  _links: Record<string, HalLink>
}

// Auth types
export interface AdminUser {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: AdminUser
}

// Location types
export interface Location {
  id: string
  name: string
  timezone: string
  currencyCode: string
  address?: string
  phone?: string
  isActive: boolean
}

// Menu types
export interface MenuItem {
  id: string
  name: string
  price: number
  categoryId: string
  accountingGroupId: string
  isActive: boolean
}

export interface MenuCategory {
  id: string
  name: string
  displayOrder: number
  color?: string
  itemCount?: number
}

// Inventory types
export interface Ingredient {
  id: string
  code: string
  name: string
  unitOfMeasure: string
  category: string
  storageType: string
  reorderLevel: number
  currentStock?: number
}

export interface Recipe {
  id: string
  code: string
  name: string
  menuItemId: string
  portionYield: number
  estimatedCost: number
  ingredients: RecipeIngredient[]
}

export interface RecipeIngredient {
  ingredientId: string
  ingredientName: string
  quantity: number
  unitOfMeasure: string
  wastePercentage: number
}

// Supplier types
export interface Supplier {
  id: string
  code: string
  name: string
  contactEmail: string
  paymentTermsDays: number
  leadTimeDays: number
  isActive: boolean
}
