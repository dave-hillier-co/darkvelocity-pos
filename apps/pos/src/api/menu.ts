import { apiClient } from './client'
import type { MenuItem, MenuCategory, HalCollection, HalResource } from '../types'

// Response types with HAL links
export interface MenuItemResponse extends MenuItem, HalResource {}
export interface MenuCategoryResponse extends MenuCategory, HalResource {}

// Menu is at org level, not site level
export async function getCategories(): Promise<MenuCategory[]> {
  const endpoint = apiClient.buildOrgPath('/menu/categories')
  // Note: Backend doesn't have a list endpoint yet, this may need a lookup grain
  // For now, return empty array - will need backend endpoint added
  try {
    const response = await apiClient.get<HalCollection<MenuCategoryResponse>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    // Endpoint may not exist yet
    return []
  }
}

export async function getCategory(categoryId: string): Promise<MenuCategoryResponse> {
  const endpoint = apiClient.buildOrgPath(`/menu/categories/${categoryId}`)
  return apiClient.get<MenuCategoryResponse>(endpoint)
}

export async function getMenuItems(): Promise<MenuItem[]> {
  const endpoint = apiClient.buildOrgPath('/menu/items')
  // Note: Backend doesn't have a list endpoint yet
  try {
    const response = await apiClient.get<HalCollection<MenuItemResponse>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getMenuItem(itemId: string): Promise<MenuItemResponse> {
  const endpoint = apiClient.buildOrgPath(`/menu/items/${itemId}`)
  return apiClient.get<MenuItemResponse>(endpoint)
}

export async function getCategoryItems(categoryId: string): Promise<MenuItem[]> {
  const endpoint = apiClient.buildOrgPath(`/menu/categories/${categoryId}/items`)
  try {
    const response = await apiClient.get<HalCollection<MenuItemResponse>>(endpoint)
    return response._embedded?.items ?? []
  } catch {
    return []
  }
}

export async function getFullMenu(): Promise<{
  categories: MenuCategory[]
  items: MenuItem[]
}> {
  const [categories, items] = await Promise.all([
    getCategories(),
    getMenuItems(),
  ])

  return { categories, items }
}

// Create/update operations for backoffice use
export interface CreateMenuCategoryRequest {
  locationId: string
  name: string
  description?: string
  displayOrder: number
  color?: string
}

export interface CreateMenuItemRequest {
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
}

export interface UpdateMenuItemRequest {
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
}

export async function createCategory(request: CreateMenuCategoryRequest): Promise<MenuCategoryResponse> {
  const endpoint = apiClient.buildOrgPath('/menu/categories')
  return apiClient.post<MenuCategoryResponse>(endpoint, request)
}

export async function createMenuItem(request: CreateMenuItemRequest): Promise<MenuItemResponse> {
  const endpoint = apiClient.buildOrgPath('/menu/items')
  return apiClient.post<MenuItemResponse>(endpoint, request)
}

export async function updateMenuItem(itemId: string, request: UpdateMenuItemRequest): Promise<MenuItemResponse> {
  const endpoint = apiClient.buildOrgPath(`/menu/items/${itemId}`)
  return apiClient.patch<MenuItemResponse>(endpoint, request)
}
