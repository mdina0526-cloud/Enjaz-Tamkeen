import { useState, type ReactNode } from 'react'
import { AlertTriangle, ChevronLeft, ChevronRight, Inbox, RefreshCw, type LucideIcon } from 'lucide-react'
import { cn, formatNumber } from '@/lib/utils'
import { orderStatusLabel, paymentMethodLabel, rentalStatusLabel } from '@/lib/labels'
import type { OrderStatus, PaymentMethod, RentalStatus } from '@/types/api'
import { Badge, type BadgeVariant } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/misc'
import { Textarea } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { AlertDialog, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogTitle } from '@/components/ui/dialog'

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h2 className="text-2xl font-bold tracking-tight">{title}</h2>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {actions && <div className="no-print flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

export function KpiCard({ label, value, icon: Icon, hint, tone = 'primary', loading }: { label: string; value: ReactNode; icon: LucideIcon; hint?: ReactNode; tone?: 'primary' | 'success' | 'warning' | 'destructive' | 'navy'; loading?: boolean }) {
  const tones = {
    primary: 'bg-primary/10 text-primary',
    success: 'bg-success/12 text-success',
    warning: 'bg-warning/12 text-warning',
    destructive: 'bg-destructive/12 text-destructive',
    navy: 'bg-navy/10 text-navy dark:bg-white/10 dark:text-white',
  }
  return (
    <Card className="relative overflow-hidden p-5 transition-shadow hover:shadow-md">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-2">
          <p className="text-sm text-muted-foreground">{label}</p>
          {loading ? <Skeleton className="h-8 w-32" /> : <p className="truncate text-xl font-bold tracking-tight whitespace-nowrap 2xl:text-2xl"><span className="num">{value}</span></p>}
          {hint && !loading && <div className="text-xs text-muted-foreground">{hint}</div>}
        </div>
        <span className={cn('flex size-11 shrink-0 items-center justify-center rounded-xl', tones[tone])}>
          <Icon className="size-5" />
        </span>
      </div>
    </Card>
  )
}

export function EmptyState({ title = 'لا توجد بيانات', description, icon: Icon = Inbox, action }: { title?: string; description?: string; icon?: LucideIcon; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-14 text-center">
      <span className="flex size-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
        <Icon className="size-7" />
      </span>
      <div>
        <p className="font-semibold">{title}</p>
        {description && <p className="mt-1 max-w-sm text-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  )
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-6 py-12 text-center">
      <span className="flex size-12 items-center justify-center rounded-2xl bg-destructive/10 text-destructive">
        <AlertTriangle className="size-6" />
      </span>
      <p className="text-sm text-muted-foreground">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RefreshCw /> إعادة المحاولة
        </Button>
      )}
    </div>
  )
}

export function TableSkeleton({ rows = 6, cols = 6 }: { rows?: number; cols?: number }) {
  return (
    <div className="space-y-3 p-4">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex gap-3">
          {Array.from({ length: cols }).map((__, j) => (
            <Skeleton key={j} className="h-6 flex-1" />
          ))}
        </div>
      ))}
    </div>
  )
}

const orderVariant: Record<OrderStatus, BadgeVariant> = { Pending: 'warning', Completed: 'success', Cancelled: 'destructive' }
export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  return <Badge variant={orderVariant[status]}>{orderStatusLabel[status]}</Badge>
}

const rentalVariant: Record<RentalStatus, BadgeVariant> = { Upcoming: 'muted', Due: 'warning', PartiallyPaid: 'default', Paid: 'success', Overdue: 'destructive' }
export function RentalStatusBadge({ status }: { status: RentalStatus }) {
  return <Badge variant={rentalVariant[status]}>{rentalStatusLabel[status]}</Badge>
}

export function PaymentBadge({ method }: { method: PaymentMethod }) {
  return <Badge variant={method === 'Cash' ? 'secondary' : 'outline'}>{paymentMethodLabel[method]}</Badge>
}

export function Pagination({ page, pageSize, total, onChange }: { page: number; pageSize: number; total: number; onChange: (p: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (total === 0) return null
  const from = (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <div className="no-print flex flex-col items-center justify-between gap-3 border-t px-4 py-3 text-sm sm:flex-row">
      <p className="text-muted-foreground">
        عرض <span className="num">{formatNumber(from)}–{formatNumber(to)}</span> من <span className="num">{formatNumber(total)}</span>
      </p>
      <div className="flex items-center gap-1">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onChange(page - 1)}>
          <ChevronRight /> السابق
        </Button>
        <span className="num px-3 text-muted-foreground">
          {page} / {pages}
        </span>
        <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => onChange(page + 1)}>
          التالي <ChevronLeft />
        </Button>
      </div>
    </div>
  )
}

/** Confirmation dialog; with `requireReason` it collects a mandatory reason (cancellations, voids). */
export function ConfirmDialog({
  open, onOpenChange, title, description, confirmLabel = 'تأكيد', destructive, requireReason, onConfirm,
}: {
  open: boolean
  onOpenChange: (o: boolean) => void
  title: string
  description?: string
  confirmLabel?: string
  destructive?: boolean
  requireReason?: boolean
  onConfirm: (reason: string) => Promise<void> | void
}) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [touched, setTouched] = useState(false)
  const invalid = requireReason && reason.trim().length === 0

  const submit = async () => {
    setTouched(true)
    if (invalid) return
    setBusy(true)
    try {
      await onConfirm(reason.trim())
      setReason('')
      setTouched(false)
      onOpenChange(false)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={(o) => !busy && onOpenChange(o)}>
      <AlertDialogContent>
        <div className="space-y-2">
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description && <AlertDialogDescription>{description}</AlertDialogDescription>}
        </div>
        {requireReason && (
          <div className="space-y-1.5">
            <Label htmlFor="confirm-reason">السبب</Label>
            <Textarea id="confirm-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} aria-invalid={touched && invalid} placeholder="اكتب السبب..." />
            {touched && invalid && <p className="text-xs text-destructive">السبب مطلوب</p>}
          </div>
        )}
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-start">
          <Button variant={destructive ? 'destructive' : 'default'} loading={busy} onClick={submit}>
            {confirmLabel}
          </Button>
          <AlertDialogCancel disabled={busy}>تراجع</AlertDialogCancel>
        </div>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function Money({ value, className }: { value: number; className?: string }) {
  return <span className={cn('num', className)}>{new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)}</span>
}

export function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 border-b border-dashed py-2.5 last:border-0">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-end text-sm font-medium">{children}</span>
    </div>
  )
}

/** Change against the previous period. `inverse` flips the colours for metrics where a rise is bad. */
export function Delta({ value, inverse = false }: { value: number | null; inverse?: boolean }) {
  if (value === null || !Number.isFinite(value)) return <span className="text-muted-foreground">لا توجد بيانات للمقارنة</span>
  const up = value > 0
  const flat = Math.abs(value) < 0.05
  const good = flat ? null : inverse ? !up : up
  return (
    <span className={cn('inline-flex items-center gap-1 font-medium', good === null ? 'text-muted-foreground' : good ? 'text-success' : 'text-destructive')}>
      <span aria-hidden>{flat ? '•' : up ? '▲' : '▼'}</span>
      <span className="num">{flat ? '0%' : `${up ? '+' : ''}${value.toFixed(1)}%`}</span>
      <span className="font-normal text-muted-foreground">عن الفترة السابقة</span>
    </span>
  )
}
