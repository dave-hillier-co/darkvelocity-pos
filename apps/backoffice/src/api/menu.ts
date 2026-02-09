import { apiClient } from './client'
import type { HalCollection, DocumentSnapshot } from '../types'

// ========================================================================
// Content Types (what the CMS documents contain)
// ========================================================================

export interface MenuItemContent {
  name: string
  description?: string
  price: number
  imageUrl?: string
  categoryId?: string
  accountingGroupId?: string
  recipeId?: string
  sku?: string
  trackInventory: boolean
  modifierBlockIds?: string[]
  tagIds?: string[]
}

export interface CategoryContent {
  name: string
  description?: string
  color?: string
  iconUrl?: string
  displayOrder: number
  itemDocumentIds?: string[]
}

export interface ModifierBlockContent {
  name: string
  selectionRule: string
  minSelections: number
  maxSelections: number
  isRequired: boolean
  options: ModifierOption[]
}

export interface ModifierOption {
  optionId: string
  name: string
  priceAdjustment: number
  isDefault: boolean
  displayOrder: number
  isActive: boolean
}

export interface ContentTag {
  tagId: string
  orgId: string
  name: string
  category: string
  iconUrl?: string
  badgeColor?: string
  displayOrder: number
  isActive: boolean
  externalTagId?: string
  externalPlatform?: string
}

// ========================================================================
// Summary Types (from registry list endpoints)
// ========================================================================

export interface MenuItemSummary {
  documentId: string
  name: string
  price: number
  categoryId?: string
  hasDraft: boolean
  isArchived: boolean
  publishedVersion?: number
  lastModified?: string
}

export interface CategorySummary {
  documentId: string
  name: string
  displayOrder: number
  color?: string
  hasDraft: boolean
  isArchived: boolean
  itemCount: number
  lastModified?: string
}

// ========================================================================
// Request Types
// ========================================================================

export interface CreateMenuItemRequest {
  name: string
  price: number
  description?: string
  categoryId?: string
  accountingGroupId?: string
  recipeId?: string
  imageUrl?: string
  sku?: string
  trackInventory?: boolean
  locale?: string
  publishImmediately?: boolean
  tagIds?: string[]
}

export interface CreateMenuItemDraftRequest {
  name: string
  price: number
  description?: string
  categoryId?: string
  accountingGroupId?: string
  recipeId?: string
  imageUrl?: string
  sku?: string
  trackInventory?: boolean
  modifierBlockIds?: string[]
  tagIds?: string[]
  changeNote?: string
}

export interface CreateCategoryRequest {
  name: string
  displayOrder: number
  description?: string
  color?: string
  iconUrl?: string
  locale?: string
  publishImmediately?: boolean
}

export interface CreateCategoryDraftRequest {
  name: string
  displayOrder: number
  description?: string
  color?: string
  iconUrl?: string
  itemDocumentIds?: string[]
  changeNote?: string
}

export interface CreateModifierBlockRequest {
  name: string
  selectionRule: string
  minSelections: number
  maxSelections: number
  isRequired: boolean
  options?: CreateModifierOptionRequest[]
  publishImmediately?: boolean
}

export interface CreateModifierOptionRequest {
  name: string
  priceAdjustment: number
  isDefault: boolean
  displayOrder: number
  servingSize?: string
  servingUnit?: string
  inventoryItemId?: string
}

export interface CreateContentTagRequest {
  name: string
  category: string
  iconUrl?: string
  badgeColor?: string
  displayOrder?: number
  externalTagId?: string
  externalPlatform?: string
}

export interface UpdateContentTagRequest {
  name?: string
  iconUrl?: string
  badgeColor?: string
  displayOrder?: number
  isActive?: boolean
}

// ========================================================================
// Menu Item Document API
// ========================================================================

export async function createMenuItem(data: CreateMenuItemRequest): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath('/menu/cms/items')
  return apiClient.post<DocumentSnapshot<MenuItemContent>>(endpoint, data)
}

export async function getMenuItems(categoryId?: string, includeArchived = false): Promise<MenuItemSummary[]> {
  let endpoint = apiClient.buildOrgPath('/menu/cms/items')
  const params = new URLSearchParams()
  if (categoryId) params.set('categoryId', categoryId)
  if (includeArchived) params.set('includeArchived', 'true')
  const qs = params.toString()
  if (qs) endpoint += `?${qs}`

  const response = await apiClient.get<{ items: MenuItemSummary[] }>(endpoint)
  return response.items ?? []
}

export async function getMenuItem(documentId: string): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}`)
  return apiClient.get<DocumentSnapshot<MenuItemContent>>(endpoint)
}

export async function createMenuItemDraft(documentId: string, data: CreateMenuItemDraftRequest): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/draft`)
  return apiClient.post<DocumentSnapshot<MenuItemContent>>(endpoint, data)
}

export async function getMenuItemDraft(documentId: string): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/draft`)
  return apiClient.get<DocumentSnapshot<MenuItemContent>>(endpoint)
}

export async function discardMenuItemDraft(documentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/draft`)
  return apiClient.delete(endpoint)
}

