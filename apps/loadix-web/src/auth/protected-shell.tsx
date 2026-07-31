import { useLocation } from '@tanstack/react-router'
import { useAuth } from '@/auth/auth-context'
import { AppShell } from '@/components/app-shell'
import { LoadingPage } from '@/pages/loading-page'
import { LoginPage } from '@/pages/login-page'

export function ProtectedShell() {
  const auth = useAuth()
  const location = useLocation()

  if (auth.loading) {
    return <LoadingPage />
  }
  if (!auth.user) {
    return <LoginPage redirectTo={safeRedirect(location.pathname)} />
  }
  return <AppShell />
}

function safeRedirect(path: string) {
  return path.startsWith('/') && !path.startsWith('//') ? path : '/'
}
