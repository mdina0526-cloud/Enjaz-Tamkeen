import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft, Banknote, CircleDollarSign, CreditCard, Receipt, Wallet } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery } from '@/hooks/useApi'
import { formatMoney, formatNumber, percentChange, startOfMonthIso, todayIso } from '@/lib/utils'
import type { AdminDashboard } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Delta, EmptyState, ErrorState, KpiCard, Money, PageHeader } from '@/components/common'
import { DateRangeFilter, PeriodTabs } from '@/components/filters'
import { DailySalesChart, EmployeeSalesChart, PaymentMethodBreakdown } from '@/components/charts'

/** Sales analysis for the selected period: payment methods, services and daily movement. */
export function SalesPage() {
  const [range, setRange] = useState({ from: startOfMonthIso(), to: todayIso() })
  // Same endpoint and key as the admin dashboard, so switching between the two pages reuses the cache.
  const { data, loading, error, reload } = useApiQuery(['dashboard', 'admin', range.from, range.to], () => api.get<AdminDashboard>('/api/dashboard/admin', range), {
    keepPrevious: true,
  })

  if (error) return <ErrorState message={error} onRetry={reload} />

  const cash = data?.byPaymentMethod.find((m) => m.key === 'Cash')
  const card = data?.byPaymentMethod.find((m) => m.key === 'Card')
  const avg = data && data.ordersCount > 0 ? data.totalSales / data.ordersCount : 0
  const ordersLink = `/orders?from=${range.from}&to=${range.to}`

  return (
    <div className="space-y-6">
      <PageHeader
        title="المبيعات"
        description="تحليل المبيعات حسب طريقة الدفع والخدمة خلال الفترة المحددة"
        actions={
          <>
            <PeriodTabs from={range.from} to={range.to} onChange={setRange} />
            <DateRangeFilter from={range.from} to={range.to} onChange={setRange} />
          </>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard loading={loading} label="إجمالي المبيعات" value={formatMoney(data?.totalSales)} icon={CircleDollarSign}
          hint={data && <Delta value={percentChange(data.totalSales, data.previous?.totalSales)} />} />
        <KpiCard loading={loading} label="المبيعات النقدية" value={formatMoney(cash?.amount)} icon={Banknote} tone="success"
          hint={<><span className="num">{formatNumber(cash?.count)}</span> عملية</>} />
        <KpiCard loading={loading} label="مبيعات الشبكة (POS)" value={formatMoney(card?.amount)} icon={CreditCard} tone="navy"
          hint={<><span className="num">{formatNumber(card?.count)}</span> عملية</>} />
        <KpiCard loading={loading} label="متوسط قيمة العملية" value={formatMoney(avg)} icon={Wallet} tone="warning"
          hint={<>الصافي: <span className="num">{formatMoney(data?.totalNet)}</span></>} />
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Card className="xl:col-span-2">
          <CardHeader>
            <CardTitle>حركة المبيعات اليومية</CardTitle>
            <CardDescription>آخر 30 يوماً حتى نهاية الفترة</CardDescription>
          </CardHeader>
          <CardContent>{loading ? <Skeleton className="h-[280px]" /> : data && <DailySalesChart data={data.dailySales} />}</CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>طرق الدفع</CardTitle>
            <CardDescription>نقداً مقابل الشبكة</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <Skeleton className="h-40" /> : data?.byPaymentMethod.some((d) => d.amount > 0) ? <PaymentMethodBreakdown data={data.byPaymentMethod} /> : <EmptyState title="لا توجد مبيعات" />}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>أعلى الخدمات مبيعاً</CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? <Skeleton className="h-64" /> : data?.byService.length ? <EmployeeSalesChart data={data.byService} /> : <EmptyState title="لا توجد مبيعات في هذه الفترة" />}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex-row items-center justify-between">
            <div>
              <CardTitle>المبيعات حسب الخدمة</CardTitle>
              <CardDescription>العمليات الملغاة غير محتسبة</CardDescription>
            </div>
            <Button variant="outline" size="sm" asChild>
              <Link to={ordersLink}>العمليات <ArrowLeft /></Link>
            </Button>
          </CardHeader>
          {loading ? (
            <div className="p-5"><Skeleton className="h-48" /></div>
          ) : data?.byService.length ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>الخدمة</TableHead>
                  <TableHead className="text-end">العمليات</TableHead>
                  <TableHead className="text-end">المبيعات</TableHead>
                  <TableHead className="text-end">الحصة</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.byService.map((s) => (
                  <TableRow key={s.key}>
                    <TableCell className="font-medium">{s.key}</TableCell>
                    <TableCell className="num text-end">{formatNumber(s.count)}</TableCell>
                    <TableCell className="text-end font-semibold"><Money value={s.amount} /></TableCell>
                    <TableCell className="num text-end text-muted-foreground">{data.totalSales > 0 ? ((s.amount / data.totalSales) * 100).toFixed(1) : '0.0'}%</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <EmptyState icon={Receipt} title="لا توجد مبيعات في هذه الفترة" />
          )}
        </Card>
      </div>
    </div>
  )
}
