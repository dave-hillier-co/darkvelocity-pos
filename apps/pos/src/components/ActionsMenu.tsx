import { useState } from 'react'
import { useOrder } from '../contexts/OrderContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

interface ActionsMenuProps {
  onClose: () => void
}

export default function ActionsMenu({ onClose }: ActionsMenuProps) {
  const { order, clearOrder, clearUnsentItems, applyOrderDiscount } = useOrder()
  const [showOrderDiscount, setShowOrderDiscount] = useState(false)
  const [discountValue, setDiscountValue] = useState('')
  const [discountReason, setDiscountReason] = useState('')

  if (!order) {
    onClose()
    return null
  }

  const unsentCount = order.lines.filter((line) => !line.sentAt).length

  function handleClearUnsent() {
    if (unsentCount > 0 && confirm(`Clear ${unsentCount} unsent items?`)) {
      clearUnsentItems()
      onClose()
    }
  }

  function handleVoid() {
    if (confirm('Are you sure you want to void this order?')) {
      clearOrder()
      onClose()
    }
  }

  function handleApplyOrderDiscount() {
    const value = parseFloat(discountValue)
    if (!isNaN(value) && value > 0) {
      applyOrderDiscount(value, discountReason || undefined)
    }
    onClose()
  }

  if (showOrderDiscount) {
    return (
      <dialog open aria-modal="true" className="actions-menu">
        <article>
          <header>
            <h3>Order Discount</h3>
          </header>

          <label>
            Discount Amount
            <input
              type="number"
              value={discountValue}
              onChange={(e) => setDiscountValue(e.target.value)}
              placeholder="0.00"
              min="0"
              step="0.01"
            />
          </label>

          <label>
            Reason (optional)
            <input
              type="text"
              value={discountReason}
              onChange={(e) => setDiscountReason(e.target.value)}
              placeholder="e.g. Loyalty discount"
            />
          </label>

          <footer>
            <button type="button" className="secondary" onClick={() => setShowOrderDiscount(false)}>
              Back
            </button>
            <button type="button" onClick={handleApplyOrderDiscount} disabled={!discountValue}>
              Apply
            </button>
          </footer>
        </article>
      </dialog>
    )
  }

  return (
    <dialog open aria-modal="true" className="actions-menu">
      <article>
        <header>
          <h3>Order Actions</h3>
        </header>

        <nav>
          <ul role="list" className="actions-list">
            <li>
              <button
                type="button"
                onClick={handleClearUnsent}
                disabled={unsentCount === 0}
              >
                Clear Unsent Items ({unsentCount})
              </button>
            </li>
            <li>
              <button
                type="button"
                onClick={() => setShowOrderDiscount(true)}
              >
                Apply Order Discount
                {order.discounts.length > 0 ? ` (${formatCurrency(order.discounts.reduce((sum, d) => sum + (d.type === 'FixedAmount' ? d.value : 0), 0))})` : ''}
              </button>
            </li>
            <li>
              <button
                type="button"
                className="secondary"
                onClick={handleVoid}
              >
                Void Order
              </button>
            </li>
          </ul>
        </nav>

        <footer>
          <button type="button" className="secondary" onClick={onClose}>
            Close
          </button>
        </footer>
      </article>
    </dialog>
  )
}
