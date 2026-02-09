import { describe, it, expect } from 'vitest'
import { recipeCmsReducer, initialRecipeCmsState } from './recipeCmsReducer'
import type { RecipeCmsState, RecipeCmsAction } from './recipeCmsReducer'
import type { RecipeSummaryResponse, RecipeDocumentResponse, RecipeCategorySummaryResponse } from '../api/recipes'

function makeSummary(overrides: Partial<RecipeSummaryResponse> = {}): RecipeSummaryResponse {
  return {
    documentId: 'recipe-1',
    name: 'Margherita Pizza',
    costPerPortion: 2.50,
    hasDraft: false,
    isArchived: false,
    linkedMenuItemCount: 0,
    ...overrides,
  }
}

function makeDocument(overrides: Partial<RecipeDocumentResponse> = {}): RecipeDocumentResponse {
  return {
    documentId: 'recipe-1',
    orgId: 'org-1',
    currentVersion: 1,
    publishedVersion: 1,
    isArchived: false,
    createdAt: '2026-01-01T00:00:00Z',
    published: {
      versionNumber: 1,
      createdAt: '2026-01-01T00:00:00Z',
      name: 'Margherita Pizza',
      portionYield: 4,
      ingredients: [],
      allergenTags: [],
      dietaryTags: [],
      theoreticalCost: 10,
      costPerPortion: 2.50,
    },
    schedules: [],
    totalVersions: 1,
    linkedMenuItemIds: [],
    _links: { self: { href: '/recipes/cms/recipes/recipe-1' } },
    ...overrides,
  }
}

function makeCategory(overrides: Partial<RecipeCategorySummaryResponse> = {}): RecipeCategorySummaryResponse {
  return {
    documentId: 'cat-1',
    name: 'Mains',
    displayOrder: 1,
    hasDraft: false,
    isArchived: false,
    recipeCount: 0,
    ...overrides,
  }
}

