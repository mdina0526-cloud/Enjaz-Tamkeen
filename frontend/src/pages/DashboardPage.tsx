import { useState, type ReactNode } from 'react'
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
import { Delta, EmptyState, ErrorState, KpiCard, Money } from '@/components/common'
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
    <Card className="h-full overflow-hidden rounded-2xl shadow-sm">
      <CardHeader className="border-b bg-muted/20 p-5">
        <CardTitle>{title}</CardTitle>
        {description && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent className="p-5">{loading ? <Skeleton className="h-[260px] w-full" /> : children}</CardContent>
    </Card>
  )
}

function HeroMetric({ label, value, icon: Icon, loading }: { label: string; value: ReactNode; icon: typeof CircleDollarSign; loading: boolean }) {
  return (
    <div className="rounded-2xl border border-white/15 bg-white/10 p-4 backdrop-blur-sm">
      <div className="flex items-center justify-between gap-3 text-blue-100">
        <span className="text-xs font-medium sm:text-sm">{label}</span>
        <Icon className="size-4 shrink-0" aria-hidden />
      </div>
      {loading ? <div className="mt-4 h-8 w-28 animate-pulse rounded-lg bg-white/20" /> : <p className="num mt-3 text-xl font-bold tracking-tight text-white sm:text-2xl">{value}</p>}
    </div>
  )
}

