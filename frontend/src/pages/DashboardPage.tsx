import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertTriangle, ArrowLeft, Banknote, Building2, CalendarClock, CircleDollarSign, ClipboardList, Clock, HandCoins, Percent, PlusCircle, Receipt, Users, Wallet } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery } from '@/hooks/useApi'
import { useAuth } from '@/context/AuthContext'
import { formatDate, formatMoney, formatNumber, percentChange, startOfMonthIso, todayIso } from '@/lib/utils'
import type { AdminDashboard, EmployeeDashboard } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/misc'
import { Delta, EmptyState, ErrorState, KpiCard, Money, PageHeader } from '@/components/common'
import { DateRangeFilter, PeriodTabs } from '@/components/filters'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { OrdersTable } from '@/components/OrdersTable'
import { DailySalesChart, EmployeeSalesChart, MonthlySalesChart, PaymentMethodBreakdown } from '@/components/charts'

export function DashboardPage() {
  const { isAdmin } = useAuth()
  return isAdmin ? <AdminDashboardView /> : <EmployeeDashboardView />
}

function ChartCard({ title, description, loading, children }: { title: string; description?: string; loading: boolean; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>{loading ? <Skeleton className="h-[260px] w-full" /> : children}</CardContent>
    </Card>
  )
}

