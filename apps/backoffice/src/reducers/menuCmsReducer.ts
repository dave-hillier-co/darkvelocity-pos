import type { DocumentSnapshot } from '../types'
import type {
  MenuItemContent,
  CategoryContent,
  ModifierBlockContent,
  ContentTag,
  MenuItemSummary,
  CategorySummary,
} from '../api/menu'

export interface MenuCmsState {
  items: MenuItemSummary[]
  categories: CategorySummary[]
  modifierBlocks: ModifierBlockContent[]
  contentTags: ContentTag[]
  selectedItem: DocumentSnapshot<MenuItemContent> | null
  isLoading: boolean
  error: string | null
}

export type MenuCmsAction =
  | { type: 'LOADING_STARTED' }
  | { type: 'LOADING_FAILED'; payload: { error: string } }
  | { type: 'ITEMS_LOADED'; payload: { items: MenuItemSummary[] } }
  | { type: 'ITEM_CREATED'; payload: { item: DocumentSnapshot<MenuItemContent> } }
  | { type: 'ITEM_SELECTED'; payload: { item: DocumentSnapshot<MenuItemContent> | null } }
  | { type: 'DRAFT_CREATED'; payload: { item: DocumentSnapshot<MenuItemContent> } }
  | { type: 'DRAFT_DISCARDED'; payload: { documentId: string } }
  | { type: 'ITEM_PUBLISHED'; payload: { item: DocumentSnapshot<MenuItemContent> } }
  | { type: 'ITEM_ARCHIVED'; payload: { documentId: string } }
  | { type: 'ITEM_RESTORED'; payload: { documentId: string } }
  | { type: 'CATEGORIES_LOADED'; payload: { categories: CategorySummary[] } }
  | { type: 'CATEGORY_CREATED'; payload: { category: DocumentSnapshot<CategoryContent> } }
  | { type: 'MODIFIER_BLOCKS_LOADED'; payload: { modifierBlocks: ModifierBlockContent[] } }
  | { type: 'TAGS_LOADED'; payload: { tags: ContentTag[] } }

export const initialMenuCmsState: MenuCmsState = {
  items: [],
  categories: [],
  modifierBlocks: [],
  contentTags: [],
  selectedItem: null,
  isLoading: false,
  error: null,
}

export function menuCmsReducer(state: MenuCmsState, action: MenuCmsAction): MenuCmsState {
  switch (action.type) {
    case 'LOADING_STARTED':
      return { ...state, isLoading: true, error: null }

    case 'LOADING_FAILED':
      return { ...state, isLoading: false, error: action.payload.error }

    case 'ITEMS_LOADED':
      return { ...state, isLoading: false, items: action.payload.items }

    case 'ITEM_CREATED': {
      const { item } = action.payload
      const summary: MenuItemSummary = {
        documentId: item.documentId,
        name: item.content.name,
        price: item.content.price,
        categoryId: item.content.categoryId,
        hasDraft: item.hasDraft,
        isArchived: item.status === 'archived',
        publishedVersion: item.currentVersion,
      }
      return {
        ...state,
        isLoading: false,
        items: [...state.items, summary],
        selectedItem: item,
      }
    }

    case 'ITEM_SELECTED':
      return { ...state, isLoading: false, selectedItem: action.payload.item }

    case 'DRAFT_CREATED': {
      const { item } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedItem: item,
        items: state.items.map((i) =>
          i.documentId === item.documentId ? { ...i, hasDraft: true } : i
        ),
      }
    }

    case 'DRAFT_DISCARDED': {
      const { documentId } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedItem:
          state.selectedItem?.documentId === documentId
            ? { ...state.selectedItem, hasDraft: false }
            : state.selectedItem,
        items: state.items.map((i) =>
          i.documentId === documentId ? { ...i, hasDraft: false } : i
        ),
      }
    }

    case 'ITEM_PUBLISHED': {
      const { item } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedItem: item,
        items: state.items.map((i) =>
          i.documentId === item.documentId
            ? {
                ...i,
                name: item.content.name,
                price: item.content.price,
                hasDraft: item.hasDraft,
                publishedVersion: item.currentVersion,
              }
            : i
        ),
      }
    }

    case 'ITEM_ARCHIVED': {
      const { documentId } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedItem:
          state.selectedItem?.documentId === documentId
            ? { ...state.selectedItem, status: 'archived' }
            : state.selectedItem,
        items: state.items.map((i) =>
          i.documentId === documentId ? { ...i, isArchived: true } : i
        ),
      }
    }

    case 'ITEM_RESTORED': {
      const { documentId } = action.payload
      return {
        ...state,
        isLoading: false,
        selectedItem:
          state.selectedItem?.documentId === documentId
            ? { ...state.selectedItem, status: 'published' }
            : state.selectedItem,
        items: state.items.map((i) =>
          i.documentId === documentId ? { ...i, isArchived: false } : i
        ),
      }
    }

    case 'CATEGORIES_LOADED':
      return { ...state, isLoading: false, categories: action.payload.categories }

    case 'CATEGORY_CREATED': {
      const { category } = action.payload
      const summary: CategorySummary = {
        documentId: category.documentId,
        name: category.content.name,
        displayOrder: category.content.displayOrder,
        color: category.content.color,
        hasDraft: category.hasDraft,
        isArchived: category.status === 'archived',
        itemCount: 0,
      }
      return {
        ...state,
        isLoading: false,
        categories: [...state.categories, summary],
      }
    }

    case 'MODIFIER_BLOCKS_LOADED':
      return { ...state, isLoading: false, modifierBlocks: action.payload.modifierBlocks }

    case 'TAGS_LOADED':
      return { ...state, isLoading: false, contentTags: action.payload.tags }

    default:
      return state
  }
}
