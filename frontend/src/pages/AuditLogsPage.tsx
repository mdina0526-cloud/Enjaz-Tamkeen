import { useState } from 'react'
import { ScrollText, Search } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery, useDebounced } from '@/hooks/useApi'
import { formatDateTime } from '@/lib/utils'
import { auditActionLabel, entityTypeLabel } from '@/lib/labels'
import type { AuditLog, PagedResult } from '@/types/api'
import { Badge, type BadgeVariant } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Input, NativeSelect } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState, ErrorState, PageHeader, Pagination, TableSkeleton } from '@/components/common'
import { DateRangeFilter } from '@/components/filters'

const actionVariant: Record<string, BadgeVariant> = {
  Cancel: 'destructive', Void: 'destructive', Deactivate: 'destructive', LoginFailed: 'destructive',
  Create: 'success', Approve: 'success', Activate: 'success',
  Update: 'default', ChangeDeduction: 'warning', ResetPassword: 'warning', ChangePassword: 'warning',
}

function Details({ json }: { json: string | null }) {
  if (!json) return <span className="text-muted-foreground">—</span>
  let text = json
  try {
    text = JSON.stringify(JSON.parse(json), null, 1).replace(/[{}"]/g, '').replace(/\n\s*/g, ' ').trim()
  } catch { /* raw */ }
  return <span className="block max-w-md truncate text-xs text-muted-foreground" title={json} dir="ltr">{text}</span>
}

export function AuditLogsPage() {
  const [search, setSearch] = useState('')
  const [action, setAction] = useState('')
  const [entityType, setEntityType] = useState('')
  const [range, setRange] = useState({ from: '', to: '' })
  const [page, setPage] = useState(1)
  const q = useDebounced(search)
  const { data, loading, error, reload } = useApiQuery(
    ['audit', q, action, entityType, range.from, range.to, page],
    () => api.get<PagedResult<AuditLog>>('/api/audit-logs', { search: q, action, entityType, ...range, page, pageSize: 50 }),
    { keepPrevious: true },
  )

  const reset = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setPage(1) }

  return (
    <div>
      <PageHeader title="سجل النشاط" description="كل عمليات الإنشاء والتعديل والإلغاء والدخول مسجّلة ولا يمكن تعديلها" />
      <Card>
        <div className="flex flex-wrap items-center gap-2 border-b p-4">
          <div className="relative min-w-56 flex-1">
            <Search className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input className="pr-9" placeholder="بحث باسم المستخدم أو رقم العنصر أو التفاصيل..." value={search} onChange={(e) => reset(setSearch)(e.target.value)} />
          </div>
          <NativeSelect aria-label="الإجراء" className="w-48" value={action} onChange={(e) => reset(setAction)(e.target.value)}>
            <option value="">كل الإجراءات</option>
            {Object.entries(auditActionLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
          <NativeSelect aria-label="نوع العنصر" className="w-44" value={entityType} onChange={(e) => reset(setEntityType)(e.target.value)}>
            <option value="">كل العناصر</option>
            {Object.entries(entityTypeLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </NativeSelect>
          <DateRangeFilter from={range.from} to={range.to} onChange={reset(setRange)} />
        </div>
        {error ? (
          <ErrorState message={error} onRetry={reload} />
        ) : loading && !data ? (
          <TableSkeleton rows={10} />
        ) : data?.items.length ? (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>الوقت</TableHead>
                  <TableHead>المستخدم</TableHead>
                  <TableHead>الإجراء</TableHead>
                  <TableHead>العنصر</TableHead>
                  <TableHead>التفاصيل</TableHead>
                  <TableHead>IP</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="text-muted-foreground">{formatDateTime(l.createdAtUtc)}</TableCell>
                    <TableCell className="num">{l.username ?? '—'}</TableCell>
                    <TableCell><Badge variant={actionVariant[l.action] ?? 'muted'}>{auditActionLabel[l.action] ?? l.action}</Badge></TableCell>
                    <TableCell>{entityTypeLabel[l.entityType] ?? l.entityType} <span className="num text-muted-foreground">#{l.entityId}</span></TableCell>
                    <TableCell><Details json={l.details} /></TableCell>
                    <TableCell className="num text-xs text-muted-foreground">{l.ipAddress ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onChange={setPage} />
          </>
        ) : (
          <EmptyState icon={ScrollText} title="لا توجد سجلات مطابقة" />
        )}
      </Card>
    </div>
  )
}
