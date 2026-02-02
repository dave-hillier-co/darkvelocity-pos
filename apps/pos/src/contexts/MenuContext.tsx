import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import type { MenuItem, MenuCategory } from '../types'
import * as menuApi from '../api/menu'

interface MenuContextValue {
  categories: MenuCategory[]
  items: MenuItem[]
  isLoading: boolean
  error: string | null
  refreshMenu: () => Promise<void>
}

const MenuContext = createContext<MenuContextValue | null>(null)

// Sample data as fallback
const sampleCategories: MenuCategory[] = [
  { id: '1', name: 'Food', displayOrder: 1 },
  { id: '2', name: 'Drinks', displayOrder: 2 },
  { id: '3', name: 'Desserts', displayOrder: 3 },
]

const sampleItems: MenuItem[] = [
  { id: '1', name: 'Burger', price: 12.50, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '2', name: 'Fish & Chips', price: 14.00, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '3', name: 'Caesar Salad', price: 9.50, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '4', name: 'Steak', price: 24.00, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '5', name: 'Pasta', price: 11.00, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '6', name: 'Pizza', price: 13.50, categoryId: '1', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '7', name: 'Cola', price: 3.00, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '8', name: 'Beer', price: 5.50, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '9', name: 'Wine (Glass)', price: 7.00, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '10', name: 'Coffee', price: 3.50, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '11', name: 'Orange Juice', price: 4.00, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '12', name: 'Water', price: 2.00, categoryId: '2', accountingGroupId: '2', isActive: true, trackInventory: false },
  { id: '13', name: 'Cheesecake', price: 6.50, categoryId: '3', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '14', name: 'Ice Cream', price: 5.00, categoryId: '3', accountingGroupId: '1', isActive: true, trackInventory: false },
  { id: '15', name: 'Brownie', price: 5.50, categoryId: '3', accountingGroupId: '1', isActive: true, trackInventory: false },
]

export function MenuProvider({ children }: { children: ReactNode }) {
  const [categories, setCategories] = useState<MenuCategory[]>(sampleCategories)
  const [items, setItems] = useState<MenuItem[]>(sampleItems)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function refreshMenu() {
    setIsLoading(true)
    setError(null)

    try {
      const menuData = await menuApi.getFullMenu()

      setCategories(menuData.categories)
      setItems(menuData.items)

      // Cache in localStorage for offline use
      localStorage.setItem('pos_categories', JSON.stringify(menuData.categories))
      localStorage.setItem('pos_items', JSON.stringify(menuData.items))
    } catch (err) {
      console.warn('Failed to fetch menu from API, using fallback data:', err)
      setError(err instanceof Error ? err.message : 'Failed to load menu')

      // Try to load from localStorage cache
      const cachedCategories = localStorage.getItem('pos_categories')
      const cachedItems = localStorage.getItem('pos_items')

      if (cachedCategories && cachedItems) {
        setCategories(JSON.parse(cachedCategories))
        setItems(JSON.parse(cachedItems))
      }
      // Otherwise keep using sample data
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    // Load cached data first
    const cachedCategories = localStorage.getItem('pos_categories')
    const cachedItems = localStorage.getItem('pos_items')

    if (cachedCategories && cachedItems) {
      setCategories(JSON.parse(cachedCategories))
      setItems(JSON.parse(cachedItems))
    }

    // Then try to refresh from API
    refreshMenu()
  }, [])

  return (
    <MenuContext.Provider value={{ categories, items, isLoading, error, refreshMenu }}>
      {children}
    </MenuContext.Provider>
  )
}

export function useMenu() {
  const context = useContext(MenuContext)
  if (!context) {
    throw new Error('useMenu must be used within a MenuProvider')
  }
  return context
}
