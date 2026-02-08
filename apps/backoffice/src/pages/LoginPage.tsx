import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function LoginPage() {
  const { loginWithGoogle, loginWithMicrosoft, loginAsDev, isAuthenticated, isLoading, error } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/dashboard')
    }
  }, [isAuthenticated, navigate])

  if (isLoading) {
    return (
      <main className="login-page">
        <article className="login-card" aria-busy="true">
          <p>Loading...</p>
        </article>
      </main>
    )
  }

  return (
    <main className="login-page">
      <article className="login-card">
        <header>
          <h1>DarkVelocity</h1>
          <p>Back Office</p>
        </header>

        {error && (
          <p role="alert" style={{ color: 'var(--pico-del-color)', marginBottom: '1rem' }}>
            {error}
          </p>
        )}

        <section className="social-login">
          <button
            type="button"
            onClick={loginWithGoogle}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '0.5rem',
              backgroundColor: '#fff',
              color: '#333',
              border: '1px solid #ddd',
            }}
          >
            <svg width="18" height="18" viewBox="0 0 18 18" xmlns="http://www.w3.org/2000/svg">
              <path
                fill="#4285F4"
                d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844a4.14 4.14 0 01-1.796 2.716v2.259h2.908c1.702-1.567 2.684-3.875 2.684-6.615z"
              />
              <path
                fill="#34A853"
                d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332A8.997 8.997 0 009 18z"
              />
              <path
                fill="#FBBC05"
                d="M3.964 10.71A5.41 5.41 0 013.682 9c0-.593.102-1.17.282-1.71V4.958H.957A8.996 8.996 0 000 9c0 1.452.348 2.827.957 4.042l3.007-2.332z"
              />
              <path
                fill="#EA4335"
                d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0A8.997 8.997 0 00.957 4.958L3.964 7.29C4.672 5.163 6.656 3.58 9 3.58z"
              />
            </svg>
            Continue with Google
          </button>

          <button
            type="button"
            onClick={loginWithMicrosoft}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '0.5rem',
              backgroundColor: '#fff',
              color: '#333',
              border: '1px solid #ddd',
              marginTop: '0.75rem',
            }}
          >
            <svg width="18" height="18" viewBox="0 0 21 21" xmlns="http://www.w3.org/2000/svg">
              <rect x="1" y="1" width="9" height="9" fill="#f25022" />
              <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
              <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
              <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
            </svg>
            Continue with Microsoft
          </button>

          <button
            type="button"
            onClick={loginAsDev}
            className="dev-login"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '0.5rem',
              backgroundColor: 'transparent',
              color: 'var(--pico-muted-color)',
              border: '1px dashed var(--pico-muted-border-color)',
              marginTop: '0.75rem',
            }}
          >
            Dev Login
          </button>
        </section>

        <footer style={{ marginTop: '2rem', textAlign: 'center' }}>
          <small style={{ color: 'var(--pico-muted-color)' }}>
            By signing in, you agree to our Terms of Service
          </small>
        </footer>
      </article>
    </main>
  )
}
