import { apiClient } from './client'
import type { HalCollection, HalLink } from '../types'

// Recipe CMS types matching backend RecipeCmsEndpoints responses

export interface RecipeIngredientResponse {
  ingredientId: string
  ingredientName: string
  quantity: number
  unit: string
  wastePercentage: number
  effectiveQuantity: number
  unitCost: number
  lineCost: number
  prepInstructions?: string
  isOptional: boolean
  displayOrder: number
  substitutionIds: string[]
}

export interface RecipeVersionResponse {
  versionNumber: number
  createdAt: string
  createdBy?: string
  changeNote?: string
  name: string
  description?: string
  portionYield: number
  yieldUnit?: string
  ingredients: RecipeIngredientResponse[]
  allergenTags: string[]
  dietaryTags: string[]
  prepInstructions?: string
  prepTimeMinutes?: number
  cookTimeMinutes?: number
  imageUrl?: string
  categoryId?: string
  theoreticalCost: number
  costPerPortion: number
}

export interface ScheduledChangeResponse {
  scheduleId: string
  version: number
  activateAt: string
  deactivateAt?: string
  name?: string
  isActive: boolean
}

export interface RecipeDocumentResponse {
  documentId: string
  orgId: string
  currentVersion: number
  publishedVersion?: number
  draftVersion?: number
  isArchived: boolean
  createdAt: string
  published?: RecipeVersionResponse
  draft?: RecipeVersionResponse
  schedules: ScheduledChangeResponse[]
  totalVersions: number
  linkedMenuItemIds: string[]
  _links: Record<string, HalLink>
}

export interface RecipeSummaryResponse {
  documentId: string
  name: string
  costPerPortion: number
  categoryId?: string
  hasDraft: boolean
  isArchived: boolean
  publishedVersion?: number
  lastModified?: string
  linkedMenuItemCount: number
}

export interface RecipeCategoryVersionResponse {
  versionNumber: number
  createdAt: string
  createdBy?: string
  changeNote?: string
  name: string
  description?: string
  color?: string
  iconUrl?: string
  displayOrder: number
  recipeDocumentIds: string[]
}

export interface RecipeCategoryDocumentResponse {
  documentId: string
  orgId: string
  currentVersion: number
  publishedVersion?: number
  draftVersion?: number
  isArchived: boolean
  createdAt: string
  published?: RecipeCategoryVersionResponse
  draft?: RecipeCategoryVersionResponse
  schedules: ScheduledChangeResponse[]
  totalVersions: number
}

export interface RecipeCategorySummaryResponse {
  documentId: string
  name: string
  displayOrder: number
  color?: string
  hasDraft: boolean
  isArchived: boolean
  recipeCount: number
  lastModified?: string
}

// Request types

export interface CreateRecipeIngredientRequest {
  ingredientId: string
  ingredientName: string
  quantity: number
  unit: string
  wastePercentage: number
  unitCost: number
  prepInstructions?: string
  isOptional?: boolean
  displayOrder?: number
  substitutionIds?: string[]
}

export interface CreateRecipeRequest {
  name: string
  description?: string
  portionYield: number
  yieldUnit?: string
  ingredients?: CreateRecipeIngredientRequest[]
  allergenTags?: string[]
  dietaryTags?: string[]
  prepInstructions?: string
  prepTimeMinutes?: number
  cookTimeMinutes?: number
  imageUrl?: string
  categoryId?: string
  locale?: string
  publishImmediately?: boolean
}

export interface CreateRecipeDraftRequest {
  name: string
  description?: string
  portionYield: number
  yieldUnit?: string
  ingredients?: CreateRecipeIngredientRequest[]
  allergenTags?: string[]
  dietaryTags?: string[]
  prepInstructions?: string
  prepTimeMinutes?: number
  cookTimeMinutes?: number
  imageUrl?: string
  categoryId?: string
  changeNote?: string
}

export interface CreateRecipeCategoryRequest {
  name: string
  displayOrder: number
  description?: string
  color?: string
  iconUrl?: string
  locale?: string
  publishImmediately?: boolean
}

export interface CreateRecipeCategoryDraftRequest {
  name: string
  displayOrder: number
  description?: string
  color?: string
  iconUrl?: string
  recipeDocumentIds?: string[]
  changeNote?: string
}

// Recipe Document API functions

export async function createRecipe(data: CreateRecipeRequest): Promise<RecipeDocumentResponse> {
  const endpoint = apiClient.buildOrgPath('/recipes/cms/recipes')
  return apiClient.post(endpoint, data)
}

