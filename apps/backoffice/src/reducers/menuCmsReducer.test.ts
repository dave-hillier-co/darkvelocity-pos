import { describe, it, expect } from 'vitest'
import { menuCmsReducer, initialMenuCmsState, type MenuCmsState } from './menuCmsReducer'
import type { DocumentSnapshot } from '../types'
import type { MenuItemContent, CategoryContent, MenuItemSummary } from '../api/menu'

function makeItemSnapshot(overrides: Partial<DocumentSnapshot<MenuItemContent>> = {}): DocumentSnapshot<MenuItemContent> {
  return {
    documentId: 'item-1',
    currentVersion: 1,
    status: 'published',
    hasDraft: false,
    content: {
      name: 'Fish & Chips',
      price: 14.99,
      trackInventory: false,
    },
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    _links: { self: { href: '/menu/cms/items/item-1' } },
    ...overrides,
  }
}

function makeCategorySnapshot(overrides: Partial<DocumentSnapshot<CategoryContent>> = {}): DocumentSnapshot<CategoryContent> {
  return {
    documentId: 'cat-1',
    currentVersion: 1,
    status: 'published',
    hasDraft: false,
    content: {
      name: 'Mains',
      displayOrder: 1,
    },
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    _links: { self: { href: '/menu/cms/categories/cat-1' } },
    ...overrides,
  }
}

