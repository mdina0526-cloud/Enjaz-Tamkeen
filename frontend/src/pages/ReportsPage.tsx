import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Activity, BarChart3, Banknote, Building2, ClipboardList, CreditCard, FileSpreadsheet, FileText, Layers, Percent, Printer, Users } from 'lucide-react'
import { api } from '@/lib/api'
import { toastError, useApiQuery } from '@/hooks/useApi'
import { cn, formatDateTime, startOfMonthIso, todayIso } from '@/lib/utils'
import type { EmployeeLookup, ReportResult, ReportType } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { NativeSelect } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState, ErrorState, PageHeader } from '@/components/common'
import { DateRangeFilter } from '@/components/filters'

const reports: { type: ReportType; title: string; icon: typeof BarChart3; employeeFilter: boolean }[] = [
  { type: 'Sales', title: 'تقرير المبيعات', icon: BarChart3, employeeFilter: true },
  { type: 'Employees', title: 'عمليات الموظفين', icon: Users, employeeFilter: true },
  { type: 'Cash', title: 'المدفوعات النقدية', icon: Banknote, employeeFilter: true },
  { type: 'Card', title: 'مدفوعات الشبكة', icon: CreditCard, employeeFilter: true },
  { type: 'Deductions', title: 'الاستقطاعات', icon: Percent, employeeFilter: true },
  { type: 'Rentals', title: 'الإيجارات', icon: Building2, employeeFilter: false },
  { type: 'Operations', title: 'إجمالي العمليات', icon: ClipboardList, employeeFilter: true },
  { type: 'Performance', title: 'الأداء خلال فترة', icon: Activity, employeeFilter: true },
  { type: 'Comprehensive', title: 'التقرير الشامل', icon: Layers, employeeFilter: true },
]

const numFmt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function ReportsPage() {
  const [params] = useSearchParams()
  const [type, setType] = useState<ReportType>(() => (reports.some((r) => r.type === params.get('type')) ? (params.get('type') as ReportType) : 'Sales'))
  const [groupBy, setGroupBy] = useState<'day' | 'week' | 'month' | 'year'>('month')
  const [range, setRange] = useState({ from: startOfMonthIso(), to: todayIso() })
  const [employeeId, setEmployeeId] = useState('')
  const [exporting, setExporting] = useState<string | null>(null)
  const meta = reports.find((r) => r.type === type)!
  const query = { ...range, employeeId: meta.employeeFilter ? employeeId : '', groupBy: type === 'Performance' ? groupBy : '' }

  const employees = useApiQuery(['employees', 'lookup'], () => api.get<EmployeeLookup[]>('/api/employees/lookup'))
  const { data, loading, fetching, error, reload } = useApiQuery(
    ['reports', type, range.from, range.to, query.employeeId, query.groupBy],
    () => api.get<ReportResult>(`/api/reports/${type}`, query),
    { keepPrevious: true },
  )

  const exportAs = async (format: 'xlsx' | 'pdf') => {
    setExporting(format)
    try {
      await api.download(`/api/reports/${type}`, { ...query, format }, `${type}.${format}`)
    } catch (e) {
      toastError(e)
    } finally {
      setExporting(null)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="التقارير المالية"
        description="استعرض التقارير حسب الفترة وصدّرها إلى Excel أو PDF أو اطبعها"
        actions={
          <>
            <Button variant="outline" onClick={() => exportAs('xlsx')} loading={exporting === 'xlsx'} disabled={!data}><FileSpreadsheet /> Excel</Button>
            <Button variant="outline" onClick={() => exportAs('pdf')} loading={exporting === 'pdf'} disabled={!data}><FileText /> PDF</Button>
            <Button variant="outline" onClick={() => window.print()} disabled={!data}><Printer /> طباعة</Button>
          </>
        }
      />

      <div className="no-print grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5 2xl:grid-cols-9">
        {reports.map((r) => (
          <button
            key={r.type}
            onClick={() => setType(r.type)}
            className={cn(
              'flex flex-col items-center gap-2 rounded-xl border bg-card p-4 text-center text-sm font-medium transition-all hover:border-primary/40 hover:shadow-sm',
              type === r.type && 'border-primary bg-primary/5 text-primary ring-1 ring-primary',
            )}
          >
            <r.icon className="size-5" />
            {r.title}
          </button>
        ))}
      </div>

      <Card className="no-print">
        <CardContent className="flex flex-wrap items-center gap-3 pt-5">
          <DateRangeFilter from={range.from} to={range.to} onChange={setRange} />
          {meta.employeeFilter && (
            <NativeSelect aria-label="الموظف" className="w-52" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
              <option value="">كل الموظفين</option>
              {employees.data?.map((e) => <option key={e.id} value={e.id}>{e.fullName}</option>)}
            </NativeSelect>
          )}
          {type === 'Performance' && (
            <NativeSelect aria-label="التجميع" className="w-40" value={groupBy} onChange={(e) => setGroupBy(e.target.value as typeof groupBy)}>
              <option value="day">حسب اليوم</option>
              <option value="week">حسب الأسبوع</option>
              <option value="month">حسب الشهر</option>
              <option value="year">حسب السنة</option>
            </NativeSelect>
          )}
        </CardContent>
      </Card>

      {error ? (
        <ErrorState message={error} onRetry={reload} />
      ) : loading && !data ? (
        <Skeleton className="h-96" />
      ) : data ? (
        <div className={cn('print-area space-y-6 transition-opacity', fetching && 'opacity-60')}>
          <div>
            <h3 className="text-xl font-bold">{data.title}</h3>
            <p className="text-sm text-muted-foreground">الفترة: {data.period} · صدر في {formatDateTime(data.generatedAtUtc)}</p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {data.summary.map((s) => (
              <Card key={s.label} className="p-4">
                <p className="text-xs text-muted-foreground">{s.label}</p>
                <p className="num mt-1 text-xl font-bold">{s.value}</p>
              </Card>
            ))}
          </div>
          {data.sections.map((section) => (
            <Card key={section.title}>
              <CardHeader><CardTitle>{section.title}</CardTitle></CardHeader>
              {section.rows.length ? (
                <Table>
                  <TableHeader>
                    <TableRow>
                      {section.columns.map((c) => <TableHead key={c.key} className={c.kind !== 'text' ? 'text-end' : ''}>{c.title}</TableHead>)}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {section.rows.slice(0, 500).map((row, i) => (
                      <TableRow key={i}>
                        {row.map((cell, j) => {
                          const kind = section.columns[j].kind
                          return (
                            <TableCell key={j} className={kind !== 'text' ? 'num text-end' : ''}>
                              {cell === null ? '—' : kind === 'money' && typeof cell === 'number' ? numFmt.format(cell) : String(cell)}
                            </TableCell>
                          )
                        })}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              ) : (
                <EmptyState title="لا توجد بيانات في هذه الفترة" />
              )}
              {section.rows.length > 500 && <p className="no-print border-t p-3 text-center text-xs text-muted-foreground">يعرض أول 500 صف — صدّر التقرير للاطلاع على جميع الصفوف ({section.rows.length}).</p>}
            </Card>
          ))}
        </div>
      ) : null}
    </div>
  )
}
