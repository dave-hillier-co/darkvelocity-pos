import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/Layout'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { MenuCmsProvider } from './contexts/MenuCmsContext'
import { RecipeCmsProvider } from './contexts/RecipeCmsContext'
import { InventoryProvider } from './contexts/InventoryContext'
import { ProcurementProvider } from './contexts/ProcurementContext'
import { ReportsProvider } from './contexts/ReportsContext'
import { EmployeeProvider } from './contexts/EmployeeContext'
import { CustomerProvider } from './contexts/CustomerContext'
import { BookingProvider } from './contexts/BookingContext'
import { ChannelProvider } from './contexts/ChannelContext'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import MenuItemsPage from './pages/MenuItemsPage'
import MenuItemDetailPage from './pages/MenuItemDetailPage'
import CategoriesPage from './pages/CategoriesPage'
import ModifierBlocksPage from './pages/ModifierBlocksPage'
import ContentTagsPage from './pages/ContentTagsPage'
import RecipesPage from './pages/RecipesPage'
import RecipeDetailPage from './pages/RecipeDetailPage'
import IngredientsPage from './pages/IngredientsPage'
import StockPage from './pages/StockPage'
import SuppliersPage from './pages/SuppliersPage'
import PurchaseOrdersPage from './pages/PurchaseOrdersPage'
import DeliveriesPage from './pages/DeliveriesPage'
import ReportsPage from './pages/ReportsPage'
import MarginAnalysisPage from './pages/MarginAnalysisPage'
import EmployeesPage from './pages/EmployeesPage'
import EmployeeDetailPage from './pages/EmployeeDetailPage'
import CustomersPage from './pages/CustomersPage'
import CustomerDetailPage from './pages/CustomerDetailPage'
import BookingsPage from './pages/BookingsPage'
import FloorPlansPage from './pages/FloorPlansPage'
import ChannelsPage from './pages/ChannelsPage'
import ChannelDetailPage from './pages/ChannelDetailPage'
import DeviceAuthorizePage from './pages/DeviceAuthorizePage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}

function AppRoutes() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <main className="container">
        <article aria-busy="true">Loading...</article>
      </main>
    )
  }

  return (
    <Routes>
      {/* Public routes */}
      <Route path="login" element={<LoginPage />} />
      <Route path="device" element={<DeviceAuthorizePage />} />

      {/* Protected routes */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="menu/items" element={<MenuItemsPage />} />
        <Route path="menu/items/:documentId" element={<MenuItemDetailPage />} />
        <Route path="menu/categories" element={<CategoriesPage />} />
        <Route path="menu/modifier-blocks" element={<ModifierBlocksPage />} />
        <Route path="menu/tags" element={<ContentTagsPage />} />
        <Route path="menu/recipes" element={<RecipesPage />} />
        <Route path="menu/recipes/:documentId" element={<RecipeDetailPage />} />
        <Route path="inventory/ingredients" element={<IngredientsPage />} />
        <Route path="inventory/stock" element={<StockPage />} />
        <Route path="procurement/suppliers" element={<SuppliersPage />} />
        <Route path="procurement/purchase-orders" element={<PurchaseOrdersPage />} />
        <Route path="procurement/deliveries" element={<DeliveriesPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="reports/margins" element={<MarginAnalysisPage />} />
        <Route path="employees" element={<EmployeesPage />} />
        <Route path="employees/:employeeId" element={<EmployeeDetailPage />} />
        <Route path="customers" element={<CustomersPage />} />
        <Route path="customers/:customerId" element={<CustomerDetailPage />} />
        <Route path="bookings" element={<BookingsPage />} />
        <Route path="bookings/floor-plans" element={<FloorPlansPage />} />
        <Route path="channels" element={<ChannelsPage />} />
        <Route path="channels/:channelId" element={<ChannelDetailPage />} />
        <Route path="devices/authorize" element={<DeviceAuthorizePage />} />
      </Route>

      {/* Catch-all redirect */}
      <Route path="*" element={<Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <MenuCmsProvider>
        <RecipeCmsProvider>
          <InventoryProvider>
            <ProcurementProvider>
              <ReportsProvider>
                <EmployeeProvider>
                  <CustomerProvider>
                    <BookingProvider>
                      <ChannelProvider>
                        <AppRoutes />
                      </ChannelProvider>
                    </BookingProvider>
                  </CustomerProvider>
                </EmployeeProvider>
              </ReportsProvider>
            </ProcurementProvider>
          </InventoryProvider>
        </RecipeCmsProvider>
      </MenuCmsProvider>
    </AuthProvider>
  )
}
