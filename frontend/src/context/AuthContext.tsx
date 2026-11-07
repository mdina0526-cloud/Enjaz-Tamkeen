import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setUnauthorizedHandler, tokenStore } from '@/lib/api'
import { queryClient } from '@/hooks/useApi'
import type { LoginResponse, UserInfo } from '@/types/api'

interface AuthState {
  user: UserInfo | null
  loading: boolean
  isAdmin: boolean
  login: (username: string, password: string, portal?: string) => Promise<UserInfo>
  /** Logs out and returns where to send the user: their personal login page if they came through one. */
  logout: () => string
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

/** Remembers the personal login link used on this device so logout returns the employee to their own page. */
const PORTAL_KEY = 'login_portal'
const portalStore = {
  get: () => {
    try {
      return localStorage.getItem(PORTAL_KEY)
    } catch {
      return null
    }
  },
  set: (v: string | null) => {
    try {
      if (v) localStorage.setItem(PORTAL_KEY, v)
      else localStorage.removeItem(PORTAL_KEY)
    } catch {
      /* storage unavailable */
    }
  },
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(true)

  const logout = useCallback(() => {
    const portal = portalStore.get()
    tokenStore.set(null)
    // Drop every cached response so the next account on this browser never sees the previous user's data.
    queryClient.clear()
    setUser(null)
    return portal ? `/e/${portal}` : '/login'
  }, [])

  const refresh = useCallback(async () => {
    if (!tokenStore.get()) {
      setUser(null)
      return
    }
    try {
      setUser(await api.get<UserInfo>('/api/auth/me'))
    } catch {
      tokenStore.set(null)
      setUser(null)
    }
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    refresh().finally(() => setLoading(false))
  }, [logout, refresh])

  const login = useCallback(async (username: string, password: string, portal?: string) => {
    const res = await api.post<LoginResponse>('/api/auth/login', portal ? { portal, password } : { username, password })
    queryClient.clear()
    tokenStore.set(res.token)
    portalStore.set(portal ?? null)
    setUser(res.user)
    return res.user
  }, [])

  const value = useMemo(() => ({ user, loading, isAdmin: user?.role === 'SuperAdmin', login, logout, refresh }), [user, loading, login, logout, refresh])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}
