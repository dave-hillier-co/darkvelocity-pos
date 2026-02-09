import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useCustomers } from '../contexts/CustomerContext'
import * as customerApi from '../api/customers'
import type { CustomerVisit } from '../api/customers'

export default function CustomerDetailPage() {
  const { customerId } = useParams<{ customerId: string }>()
  const navigate = useNavigate()
  const { selectedCustomer, selectCustomer, deselectCustomer, earnPoints, redeemPoints, enrollLoyalty, addTag, removeTag, error } = useCustomers()
  const [isLoadingDetail, setIsLoadingDetail] = useState(false)
  const [visits, setVisits] = useState<CustomerVisit[]>([])
  const [newTag, setNewTag] = useState('')

  useEffect(() => {
    if (!customerId) return
    setIsLoadingDetail(true)
    Promise.all([
      customerApi.getCustomer(customerId).then(selectCustomer),
      customerApi.getVisits(customerId, 20).then(setVisits),
    ])
      .catch(console.error)
      .finally(() => setIsLoadingDetail(false))

    return () => { deselectCustomer() }
  }, [customerId])

  if (isLoadingDetail) {
    return <article aria-busy="true">Loading customer details...</article>
  }

  if (!selectedCustomer) {
    return (
      <article>
        <p>Customer not found</p>
        <button onClick={() => navigate('/customers')}>Back to Customers</button>
      </article>
    )
  }

  const cust = selectedCustomer

  function handleAddTag(e: React.FormEvent) {
    e.preventDefault()
    if (newTag.trim() && customerId) {
      addTag(customerId, newTag.trim())
      setNewTag('')
    }
  }

  return (
    <>
      <nav aria-label="Breadcrumb">
        <ul>
          <li><a href="#" onClick={(e) => { e.preventDefault(); navigate('/customers') }} className="secondary">Customers</a></li>
          <li>{cust.firstName} {cust.lastName}</li>
        </ul>
      </nav>

      <hgroup>
        <h1>{cust.firstName} {cust.lastName}</h1>
        <p>Customer since {new Date(cust.createdAt).toLocaleDateString()}</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
        <article>
          <header><h3>Contact</h3></header>
          <dl>
            <dt>Email</dt>
            <dd>{cust.email ?? '-'}</dd>
            <dt>Phone</dt>
            <dd>{cust.phone ?? '-'}</dd>
            {cust.dateOfBirth && (
              <>
                <dt>Date of Birth</dt>
                <dd>{cust.dateOfBirth}</dd>
              </>
            )}
            <dt>Source</dt>
            <dd>{cust.source}</dd>
          </dl>
        </article>

        <article>
          <header><h3>Loyalty</h3></header>
          {cust.loyalty ? (
            <>
              <dl>
                <dt>Tier</dt>
                <dd><span className="badge badge-success">{cust.loyalty.tierName}</span></dd>
                <dt>Points Balance</dt>
                <dd><strong>{cust.loyalty.pointsBalance}</strong></dd>
                <dt>Lifetime Points</dt>
                <dd>{cust.loyalty.lifetimePoints}</dd>
                <dt>Member #</dt>
                <dd>{cust.loyalty.memberNumber}</dd>
              </dl>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <button
                  style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                  onClick={() => earnPoints(cust.id, { points: 100, reason: 'Manual adjustment' })}
                >
                  Earn 100 pts
                </button>
                <button
                  className="secondary outline"
                  style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                  onClick={() => redeemPoints(cust.id, { points: 50, orderId: '', reason: 'Manual redemption' })}
                  disabled={cust.loyalty.pointsBalance < 50}
                >
                  Redeem 50 pts
                </button>
              </div>
            </>
          ) : (
            <>
              <p style={{ color: 'var(--pico-muted-color)' }}>Not enrolled in loyalty program</p>
              <button
                onClick={() => enrollLoyalty(cust.id, {
                  programId: 'default',
                  memberNumber: `M-${Date.now()}`,
                  initialTierId: 'bronze',
                  tierName: 'Bronze',
                })}
              >
                Enroll in Loyalty
              </button>
            </>
          )}
        </article>
      </section>

      <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
        <article>
          <header><h3>Preferences</h3></header>
          <dl>
            <dt>Dietary Restrictions</dt>
            <dd>{cust.preferences.dietaryRestrictions?.join(', ') || 'None'}</dd>
            <dt>Allergens</dt>
            <dd>{cust.preferences.allergens?.join(', ') || 'None'}</dd>
            <dt>Seating Preference</dt>
            <dd>{cust.preferences.seatingPreference ?? 'None'}</dd>
            {cust.preferences.notes && (
              <>
                <dt>Notes</dt>
                <dd>{cust.preferences.notes}</dd>
              </>
            )}
          </dl>
        </article>

        <article>
          <header><h3>Tags</h3></header>
          {cust.tags.length > 0 ? (
            <ul style={{ listStyle: 'none', padding: 0, display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
              {cust.tags.map((tag) => (
                <li key={tag}>
                  <span className="badge badge-success" style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
                    {tag}
                    <button
                      className="secondary outline"
                      style={{ padding: '0 0.25rem', fontSize: '0.75rem', margin: 0 }}
                      onClick={() => customerId && removeTag(customerId, tag)}
                      aria-label={`Remove tag ${tag}`}
                    >
                      x
                    </button>
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p style={{ color: 'var(--pico-muted-color)' }}>No tags</p>
          )}
          <form onSubmit={handleAddTag} role="group" style={{ marginTop: '0.5rem' }}>
            <input
              type="text"
              placeholder="Add tag..."
              value={newTag}
              onChange={(e) => setNewTag(e.target.value)}
              aria-label="New tag"
            />
            <button type="submit">Add</button>
          </form>
        </article>
      </section>

      <details>
        <summary>Visit History ({visits.length})</summary>
        {visits.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Site</th>
                <th>Spend</th>
              </tr>
            </thead>
            <tbody>
              {visits.map((visit, i) => (
                <tr key={i}>
                  <td>{new Date(visit.visitedAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}</td>
                  <td>{visit.siteId}</td>
                  <td>{visit.spendAmount !== undefined ? `${visit.spendAmount.toFixed(2)}` : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p style={{ color: 'var(--pico-muted-color)' }}>No visits recorded</p>
        )}
      </details>
    </>
  )
}