function AdminDashboardView() {
  const [range, setRange] = useState({ from: startOfMonthIso(), to: todayIso() })
  const { data, loading, error, reload } = useApiQuery(['dashboard', 'admin', range.from, range.to], () => api.get<AdminDashboard>('/api/dashboard/admin', range), {
    keepPrevious: true,
  })
  const prev = data?.previous

  if (error) return <ErrorState message={error} onRetry={reload} />

  return (
    <div className="space-y-6">
      <PageHeader
        title="لوحة تحكم المدير"
        description="نظرة شاملة على المبيعات والتحصيل والإيجارات خلال الفترة المحددة"
        actions={
          <>
            <PeriodTabs from={range.from} to={range.to} onChange={setRange} />
            <DateRangeFilter from={range.from} to={range.to} onChange={setRange} />
            <Button asChild>
              <Link to="/orders/new"><PlusCircle /> طلب جديد</Link>
            </Button>
          </>
        }
      />

      {data && (data.pendingOrders > 0 || data.overdueRentals > 0) && (
        <div className="flex flex-wrap gap-3">
          {data.pendingOrders > 0 && (
            <Link to="/orders?status=Pending" className="flex items-center gap-2 rounded-lg border border-warning/30 bg-warning/8 px-3 py-2 text-sm text-warning hover:bg-warning/12">
              <Clock className="size-4" /> <span className="num">{data.pendingOrders}</span> طلبات بانتظار الاعتماد <ArrowLeft className="size-4" />
            </Link>
          )}
          {data.overdueRentals > 0 && (
            <Link to="/rentals?status=Overdue" className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/8 px-3 py-2 text-sm text-destructive hover:bg-destructive/12">
              <AlertTriangle className="size-4" /> <span className="num">{data.overdueRentals}</span> إيجارات متأخرة السداد <ArrowLeft className="size-4" />
            </Link>
          )}
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard loading={loading} label="إجمالي المبيعات" value={formatMoney(data?.totalSales)} icon={CircleDollarSign}
          hint={data && <Delta value={percentChange(data.totalSales, prev?.totalSales)} />} />
        <KpiCard loading={loading} label="المبالغ المحصّلة (المعتمدة)" value={formatMoney(data?.totalCollected)} icon={Wallet} tone="success"
          hint={data && <Delta value={percentChange(data.totalCollected, prev?.totalCollected)} />} />
        <KpiCard loading={loading} label="إجمالي العمليات" value={formatNumber(data?.ordersCount)} icon={ClipboardList} tone="navy"
          hint={data && <Delta value={percentChange(data.ordersCount, prev?.ordersCount)} />} />
        <KpiCard loading={loading} label="إجمالي الاستقطاعات" value={formatMoney(data?.totalDeductions)} icon={Percent} tone="warning"
          hint={<>الصافي: <span className="num">{formatMoney(data?.totalNet)}</span></>} />
        <KpiCard loading={loading} label="الإيجارات المدفوعة (بالفترة)" value={formatMoney(data?.totalRentalPayments)} icon={HandCoins} tone="navy" />
        <KpiCard loading={loading} label="الإيجارات المستحقة (المتبقي)" value={formatMoney(data?.rentalsOutstanding)} icon={Building2} tone="destructive"
          hint={<>المستحق بالفترة: <span className="num">{formatMoney(data?.totalRentals)}</span></>} />
        <KpiCard loading={loading} label="عدد الموظفين النشطين" value={formatNumber(data?.activeEmployees)} icon={Users} tone="success" />
        <KpiCard loading={loading} label="طلبات قيد المراجعة" value={formatNumber(data?.pendingOrders)} icon={CalendarClock} tone="warning" />
      </div>

      {prev && data && (
        <Card>
          <CardHeader>
            <CardTitle>مقارنة الأداء</CardTitle>
            <CardDescription>
              الفترة الحالية مقابل الفترة السابقة المماثلة (<span className="num">{formatDate(prev.from)}</span> – <span className="num">{formatDate(prev.to)}</span>)
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { l: 'المبيعات', cur: data.totalSales, p: prev.totalSales, money: true },
              { l: 'المحصّل', cur: data.totalCollected, p: prev.totalCollected, money: true },
              { l: 'العمليات', cur: data.ordersCount, p: prev.ordersCount, money: false },
              { l: 'الصافي', cur: data.totalNet, p: prev.totalNet, money: true },
            ].map((m) => (
              <div key={m.l} className="rounded-xl border bg-muted/30 p-4">
                <p className="text-sm text-muted-foreground">{m.l}</p>
                <div className="mt-2 flex items-baseline justify-between gap-2">
                  <span className="num text-lg font-bold">{m.money ? formatMoney(m.cur) : formatNumber(m.cur)}</span>
                  <span className="num text-xs text-muted-foreground">السابقة: {m.money ? formatMoney(m.p) : formatNumber(m.p)}</span>
                </div>
                <div className="mt-1 text-xs"><Delta value={percentChange(m.cur, m.p)} /></div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 xl:grid-cols-3">
        <div className="xl:col-span-2">
          <ChartCard title="المبيعات اليومية" description="آخر 30 يوماً حتى نهاية الفترة" loading={loading}>
            {data && <DailySalesChart data={data.dailySales} />}
          </ChartCard>
        </div>
        <ChartCard title="حسب طريقة الدفع" description="خلال الفترة المحددة" loading={loading}>
          {data && (data.byPaymentMethod.some((d) => d.amount > 0) ? <PaymentMethodBreakdown data={data.byPaymentMethod} /> : <EmptyState title="لا توجد مبيعات" />)}
        </ChartCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <ChartCard title="المبيعات الشهرية" description="آخر 12 شهراً" loading={loading}>
          {data && <MonthlySalesChart data={data.monthlySales} />}
        </ChartCard>
        <ChartCard title="المبيعات حسب الموظف" description="خلال الفترة المحددة" loading={loading}>
          {data && (data.byEmployee.length ? <EmployeeSalesChart data={data.byEmployee} /> : <EmptyState title="لا توجد مبيعات في هذه الفترة" />)}
        </ChartCard>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <div>
            <CardTitle>تقرير الأداء حسب الموظف</CardTitle>
            <CardDescription>المبيعات والاستقطاعات والصافي لكل موظف خلال الفترة</CardDescription>
          </div>
          <Button variant="outline" size="sm" asChild>
            <Link to="/reports">التقارير المفصلة <ArrowLeft /></Link>
          </Button>
        </CardHeader>
        {loading ? (
          <div className="p-5"><Skeleton className="h-40" /></div>
        ) : data?.employeePerformance.length ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>الموظف</TableHead>
                <TableHead className="text-end">العمليات</TableHead>
                <TableHead className="text-end">المبيعات</TableHead>
                <TableHead className="text-end">الاستقطاعات</TableHead>
                <TableHead className="text-end">الصافي</TableHead>
                <TableHead className="text-end">الحصة</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.employeePerformance.map((p) => (
                <TableRow key={p.employeeId}>
                  <TableCell>
                    <Link to={`/employees/${p.employeeId}`} className="font-medium hover:text-primary">{p.employeeName}</Link>
                    <span className="num ms-2 text-xs text-muted-foreground">{p.employeeCode}</span>
                  </TableCell>
                  <TableCell className="num text-end">{formatNumber(p.ordersCount)}</TableCell>
                  <TableCell className="text-end font-semibold"><Money value={p.totalSales} /></TableCell>
                  <TableCell className="text-end text-warning"><Money value={p.totalDeductions} /></TableCell>
                  <TableCell className="text-end"><Money value={p.totalNet} /></TableCell>
                  <TableCell className="num text-end text-muted-foreground">{data.totalSales > 0 ? ((p.totalSales / data.totalSales) * 100).toFixed(1) : '0.0'}%</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <EmptyState title="لا توجد مبيعات في هذه الفترة" />
        )}
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <div>
            <CardTitle>العمليات الأخيرة</CardTitle>
            <CardDescription>أحدث الطلبات المسجلة في النظام</CardDescription>
          </div>
          <Button variant="outline" size="sm" asChild>
            <Link to="/orders">عرض الكل <ArrowLeft /></Link>
          </Button>
        </CardHeader>
        {loading ? <div className="p-5"><Skeleton className="h-48" /></div> : data?.recentOrders.length ? <OrdersTable orders={data.recentOrders} compact /> : <EmptyState title="لا توجد عمليات بعد" icon={Receipt} />}
      </Card>
    </div>
  )
}

function EmployeeDashboardView() {
  const { user } = useAuth()
  const { data, loading, error, reload } = useApiQuery(['dashboard', 'employee'], () => api.get<EmployeeDashboard>('/api/dashboard/employee'))

  if (error) return <ErrorState message={error} onRetry={reload} />

  return (
    <div className="space-y-6">
      <PageHeader
        title={`أهلاً، ${user?.fullName ?? ''}`}
        description="ملخص عملياتك ومبيعاتك"
        actions={
          <Button asChild size="lg">
            <Link to="/orders/new"><PlusCircle /> إنشاء طلب جديد</Link>
          </Button>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard loading={loading} label="عملياتي اليوم" value={formatNumber(data?.todayOrders)} icon={ClipboardList} hint={<>مبيعات اليوم: <span className="num">{formatMoney(data?.todaySales)}</span></>} />
        <KpiCard loading={loading} label="إجمالي مبيعاتي" value={formatMoney(data?.totalSales)} icon={CircleDollarSign} tone="navy" hint={<><span className="num">{formatNumber(data?.ordersCount)}</span> عملية</>} />
        <KpiCard loading={loading} label="إجمالي المبالغ المحصّلة" value={formatMoney(data?.totalCollected)} icon={Banknote} tone="success"
          hint={<>صافي عملياتي: <span className="num">{formatMoney(data?.totalNet)}</span></>} />
        <KpiCard
          loading={loading}
          label={`إجمالي ${data?.deductionLabel ?? 'الاستقطاعات'}`}
          value={formatMoney(data?.totalDeductions)}
          icon={Percent}
          tone="warning"
          hint={<>النسبة الحالية: <span className="num">{data?.deductionPercentage ?? 0}%</span></>}
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        {[
          { label: 'قيد المراجعة', value: data?.pendingOrders, cls: 'text-warning bg-warning/10', status: 'Pending' },
          { label: 'مكتملة', value: data?.completedOrders, cls: 'text-success bg-success/10', status: 'Completed' },
          { label: 'ملغاة', value: data?.cancelledOrders, cls: 'text-destructive bg-destructive/10', status: 'Cancelled' },
        ].map((s) => (
          <Link key={s.label} to={`/orders?status=${s.status}`}>
            <Card className="flex items-center justify-between p-4 transition-shadow hover:shadow-md">
              <span className="text-sm text-muted-foreground">طلبات {s.label}</span>
              {loading ? <Skeleton className="h-7 w-10" /> : <span className={`num rounded-lg px-3 py-1 text-lg font-bold ${s.cls}`}>{s.value ?? 0}</span>}
            </Card>
          </Link>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <div className="xl:col-span-2">
          <ChartCard title="مبيعاتي اليومية" description="آخر 30 يوماً" loading={loading}>
            {data && <DailySalesChart data={data.dailySales} />}
          </ChartCard>
        </div>
        <ChartCard title="حسب طريقة الدفع" loading={loading}>
          {data && (data.byPaymentMethod.some((d) => d.amount > 0) ? <PaymentMethodBreakdown data={data.byPaymentMethod} /> : <EmptyState title="لا توجد مبيعات بعد" />)}
        </ChartCard>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <div>
            <CardTitle>آخر طلباتي</CardTitle>
            <CardDescription>حالة كل طلب ومبالغه</CardDescription>
          </div>
          <Button variant="outline" size="sm" asChild>
            <Link to="/orders">سجل العمليات <ArrowLeft /></Link>
          </Button>
        </CardHeader>
        {loading ? (
          <div className="p-5"><Skeleton className="h-48" /></div>
        ) : data?.recentOrders.length ? (
          <OrdersTable orders={data.recentOrders} showEmployee={false} />
        ) : (
          <EmptyState title="لم تسجل أي طلب بعد" description="ابدأ بإنشاء أول طلب مبيعات" icon={Receipt} action={<Button asChild><Link to="/orders/new"><PlusCircle /> طلب جديد</Link></Button>} />
        )}
      </Card>
    </div>
  )
}