export async function publishMenuItem(documentId: string, note?: string): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/publish`)
  return apiClient.post<DocumentSnapshot<MenuItemContent>>(endpoint, note ? { note } : undefined)
}

export async function getMenuItemVersions(documentId: string, skip = 0, take = 20): Promise<{ versions: unknown[] }> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/versions?skip=${skip}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function revertMenuItem(documentId: string, version: number, reason?: string): Promise<DocumentSnapshot<MenuItemContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/revert`)
  return apiClient.post<DocumentSnapshot<MenuItemContent>>(endpoint, { version, reason })
}

export async function archiveMenuItem(documentId: string, reason?: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/archive`)
  return apiClient.post(endpoint, reason ? { reason } : undefined)
}

export async function restoreMenuItem(documentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/items/${documentId}/restore`)
  return apiClient.post(endpoint)
}

// ========================================================================
// Category Document API
// ========================================================================

export async function createCategory(data: CreateCategoryRequest): Promise<DocumentSnapshot<CategoryContent>> {
  const endpoint = apiClient.buildOrgPath('/menu/cms/categories')
  return apiClient.post<DocumentSnapshot<CategoryContent>>(endpoint, data)
}

export async function getCategories(includeArchived = false): Promise<CategorySummary[]> {
  let endpoint = apiClient.buildOrgPath('/menu/cms/categories')
  if (includeArchived) endpoint += '?includeArchived=true'

  const response = await apiClient.get<{ categories: CategorySummary[] }>(endpoint)
  return response.categories ?? []
}

export async function getCategory(documentId: string): Promise<DocumentSnapshot<CategoryContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/categories/${documentId}`)
  return apiClient.get<DocumentSnapshot<CategoryContent>>(endpoint)
}

export async function createCategoryDraft(documentId: string, data: CreateCategoryDraftRequest): Promise<DocumentSnapshot<CategoryContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/categories/${documentId}/draft`)
  return apiClient.post<DocumentSnapshot<CategoryContent>>(endpoint, data)
}

export async function publishCategory(documentId: string, note?: string): Promise<DocumentSnapshot<CategoryContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/categories/${documentId}/publish`)
  return apiClient.post<DocumentSnapshot<CategoryContent>>(endpoint, note ? { note } : undefined)
}

export async function getCategoryVersions(documentId: string, skip = 0, take = 20): Promise<{ versions: unknown[] }> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/categories/${documentId}/versions?skip=${skip}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function revertCategory(documentId: string, version: number, reason?: string): Promise<DocumentSnapshot<CategoryContent>> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/categories/${documentId}/revert`)
  return apiClient.post<DocumentSnapshot<CategoryContent>>(endpoint, { version, reason })
}

// ========================================================================
// Modifier Block API
// ========================================================================

export async function createModifierBlock(data: CreateModifierBlockRequest): Promise<ModifierBlockContent> {
  const endpoint = apiClient.buildOrgPath('/menu/cms/modifier-blocks')
  return apiClient.post<ModifierBlockContent>(endpoint, data)
}

export async function getModifierBlocks(): Promise<ModifierBlockContent[]> {
  const endpoint = apiClient.buildOrgPath('/menu/cms/modifier-blocks')
  const response = await apiClient.get<{ modifierBlocks: ModifierBlockContent[] }>(endpoint)
  return response.modifierBlocks ?? []
}

export async function getModifierBlock(blockId: string): Promise<ModifierBlockContent> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/modifier-blocks/${blockId}`)
  return apiClient.get<ModifierBlockContent>(endpoint)
}

export async function getModifierBlockVersions(blockId: string, skip = 0, take = 20): Promise<{ versions: unknown[] }> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/modifier-blocks/${blockId}/versions?skip=${skip}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function revertModifierBlock(blockId: string, version: number, reason?: string): Promise<ModifierBlockContent> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/modifier-blocks/${blockId}/revert`)
  return apiClient.post<ModifierBlockContent>(endpoint, { version, reason })
}

export async function publishModifierBlock(blockId: string, note?: string): Promise<ModifierBlockContent> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/modifier-blocks/${blockId}/publish`)
  return apiClient.post<ModifierBlockContent>(endpoint, note ? { note } : undefined)
}

// ========================================================================
// Content Tag API
// ========================================================================

export async function createContentTag(data: CreateContentTagRequest): Promise<ContentTag> {
  const endpoint = apiClient.buildOrgPath('/menu/cms/tags')
  return apiClient.post<ContentTag>(endpoint, data)
}

export async function getContentTags(category?: string): Promise<ContentTag[]> {
  let endpoint = apiClient.buildOrgPath('/menu/cms/tags')
  if (category) endpoint += `?category=${encodeURIComponent(category)}`

  const response = await apiClient.get<{ tags: ContentTag[] }>(endpoint)
  return response.tags ?? []
}

export async function updateContentTag(tagId: string, data: UpdateContentTagRequest): Promise<ContentTag> {
  const endpoint = apiClient.buildOrgPath(`/menu/cms/tags/${tagId}`)
  return apiClient.patch<ContentTag>(endpoint, data)
}
