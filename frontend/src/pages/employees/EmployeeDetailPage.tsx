import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ArrowRight, Ban, Banknote, CheckCircle2, CircleDollarSign, ClipboardList, CreditCard, KeyRound, Pencil, Percent, XCircle } from 'lucide-react'
import { api } from '@/lib/api'
import { toastError, useApiQuery } from '@/hooks/useApi'
import { formatDate, formatDateTime, formatMoney, formatNumber } from '@/lib/utils'
import type { DeductionRule, EmployeeDetail, EmployeeStats, OrderListItem, PagedResult, Role } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Avatar, Skeleton, Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ConfirmDialog, EmptyState, ErrorState, InfoRow, KpiCard, PageHeader, Pagination, TableSkeleton } from '@/components/common'
import { DateRangeFilter } from '@/components/filters'
import { OrdersTable } from '@/components/OrdersTable'
import { DeductionDialog, EmployeeFormDialog, ResetPasswordDialog } from './EmployeeDialogs'
import { LoginLinkCard } from './LoginLinkCard'

export function EmployeeDetailPage() {
  const { id } = useParams()
  const employee = useApiQuery(['employees', 'detail', id], () => api.get<EmployeeDetail>(`/api/employees/${id}`))
  const [range, setRange] = useState({ from: '', to: '' })
  const [page, setPage] = useState(1)
  const stats = useApiQuery(['employees', 'stats', id, range.from, range.to], () => api.get<EmployeeStats>(`/api/employees/${id}/stats`, range), { keepPrevious: true })
  const orders = useApiQuery(
    ['employees', 'orders', id, range.from, range.to, page],
    () => api.get<PagedResult<OrderListItem>>(`/api/employees/${id}/orders`, { ...range, page, pageSize: 15 }),
    { keepPrevious: true },
  )
  const history = useApiQuery(['employees', 'deduction-history', id], () => api.get<DeductionRule[]>(`/api/employees/${id}/deduction-history`))
  const [dialog, setDialog] = useState<'edit' | 'reset' | 'deduction' | 'status' | null>(null)

  if (employee.error) return <ErrorState message={employee.error} onRetry={employee.reload} />
  const e = employee.data
  if (!e) return <Skeleton className="h-96" />

  const toggleStatus = async () => {
    try {
      await api.post(`/api/employees/${e.id}/status`, { isActive: !e.isActive })
      toast.success(e.isActive ? 'تم تعطيل الحساب وإنهاء جلساته' : 'تم تفعيل الحساب')
      employee.setData({ ...e, isActive: !e.isActive })
    } catch (err) {
      toastError(err)
      throw err
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={e.fullName}
        description={`${e.employeeCode} · ${e.jobTitle ?? 'موظف'}`}
        actions={
          <>
            <Button variant="outline" asChild><Link to="/employees"><ArrowRight /> الموظفون</Link></Button>
            <Button variant="outline" onClick={() => setDialog('edit')}><Pencil /> تعديل</Button>
            <Button variant="outline" onClick={() => setDialog('reset')}><KeyRound /> إعادة تعيين كلمة المرور</Button>
            {e.isActive ? (
              <Button variant="destructive" onClick={() => setDialog('status')}><Ban /> تعطيل الحساب</Button>
            ) : (
              <Button variant="success" onClick={() => setDialog('status')}><CheckCircle2 /> تفعيل الحساب</Button>
            )}
          </>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6">
        <Card>
          <CardContent className="pt-6">
            <div className="mb-4 flex items-center gap-3">
              <Avatar name={e.fullName} className="size-14 text-lg" />
              <div>
                <p className="font-semibold">{e.fullName}</p>
                <p className="num text-end text-sm text-muted-foreground">@{e.username}</p>
              </div>
              <div className="ms-auto">{e.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="muted">معطل</Badge>}</div>
            </div>
            <InfoRow label="الرقم الوظيفي"><span className="num">{e.employeeCode}</span></InfoRow>
            <InfoRow label="الصلاحية">
              <select className="rounded-md border bg-background px-2 py-1 text-sm" value={e.role} onChange={async (event) => {
                try {
                  const saved = await api.put<EmployeeDetail>(`/api/employees/${e.id}/role`, { role: event.target.value as Role })
                  employee.setData(saved)
                  toast.success('تم تحديث الصلاحية وإنهاء الجلسات القديمة')
                } catch (err) { toastError(err) }
              }}>
                <option value="Employee">موظف</option>
                <option value="OperationsManager">مدير عمليات</option>
                <option value="Accountant">محاسب</option>
              </select>
            </InfoRow>
            <InfoRow label="البريد الإلكتروني"><span className="num">{e.email ?? '—'}</span></InfoRow>
            <InfoRow label="الجوال"><span className="num">{e.phone ?? '—'}</span></InfoRow>
            <InfoRow label="تاريخ التعيين">{formatDate(e.hireDate)}</InfoRow>
            <InfoRow label="آخر دخول">{e.lastLoginAtUtc ? formatDateTime(e.lastLoginAtUtc) : '—'}</InfoRow>
            <InfoRow label="نسبة الاستقطاع">
              <span className="flex items-center gap-2">
                <span className="num font-bold text-primary">{e.deductionPercentage}%</span>
                <Button size="sm" variant="ghost" onClick={() => setDialog('deduction')}><Pencil /> تعديل</Button>
              </span>
            </InfoRow>
          </CardContent>
        </Card>
        <LoginLinkCard employee={e} onChange={employee.setData} />
        </div>

        <div className="space-y-4 lg:col-span-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-semibold">إحصائيات الموظف</h3>
            <DateRangeFilter from={range.from} to={range.to} onChange={(r) => { setRange(r); setPage(1) }} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            <KpiCard loading={stats.loading} label="عدد العمليات" value={formatNumber(stats.data?.ordersCount)} icon={ClipboardList} tone="navy" />
            <KpiCard loading={stats.loading} label="إجمالي المبيعات" value={formatMoney(stats.data?.totalSales)} icon={CircleDollarSign} />
            <KpiCard loading={stats.loading} label="إجمالي الاستقطاعات" value={formatMoney(stats.data?.totalDeductions)} icon={Percent} tone="warning" />
            <KpiCard loading={stats.loading} label="نقداً" value={formatMoney(stats.data?.cashTotal)} icon={Banknote} tone="success" />
            <KpiCard loading={stats.loading} label="شبكة" value={formatMoney(stats.data?.cardTotal)} icon={CreditCard} />
            <KpiCard loading={stats.loading} label="الطلبات الملغاة" value={formatNumber(stats.data?.cancelledCount)} icon={XCircle} tone="destructive" />
          </div>
        </div>
      </div>

      <Tabs defaultValue="orders">
        <TabsList>
          <TabsTrigger value="orders">العمليات</TabsTrigger>
          <TabsTrigger value="deductions">سجل نسب الاستقطاع</TabsTrigger>
        </TabsList>
        <TabsContent value="orders">
          <Card>
            {orders.loading && !orders.data ? (
              <TableSkeleton />
            ) : orders.data?.items.length ? (
              <>
                <OrdersTable orders={orders.data.items} showEmployee={false} />
                <Pagination page={orders.data.page} pageSize={orders.data.pageSize} total={orders.data.total} onChange={setPage} />
              </>
            ) : (
              <EmptyState title="لا توجد عمليات في هذه الفترة" />
            )}
          </Card>
        </TabsContent>
        <TabsContent value="deductions">
          <Card>
            <CardHeader><CardTitle>سجل التعديلات</CardTitle></CardHeader>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="text-end">النسبة</TableHead>
                  <TableHead>من تاريخ</TableHead>
                  <TableHead>إلى تاريخ</TableHead>
                  <TableHead>بواسطة</TableHead>
                  <TableHead>ملاحظة</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data?.map((h) => (
                  <TableRow key={h.id}>
                    <TableCell className="num text-end font-semibold">{h.percentage}%</TableCell>
                    <TableCell>{formatDateTime(h.effectiveFromUtc)}</TableCell>
                    <TableCell>{h.effectiveToUtc ? formatDateTime(h.effectiveToUtc) : <Badge variant="success">سارية</Badge>}</TableCell>
                    <TableCell>{h.createdBy ?? '—'}</TableCell>
                    <TableCell className="whitespace-normal text-muted-foreground">{h.notes ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        </TabsContent>
      </Tabs>

      <EmployeeFormDialog open={dialog === 'edit'} onOpenChange={(o) => setDialog(o ? 'edit' : null)} employee={e} onSaved={(saved) => employee.setData(saved)} />
      <ResetPasswordDialog open={dialog === 'reset'} onOpenChange={(o) => setDialog(o ? 'reset' : null)} employee={e} />
      <DeductionDialog open={dialog === 'deduction'} onOpenChange={(o) => setDialog(o ? 'deduction' : null)} employee={e} onSaved={() => { employee.reload(); history.reload() }} />
      <ConfirmDialog
        open={dialog === 'status'}
        onOpenChange={(o) => setDialog(o ? 'status' : null)}
        title={e.isActive ? 'تعطيل حساب الموظف' : 'تفعيل حساب الموظف'}
        description={e.isActive ? 'لن يتمكن الموظف من الدخول وسيتم إنهاء جلساته الحالية. تبقى عملياته محفوظة.' : 'سيتمكن الموظف من تسجيل الدخول مجدداً.'}
        confirmLabel={e.isActive ? 'تعطيل' : 'تفعيل'}
        destructive={e.isActive}
        onConfirm={toggleStatus}
      />
    </div>
  )
}
