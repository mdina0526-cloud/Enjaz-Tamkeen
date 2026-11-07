import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, UserPlus, Users } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery, useDebounced } from '@/hooks/useApi'
import { formatDateTime, formatMoney, formatNumber } from '@/lib/utils'
import type { EmployeeListItem } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input, NativeSelect } from '@/components/ui/input'
import { Avatar } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState, ErrorState, PageHeader, TableSkeleton } from '@/components/common'
import { EmployeeFormDialog } from './EmployeeDialogs'

export function EmployeesPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const q = useDebounced(search)
  const { data, loading, error, reload } = useApiQuery(['employees', 'list', q, status], () => api.get<EmployeeListItem[]>('/api/employees', { search: q, isActive: status }), {
    keepPrevious: true,
  })

  return (
    <div>
      <PageHeader
        title="إدارة الموظفين"
        description="الحسابات، الصلاحيات، نسب الاستقطاع، وإحصائيات الأداء"
        actions={<Button onClick={() => setCreateOpen(true)}><UserPlus /> إضافة موظف</Button>}
      />
      <Card>
        <div className="flex flex-wrap gap-2 border-b p-4">
          <div className="relative min-w-56 flex-1">
            <Search className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input className="pr-9" placeholder="بحث بالاسم أو اسم المستخدم أو الرقم الوظيفي..." value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <NativeSelect aria-label="الحالة" className="w-40" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">كل الحالات</option>
            <option value="true">نشط</option>
            <option value="false">معطل</option>
          </NativeSelect>
        </div>
        {error ? (
          <ErrorState message={error} onRetry={reload} />
        ) : loading && !data ? (
          <TableSkeleton />
        ) : data && data.length > 0 ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>الموظف</TableHead>
                <TableHead>الرقم الوظيفي</TableHead>
                <TableHead>المسمى</TableHead>
                <TableHead className="text-end">نسبة الاستقطاع</TableHead>
                <TableHead className="text-end">العمليات</TableHead>
                <TableHead className="text-end">إجمالي المبيعات</TableHead>
                <TableHead>آخر دخول</TableHead>
                <TableHead>الحالة</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.map((e) => (
                <TableRow key={e.id} className="cursor-pointer" onClick={() => navigate(`/employees/${e.id}`)}>
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <Avatar name={e.fullName} />
                      <div>
                        <p className="font-medium">{e.fullName}</p>
                        <p className="num text-end text-xs text-muted-foreground">@{e.username}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell className="num">{e.employeeCode}</TableCell>
                  <TableCell className="text-muted-foreground">{e.jobTitle ?? '—'}</TableCell>
                  <TableCell className="num text-end font-semibold">{e.deductionPercentage}%</TableCell>
                  <TableCell className="num text-end">{formatNumber(e.ordersCount)}</TableCell>
                  <TableCell className="num text-end font-semibold">{formatMoney(e.totalSales)}</TableCell>
                  <TableCell className="text-muted-foreground">{e.lastLoginAtUtc ? formatDateTime(e.lastLoginAtUtc) : 'لم يسجل الدخول'}</TableCell>
                  <TableCell>{e.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="muted">معطل</Badge>}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <EmptyState icon={Users} title={q || status ? 'لا توجد نتائج' : 'لا يوجد موظفون بعد'} description="أضف موظفاً ليتمكن من تسجيل الطلبات" action={<Button onClick={() => setCreateOpen(true)}><UserPlus /> إضافة موظف</Button>} />
        )}
      </Card>
      <EmployeeFormDialog open={createOpen} onOpenChange={setCreateOpen} onSaved={(e) => navigate(`/employees/${e.id}`)} />
    </div>
  )
}
