const BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''
const TOKEN_KEY = 'auth_token'

export class ApiError extends Error {
  status: number
  fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

export const tokenStore = {
  get: () => {
    try {
      return sessionStorage.getItem(TOKEN_KEY)
    } catch {
      return null
    }
  },
  set: (t: string | null) => {
    try {
      if (t) sessionStorage.setItem(TOKEN_KEY, t)
      else sessionStorage.removeItem(TOKEN_KEY)
    } catch {
      /* storage unavailable */
    }
  },
}

let onUnauthorized: (() => void) | null = null
export function setUnauthorizedHandler(fn: () => void) {
  onUnauthorized = fn
}

type Query = Record<string, string | number | boolean | null | undefined>

export function buildQuery(q?: Query) {
  if (!q) return ''
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(q)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v))
  const s = p.toString()
  return s ? `?${s}` : ''
}

async function parseError(res: Response): Promise<ApiError> {
  let message = 'تعذر إتمام الطلب'
  let fieldErrors: Record<string, string[]> = {}
  try {
    const body = await res.json()
    if (body?.errors && typeof body.errors === 'object') {
      fieldErrors = body.errors
      const first = Object.values(fieldErrors).flat()[0]
      message = first ?? body.title ?? message
    } else {
      message = body?.detail ?? body?.title ?? message
    }
  } catch {
    // Vite returns a plain 500 response when its API proxy cannot reach the backend.
    if (res.status >= 500) message = 'خادم النظام لا يستجيب. شغّل خادم API ثم حاول مجدداً'
  }
  if (res.status === 401 && message === 'تعذر إتمام الطلب') message = 'انتهت الجلسة، يرجى تسجيل الدخول مجدداً'
  if (res.status === 403 && message === 'تعذر إتمام الطلب') message = 'ليست لديك صلاحية لتنفيذ هذا الإجراء'
  if (res.status === 429) message = 'محاولات كثيرة، يرجى الانتظار قليلاً'
  return new ApiError(res.status, message, fieldErrors)
}

async function request<T>(method: string, path: string, opts: { query?: Query; body?: unknown; headers?: Record<string, string> } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', ...opts.headers }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`

  let body: BodyInit | undefined
  if (opts.body instanceof FormData) body = opts.body
  else if (opts.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(opts.body)
  }

  let res: Response
  try {
    res = await fetch(`${BASE}${path}${buildQuery(opts.query)}`, { method, headers, body })
  } catch {
    throw new ApiError(0, 'تعذر الاتصال بالخادم. تحقق من تشغيل الخادم والشبكة')
  }

  if (!res.ok) {
    const err = await parseError(res)
    if (res.status === 401 && token) onUnauthorized?.()
    throw err
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, query?: Query) => request<T>('GET', path, { query }),
  post: <T>(path: string, body?: unknown, headers?: Record<string, string>) => request<T>('POST', path, { body, headers }),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, { body }),

  /** Downloads a protected file (reports, receipts) with the bearer token. */
  async download(path: string, query?: Query, fallbackName = 'download') {
    const token = tokenStore.get()
    const res = await fetch(`${BASE}${path}${buildQuery(query)}`, { headers: token ? { Authorization: `Bearer ${token}` } : {} })
    if (!res.ok) throw await parseError(res)
    const blob = await res.blob()
    const cd = res.headers.get('Content-Disposition') ?? ''
    const match = /filename\*=UTF-8''([^;]+)/i.exec(cd) ?? /filename="?([^";]+)"?/i.exec(cd)
    const name = match ? decodeURIComponent(match[1]) : fallbackName
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  },
}
