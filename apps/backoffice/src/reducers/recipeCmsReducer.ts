import type {
  RecipeSummaryResponse,
  RecipeCategorySummaryResponse,
  RecipeDocumentResponse,
} from '../api/recipes'

export type RecipeCmsAction =
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }
  | { type: 'RECIPES_LOADED'; payload: { recipes: RecipeSummaryResponse[] } }
  | { type: 'RECIPE_CREATED'; payload: { recipe: RecipeDocumentResponse } }
  | { type: 'RECIPE_SELECTED'; payload: { recipe: RecipeDocumentResponse } }
  | { type: 'RECIPE_DESELECTED' }
  | { type: 'DRAFT_CREATED'; payload: { recipe: RecipeDocumentResponse } }
  | { type: 'DRAFT_DISCARDED'; payload: { documentId: string } }
  | { type: 'RECIPE_PUBLISHED'; payload: { recipe: RecipeDocumentResponse } }
  | { type: 'RECIPE_ARCHIVED'; payload: { documentId: string } }
  | { type: 'RECIPE_RESTORED'; payload: { documentId: string } }
  | { type: 'COST_RECALCULATED'; payload: { recipe: RecipeDocumentResponse } }
  | { type: 'CATEGORIES_LOADED'; payload: { categories: RecipeCategorySummaryResponse[] } }
  | { type: 'CATEGORY_CREATED'; payload: { category: RecipeCategorySummaryResponse } }

export interface RecipeCmsState {
  recipes: RecipeSummaryResponse[]
  categories: RecipeCategorySummaryResponse[]
  selectedRecipe: RecipeDocumentResponse | null
  isLoading: boolean
  error: string | null
}

export const initialRecipeCmsState: RecipeCmsState = {
  recipes: [],
  categories: [],
  selectedRecipe: null,
  isLoading: false,
  error: null,
}

export function recipeCmsReducer(state: RecipeCmsState, action: RecipeCmsAction): RecipeCmsState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'RECIPES_LOADED':
      return { ...state, isLoading: false, recipes: action.payload.recipes }

    case 'RECIPE_CREATED': {
      const { recipe } = action.payload
      const summary: RecipeSummaryResponse = {
        documentId: recipe.documentId,
        name: recipe.published?.name ?? recipe.draft?.name ?? '',
        costPerPortion: recipe.published?.costPerPortion ?? recipe.draft?.costPerPortion ?? 0,
        categoryId: recipe.published?.categoryId ?? recipe.draft?.categoryId,
        hasDraft: recipe.draftVersion != null,
        isArchived: recipe.isArchived,
        publishedVersion: recipe.publishedVersion,
        lastModified: recipe.createdAt,
        linkedMenuItemCount: recipe.linkedMenuItemIds.length,
      }
      return {
        ...state,
        isLoading: false,
        recipes: [...state.recipes, summary],
      }
    }

    case 'RECIPE_SELECTED':
      return { ...state, isLoading: false, selectedRecipe: action.payload.recipe }

    case 'RECIPE_DESELECTED':
      return { ...state, selectedRecipe: null }

    case 'DRAFT_CREATED':
      return {
        ...state,
        isLoading: false,
        selectedRecipe: action.payload.recipe,
        recipes: state.recipes.map((r) =>
          r.documentId === action.payload.recipe.documentId
            ? { ...r, hasDraft: true }
            : r
        ),
      }

    case 'DRAFT_DISCARDED':
      return {
        ...state,
        isLoading: false,
        selectedRecipe: state.selectedRecipe?.documentId === action.payload.documentId
          ? state.selectedRecipe ? { ...state.selectedRecipe, draft: undefined, draftVersion: undefined } : null
          : state.selectedRecipe,
        recipes: state.recipes.map((r) =>
          r.documentId === action.payload.documentId
            ? { ...r, hasDraft: false }
            : r
        ),
      }

    case 'RECIPE_PUBLISHED': {
      const { recipe } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedRecipe: recipe,
        recipes: state.recipes.map((r) =>
          r.documentId === recipe.documentId
            ? {
                ...r,
                name: recipe.published?.name ?? r.name,
                costPerPortion: recipe.published?.costPerPortion ?? r.costPerPortion,
                hasDraft: false,
                publishedVersion: recipe.publishedVersion,
              }
            : r
        ),
      }
    }

    case 'RECIPE_ARCHIVED':
      return {
        ...state,
        isLoading: false,
        recipes: state.recipes.map((r) =>
          r.documentId === action.payload.documentId
            ? { ...r, isArchived: true }
            : r
        ),
        selectedRecipe: state.selectedRecipe?.documentId === action.payload.documentId
          ? state.selectedRecipe ? { ...state.selectedRecipe, isArchived: true } : null
          : state.selectedRecipe,
      }

    case 'RECIPE_RESTORED':
      return {
        ...state,
        isLoading: false,
        recipes: state.recipes.map((r) =>
          r.documentId === action.payload.documentId
            ? { ...r, isArchived: false }
            : r
        ),
        selectedRecipe: state.selectedRecipe?.documentId === action.payload.documentId
          ? state.selectedRecipe ? { ...state.selectedRecipe, isArchived: false } : null
          : state.selectedRecipe,
      }

    case 'COST_RECALCULATED': {
      const { recipe } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedRecipe: recipe,
        recipes: state.recipes.map((r) =>
          r.documentId === recipe.documentId
            ? { ...r, costPerPortion: recipe.published?.costPerPortion ?? r.costPerPortion }
            : r
        ),
      }
    }

    case 'CATEGORIES_LOADED':
      return { ...state, isLoading: false, categories: action.payload.categories }

    case 'CATEGORY_CREATED':
      return {
        ...state,
        isLoading: false,
        categories: [...state.categories, action.payload.category],
      }

    default:
      return state
  }
}
