import { apiClient } from './client'

export interface Recipe {
  id: string
  code: string
  name: string
  menuItemId: string | null
  portionYield: number
  currentCostPerPortion: number
  isActive: boolean
  createdAt: string
  updatedAt: string
  _links: {
    self: { href: string }
    ingredients: { href: string }
    menuItem?: { href: string }
  }
}

export interface RecipeIngredient {
  id: string
  recipeId: string
  ingredientId: string
  ingredientName: string
  quantity: number
  unitOfMeasure: string
  wastePercentage: number
  costPerUnit: number
  lineCost: number
  _links: {
    self: { href: string }
    ingredient: { href: string }
  }
}

export interface RecipeCost {
  recipeId: string
  recipeName: string
  portionYield: number
  totalIngredientCost: number
  costPerPortion: number
  ingredients: {
    ingredientId: string
    ingredientName: string
    quantity: number
    wasteQuantity: number
    totalQuantity: number
    unitCost: number
    lineCost: number
  }[]
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

export async function getRecipes(locationId?: string): Promise<HalCollection<Recipe>> {
  const url = locationId ? `/api/recipes?locationId=${locationId}` : '/api/recipes'
  return apiClient.get(url)
}

export async function getRecipe(recipeId: string): Promise<Recipe> {
  return apiClient.get(`/api/recipes/${recipeId}`)
}

export async function createRecipe(data: {
  code: string
  name: string
  menuItemId?: string
  portionYield: number
}): Promise<Recipe> {
  return apiClient.post('/api/recipes', data)
}

export async function updateRecipe(recipeId: string, data: {
  code?: string
  name?: string
  portionYield?: number
  isActive?: boolean
}): Promise<Recipe> {
  return apiClient.put(`/api/recipes/${recipeId}`, data)
}

export async function deleteRecipe(recipeId: string): Promise<void> {
  return apiClient.delete(`/api/recipes/${recipeId}`)
}

export async function getRecipeIngredients(recipeId: string): Promise<HalCollection<RecipeIngredient>> {
  return apiClient.get(`/api/recipes/${recipeId}/ingredients`)
}

export async function addRecipeIngredient(recipeId: string, data: {
  ingredientId: string
  quantity: number
  unitOfMeasure: string
  wastePercentage?: number
}): Promise<RecipeIngredient> {
  return apiClient.post(`/api/recipes/${recipeId}/ingredients`, data)
}

export async function updateRecipeIngredient(recipeId: string, ingredientId: string, data: {
  quantity?: number
  wastePercentage?: number
}): Promise<RecipeIngredient> {
  return apiClient.put(`/api/recipes/${recipeId}/ingredients/${ingredientId}`, data)
}

export async function removeRecipeIngredient(recipeId: string, ingredientId: string): Promise<void> {
  return apiClient.delete(`/api/recipes/${recipeId}/ingredients/${ingredientId}`)
}

export async function calculateRecipeCost(recipeId: string): Promise<RecipeCost> {
  return apiClient.get(`/api/recipes/${recipeId}/cost`)
}

export async function recalculateRecipeCost(recipeId: string): Promise<RecipeCost> {
  return apiClient.post(`/api/recipes/${recipeId}/recalculate`)
}
