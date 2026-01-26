import { NavLink, Outlet } from 'react-router-dom'

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
    title: 'Analytics',
    links: [
      { to: '/reports', label: 'Sales Reports' },
      { to: '/reports/margins', label: 'Margin Analysis' },
    ],
  },
]

export default function Layout() {
  return (
    <div className="app-layout">
      <aside className="sidebar">
        <header className="sidebar-header">
          <h1>DarkVelocity</h1>
          <small>Back Office</small>
        </header>

        <nav className="sidebar-nav">
          {navSections.map((section) => (
            <div key={section.title} className="sidebar-section">
              <div className="sidebar-section-title">{section.title}</div>
              {section.links.map((link) => (
                <NavLink
                  key={link.to}
                  to={link.to}
                  className={({ isActive }) => isActive ? 'active' : ''}
                >
                  {link.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <footer className="sidebar-footer">
          <small>Admin User</small>
          <br />
          <button className="secondary outline" style={{ marginTop: '0.5rem' }}>
            Log Out
          </button>
        </footer>
      </aside>

      <main className="main-content">
        <Outlet />
      </main>
    </div>
  )
}
