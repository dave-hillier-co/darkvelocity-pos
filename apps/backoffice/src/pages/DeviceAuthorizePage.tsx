import { useState, useEffect } from 'react'
import { useSearchParams, useNavigate } from 'react-router-dom'

interface Site {
  id: string
  name: string
}

type DeviceType = 'Pos' | 'Kds'

export default function DeviceAuthorizePage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()

  const [userCode, setUserCode] = useState(searchParams.get('code') || '')
  const [sites, setSites] = useState<Site[]>([])
  const [selectedSite, setSelectedSite] = useState('')
  const [deviceName, setDeviceName] = useState('')
  const [appType, setAppType] = useState<DeviceType>('Pos')
  const [status, setStatus] = useState<'input' | 'loading' | 'success' | 'error'>('input')
  const [error, setError] = useState<string | null>(null)
  const [isLoadingSites, setIsLoadingSites] = useState(true)

  // Mock organization ID - in production this would come from auth context
  const organizationId = '00000000-0000-0000-0000-000000000001'
  const authorizedBy = '00000000-0000-0000-0000-000000000001'

  useEffect(() => {
    // Load available sites for the organization
    // In production, this would be an API call
    setIsLoadingSites(true)
    setTimeout(() => {
      setSites([
        { id: '00000000-0000-0000-0000-000000000001', name: 'Main Location' },
        { id: '00000000-0000-0000-0000-000000000002', name: 'Downtown Branch' },
        { id: '00000000-0000-0000-0000-000000000003', name: 'Airport Kiosk' },
      ])
      setIsLoadingSites(false)
    }, 500)
  }, [])

  function formatUserCode(code: string): string {
    // Remove any non-alphanumeric characters and format as XXXX-XXXX
    const clean = code.replace(/[^A-Za-z0-9]/g, '').toUpperCase()
    if (clean.length <= 4) return clean
    return `${clean.slice(0, 4)}-${clean.slice(4, 8)}`
  }

  function handleCodeChange(e: React.ChangeEvent<HTMLInputElement>) {
    const formatted = formatUserCode(e.target.value)
    setUserCode(formatted)
  }

  async function handleAuthorize(e: React.FormEvent) {
    e.preventDefault()
    setStatus('loading')
    setError(null)

    try {
      const response = await fetch(`${import.meta.env.VITE_API_URL || 'http://localhost:5200'}/api/device/authorize`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userCode: userCode.replace('-', ''),
          authorizedBy,
          organizationId,
          siteId: selectedSite,
          deviceName,
          appType,
        }),
      })

      if (response.ok) {
        setStatus('success')
      } else {
        const data = await response.json().catch(() => ({ error: 'Authorization failed' }))
        setError(data.error_description || data.error || 'Authorization failed')
        setStatus('error')
      }
    } catch {
      setError('Network error. Please try again.')
      setStatus('error')
    }
  }

  if (status === 'success') {
    return (
      <main className="container" style={{ maxWidth: '500px', marginTop: '10vh' }}>
        <article style={{ textAlign: 'center', padding: '2rem' }}>
          <header>
            <h1 style={{ color: 'var(--pico-ins-color)' }}>Device Authorized</h1>
          </header>

          <p style={{ marginBottom: '2rem' }}>
            The device "{deviceName}" has been successfully authorized.
            You can now return to the device to continue setup.
          </p>

          <button onClick={() => navigate('/dashboard')}>
            Back to Dashboard
          </button>
        </article>
      </main>
    )
  }

  return (
    <main className="container" style={{ maxWidth: '500px', marginTop: '5vh' }}>
      <article style={{ padding: '2rem' }}>
        <header style={{ textAlign: 'center', marginBottom: '2rem' }}>
          <h1>Authorize Device</h1>
          <p style={{ color: 'var(--pico-muted-color)' }}>
            Enter the code shown on your POS or KDS device
          </p>
        </header>

        <form onSubmit={handleAuthorize}>
          <label>
            Device Code
            <input
              type="text"
              value={userCode}
              onChange={handleCodeChange}
              placeholder="ABCD-1234"
              maxLength={9}
              required
              autoComplete="off"
              autoFocus
              style={{
                textAlign: 'center',
                fontSize: '1.5rem',
                letterSpacing: '0.15em',
                textTransform: 'uppercase',
                fontFamily: 'monospace',
              }}
            />
          </label>

          <label>
            Device Name
            <input
              type="text"
              value={deviceName}
              onChange={(e) => setDeviceName(e.target.value)}
              placeholder="e.g., Bar Terminal 1, Kitchen Display"
              required
            />
            <small>A friendly name to identify this device</small>
          </label>

          <label>
            App Type
            <select
              value={appType}
              onChange={(e) => setAppType(e.target.value as DeviceType)}
              required
            >
              <option value="Pos">Point of Sale (POS)</option>
              <option value="Kds">Kitchen Display System (KDS)</option>
            </select>
          </label>

          <label>
            Location
            <select
              value={selectedSite}
              onChange={(e) => setSelectedSite(e.target.value)}
              required
              disabled={isLoadingSites}
              aria-busy={isLoadingSites}
            >
              <option value="">Select a location...</option>
              {sites.map(site => (
                <option key={site.id} value={site.id}>{site.name}</option>
              ))}
            </select>
            <small>The site where this device will be used</small>
          </label>

          {error && (
            <p role="alert" style={{ color: 'var(--pico-del-color)', marginTop: '1rem' }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={status === 'loading' || !userCode || !deviceName || !selectedSite}
            aria-busy={status === 'loading'}
            style={{ marginTop: '1rem' }}
          >
            Authorize Device
          </button>
        </form>

        <footer style={{ marginTop: '2rem', textAlign: 'center' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>
            The device will automatically continue once authorized.
          </small>
        </footer>
      </article>
    </main>
  )
}
