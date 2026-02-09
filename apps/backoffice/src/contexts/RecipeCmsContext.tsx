import { createContext, useContext, useReducer, type ReactNode } from 'react'
import { recipeCmsReducer, initialRecipeCmsState, type RecipeCmsState, type RecipeCmsAction } from '../reducers/recipeCmsReducer'
import * as recipesApi from '../api/recipes'

interface RecipeCmsContextValue extends RecipeCmsState {
  loadRecipes: (categoryId?: string) => Promise<void>
  createRecipe: (data: recipesApi.CreateRecipeRequest) => Promise<void>
  selectRecipe: (documentId: string) => Promise<void>
  deselectRecipe: () => void
  createDraft: (documentId: string, data: recipesApi.CreateRecipeDraftRequest) => Promise<void>
  discardDraft: (documentId: string) => Promise<void>
  publishDraft: (documentId: string, note?: string) => Promise<void>
  archiveRecipe: (documentId: string, reason?: string) => Promise<void>
  restoreRecipe: (documentId: string) => Promise<void>
  recalculateCost: (documentId: string) => Promise<void>
  loadCategories: () => Promise<void>
  createCategory: (data: recipesApi.CreateRecipeCategoryRequest) => Promise<void>
  dispatch: React.Dispatch<RecipeCmsAction>
}

const RecipeCmsContext = createContext<RecipeCmsContextValue | null>(null)

export function RecipeCmsProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(recipeCmsReducer, initialRecipeCmsState)

  async function loadRecipes(categoryId?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await recipesApi.getRecipes(categoryId)
      dispatch({ type: 'RECIPES_LOADED', payload: { recipes: result.recipes } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function createRecipe(data: recipesApi.CreateRecipeRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const recipe = await recipesApi.createRecipe(data)
      dispatch({ type: 'RECIPE_CREATED', payload: { recipe } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function selectRecipe(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const recipe = await recipesApi.getRecipe(documentId)
      dispatch({ type: 'RECIPE_SELECTED', payload: { recipe } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  function deselectRecipe() {
    dispatch({ type: 'RECIPE_DESELECTED' })
  }

  async function createDraft(documentId: string, data: recipesApi.CreateRecipeDraftRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await recipesApi.createDraft(documentId, data)
      const recipe = await recipesApi.getRecipe(documentId)
      dispatch({ type: 'DRAFT_CREATED', payload: { recipe } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function discardDraft(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await recipesApi.discardDraft(documentId)
      dispatch({ type: 'DRAFT_DISCARDED', payload: { documentId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function publishDraft(documentId: string, note?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const recipe = await recipesApi.publishDraft(documentId, note)
      dispatch({ type: 'RECIPE_PUBLISHED', payload: { recipe } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function archiveRecipe(documentId: string, reason?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await recipesApi.archiveRecipe(documentId, reason)
      dispatch({ type: 'RECIPE_ARCHIVED', payload: { documentId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function restoreRecipe(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await recipesApi.restoreRecipe(documentId)
      dispatch({ type: 'RECIPE_RESTORED', payload: { documentId } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function recalculateCost(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const recipe = await recipesApi.recalculateCost(documentId)
      dispatch({ type: 'COST_RECALCULATED', payload: { recipe } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function loadCategories() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const result = await recipesApi.getCategories()
      dispatch({ type: 'CATEGORIES_LOADED', payload: { categories: result.categories } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  async function createCategory(data: recipesApi.CreateRecipeCategoryRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const response = await recipesApi.createCategory(data)
      const published = response.published
      const category: recipesApi.RecipeCategorySummaryResponse = {
        documentId: response.documentId,
        name: published?.name ?? data.name,
        displayOrder: published?.displayOrder ?? data.displayOrder,
        color: published?.color ?? data.color,
        hasDraft: response.draftVersion != null,
        isArchived: response.isArchived,
        recipeCount: 0,
      }
      dispatch({ type: 'CATEGORY_CREATED', payload: { category } })
    } catch (error) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (error as Error).message } })
    }
  }

  return (
    <RecipeCmsContext.Provider
      value={{
        ...state,
        loadRecipes,
        createRecipe,
        selectRecipe,
        deselectRecipe,
        createDraft,
        discardDraft,
        publishDraft,
        archiveRecipe,
        restoreRecipe,
        recalculateCost,
        loadCategories,
        createCategory,
        dispatch,
      }}
    >
      {children}
    </RecipeCmsContext.Provider>
  )
}

export function useRecipeCms() {
  const context = useContext(RecipeCmsContext)
  if (!context) {
    throw new Error('useRecipeCms must be used within a RecipeCmsProvider')
  }
  return context
}
