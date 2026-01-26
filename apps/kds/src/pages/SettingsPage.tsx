import { Link } from 'react-router-dom'
import { useSettings } from '../contexts/SettingsContext'
import type { StationType, TicketLayout } from '../types'

export default function SettingsPage() {
  const { settings, updateSettings } = useSettings()

  return (
    <main className="settings-page">
      <header className="settings-header">
        <Link to="/" className="back-link">Back to KDS</Link>
        <h1>Station Settings</h1>
      </header>

      <form className="settings-form" onSubmit={(e) => e.preventDefault()}>
        <fieldset>
          <legend>Station Info</legend>

          <label htmlFor="station-name">
            Station Name
            <input
              type="text"
              id="station-name"
              value={settings.name}
              onChange={(e) => updateSettings({ name: e.target.value })}
            />
          </label>

          <label htmlFor="station-type">
            Station Type
            <select
              id="station-type"
              value={settings.type}
              onChange={(e) => updateSettings({ type: e.target.value as StationType })}
            >
              <option value="prep">Prep Station</option>
              <option value="expo">Expo Station</option>
            </select>
          </label>

          <label htmlFor="layout">
            Layout
            <select
              id="layout"
              value={settings.layout}
              onChange={(e) => updateSettings({ layout: e.target.value as TicketLayout })}
            >
              <option value="tiled">Tiled</option>
              <option value="classic">Classic</option>
            </select>
          </label>
        </fieldset>

        <fieldset>
          <legend>Time Thresholds</legend>

          <label htmlFor="yellow-threshold">
            Yellow Threshold (seconds)
            <input
              type="number"
              id="yellow-threshold"
              value={settings.yellowThresholdSeconds}
              min={60}
              max={1800}
              onChange={(e) =>
                updateSettings({ yellowThresholdSeconds: parseInt(e.target.value, 10) || 300 })
              }
            />
            <small>Tickets turn yellow after this many seconds (default: 300 = 5 min)</small>
          </label>

          <label htmlFor="red-threshold">
            Red Threshold (seconds)
            <input
              type="number"
              id="red-threshold"
              value={settings.redThresholdSeconds}
              min={120}
              max={3600}
              onChange={(e) =>
                updateSettings({ redThresholdSeconds: parseInt(e.target.value, 10) || 600 })
              }
            />
            <small>Tickets turn red after this many seconds (default: 600 = 10 min)</small>
          </label>
        </fieldset>

        <p className="settings-note">
          Settings are saved automatically and persist across sessions.
        </p>
      </form>
    </main>
  )
}