function AdminDashboardView() {
  const { user } = useAuth()
  const [range, setRange] = useState({ from: startOfMonthIso(), to: todayIso() })
  const { data, loading, error, reload } = useApiQuery(['dashboard', 'admin', range.from, range.to], () => api.get<AdminDashboard>('/api/dashboard/admin', range), {
    keepPrevious: true,
  })
  const prev = data?.previous

  if (error) return <ErrorState message={error} onRetry={reload} />

  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-[28px] bg-gradient-to-br from-[#10224b] via-[#153775] to-[#2459bd] text-white shadow-[0_20px_55px_-28px_rgba(16,45,102,0.75)]">
        <div className="pointer-events-none absolute -left-16 -top-20 size-72 rounded-full border border-white/10 bg-white/5" aria-hidden />
        <div className="pointer-events-none absolute -bottom-32 left-1/3 size-80 rounded-full border border-white/10" aria-hidden />
        <div className="relative grid gap-8 p-6 sm:p-8 xl:grid-cols-[minmax(0,1fr)_minmax(420px,0.9fr)] xl:items-end">
          <div className="max-w-xl">
            <span className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-3 py-1 text-xs font-semibold text-blue-50">
              <span className="size-2 rounded-full bg-emerald-300" /> لوحة الإدارة
            </span>
            <h2 className="mt-5 text-2xl font-bold tracking-tight sm:text-3xl">مرحباً، {user?.fullName ?? 'المدير'}</h2>
            <p className="mt-2 max-w-lg text-sm leading-7 text-blue-100/90">تابع أداء المبيعات والتحصيل والعمليات المالية من مكان واحد. كل الأرقام هنا من سجلات النظام الفعلية.</p>
            <p className="mt-4 flex items-center gap-2 text-xs text-blue-100/80">
              <CalendarClock className="size-4" /> الفترة: <span className="num">{formatDate(range.from)}</span> – <span className="num">{formatDate(range.to)}</span>
            </p>
            <div className="no-print mt-6 flex flex-wrap gap-2.5">
              <Button asChild className="bg-white text-[#153775] hover:bg-blue-50">
                <Link to="/orders/new"><PlusCircle /> طلب جديد</Link>
              </Button>
              <Button asChild variant="outline" className="border-white/30 bg-white/5 text-white hover:bg-white/15 hover:text-white">
                <Link to="/reports">عرض التقارير <ArrowLeft /></Link>
              </Button>
            </div>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <HeroMetric loading={loading} label="إجمالي المبيعات" value={formatMoney(data?.totalSales)} icon={CircleDollarSign} />
            <HeroMetric loading={loading} label="إجمالي العمليات" value={formatNumber(data?.ordersCount)} icon={ClipboardList} />
            <HeroMetric loading={loading} label="الموظفون النشطون" value={formatNumber(data?.activeEmployees)} icon={Users} />
            <HeroMetric loading={loading} label="طلبات قيد المراجعة" value={formatNumber(data?.pendingOrders)} icon={Clock} />
          </div>
        </div>
      </section>

      <div className="no-print flex flex-col gap-3 rounded-2xl border bg-card px-4 py-3 shadow-sm lg:flex-row lg:items-center lg:justify-between">
        <div>
          <p className="text-sm font-semibold">الفترة المعروضة</p>
          <p className="text-xs text-muted-foreground">غيّر الفترة لتحديث المؤشرات والرسوم</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <PeriodTabs from={range.from} to={range.to} onChange={setRange} />
          <DateRangeFilter from={range.from} to={range.to} onChange={setRange} />
        </div>
      </div>

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
        <KpiCard loading={loading} label="المبالغ المحصّلة (المعتمدة)" value={formatMoney(data?.totalCollected)} icon={Wallet} tone="success"
          hint={data && <Delta value={percentChange(data.totalCollected, prev?.totalCollected)} />} />
        <KpiCard loading={loading} label="إجمالي الاستقطاعات" value={formatMoney(data?.totalDeductions)} icon={Percent} tone="warning"
          hint={<>الصافي: <span className="num">{formatMoney(data?.totalNet)}</span></>} />
        <KpiCard loading={loading} label="الإيجارات المدفوعة (بالفترة)" value={formatMoney(data?.totalRentalPayments)} icon={HandCoins} tone="navy" />
        <KpiCard loading={loading} label="الإيجارات المستحقة (المتبقي)" value={formatMoney(data?.rentalsOutstanding)} icon={Building2} tone="destructive"
          hint={<>المستحق بالفترة: <span className="num">{formatMoney(data?.totalRentals)}</span></>} />
      </div>

      {prev && data && (
        <Card className="overflow-hidden rounded-2xl shadow-sm">
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
              <div key={m.l} className="rounded-xl border bg-muted/30 p-4 transition-colors hover:bg-muted/50">
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

      <div className="flex flex-wrap items-end justify-between gap-2 pt-2">
        <div>
          <h3 className="text-xl font-bold tracking-tight">تحليل الأداء</h3>
          <p className="mt-1 text-sm text-muted-foreground">اتجاهات المبيعات وتوزيع الإيرادات خلال الفترة المختارة</p>
        </div>
        <Link to="/sales" className="no-print inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">تفاصيل المبيعات <ArrowLeft className="size-4" /></Link>
      </div>

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

      <Card className="overflow-hidden rounded-2xl shadow-sm">
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

      <Card className="overflow-hidden rounded-2xl shadow-sm">
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
      <section className="relative overflow-hidden rounded-[28px] bg-gradient-to-l from-[#10224b] to-[#255cc2] p-6 text-white shadow-[0_20px_55px_-28px_rgba(16,45,102,0.75)] sm:p-8">
        <div className="pointer-events-none absolute -bottom-36 -left-10 size-80 rounded-full border border-white/15" aria-hidden />
        <div className="relative flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <span className="inline-flex rounded-full border border-white/20 bg-white/10 px-3 py-1 text-xs font-semibold text-blue-50">لوحة الموظف</span>
            <h2 className="mt-5 text-2xl font-bold tracking-tight sm:text-3xl">أهلاً، {user?.fullName ?? ''}</h2>
            <p className="mt-2 max-w-lg text-sm leading-7 text-blue-100/90">تابع طلباتك ومبيعاتك، وأنشئ عملية جديدة من مكان واحد.</p>
          </div>
          <Button asChild size="lg" className="no-print shrink-0 bg-white text-[#153775] hover:bg-blue-50">
            <Link to="/orders/new"><PlusCircle /> إنشاء طلب جديد</Link>
          </Button>
        </div>
      </section>

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
            <Card className="flex items-center justify-between rounded-2xl p-4 shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-md">
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
