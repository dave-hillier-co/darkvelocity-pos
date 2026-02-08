import { useCallback, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useOrdering } from '../contexts/OrderingContext.tsx'
import { updateCartItem, removeFromCart } from '../api.ts'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(amount)
}

export default function CartPage() {
  const { orgId, siteId, linkCode } = useParams<{ orgId: string; siteId: string; linkCode: string }>()
  const navigate = useNavigate()
  const { cartItems, cartTotal, sessionId, dispatch } = useOrdering()
  const [busy, setBusy] = useState<string | null>(null)

  const handleQuantityChange = useCallback(async (cartItemId: string, newQuantity: number) => {
    if (!sessionId) return
    setBusy(cartItemId)
    try {
      if (newQuantity <= 0) {
        const result = await removeFromCart(sessionId, cartItemId)
        dispatch({ type: 'CART_UPDATED', session: result })
      } else {
        const result = await updateCartItem(sessionId, cartItemId, newQuantity)
        dispatch({ type: 'CART_UPDATED', session: result })
      }
    } catch (err) {
      dispatch({ type: 'ERROR_OCCURRED', error: (err as Error).message })
    } finally {
      setBusy(null)
    }
  }, [sessionId, dispatch])

  const handleRemove = useCallback(async (cartItemId: string) => {
    if (!sessionId) return
    setBusy(cartItemId)
    try {
      const result = await removeFromCart(sessionId, cartItemId)
      dispatch({ type: 'CART_UPDATED', session: result })
    } catch (err) {
      dispatch({ type: 'ERROR_OCCURRED', error: (err as Error).message })
    } finally {
      setBusy(null)
    }
  }, [sessionId, dispatch])

  return (
    <>
      <header className="container" style={{ paddingBlock: '0.5rem' }}>
        <nav>
          <ul>
            <li>
              <button className="outline" onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}`)}>
                Back to Menu
              </button>
            </li>
          </ul>
          <ul>
            <li><strong>Your Cart</strong></li>
          </ul>
        </nav>
      </header>

      <main className="container">
        {cartItems.length === 0 ? (
          <article style={{ textAlign: 'center' }}>
            <p>Your cart is empty</p>
            <button onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}`)}>
              Browse Menu
            </button>
          </article>
        ) : (
          <>
            {cartItems.map(item => {
              const modifierTotal = item.modifiers?.reduce((sum, m) => sum + m.priceAdjustment, 0) ?? 0
              const lineTotal = (item.unitPrice + modifierTotal) * item.quantity

              return (
                <article key={item.cartItemId} style={{ marginBottom: '0.75rem' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start' }}>
                    <div>
                      <strong>{item.name}</strong>
                      {item.modifiers && item.modifiers.length > 0 && (
                        <small style={{ display: 'block', opacity: 0.7 }}>
                          {item.modifiers.map(m => m.name).join(', ')}
                        </small>
                      )}
                      {item.notes && (
                        <small style={{ display: 'block', fontStyle: 'italic' }}>{item.notes}</small>
                      )}
                      <small>
                        {formatCurrency(item.unitPrice + modifierTotal)} each
                      </small>
                    </div>
                    <strong>{formatCurrency(lineTotal)}</strong>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '0.5rem' }}>
                    <button
                      className="outline"
                      style={{ padding: '0.25rem 0.75rem' }}
                      onClick={() => handleQuantityChange(item.cartItemId, item.quantity - 1)}
                      disabled={busy === item.cartItemId}
                    >
                      -
                    </button>
                    <span style={{ minWidth: '2rem', textAlign: 'center' }}>{item.quantity}</span>
                    <button
                      className="outline"
                      style={{ padding: '0.25rem 0.75rem' }}
                      onClick={() => handleQuantityChange(item.cartItemId, item.quantity + 1)}
                      disabled={busy === item.cartItemId}
                    >
                      +
                    </button>
                    <button
                      className="outline secondary"
                      style={{ marginLeft: 'auto', padding: '0.25rem 0.75rem' }}
                      onClick={() => handleRemove(item.cartItemId)}
                      disabled={busy === item.cartItemId}
                    >
                      Remove
                    </button>
                  </div>
                </article>
              )
            })}

            <hr />

            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '1.25rem', marginBottom: '1rem' }}>
              <strong>Total</strong>
              <strong>{formatCurrency(cartTotal)}</strong>
            </div>

            <button
              style={{ width: '100%' }}
              onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}/checkout`)}
            >
              Proceed to Checkout - {formatCurrency(cartTotal)}
            </button>
          </>
        )}
      </main>
    </>
  )
}
