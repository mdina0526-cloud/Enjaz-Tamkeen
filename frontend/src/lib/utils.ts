import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

const moneyFmt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const intFmt = new Intl.NumberFormat('en-US')

export const CURRENCY = 'ر.س'

export function formatMoney(v: number | null | undefined, withCurrency = true) {
  const s = moneyFmt.format(v ?? 0)
  return withCurrency ? `${s} ${CURRENCY}` : s
}

export function formatNumber(v: number | null | undefined) {
  return intFmt.format(v ?? 0)
}

/** Server timestamps are UTC ISO strings; display in the business time zone. */
const dateTimeFmt = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', {
  timeZone: 'Asia/Riyadh',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})
const dateFmt = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { timeZone: 'Asia/Riyadh', year: 'numeric', month: 'short', day: 'numeric' })

function asUtc(iso: string) {
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`)
}

export function formatDateTime(iso: string | null | undefined) {
  return iso ? dateTimeFmt.format(asUtc(iso)) : '—'
}

export function formatDate(iso: string | null | undefined) {
  if (!iso) return '—'
  // Plain dates (yyyy-MM-dd) are calendar dates, not instants.
  if (/^\d{4}-\d{2}-\d{2}$/.test(iso)) {
    const [y, m, d] = iso.split('-').map(Number)
    return new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(y, m - 1, d))
  }
  return dateFmt.format(asUtc(iso))
}

/** Today's date (yyyy-MM-dd) in the business time zone. */
export function todayIso() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Riyadh' }).format(new Date())
}

export function shiftIso(iso: string, days: number) {
  const [y, m, d] = iso.split('-').map(Number)
  const dt = new Date(Date.UTC(y, m - 1, d + days))
  return dt.toISOString().slice(0, 10)
}

/** Saturday-based business week, as used in Saudi Arabia. */
export function startOfWeekIso() {
  const t = todayIso()
  const [y, m, d] = t.split('-').map(Number)
  const dow = new Date(Date.UTC(y, m - 1, d)).getUTCDay()
  return shiftIso(t, -((dow + 1) % 7))
}

export function startOfYearIso() {
  return todayIso().slice(0, 4) + '-01-01'
}

/** Percentage change, or null when there is no baseline to compare with. */
export function percentChange(current: number, previous: number | null | undefined) {
  if (previous === null || previous === undefined || previous === 0) return null
  return ((current - previous) / Math.abs(previous)) * 100
}

export function startOfMonthIso() {
  return todayIso().slice(0, 8) + '01'
}

export function newIdempotencyKey() {
  return crypto.randomUUID()
}
