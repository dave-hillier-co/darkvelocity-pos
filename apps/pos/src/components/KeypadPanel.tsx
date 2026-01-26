import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useOrder } from '../contexts/OrderContext'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
  }).format(amount)
}

export default function KeypadPanel() {
  const [keypadValue, setKeypadValue] = useState('')
  const navigate = useNavigate()
  const { order, selectedLineId, changeQuantity, clearOrder } = useOrder()

  function handleDigit(digit: string) {
    setKeypadValue((prev) => prev + digit)
  }

  function handleClear() {
    setKeypadValue('')
  }

  function handleBackspace() {
    setKeypadValue((prev) => prev.slice(0, -1))
  }

  function handleQuantity() {
    if (selectedLineId && keypadValue) {
      const qty = parseInt(keypadValue, 10)
      if (!isNaN(qty)) {
        changeQuantity(selectedLineId, qty)
      }
    }
    setKeypadValue('')
  }

  function handleVoid() {
    if (confirm('Are you sure you want to void this order?')) {
      clearOrder()
    }
  }

  function handlePay() {
    if (order && order.lines.length > 0) {
      navigate('/payment')
    }
  }

  const canPay = order && order.lines.length > 0

  return (
    <section className="keypad-panel">
      <div className="keypad-display" aria-live="polite">
        {keypadValue || '0'}
      </div>

      <div className="keypad-actions">
        <button
          onClick={handleQuantity}
          disabled={!selectedLineId || !keypadValue}
        >
          Qty
        </button>
        <button
          className="secondary"
          onClick={handleVoid}
          disabled={!order}
        >
          Void
        </button>
      </div>

      <div className="pin-keypad">
        {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((digit) => (
          <button
            key={digit}
            type="button"
            onClick={() => handleDigit(digit)}
            aria-label={digit}
          >
            {digit}
          </button>
        ))}
        <button
          type="button"
          onClick={handleClear}
          className="secondary"
          aria-label="Clear"
        >
          C
        </button>
        <button
          type="button"
          onClick={() => handleDigit('0')}
          aria-label="0"
        >
          0
        </button>
        <button
          type="button"
          onClick={handleBackspace}
          className="secondary"
          aria-label="Backspace"
        >
          &larr;
        </button>
      </div>

      <div style={{ marginTop: '1rem' }}>
        <button
          onClick={handlePay}
          disabled={!canPay}
          className="contrast"
          style={{ width: '100%', padding: '1rem' }}
        >
          Pay {canPay ? formatCurrency(order.grandTotal) : ''}
        </button>
      </div>
    </section>
  )
}
