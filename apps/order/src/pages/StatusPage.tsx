import { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useOrdering } from '../contexts/OrderingContext.tsx'
import { getSessionStatus } from '../api.ts'

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' }).format(amount)
}

interface OrderStatus {
  sessionId: string
  sessionStatus: string
  orderId?: string
  orderNumber?: string
  orderStatus?: string
  cartTotal: number
  guestName?: string
  tableNumber?: string
}

export default function StatusPage() {
  const { orgId, siteId, linkCode, sessionId } = useParams<{
    orgId: string; siteId: string; linkCode: string; sessionId: string
  }>()
  const navigate = useNavigate()
  const { orderNumber: contextOrderNumber } = useOrdering()
  const [orderStatus, setOrderStatus] = useState<OrderStatus | null>(null)
  const [polling, setPolling] = useState(true)

  const fetchStatus = useCallback(async () => {
    if (!sessionId) return
    try {
      const status = await getSessionStatus(sessionId)
      setOrderStatus(status)

      // Stop polling if order is completed
      if (status.orderStatus === 'Closed' || status.orderStatus === 'Voided') {
        setPolling(false)
      }
    } catch {
      // Ignore polling errors
    }
  }, [sessionId])

  useEffect(() => {
    fetchStatus()

    if (!polling) return

    const interval = setInterval(fetchStatus, 5000)
    return () => clearInterval(interval)
  }, [fetchStatus, polling])

  const displayNumber = orderStatus?.orderNumber ?? contextOrderNumber

  return (
    <main className="container" style={{ textAlign: 'center', paddingTop: '2rem' }}>
      <article>
        <header>
          <h2>Order Placed</h2>
        </header>

        {displayNumber && (
          <p style={{ fontSize: '2rem', fontWeight: 'bold' }}>
            #{displayNumber}
          </p>
        )}

        {orderStatus?.tableNumber && (
          <p>Table {orderStatus.tableNumber}</p>
        )}

        {orderStatus?.guestName && (
          <p>Name: {orderStatus.guestName}</p>
        )}

        <p>Total: <strong>{formatCurrency(orderStatus?.cartTotal ?? 0)}</strong></p>

        <p style={{ opacity: 0.7 }}>
          Status: <strong>{orderStatus?.orderStatus ?? 'Submitted'}</strong>
        </p>

        {polling && (
          <small style={{ opacity: 0.5 }}>Updating automatically...</small>
        )}
      </article>

      <button
        className="outline"
        onClick={() => navigate(`/${orgId}/${siteId}/${linkCode}`)}
        style={{ marginTop: '1rem' }}
      >
        Order More
      </button>
    </main>
  )
}
