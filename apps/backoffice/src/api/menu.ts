import { apiClient } from './client'

export interface Category {
  id: string
  locationId: string
  name: string
  displayOrder: number
  color: string
  isActive: boolean
  _links: {
    self: { href: string }
    items: { href: string }
  }
}

export interface MenuItem {
  id: string
  locationId: string
  categoryId: string
  accountingGroupId: string
  name: string
  description: string
  price: number
  imageUrl: string | null
  isActive: boolean
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
  total: number
}

export async function getCategories(locationId: string): Promise<HalCollection<Category>> {
  return apiClient.get(`/api/locations/${locationId}/categories`)
}

export async function getCategory(locationId: string, categoryId: string): Promise<Category> {
  return apiClient.get(`/api/locations/${locationId}/categories/${categoryId}`)
}

export async function createCategory(locationId: string, data: {
  name: string
  displayOrder?: number
  color?: string
}): Promise<Category> {
  return apiClient.post(`/api/locations/${locationId}/categories`, data)
}

export async function updateCategory(locationId: string, categoryId: string, data: {
  name?: string
  displayOrder?: number
  color?: string
  isActive?: boolean
}): Promise<Category> {
  return apiClient.put(`/api/locations/${locationId}/categories/${categoryId}`, data)
}

export async function deleteCategory(locationId: string, categoryId: string): Promise<void> {
  return apiClient.delete(`/api/locations/${locationId}/categories/${categoryId}`)
}

export async function getMenuItems(locationId: string, categoryId?: string): Promise<HalCollection<MenuItem>> {
  const url = categoryId
    ? `/api/locations/${locationId}/categories/${categoryId}/items`
    : `/api/locations/${locationId}/items`
  return apiClient.get(url)
}

export async function getMenuItem(locationId: string, itemId: string): Promise<MenuItem> {
  return apiClient.get(`/api/locations/${locationId}/items/${itemId}`)
}

export async function createMenuItem(locationId: string, data: {
  categoryId: string
  accountingGroupId: string
  name: string
  description?: string
  price: number
}): Promise<MenuItem> {
  return apiClient.post(`/api/locations/${locationId}/items`, data)
}

export async function updateMenuItem(locationId: string, itemId: string, data: {
  categoryId?: string
  name?: string
  description?: string
  price?: number
  isActive?: boolean
}): Promise<MenuItem> {
  return apiClient.put(`/api/locations/${locationId}/items/${itemId}`, data)
}

export async function deleteMenuItem(locationId: string, itemId: string): Promise<void> {
  return apiClient.delete(`/api/locations/${locationId}/items/${itemId}`)
}
