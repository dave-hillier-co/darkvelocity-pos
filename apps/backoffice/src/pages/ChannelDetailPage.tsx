import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useChannels } from '../contexts/ChannelContext'
import * as channelApi from '../api/channels'

export default function ChannelDetailPage() {
  const { channelId } = useParams<{ channelId: string }>()
  const navigate = useNavigate()
  const { selectedChannel, selectChannel, deselectChannel, pauseOrders, resumeOrders, addLocation, removeLocation, triggerMenuSync, disconnectChannel, error } = useChannels()
  const [isLoadingDetail, setIsLoadingDetail] = useState(false)

  useEffect(() => {
    if (!channelId) return
    setIsLoadingDetail(true)
    channelApi.getChannel(channelId)
      .then(selectChannel)
      .catch(console.error)
      .finally(() => setIsLoadingDetail(false))

    return () => { deselectChannel() }
  }, [channelId])

  if (isLoadingDetail) {
    return <article aria-busy="true">Loading channel details...</article>
  }

  if (!selectedChannel) {
    return (
      <article>
        <p>Channel not found</p>
        <button onClick={() => navigate('/channels')}>Back to Channels</button>
      </article>
    )
  }

  const ch = selectedChannel

  return (
    <>
      <nav aria-label="Breadcrumb">
        <ul>
          <li><a href="#" onClick={(e) => { e.preventDefault(); navigate('/channels') }} className="secondary">Channels</a></li>
          <li>{ch.name}</li>
        </ul>
      </nav>

      <hgroup>
        <h1>{ch.name}</h1>
        <p>{ch.platformType} &middot; {ch.integrationType}</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
        <article>
          <header><h3>Status</h3></header>
          <dl>
            <dt>Status</dt>
            <dd>
              <span className={
                ch.status === 'Active' ? 'badge badge-success' :
                ch.status === 'Paused' ? 'badge badge-warning' :
                'badge badge-danger'
              }>
                {ch.status}
              </span>
            </dd>
            {ch.connectedAt && (
              <>
                <dt>Connected At</dt>
                <dd>{new Date(ch.connectedAt).toLocaleString()}</dd>
              </>
            )}
            {ch.lastSyncAt && (
              <>
                <dt>Last Sync</dt>
                <dd>{new Date(ch.lastSyncAt).toLocaleString()}</dd>
              </>
            )}
            {ch.lastOrderAt && (
              <>
                <dt>Last Order</dt>
                <dd>{new Date(ch.lastOrderAt).toLocaleString()}</dd>
              </>
            )}
            {ch.lastErrorMessage && (
              <>
                <dt>Last Error</dt>
                <dd style={{ color: 'var(--pico-del-color)' }}>{ch.lastErrorMessage}</dd>
              </>
            )}
          </dl>
          <div style={{ display: 'flex', gap: '0.5rem' }}>
            {ch.status === 'Active' && (
              <button className="secondary" onClick={() => pauseOrders(ch.channelId)}>Pause Orders</button>
            )}
            {(ch.status === 'Paused' || ch.status === 'Error') && (
              <button onClick={() => resumeOrders(ch.channelId)}>Resume Orders</button>
            )}
            <button className="secondary" onClick={() => triggerMenuSync(ch.channelId)}>Sync Menu</button>
          </div>
        </article>

        <article>
          <header><h3>Metrics</h3></header>
          <dl>
            <dt>Orders Today</dt>
            <dd>{ch.totalOrdersToday}</dd>
            <dt>Revenue Today</dt>
            <dd>{ch.totalRevenueToday.toFixed(2)}</dd>
          </dl>
        </article>
      </section>

      <section>
        <article>
          <header>
            <h3>Location Mappings ({ch.locations.length})</h3>
          </header>
          {ch.locations.length > 0 ? (
            <table>
              <thead>
                <tr>
                  <th>Location ID</th>
                  <th>External Store ID</th>
                  <th>Active</th>
                  <th>Menu</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {ch.locations.map((loc) => (
                  <tr key={loc.locationId}>
                    <td><code>{loc.locationId}</code></td>
                    <td>{loc.externalStoreId}</td>
                    <td>
                      <span className={loc.isActive ? 'badge badge-success' : 'badge badge-danger'}>
                        {loc.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>{loc.menuId ?? '-'}</td>
                    <td>
                      <button
                        className="secondary outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => removeLocation(ch.channelId, loc.locationId)}
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p style={{ color: 'var(--pico-muted-color)' }}>No locations mapped</p>
          )}
        </article>
      </section>

      <section>
        <article>
          <header><h3>Danger Zone</h3></header>
          <button
            className="secondary"
            style={{ color: 'var(--pico-del-color)' }}
            onClick={() => {
              if (confirm('Are you sure you want to disconnect this channel?')) {
                disconnectChannel(ch.channelId).then(() => navigate('/channels'))
              }
            }}
          >
            Disconnect Channel
          </button>
        </article>
      </section>
    </>
  )
}
