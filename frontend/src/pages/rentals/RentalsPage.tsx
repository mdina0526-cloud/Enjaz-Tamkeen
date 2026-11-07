import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { AlertTriangle, Building2, CalendarClock, CheckCircle2, Plus, Search, Wallet } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery, useDebounced } from '@/hooks/useApi'
import { cn, formatDate, formatMoney, formatNumber } from '@/lib/utils'
import { propertyTypeLabel, rentalStatusLabel } from '@/lib/labels'
import type { RentalListItem, RentalStatus, RentalSummary } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Switch } from '@/components/ui/misc'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState, ErrorState, KpiCard, Money, PageHeader, RentalStatusBadge, TableSkeleton } from '@/components/common'
import { RentalFormDialog } from './RentalDialogs'

const statuses: (RentalStatus | '')[] = ['', 'Upcoming', 'Due', 'PartiallyPaid', 'Overdue', 'Paid']

export function RentalsPage() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const status = (params.get('status') ?? '') as RentalStatus | ''
  const [search, setSearch] = useState('')
  const [archived, setArchived] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const q = useDebounced(search)

  const summary = useApiQuery(['rentals', 'summary', archived], () => api.get<RentalSummary>('/api/rentals/summary', { includeArchived: archived }))
  const { data, loading, error, reload } = useApiQuery(
    ['rentals', 'list', q, status, archived],
    () => api.get<RentalListItem[]>('/api/rentals', { search: q, status, includeArchived: archived }),
    { keepPrevious: true },
  )
  const s = summary.data
  const counts: Record<string, number | undefined> = { '': s?.total, Upcoming: s?.upcoming, Due: s?.due, PartiallyPaid: s?.partiallyPaid, Overdue: s?.overdue, Paid: s?.paid }

  return (
    <div className="space-y-6">
      <PageHeader title="إدارة الإيجارات" description="العقارات والمكاتب والمواقع المستأجرة، الاستحقاقات والدفعات" actions={<Button onClick={() => setCreateOpen(true)}><Plus /> إضافة إيجار</Button>} />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard loading={summary.loading} label="إجمالي قيمة الإيجارات" value={formatMoney(s?.totalAmount)} icon={Building2} tone="navy" hint={<><span className="num">{formatNumber(s?.total)}</span> إيجار</>} />
        <KpiCard loading={summary.loading} label="المدفوع" value={formatMoney(s?.totalPaid)} icon={CheckCircle2} tone="success" />
        <KpiCard loading={summary.loading} label="المتبقي" value={formatMoney(s?.totalRemaining)} icon={Wallet} tone="warning" />
        <KpiCard loading={summary.loading} label="المتأخر" value={formatMoney(s?.overdueAmount)} icon={AlertTriangle} tone="destructive" hint={<><span className="num">{formatNumber(s?.overdue)}</span> إيجار متأخر</>} />
      </div>

      <Card>
        <div className="flex flex-col gap-3 border-b p-4">
          <div className="flex flex-wrap gap-2">
            {statuses.map((st) => (
              <button
                key={st || 'all'}
                onClick={() => setParams(st ? { status: st } : {}, { replace: true })}
                className={cn('flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm transition-colors', status === st ? 'border-primary bg-primary text-white' : 'hover:bg-accent')}
              >
                {st ? rentalStatusLabel[st] : 'الكل'}
                <span className={cn('num rounded-full px-1.5 text-xs', status === st ? 'bg-white/20' : 'bg-muted')}>{counts[st] ?? 0}</span>
              </button>
            ))}
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative min-w-56 flex-1">
              <Search className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input className="pr-9" placeholder="بحث بالاسم أو المستفيد أو الموقع..." value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
            <div className="flex items-center gap-2">
              <Switch id="archived" checked={archived} onCheckedChange={setArchived} />
              <Label htmlFor="archived">عرض المؤرشفة</Label>
            </div>
          </div>
        </div>
        {error ? (
          <ErrorState message={error} onRetry={reload} />
        ) : loading && !data ? (
          <TableSkeleton />
        ) : data && data.length ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>العقار</TableHead>
                <TableHead>المستفيد</TableHead>
                <TableHead>تاريخ الاستحقاق</TableHead>
                <TableHead className="text-end">القيمة</TableHead>
                <TableHead className="text-end">المدفوع</TableHead>
                <TableHead className="text-end">المتبقي</TableHead>
                <TableHead>الحالة</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.map((r) => (
                <TableRow key={r.id} className={cn('cursor-pointer', r.isArchived && 'opacity-60')} onClick={() => navigate(`/rentals/${r.id}`)}>
                  <TableCell>
                    <p className="font-medium">{r.title}</p>
                    <p className="text-xs text-muted-foreground">{propertyTypeLabel[r.propertyType]}{r.location ? ` · ${r.location}` : ''}</p>
                  </TableCell>
                  <TableCell>{r.beneficiaryName}</TableCell>
                  <TableCell>
                    <span className="flex items-center gap-1.5"><CalendarClock className="size-3.5 text-muted-foreground" /> {formatDate(r.dueDate)}</span>
                  </TableCell>
                  <TableCell className="text-end font-semibold"><Money value={r.amount} /></TableCell>
                  <TableCell className="text-end text-success"><Money value={r.paidAmount} /></TableCell>
                  <TableCell className="text-end"><Money value={r.remainingAmount} /></TableCell>
                  <TableCell><RentalStatusBadge status={r.status} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <EmptyState icon={Building2} title="لا توجد إيجارات" description={status || q ? 'لا توجد نتائج مطابقة' : 'أضف أول عقار أو مكتب مستأجر'} action={!status && !q && <Button onClick={() => setCreateOpen(true)}><Plus /> إضافة إيجار</Button>} />
        )}
      </Card>

      <RentalFormDialog open={createOpen} onOpenChange={setCreateOpen} onSaved={(r) => navigate(`/rentals/${r.id}`)} />
    </div>
  )
}