describe('menuCmsReducer', () => {
  it('returns the initial state for an unknown action', () => {
    const result = menuCmsReducer(initialMenuCmsState, { type: 'LOADING_STARTED' })
    expect(result.isLoading).toBe(true)
  })

  describe('LOADING_STARTED', () => {
    it('sets isLoading and clears error', () => {
      const state: MenuCmsState = { ...initialMenuCmsState, error: 'old error' }
      const result = menuCmsReducer(state, { type: 'LOADING_STARTED' })
      expect(result.isLoading).toBe(true)
      expect(result.error).toBeNull()
    })
  })

  describe('LOADING_FAILED', () => {
    it('sets error and clears loading', () => {
      const state: MenuCmsState = { ...initialMenuCmsState, isLoading: true }
      const result = menuCmsReducer(state, {
        type: 'LOADING_FAILED',
        payload: { error: 'Network error' },
      })
      expect(result.isLoading).toBe(false)
      expect(result.error).toBe('Network error')
    })
  })

  describe('ITEMS_LOADED', () => {
    it('replaces items list and clears loading', () => {
      const items: MenuItemSummary[] = [
        { documentId: 'i-1', name: 'Burger', price: 12, hasDraft: false, isArchived: false },
        { documentId: 'i-2', name: 'Pizza', price: 10, hasDraft: true, isArchived: false },
      ]
      const state: MenuCmsState = { ...initialMenuCmsState, isLoading: true }
      const result = menuCmsReducer(state, { type: 'ITEMS_LOADED', payload: { items } })
      expect(result.items).toEqual(items)
      expect(result.isLoading).toBe(false)
    })
  })

  describe('ITEM_CREATED', () => {
    it('appends new item to list and sets selectedItem', () => {
      const item = makeItemSnapshot()
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'ITEM_CREATED',
        payload: { item },
      })
      expect(result.items).toHaveLength(1)
      expect(result.items[0].documentId).toBe('item-1')
      expect(result.items[0].name).toBe('Fish & Chips')
      expect(result.selectedItem).toBe(item)
    })
  })

  describe('ITEM_SELECTED', () => {
    it('sets selectedItem', () => {
      const item = makeItemSnapshot()
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'ITEM_SELECTED',
        payload: { item },
      })
      expect(result.selectedItem).toBe(item)
    })

    it('clears selectedItem when null', () => {
      const state: MenuCmsState = {
        ...initialMenuCmsState,
        selectedItem: makeItemSnapshot(),
      }
      const result = menuCmsReducer(state, {
        type: 'ITEM_SELECTED',
        payload: { item: null },
      })
      expect(result.selectedItem).toBeNull()
    })
  })

  describe('DRAFT_CREATED', () => {
    it('updates selectedItem and marks item as having draft', () => {
      const existing: MenuItemSummary = {
        documentId: 'item-1',
        name: 'Fish & Chips',
        price: 14.99,
        hasDraft: false,
        isArchived: false,
      }
      const state: MenuCmsState = { ...initialMenuCmsState, items: [existing] }
      const draftItem = makeItemSnapshot({ hasDraft: true })

      const result = menuCmsReducer(state, {
        type: 'DRAFT_CREATED',
        payload: { item: draftItem },
      })
      expect(result.selectedItem).toBe(draftItem)
      expect(result.items[0].hasDraft).toBe(true)
    })
  })

  describe('DRAFT_DISCARDED', () => {
    it('clears hasDraft on the item and selectedItem', () => {
      const existing: MenuItemSummary = {
        documentId: 'item-1',
        name: 'Fish & Chips',
        price: 14.99,
        hasDraft: true,
        isArchived: false,
      }
      const state: MenuCmsState = {
        ...initialMenuCmsState,
        items: [existing],
        selectedItem: makeItemSnapshot({ hasDraft: true }),
      }

      const result = menuCmsReducer(state, {
        type: 'DRAFT_DISCARDED',
        payload: { documentId: 'item-1' },
      })
      expect(result.items[0].hasDraft).toBe(false)
      expect(result.selectedItem?.hasDraft).toBe(false)
    })
  })

  describe('ITEM_PUBLISHED', () => {
    it('updates the item summary with new content', () => {
      const existing: MenuItemSummary = {
        documentId: 'item-1',
        name: 'Old Name',
        price: 10,
        hasDraft: true,
        isArchived: false,
        publishedVersion: 1,
      }
      const state: MenuCmsState = { ...initialMenuCmsState, items: [existing] }
      const published = makeItemSnapshot({ currentVersion: 2, hasDraft: false })

      const result = menuCmsReducer(state, {
        type: 'ITEM_PUBLISHED',
        payload: { item: published },
      })
      expect(result.items[0].name).toBe('Fish & Chips')
      expect(result.items[0].price).toBe(14.99)
      expect(result.items[0].hasDraft).toBe(false)
      expect(result.items[0].publishedVersion).toBe(2)
      expect(result.selectedItem).toBe(published)
    })
  })

  describe('ITEM_ARCHIVED', () => {
    it('marks item as archived in both list and selectedItem', () => {
      const existing: MenuItemSummary = {
        documentId: 'item-1',
        name: 'Fish & Chips',
        price: 14.99,
        hasDraft: false,
        isArchived: false,
      }
      const state: MenuCmsState = {
        ...initialMenuCmsState,
        items: [existing],
        selectedItem: makeItemSnapshot(),
      }

      const result = menuCmsReducer(state, {
        type: 'ITEM_ARCHIVED',
        payload: { documentId: 'item-1' },
      })
      expect(result.items[0].isArchived).toBe(true)
      expect(result.selectedItem?.status).toBe('archived')
    })
  })

  describe('ITEM_RESTORED', () => {
    it('marks item as not archived', () => {
      const existing: MenuItemSummary = {
        documentId: 'item-1',
        name: 'Fish & Chips',
        price: 14.99,
        hasDraft: false,
        isArchived: true,
      }
      const state: MenuCmsState = {
        ...initialMenuCmsState,
        items: [existing],
        selectedItem: makeItemSnapshot({ status: 'archived' }),
      }

      const result = menuCmsReducer(state, {
        type: 'ITEM_RESTORED',
        payload: { documentId: 'item-1' },
      })
      expect(result.items[0].isArchived).toBe(false)
      expect(result.selectedItem?.status).toBe('published')
    })
  })

  describe('CATEGORIES_LOADED', () => {
    it('replaces categories list', () => {
      const categories = [
        { documentId: 'c-1', name: 'Mains', displayOrder: 1, hasDraft: false, isArchived: false, itemCount: 5 },
      ]
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'CATEGORIES_LOADED',
        payload: { categories },
      })
      expect(result.categories).toEqual(categories)
    })
  })

  describe('CATEGORY_CREATED', () => {
    it('appends new category to list', () => {
      const category = makeCategorySnapshot()
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'CATEGORY_CREATED',
        payload: { category },
      })
      expect(result.categories).toHaveLength(1)
      expect(result.categories[0].documentId).toBe('cat-1')
      expect(result.categories[0].name).toBe('Mains')
    })
  })

  describe('MODIFIER_BLOCKS_LOADED', () => {
    it('replaces modifier blocks list', () => {
      const modifierBlocks = [
        {
          name: 'Size',
          selectionRule: 'single',
          minSelections: 1,
          maxSelections: 1,
          isRequired: true,
          options: [],
        },
      ]
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'MODIFIER_BLOCKS_LOADED',
        payload: { modifierBlocks },
      })
      expect(result.modifierBlocks).toEqual(modifierBlocks)
    })
  })

  describe('TAGS_LOADED', () => {
    it('replaces content tags list', () => {
      const tags = [
        {
          tagId: 't-1',
          orgId: 'org-1',
          name: 'Vegan',
          category: 'dietary',
          displayOrder: 1,
          isActive: true,
        },
      ]
      const result = menuCmsReducer(initialMenuCmsState, {
        type: 'TAGS_LOADED',
        payload: { tags },
      })
      expect(result.contentTags).toEqual(tags)
    })
  })
})
