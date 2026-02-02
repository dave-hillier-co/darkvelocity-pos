import { apiClient } from './client'

export interface Category {
  id: string
  name: string
  description?: string
  displayOrder: number
  color?: string
  itemCount?: number
  _links: {
    self: { href: string }
    items: { href: string }
  }
}

export interface MenuItem {
  id: string
  categoryId: string
  accountingGroupId?: string
  recipeId?: string
  name: string
  description?: string
  price: number
  imageUrl?: string
  sku?: string
  isActive: boolean
  trackInventory: boolean
  _links: {
    self: { href: string }
    category: { href: string }
  }
}

export interface HalCollection<T> {
  _embedded: {
    items: T[]
  }
  _links: {
    self: { href: string }
  }
  total?: number
  count: number
}

// Categories - at org level
export async function getCategories(): Promise<Category[]> {
  try {
    const endpoint = apiClient.buildOrgPath('/menu/categories')
    const response = await apiClient.get<HalCollection<Category>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getCategory(categoryId: string): Promise<Category> {
  const endpoint = apiClient.buildOrgPath(`/menu/categories/${categoryId}`)
  return apiClient.get<Category>(endpoint)
}

export async function createCategory(data: {
  locationId: string
  name: string
  description?: string
  displayOrder: number
  color?: string
}): Promise<Category> {
  const endpoint = apiClient.buildOrgPath('/menu/categories')
  return apiClient.post<Category>(endpoint, data)
}

export async function updateCategory(categoryId: string, data: {
  name?: string
  description?: string
  displayOrder?: number
  color?: string
}): Promise<Category> {
  const endpoint = apiClient.buildOrgPath(`/menu/categories/${categoryId}`)
  return apiClient.patch<Category>(endpoint, data)
}

export async function deleteCategory(categoryId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/menu/categories/${categoryId}`)
  return apiClient.delete(endpoint)
}

// Menu Items - at org level
export async function getMenuItems(categoryId?: string): Promise<MenuItem[]> {
  try {
    const endpoint = categoryId
      ? apiClient.buildOrgPath(`/menu/categories/${categoryId}/items`)
      : apiClient.buildOrgPath('/menu/items')
    const response = await apiClient.get<HalCollection<MenuItem>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getMenuItem(itemId: string): Promise<MenuItem> {
  const endpoint = apiClient.buildOrgPath(`/menu/items/${itemId}`)
  return apiClient.get<MenuItem>(endpoint)
}

export async function createMenuItem(data: {
  locationId: string
  categoryId: string
  name: string
  price: number
  accountingGroupId?: string
  recipeId?: string
  description?: string
  imageUrl?: string
  sku?: string
  trackInventory: boolean
}): Promise<MenuItem> {
  const endpoint = apiClient.buildOrgPath('/menu/items')
  return apiClient.post<MenuItem>(endpoint, data)
}

export async function updateMenuItem(itemId: string, data: {
  categoryId?: string
  accountingGroupId?: string
  recipeId?: string
  name?: string
  description?: string
  price?: number
  imageUrl?: string
  sku?: string
  isActive?: boolean
  trackInventory?: boolean
}): Promise<MenuItem> {
  const endpoint = apiClient.buildOrgPath(`/menu/items/${itemId}`)
  return apiClient.patch<MenuItem>(endpoint, data)
}

export async function deleteMenuItem(itemId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/menu/items/${itemId}`)
  return apiClient.delete(endpoint)
}
