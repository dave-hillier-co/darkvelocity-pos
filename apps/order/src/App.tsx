import { Routes, Route } from 'react-router-dom'
import { OrderingProvider } from './contexts/OrderingContext.tsx'
import MenuPage from './pages/MenuPage.tsx'
import CartPage from './pages/CartPage.tsx'
import CheckoutPage from './pages/CheckoutPage.tsx'
import StatusPage from './pages/StatusPage.tsx'

function OrderingRoutes() {
  return (
    <OrderingProvider>
      <Routes>
        <Route path="/" element={<MenuPage />} />
        <Route path="/cart" element={<CartPage />} />
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route path="/status/:sessionId" element={<StatusPage />} />
      </Routes>
    </OrderingProvider>
  )
}

export default function App() {
  return (
    <Routes>
      <Route path="/:orgId/:siteId/:linkCode/*" element={<OrderingRoutes />} />
      <Route path="*" element={
        <main className="container" style={{ textAlign: 'center', paddingTop: '3rem' }}>
          <h2>Scan a QR code to start ordering</h2>
          <p>Point your camera at the QR code on your table to begin.</p>
        </main>
      } />
    </Routes>
  )
}