export async function getRecipes(categoryId?: string, includeArchived = false): Promise<{ recipes: RecipeSummaryResponse[] }> {
  let endpoint = apiClient.buildOrgPath('/recipes/cms/recipes')
  const params = new URLSearchParams()
  if (categoryId) params.set('categoryId', categoryId)
  if (includeArchived) params.set('includeArchived', 'true')
  const qs = params.toString()
  if (qs) endpoint += `?${qs}`
  return apiClient.get(endpoint)
}

export async function getRecipe(documentId: string): Promise<RecipeDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}`)
  return apiClient.get(endpoint)
}

export async function createDraft(documentId: string, data: CreateRecipeDraftRequest): Promise<RecipeVersionResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/draft`)
  return apiClient.post(endpoint, data)
}

export async function getDraft(documentId: string): Promise<RecipeVersionResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/draft`)
  return apiClient.get(endpoint)
}

export async function discardDraft(documentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/draft`)
  return apiClient.delete(endpoint)
}

export async function publishDraft(documentId: string, note?: string): Promise<RecipeDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/publish`)
  return apiClient.post(endpoint, note ? { note } : undefined)
}

export async function getVersions(documentId: string, skip = 0, take = 20): Promise<{ versions: RecipeVersionResponse[] }> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/versions?skip=${skip}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function revertToVersion(documentId: string, version: number, reason?: string): Promise<RecipeDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/revert`)
  return apiClient.post(endpoint, { version, reason })
}

export async function archiveRecipe(documentId: string, reason?: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/archive`)
  return apiClient.post(endpoint, reason ? { reason } : undefined)
}

export async function restoreRecipe(documentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/restore`)
  return apiClient.post(endpoint)
}

export async function recalculateCost(documentId: string, ingredientPrices?: Record<string, number>): Promise<RecipeDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/recalculate-cost`)
  return apiClient.post(endpoint, ingredientPrices ? { ingredientPrices } : undefined)
}

export async function linkMenuItem(documentId: string, menuItemDocumentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/link-menu-item`)
  return apiClient.post(endpoint, { menuItemDocumentId })
}

export async function unlinkMenuItem(documentId: string, menuItemDocumentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/link-menu-item/${menuItemDocumentId}`)
  return apiClient.delete(endpoint)
}

export async function searchRecipes(query: string, take = 20): Promise<{ recipes: RecipeSummaryResponse[] }> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/search?query=${encodeURIComponent(query)}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function getLinkedMenuItems(documentId: string): Promise<HalCollection<{ documentId: string; name: string; price: number }>> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/recipes/${documentId}/menu-items`)
  return apiClient.get(endpoint)
}

// Recipe Category API functions

export async function createCategory(data: CreateRecipeCategoryRequest): Promise<RecipeCategoryDocumentResponse> {
  const endpoint = apiClient.buildOrgPath('/recipes/cms/categories')
  return apiClient.post(endpoint, data)
}

export async function getCategories(includeArchived = false): Promise<{ categories: RecipeCategorySummaryResponse[] }> {
  let endpoint = apiClient.buildOrgPath('/recipes/cms/categories')
  if (includeArchived) endpoint += '?includeArchived=true'
  return apiClient.get(endpoint)
}

export async function getCategory(documentId: string): Promise<RecipeCategoryDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}`)
  return apiClient.get(endpoint)
}

export async function createCategoryDraft(documentId: string, data: CreateRecipeCategoryDraftRequest): Promise<RecipeCategoryVersionResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/draft`)
  return apiClient.post(endpoint, data)
}

export async function publishCategory(documentId: string, note?: string): Promise<RecipeCategoryDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/publish`)
  return apiClient.post(endpoint, note ? { note } : undefined)
}

export async function getCategoryVersions(documentId: string, skip = 0, take = 20): Promise<{ versions: RecipeCategoryVersionResponse[] }> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/versions?skip=${skip}&take=${take}`)
  return apiClient.get(endpoint)
}

export async function revertCategory(documentId: string, version: number, reason?: string): Promise<RecipeCategoryDocumentResponse> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/revert`)
  return apiClient.post(endpoint, { version, reason })
}

export async function archiveCategory(documentId: string, reason?: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/archive`)
  return apiClient.post(endpoint, reason ? { reason } : undefined)
}

export async function restoreCategory(documentId: string): Promise<void> {
  const endpoint = apiClient.buildOrgPath(`/recipes/cms/categories/${documentId}/restore`)
  return apiClient.post(endpoint)
}
