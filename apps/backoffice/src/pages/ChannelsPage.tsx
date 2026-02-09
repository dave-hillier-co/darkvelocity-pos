import { useNavigate } from 'react-router-dom'
import { useChannels } from '../contexts/ChannelContext'
import type { ChannelStatus } from '../api/channels'

function getStatusBadge(status: ChannelStatus) {
  switch (status) {
    case 'Active':
      return { className: 'badge badge-success', label: 'Active' }
    case 'Paused':
      return { className: 'badge badge-warning', label: 'Paused' }
    case 'Error':
      return { className: 'badge badge-danger', label: 'Error' }
    case 'Disconnected':
      return { className: 'badge badge-danger', label: 'Disconnected' }
    default:
      return { className: 'badge', label: status }
  }
}

export default function ChannelsPage() {
  const navigate = useNavigate()
  const { channels, isLoading, error, pauseOrders, resumeOrders } = useChannels()

  return (
    <>
      <hgroup>
        <h1>Channels</h1>
        <p>Manage delivery platform integrations</p>
      </hgroup>

      {error && (
        <article aria-label="Error">
          <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>
        </article>
      )}

      <div style={{ marginBottom: '1rem', display: 'flex', justifyContent: 'flex-end' }}>
        <button>Connect Channel</button>
      </div>

      <table aria-busy={isLoading}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Platform</th>
            <th>Integration</th>
            <th>Status</th>
            <th>Orders Today</th>
            <th>Revenue Today</th>
            <th>Locations</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {channels.map((ch) => {
            const badge = getStatusBadge(ch.status)
            return (
              <tr key={ch.channelId}>
                <td><strong>{ch.name}</strong></td>
                <td>{ch.platformType}</td>
                <td>{ch.integrationType}</td>
                <td><span className={badge.className}>{badge.label}</span></td>
                <td>{ch.totalOrdersToday}</td>
                <td>{ch.totalRevenueToday.toFixed(2)}</td>
                <td>{ch.locations.length}</td>
                <td>
                  <div style={{ display: 'flex', gap: '0.25rem' }}>
                    <button
                      className="secondary outline"
                      style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                      onClick={() => navigate(`/channels/${ch.channelId}`)}
                    >
                      Details
                    </button>
                    {ch.status === 'Active' && (
                      <button
                        className="secondary outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => pauseOrders(ch.channelId)}
                      >
                        Pause
                      </button>
                    )}
                    {(ch.status === 'Paused' || ch.status === 'Error') && (
                      <button
                        className="outline"
                        style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}
                        onClick={() => resumeOrders(ch.channelId)}
                      >
                        Resume
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {!isLoading && channels.length === 0 && (
        <p style={{ textAlign: 'center', padding: '2rem', color: 'var(--pico-muted-color)' }}>
          No channels connected
        </p>
      )}
    </>
  )
}
