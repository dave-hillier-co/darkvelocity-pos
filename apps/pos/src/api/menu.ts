import { apiClient } from './client'
import type { MenuItem, MenuCategory, HalCollection } from '../types'

const API_BASE = '/api/locations'

export interface MenuItemResponse extends MenuItem {
  _links: Record<string, { href: string }>
}

export interface MenuCategoryResponse extends MenuCategory {
  _links: Record<string, { href: string }>
}

export async function getCategories(locationId: string): Promise<MenuCategory[]> {
  const response = await apiClient.get<HalCollection<MenuCategoryResponse>>(
    `${API_BASE}/${locationId}/categories`
  )
  return response._embedded?.items ?? []
}

export async function getMenuItems(locationId: string, categoryId?: string): Promise<MenuItem[]> {
  const url = categoryId
    ? `${API_BASE}/${locationId}/categories/${categoryId}/items`
    : `${API_BASE}/${locationId}/items`

  const response = await apiClient.get<HalCollection<MenuItemResponse>>(url)
  return response._embedded?.items ?? []
}

export async function getMenuItem(locationId: string, itemId: string): Promise<MenuItem> {
  return apiClient.get<MenuItemResponse>(`${API_BASE}/${locationId}/items/${itemId}`)
}

export async function getFullMenu(locationId: string): Promise<{
  categories: MenuCategory[]
  items: MenuItem[]
}> {
  const [categories, items] = await Promise.all([
    getCategories(locationId),
    getMenuItems(locationId),
  ])

  return { categories, items }
}
