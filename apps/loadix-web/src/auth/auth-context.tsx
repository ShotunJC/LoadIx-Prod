import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { clearDriverOfflineData } from '@/offline/command-queue'

export interface UserRole {
  id: string
  name: string
}

export interface CurrentUser {
  id: string
  tenantId: string | null
  email: string
  displayName: string
  permissions: string[]
  roles: UserRole[]
  isPlatformAdministrator: boolean
  isSupportContext: boolean
}

interface AuthResponse {
  accessToken: string
  expiresAtUtc: string
  sessionId: string
  user: CurrentUser
}

interface AuthContextValue {
  user: CurrentUser | null
  loading: boolean
  sessionExpired: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>
  upload: (
    input: string,
    body: FormData,
    headers: Record<string, string>,
    onProgress: (percent: number) => void,
  ) => Promise<Response>
  clearSessionExpired: () => void
  getAccessToken: () => string | null
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)
  const [sessionExpired, setSessionExpired] = useState(false)
  const accessToken = useRef<string | null>(null)
  const refreshPromise = useRef<Promise<boolean> | null>(null)

  const applyAuthentication = useCallback((response: AuthResponse) => {
    accessToken.current = response.accessToken
    setUser(response.user)
    setSessionExpired(false)
  }, [])

  const refresh = useCallback(async () => {
    if (refreshPromise.current) {
      return refreshPromise.current
    }

    refreshPromise.current = fetch('/api/v1/auth/refresh', {
      method: 'POST',
      credentials: 'include',
      headers: { 'X-LoadIx-Client': 'web' },
    })
      .then(async (response) => {
        if (!response.ok) {
          accessToken.current = null
          setUser(null)
          return false
        }

        applyAuthentication((await response.json()) as AuthResponse)
        return true
      })
      .finally(() => {
        refreshPromise.current = null
      })
    return refreshPromise.current
  }, [applyAuthentication])

  useEffect(() => {
    void refresh().finally(() => {
      setLoading(false)
    })
  }, [refresh])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await fetch('/api/v1/auth/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) {
        throw new Error('The email or password is incorrect.')
      }

      applyAuthentication((await response.json()) as AuthResponse)
    },
    [applyAuthentication],
  )

  const logout = useCallback(async () => {
    if (accessToken.current) {
      await fetch('/api/v1/auth/logout', {
        method: 'POST',
        credentials: 'include',
        headers: {
          Authorization: `Bearer ${accessToken.current}`,
          'X-LoadIx-Client': 'web',
        },
      })
    }
    accessToken.current = null
    await clearDriverOfflineData()
    setUser(null)
  }, [])

  const request = useCallback(
    async (input: RequestInfo | URL, init: RequestInit = {}) => {
      const send = () => {
        const headers = new Headers(init.headers)
        if (accessToken.current) {
          headers.set('Authorization', `Bearer ${accessToken.current}`)
        }
        return fetch(input, { ...init, headers, credentials: 'include' })
      }

      let response = await send()
      if (response.status === 401 && (await refresh())) {
        response = await send()
      } else if (response.status === 401) {
        setSessionExpired(true)
      }
      return response
    },
    [refresh],
  )

  const upload = useCallback(
    async (
      input: string,
      body: FormData,
      headers: Record<string, string>,
      onProgress: (percent: number) => void,
    ): Promise<Response> => {
      const send = () =>
        new Promise<Response>((resolve, reject) => {
          const xhr = new XMLHttpRequest()
          xhr.open('POST', input)
          xhr.withCredentials = true
          if (accessToken.current) {
            xhr.setRequestHeader(
              'Authorization',
              `Bearer ${accessToken.current}`,
            )
          }
          Object.entries(headers).forEach(([name, value]) => {
            xhr.setRequestHeader(name, value)
          })
          xhr.upload.addEventListener('progress', (event) => {
            if (event.lengthComputable) {
              onProgress(Math.round((event.loaded / event.total) * 100))
            }
          })
          xhr.addEventListener('error', () => {
            reject(new Error('The upload connection failed.'))
          })
          xhr.addEventListener('load', () => {
            resolve(
              new Response(xhr.responseText, {
                status: xhr.status,
                statusText: xhr.statusText,
                headers: {
                  'Content-Type':
                    xhr.getResponseHeader('Content-Type') ?? 'application/json',
                },
              }),
            )
          })
          xhr.send(body)
        })

      let response = await send()
      if (response.status === 401 && (await refresh())) {
        response = await send()
      } else if (response.status === 401) {
        setSessionExpired(true)
      }
      return response
    },
    [refresh],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      sessionExpired,
      login,
      logout,
      request,
      upload,
      clearSessionExpired: () => {
        setSessionExpired(false)
      },
      getAccessToken: () => accessToken.current,
    }),
    [loading, login, logout, request, sessionExpired, upload, user],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// The provider and hook intentionally share the same private context.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.')
  }
  return context
}
