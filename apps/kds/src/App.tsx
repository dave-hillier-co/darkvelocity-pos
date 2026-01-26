import { Routes, Route, Navigate } from 'react-router-dom'
import { KdsProvider } from './contexts/KdsContext'
import { SettingsProvider } from './contexts/SettingsContext'
import KdsPage from './pages/KdsPage'
import SettingsPage from './pages/SettingsPage'

export default function App() {
  return (
    <SettingsProvider>
      <KdsProvider>
        <Routes>
          <Route path="/" element={<KdsPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </KdsProvider>
    </SettingsProvider>
  )
}