describe('recipeCmsReducer', () => {
  it('returns initial state for unknown action', () => {
    const result = recipeCmsReducer(initialRecipeCmsState, { type: 'UNKNOWN' } as unknown as RecipeCmsAction)
    expect(result).toEqual(initialRecipeCmsState)
  })

  describe('LOADING_STARTED', () => {
    it('sets isLoading true and clears error', () => {
      const state: RecipeCmsState = { ...initialRecipeCmsState, error: 'old error' }
      const result = recipeCmsReducer(state, { type: 'LOADING_STARTED' })
      expect(result.isLoading).toBe(true)
      expect(result.error).toBeNull()
    })
  })

  describe('LOADING_FAILED', () => {
    it('sets error and clears isLoading', () => {
      const state: RecipeCmsState = { ...initialRecipeCmsState, isLoading: true }
      const result = recipeCmsReducer(state, { type: 'LOADING_FAILED', payload: { error: 'Network error' } })
      expect(result.isLoading).toBe(false)
      expect(result.error).toBe('Network error')
    })
  })

  describe('RECIPES_LOADED', () => {
    it('replaces recipes list', () => {
      const recipes = [makeSummary(), makeSummary({ documentId: 'recipe-2', name: 'Carbonara' })]
      const result = recipeCmsReducer(initialRecipeCmsState, { type: 'RECIPES_LOADED', payload: { recipes } })
      expect(result.recipes).toEqual(recipes)
      expect(result.isLoading).toBe(false)
    })
  })

  describe('RECIPE_CREATED', () => {
    it('appends summary to recipes list', () => {
      const recipe = makeDocument()
      const result = recipeCmsReducer(initialRecipeCmsState, { type: 'RECIPE_CREATED', payload: { recipe } })
      expect(result.recipes).toHaveLength(1)
      expect(result.recipes[0].documentId).toBe('recipe-1')
      expect(result.recipes[0].name).toBe('Margherita Pizza')
    })
  })

  describe('RECIPE_SELECTED', () => {
    it('sets selectedRecipe', () => {
      const recipe = makeDocument()
      const result = recipeCmsReducer(initialRecipeCmsState, { type: 'RECIPE_SELECTED', payload: { recipe } })
      expect(result.selectedRecipe).toEqual(recipe)
    })
  })

  describe('RECIPE_DESELECTED', () => {
    it('clears selectedRecipe', () => {
      const state: RecipeCmsState = { ...initialRecipeCmsState, selectedRecipe: makeDocument() }
      const result = recipeCmsReducer(state, { type: 'RECIPE_DESELECTED' })
      expect(result.selectedRecipe).toBeNull()
    })
  })

  describe('DRAFT_CREATED', () => {
    it('updates selectedRecipe and marks hasDraft in list', () => {
      const existing = makeSummary()
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing] }
      const recipe = makeDocument({ draftVersion: 2 })
      const result = recipeCmsReducer(state, { type: 'DRAFT_CREATED', payload: { recipe } })
      expect(result.selectedRecipe).toEqual(recipe)
      expect(result.recipes[0].hasDraft).toBe(true)
    })
  })

  describe('DRAFT_DISCARDED', () => {
    it('clears hasDraft in list and draft from selectedRecipe', () => {
      const existing = makeSummary({ hasDraft: true })
      const selectedRecipe = makeDocument({ draftVersion: 2, draft: { versionNumber: 2, createdAt: '', name: 'Draft', portionYield: 4, ingredients: [], allergenTags: [], dietaryTags: [], theoreticalCost: 10, costPerPortion: 2.50 } })
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing], selectedRecipe }
      const result = recipeCmsReducer(state, { type: 'DRAFT_DISCARDED', payload: { documentId: 'recipe-1' } })
      expect(result.recipes[0].hasDraft).toBe(false)
      expect(result.selectedRecipe?.draft).toBeUndefined()
    })
  })

  describe('RECIPE_PUBLISHED', () => {
    it('updates summary and selected recipe', () => {
      const existing = makeSummary({ hasDraft: true })
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing] }
      const recipe = makeDocument({ publishedVersion: 2, published: { versionNumber: 2, createdAt: '', name: 'Updated Pizza', portionYield: 4, ingredients: [], allergenTags: [], dietaryTags: [], theoreticalCost: 12, costPerPortion: 3.00 } })
      const result = recipeCmsReducer(state, { type: 'RECIPE_PUBLISHED', payload: { recipe } })
      expect(result.recipes[0].name).toBe('Updated Pizza')
      expect(result.recipes[0].costPerPortion).toBe(3.00)
      expect(result.recipes[0].hasDraft).toBe(false)
      expect(result.selectedRecipe).toEqual(recipe)
    })
  })

  describe('RECIPE_ARCHIVED', () => {
    it('marks recipe as archived in list and selected', () => {
      const existing = makeSummary()
      const selectedRecipe = makeDocument()
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing], selectedRecipe }
      const result = recipeCmsReducer(state, { type: 'RECIPE_ARCHIVED', payload: { documentId: 'recipe-1' } })
      expect(result.recipes[0].isArchived).toBe(true)
      expect(result.selectedRecipe?.isArchived).toBe(true)
    })
  })

  describe('RECIPE_RESTORED', () => {
    it('clears archived flag in list and selected', () => {
      const existing = makeSummary({ isArchived: true })
      const selectedRecipe = makeDocument({ isArchived: true })
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing], selectedRecipe }
      const result = recipeCmsReducer(state, { type: 'RECIPE_RESTORED', payload: { documentId: 'recipe-1' } })
      expect(result.recipes[0].isArchived).toBe(false)
      expect(result.selectedRecipe?.isArchived).toBe(false)
    })
  })

  describe('COST_RECALCULATED', () => {
    it('updates cost in list and selected recipe', () => {
      const existing = makeSummary({ costPerPortion: 2.50 })
      const state: RecipeCmsState = { ...initialRecipeCmsState, recipes: [existing] }
      const recipe = makeDocument({ published: { versionNumber: 1, createdAt: '', name: 'Margherita Pizza', portionYield: 4, ingredients: [], allergenTags: [], dietaryTags: [], theoreticalCost: 14, costPerPortion: 3.50 } })
      const result = recipeCmsReducer(state, { type: 'COST_RECALCULATED', payload: { recipe } })
      expect(result.recipes[0].costPerPortion).toBe(3.50)
      expect(result.selectedRecipe).toEqual(recipe)
    })
  })

  describe('CATEGORIES_LOADED', () => {
    it('replaces categories list', () => {
      const categories = [makeCategory(), makeCategory({ documentId: 'cat-2', name: 'Starters' })]
      const result = recipeCmsReducer(initialRecipeCmsState, { type: 'CATEGORIES_LOADED', payload: { categories } })
      expect(result.categories).toEqual(categories)
    })
  })

  describe('CATEGORY_CREATED', () => {
    it('appends category to list', () => {
      const category = makeCategory()
      const result = recipeCmsReducer(initialRecipeCmsState, { type: 'CATEGORY_CREATED', payload: { category } })
      expect(result.categories).toHaveLength(1)
      expect(result.categories[0].name).toBe('Mains')
    })
  })
})
