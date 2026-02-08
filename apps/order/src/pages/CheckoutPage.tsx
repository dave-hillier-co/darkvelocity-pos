import { useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useOrdering } from '../contexts/OrderingContext.tsx'
import { submitOrder } from '../api.ts'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(amount)
}

export default function CheckoutPage() {
  const { orgId, siteId, linkCode } = useParams<{ orgId: string; siteId: string; linkCode: string }>()
  const navigate = useNavigate()
  const { cartItems, cartTotal, sessionId, linkType, tableNumber, dispatch } = useOrdering()
  const [guestName, setGuestName] = useState('')
  const [guestPhone, setGuestPhone] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault()
    if (!sessionId) return

    setSubmitting(true)
    setSubmitError(null)

    try {
      const result = await submitOrder(
        sessionId,
        guestName || undefined,
        guestPhone || undefined
      )
      dispatch({
        type: 'ORDER_SUBMITTED',
        orderId: result.orderId,
        orderNumber: result.orderNumber,
      })
      navigate(`/${orgId}/${siteId}/${linkCode}/status/${sessionId}`)
    } catch (err) {
      setSubmitError((err as Error).message)
    } finally {
      setSubmitting(false)
    }
  }, [sessionId, guestName, guestPhone, orgId, siteId, linkCode, navigate, dispatch])

  if (cartItems.length === 0) {
    return (
      <main className="container">
        <article style={{ textAlign: 'center' }}>
          <p>Your cart is empty</p>
          <button onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}`)}>
            Browse Menu
          </button>
        </article>
      </main>
    )
  }

  return (
    <>
      <header className="container" style={{ paddingBlock: '0.5rem' }}>
        <nav>
          <ul>
            <li>
              <button className="outline" onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}/cart`)}>
                Back to Cart
              </button>
            </li>
          </ul>
          <ul>
            <li><strong>Checkout</strong></li>
          </ul>
        </nav>
      </header>

      <main className="container">
        <article>
          <header>Order Summary</header>

          {linkType === 'TableQr' && tableNumber && (
            <p><strong>Table {tableNumber}</strong></p>
          )}
          {linkType === 'TakeOut' && <p><strong>Takeout Order</strong></p>}
          {linkType === 'Kiosk' && <p><strong>Kiosk Order</strong></p>}

          <table>
            <tbody>
              {cartItems.map(item => {
                const modifierTotal = item.modifiers?.reduce((sum, m) => sum + m.priceAdjustment, 0) ?? 0
                const lineTotal = (item.unitPrice + modifierTotal) * item.quantity

                return (
                  <tr key={item.cartItemId}>
                    <td>
                      {item.quantity}x {item.name}
                      {item.modifiers && item.modifiers.length > 0 && (
                        <small style={{ display: 'block', opacity: 0.7 }}>
                          {item.modifiers.map(m => m.name).join(', ')}
                        </small>
                      )}
                    </td>
                    <td style={{ textAlign: 'right' }}>{formatCurrency(lineTotal)}</td>
                  </tr>
                )
              })}
            </tbody>
            <tfoot>
              <tr>
                <td><strong>Total</strong></td>
                <td style={{ textAlign: 'right' }}><strong>{formatCurrency(cartTotal)}</strong></td>
              </tr>
            </tfoot>
          </table>
        </article>

        <form onSubmit={handleSubmit}>
          <fieldset>
            <label>
              Name (optional)
              <input
                type="text"
                value={guestName}
                onChange={e => setGuestName(e.target.value)}
                placeholder="Your name for the order"
              />
            </label>

            {(linkType === 'TakeOut' || linkType === 'Kiosk') && (
              <label>
                Phone (optional)
                <input
                  type="tel"
                  value={guestPhone}
                  onChange={e => setGuestPhone(e.target.value)}
                  placeholder="For order notifications"
                />
              </label>
            )}
          </fieldset>

          {submitError && (
            <article style={{ background: 'var(--pico-del-color)', padding: '0.75rem' }}>
              {submitError}
            </article>
          )}

          <button
            type="submit"
            style={{ width: '100%' }}
            aria-busy={submitting}
            disabled={submitting}
          >
            {submitting ? 'Placing Order...' : `Place Order - ${formatCurrency(cartTotal)}`}
          </button>
        </form>
      </main>
    </>
  )
}
