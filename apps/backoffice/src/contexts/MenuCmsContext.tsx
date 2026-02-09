import { createContext, useContext, useReducer, type ReactNode } from 'react'
import {
  menuCmsReducer,
  initialMenuCmsState,
  type MenuCmsState,
  type MenuCmsAction,
} from '../reducers/menuCmsReducer'
import * as menuApi from '../api/menu'
import type { DocumentSnapshot } from '../types'
import type { MenuItemContent } from '../api/menu'

interface MenuCmsContextValue extends MenuCmsState {
  loadItems: (categoryId?: string) => Promise<void>
  loadCategories: () => Promise<void>
  loadModifierBlocks: () => Promise<void>
  loadTags: (category?: string) => Promise<void>
  createItem: (data: menuApi.CreateMenuItemRequest) => Promise<void>
  selectItem: (documentId: string) => Promise<void>
  clearSelection: () => void
  createDraft: (documentId: string, data: menuApi.CreateMenuItemDraftRequest) => Promise<void>
  discardDraft: (documentId: string) => Promise<void>
  publishItem: (documentId: string, note?: string) => Promise<void>
  archiveItem: (documentId: string, reason?: string) => Promise<void>
  restoreItem: (documentId: string) => Promise<void>
  createCategory: (data: menuApi.CreateCategoryRequest) => Promise<void>
  dispatch: React.Dispatch<MenuCmsAction>
}

const MenuCmsContext = createContext<MenuCmsContextValue | null>(null)

export function MenuCmsProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(menuCmsReducer, initialMenuCmsState)

  async function loadItems(categoryId?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const items = await menuApi.getMenuItems(categoryId)
      dispatch({ type: 'ITEMS_LOADED', payload: { items } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function loadCategories() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const categories = await menuApi.getCategories()
      dispatch({ type: 'CATEGORIES_LOADED', payload: { categories } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function loadModifierBlocks() {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const modifierBlocks = await menuApi.getModifierBlocks()
      dispatch({ type: 'MODIFIER_BLOCKS_LOADED', payload: { modifierBlocks } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function loadTags(category?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const tags = await menuApi.getContentTags(category)
      dispatch({ type: 'TAGS_LOADED', payload: { tags } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function createItem(data: menuApi.CreateMenuItemRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const item = await menuApi.createMenuItem(data)
      dispatch({ type: 'ITEM_CREATED', payload: { item } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function selectItem(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const item = await menuApi.getMenuItem(documentId)
      dispatch({ type: 'ITEM_SELECTED', payload: { item } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  function clearSelection() {
    dispatch({ type: 'ITEM_SELECTED', payload: { item: null } })
  }

  async function createDraft(documentId: string, data: menuApi.CreateMenuItemDraftRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const item = await menuApi.createMenuItemDraft(documentId, data)
      dispatch({ type: 'DRAFT_CREATED', payload: { item } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function discardDraft(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await menuApi.discardMenuItemDraft(documentId)
      dispatch({ type: 'DRAFT_DISCARDED', payload: { documentId } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function publishItem(documentId: string, note?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const item = await menuApi.publishMenuItem(documentId, note)
      dispatch({ type: 'ITEM_PUBLISHED', payload: { item } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function archiveItem(documentId: string, reason?: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await menuApi.archiveMenuItem(documentId, reason)
      dispatch({ type: 'ITEM_ARCHIVED', payload: { documentId } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function restoreItem(documentId: string) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      await menuApi.restoreMenuItem(documentId)
      dispatch({ type: 'ITEM_RESTORED', payload: { documentId } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  async function createCategoryAction(data: menuApi.CreateCategoryRequest) {
    dispatch({ type: 'LOADING_STARTED' })
    try {
      const category = await menuApi.createCategory(data)
      dispatch({ type: 'CATEGORY_CREATED', payload: { category } })
    } catch (err) {
      dispatch({ type: 'LOADING_FAILED', payload: { error: (err as Error).message } })
    }
  }

  return (
    <MenuCmsContext.Provider
      value={{
        ...state,
        loadItems,
        loadCategories,
        loadModifierBlocks,
        loadTags,
        createItem,
        selectItem,
        clearSelection,
        createDraft,
        discardDraft,
        publishItem,
        archiveItem,
        restoreItem,
        createCategory: createCategoryAction,
        dispatch,
      }}
    >
      {children}
    </MenuCmsContext.Provider>
  )
}

export function useMenuCms() {
  const context = useContext(MenuCmsContext)
  if (!context) {
    throw new Error('useMenuCms must be used within a MenuCmsProvider')
  }
  return context
}
