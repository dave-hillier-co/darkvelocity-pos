import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

const navSections = [
  {
    title: 'Overview',
    links: [
      { to: '/dashboard', label: 'Dashboard' },
    ],
  },
  {
    title: 'Menu',
    links: [
      { to: '/menu/items', label: 'Item List' },
      { to: '/menu/categories', label: 'Categories' },
      { to: '/menu/modifier-blocks', label: 'Modifier Blocks' },
      { to: '/menu/tags', label: 'Content Tags' },
      { to: '/menu/recipes', label: 'Recipes' },
    ],
  },
  {
    title: 'Inventory',
    links: [
      { to: '/inventory/ingredients', label: 'Ingredients' },
      { to: '/inventory/stock', label: 'Stock Levels' },
    ],
  },
  {
    title: 'Procurement',
    links: [
      { to: '/procurement/suppliers', label: 'Suppliers' },
      { to: '/procurement/purchase-orders', label: 'Purchase Orders' },
      { to: '/procurement/deliveries', label: 'Deliveries' },
    ],
  },
  {
    title: 'People',
    links: [
      { to: '/employees', label: 'Employees' },
      { to: '/customers', label: 'Customers' },
    ],
  },
  {
    title: 'Bookings',
    links: [
      { to: '/bookings', label: 'Reservations' },
      { to: '/bookings/floor-plans', label: 'Floor Plans' },
    ],
  },
  {
    title: 'Channels',
    links: [
      { to: '/channels', label: 'Delivery Platforms' },
    ],
  },
  {
    title: 'Analytics',
    links: [
      { to: '/reports', label: 'Sales Reports' },
      { to: '/reports/margins', label: 'Margin Analysis' },
    ],
  },
]

export default function Layout() {
  const { pathname } = useLocation()
  const { user, logout } = useAuth()

  return (
    <div style={{ display: 'flex', minHeight: '100vh' }}>
      <aside>
        <header>
          <hgroup>
            <h2>DarkVelocity</h2>
            <p>Back Office</p>
          </hgroup>
        </header>

        <nav>
          {navSections.map((section) => {
            const isCurrentSection = section.links.some((link) => pathname.startsWith(link.to))
            return (
              <details key={section.title} open={isCurrentSection}>
                <summary>{section.title}</summary>
                <ul>
                  {section.links.map((link) => (
                    <li key={link.to}>
                      <NavLink to={link.to} className="secondary">
                        {link.label}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </details>
            )
          })}
        </nav>

        <footer>
          <small>{user?.displayName ?? 'Admin User'}</small>
          <button className="secondary outline" style={{ marginTop: '0.5rem' }} onClick={logout}>
            Log Out
          </button>
        </footer>
      </aside>

      <main>
        <Outlet />
      </main>
    </div>
  )
}
