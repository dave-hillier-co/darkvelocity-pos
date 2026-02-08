import { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useOrdering } from '../contexts/OrderingContext.tsx'
import { getOrderingMenu, startSession, addToCart } from '../api.ts'
import type { MenuItem } from '../types.ts'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(amount)
}

export default function MenuPage() {
  const { orgId, siteId, linkCode } = useParams<{ orgId: string; siteId: string; linkCode: string }>()
  const navigate = useNavigate()
  const { categories, items, cartItems, cartTotal, sessionId, linkType, tableNumber, status, error, dispatch } = useOrdering()
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const [adding, setAdding] = useState<string | null>(null)

  useEffect(() => {
    if (!orgId || !siteId || !linkCode) return
    if (status !== 'loading') return

    async function load() {
      try {
        const menu = await getOrderingMenu(orgId!, siteId!, linkCode!)
        dispatch({
          type: 'MENU_LOADED',
          categories: menu.categories,
          items: menu.items,
          linkType: menu.type,
          tableNumber: menu.tableNumber,
        })

        // Auto-start a session
        const session = await startSession(orgId!, siteId!, linkCode!)
        dispatch({ type: 'SESSION_STARTED', sessionId: session.id })
      } catch (err) {
        dispatch({ type: 'ERROR_OCCURRED', error: (err as Error).message })
      }
    }
    load()
  }, [orgId, siteId, linkCode, status, dispatch])

  const handleAddItem = useCallback(async (item: MenuItem) => {
    if (!sessionId) return
    setAdding(item.id)
    try {
      const result = await addToCart(sessionId, item.id, item.name, 1, item.price)
      dispatch({ type: 'CART_UPDATED', session: result })
    } catch (err) {
      dispatch({ type: 'ERROR_OCCURRED', error: (err as Error).message })
    } finally {
      setAdding(null)
    }
  }, [sessionId, dispatch])

  if (status === 'loading') {
    return (
      <main className="container">
        <article aria-busy="true">Loading menu...</article>
      </main>
    )
  }

  if (status === 'error') {
    return (
      <main className="container">
        <article>
          <header>Something went wrong</header>
          <p>{error}</p>
        </article>
      </main>
    )
  }

  const filteredItems = selectedCategory
    ? items.filter(item => item.categoryId === selectedCategory)
    : items

  const cartCount = cartItems.reduce((sum, item) => sum + item.quantity, 0)

  return (
    <>
      <header className="container" style={{ paddingBlock: '0.5rem' }}>
        <nav>
          <ul>
            <li>
              <strong>
                {linkType === 'TableQr' && tableNumber ? `Table ${tableNumber}` : ''}
                {linkType === 'Kiosk' ? 'Kiosk Order' : ''}
                {linkType === 'TakeOut' ? 'Takeout Order' : ''}
              </strong>
            </li>
          </ul>
          <ul>
            <li>
              <button
                className="outline"
                onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}/cart`)}
                disabled={cartCount === 0}
              >
                Cart ({cartCount}) - {formatCurrency(cartTotal)}
              </button>
            </li>
          </ul>
        </nav>
      </header>

      <main className="container">
        <nav style={{ display: 'flex', gap: '0.5rem', overflowX: 'auto', paddingBottom: '0.5rem' }}>
          <button
            className={selectedCategory === null ? '' : 'outline'}
            onClick={() => setSelectedCategory(null)}
            style={{ whiteSpace: 'nowrap' }}
          >
            All
          </button>
          {categories.map(cat => (
            <button
              key={cat.id}
              className={selectedCategory === cat.id ? '' : 'outline'}
              onClick={() => setSelectedCategory(cat.id)}
              style={{
                whiteSpace: 'nowrap',
                borderColor: cat.color || undefined,
                color: selectedCategory === cat.id ? undefined : cat.color || undefined,
              }}
            >
              {cat.name}
            </button>
          ))}
        </nav>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))',
          gap: '0.75rem',
        }}>
          {filteredItems.map(item => (
            <article
              key={item.id}
              style={{ marginBottom: 0, cursor: 'pointer', textAlign: 'center' }}
              onClick={() => handleAddItem(item)}
            >
              {item.imageUrl && (
                <img
                  src={item.imageUrl}
                  alt={item.name}
                  style={{ width: '100%', height: '120px', objectFit: 'cover', borderRadius: '4px' }}
                />
              )}
              <strong style={{ display: 'block', marginTop: '0.5rem' }}>{item.name}</strong>
              {item.description && (
                <small style={{ display: 'block', opacity: 0.7 }}>{item.description}</small>
              )}
              <p style={{ marginBottom: '0.5rem' }}>
                <strong>{formatCurrency(item.price)}</strong>
              </p>
              <button
                className="outline"
                style={{ width: '100%', padding: '0.5rem' }}
                aria-busy={adding === item.id}
                disabled={adding === item.id}
              >
                {adding === item.id ? 'Adding...' : 'Add'}
              </button>
            </article>
          ))}
        </div>

        {filteredItems.length === 0 && (
          <p style={{ textAlign: 'center', opacity: 0.6 }}>No items in this category</p>
        )}
      </main>
    </>
  )
}
