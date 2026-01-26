import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import MenuItemsPage from './pages/MenuItemsPage'
import CategoriesPage from './pages/CategoriesPage'
import RecipesPage from './pages/RecipesPage'
import IngredientsPage from './pages/IngredientsPage'
import StockPage from './pages/StockPage'
import SuppliersPage from './pages/SuppliersPage'
import PurchaseOrdersPage from './pages/PurchaseOrdersPage'
import DeliveriesPage from './pages/DeliveriesPage'
import ReportsPage from './pages/ReportsPage'
import MarginAnalysisPage from './pages/MarginAnalysisPage'

export default function App() {
  // For now, skip auth and show logged in state
  const isAuthenticated = true

  if (!isAuthenticated) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="menu/items" element={<MenuItemsPage />} />
        <Route path="menu/categories" element={<CategoriesPage />} />
        <Route path="menu/recipes" element={<RecipesPage />} />
        <Route path="inventory/ingredients" element={<IngredientsPage />} />
        <Route path="inventory/stock" element={<StockPage />} />
        <Route path="procurement/suppliers" element={<SuppliersPage />} />
        <Route path="procurement/purchase-orders" element={<PurchaseOrdersPage />} />
        <Route path="procurement/deliveries" element={<DeliveriesPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="reports/margins" element={<MarginAnalysisPage />} />
      </Route>
      <Route path="login" element={<LoginPage />} />
    </Routes>
  )
}
