import { Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from 'recharts'
import { formatMoney, formatNumber } from '@/lib/utils'
import { paymentMethodLabel } from '@/lib/labels'
import type { KeyValueAmount, PaymentMethod } from '@/types/api'

const axisTick = { fill: 'var(--muted-foreground)', fontSize: 11 }
const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })

const arMonth = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { month: 'short', timeZone: 'UTC' })
const arDay = new Intl.DateTimeFormat('ar-SA-u-ca-gregory-nu-latn', { day: 'numeric', month: 'short', timeZone: 'UTC' })

function dayLabel(key: string) {
  const [y, m, d] = key.split('-').map(Number)
  return arDay.format(Date.UTC(y, m - 1, d))
}
function monthLabel(key: string) {
  const [y, m] = key.split('-').map(Number)
  return `${arMonth.format(Date.UTC(y, m - 1, 1))} ${String(y).slice(2)}`
}

function ChartTooltip({ active, payload, label, formatLabel }: Partial<TooltipContentProps<number, string>> & { formatLabel?: (l: string) => string }) {
  if (!active || !payload?.length) return null
  const row = payload[0].payload as KeyValueAmount
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-lg" dir="rtl">
      <p className="mb-1 font-semibold text-popover-foreground">{formatLabel ? formatLabel(String(label)) : label}</p>
      <p className="text-muted-foreground">
        المبيعات: <span className="num font-semibold text-popover-foreground">{formatMoney(row.amount)}</span>
      </p>
      <p className="text-muted-foreground">
        العمليات: <span className="num font-semibold text-popover-foreground">{formatNumber(row.count)}</span>
      </p>
    </div>
  )
}

/** Daily sales, single series: the card title names it, so no legend. Time runs right-to-left for RTL readers. */
export function DailySalesChart({ data }: { data: KeyValueAmount[] }) {
  return (
    <div dir="ltr"><ResponsiveContainer width="100%" height={280}>
      <AreaChart data={data} margin={{ top: 8, right: 4, left: 4, bottom: 0 }}>
        <defs>
          <linearGradient id="dailyFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--chart-1)" stopOpacity={0.28} />
            <stop offset="100%" stopColor="var(--chart-1)" stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
        <XAxis dataKey="key" reversed tickFormatter={dayLabel} tick={axisTick} tickLine={false} axisLine={false} minTickGap={24} />
        <YAxis orientation="right" tickFormatter={(v: number) => compact.format(v)} tick={axisTick} tickLine={false} axisLine={false} width={44} />
        <Tooltip content={<ChartTooltip formatLabel={dayLabel} />} cursor={{ stroke: 'var(--muted-foreground)', strokeDasharray: '3 3' }} />
        <Area type="monotone" dataKey="amount" stroke="var(--chart-1)" strokeWidth={2} fill="url(#dailyFill)" activeDot={{ r: 5, strokeWidth: 2, stroke: 'var(--card)' }} />
      </AreaChart>
    </ResponsiveContainer></div>
  )
}

export function MonthlySalesChart({ data }: { data: KeyValueAmount[] }) {
  return (
    <div dir="ltr"><ResponsiveContainer width="100%" height={280}>
      <BarChart data={data} margin={{ top: 8, right: 4, left: 4, bottom: 0 }} barCategoryGap="28%">
        <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
        <XAxis dataKey="key" reversed tickFormatter={monthLabel} tick={axisTick} tickLine={false} axisLine={false} interval="preserveStartEnd" />
        <YAxis orientation="right" tickFormatter={(v: number) => compact.format(v)} tick={axisTick} tickLine={false} axisLine={false} width={44} />
        <Tooltip content={<ChartTooltip formatLabel={monthLabel} />} cursor={{ fill: 'var(--muted)', opacity: 0.6 }} />
        <Bar dataKey="amount" fill="var(--chart-1)" radius={[4, 4, 0, 0]} maxBarSize={36} />
      </BarChart>
    </ResponsiveContainer></div>
  )
}

/** Ranked horizontal bars; names are axis labels, values in the tooltip. */
export function EmployeeSalesChart({ data }: { data: KeyValueAmount[] }) {
  const rows = data.slice(0, 8)
  return (
    <div dir="ltr"><ResponsiveContainer width="100%" height={Math.max(160, rows.length * 44)}>
      <BarChart data={rows} layout="vertical" margin={{ top: 0, right: 4, left: 8, bottom: 0 }} barCategoryGap="30%">
        <CartesianGrid horizontal={false} stroke="var(--chart-grid)" />
        <XAxis type="number" reversed tickFormatter={(v: number) => compact.format(v)} tick={axisTick} tickLine={false} axisLine={false} />
        <YAxis type="category" dataKey="key" orientation="right" tick={{ ...axisTick, fill: 'var(--foreground)', fontSize: 12 }} tickLine={false} axisLine={false} width={110} />
        <Tooltip content={<ChartTooltip />} cursor={{ fill: 'var(--muted)', opacity: 0.6 }} />
        <Bar dataKey="amount" fill="var(--chart-1)" radius={[4, 0, 0, 4]} maxBarSize={22} />
      </BarChart>
    </ResponsiveContainer></div>
  )
}

const methodColor: Record<PaymentMethod, string> = { Card: 'var(--chart-1)', Cash: 'var(--chart-2)' }

/** Two categories: a proportion bar with a labelled legend (identity never by color alone). */
export function PaymentMethodBreakdown({ data }: { data: KeyValueAmount[] }) {
  const total = data.reduce((s, d) => s + d.amount, 0)
  const rows = data.map((d) => ({ ...d, method: d.key as PaymentMethod, pct: total ? (d.amount / total) * 100 : 0 }))
  return (
    <div className="space-y-5">
      <div className="flex h-3 w-full gap-[2px] overflow-hidden rounded-full bg-muted" role="img" aria-label="توزيع طرق الدفع">
        {rows.filter((r) => r.pct > 0).map((r) => (
          <div key={r.key} style={{ width: `${r.pct}%`, background: methodColor[r.method] }} title={`${paymentMethodLabel[r.method]}: ${r.pct.toFixed(1)}%`} />
        ))}
      </div>
      <ul className="space-y-3">
        {rows.map((r) => (
          <li key={r.key} className="flex items-center gap-3">
            <span className="size-3 shrink-0 rounded-sm" style={{ background: methodColor[r.method] }} aria-hidden />
            <span className="flex-1 whitespace-nowrap text-sm">{paymentMethodLabel[r.method]}</span>
            <span className="num text-xs text-muted-foreground">{formatNumber(r.count)} عملية</span>
            <span className="num w-14 text-end text-xs text-muted-foreground">{r.pct.toFixed(1)}%</span>
            <span className="num text-end text-sm font-semibold whitespace-nowrap">{formatMoney(r.amount)}</span>
          </li>
        ))}
      </ul>
      <div className="h-40" dir="ltr">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 4, right: 4, left: 4, bottom: 0 }} barCategoryGap="35%">
            <CartesianGrid vertical={false} stroke="var(--chart-grid)" />
            <XAxis dataKey="key" tickFormatter={(k: string) => paymentMethodLabel[k as PaymentMethod] ?? k} tick={axisTick} tickLine={false} axisLine={false} reversed />
            <YAxis orientation="right" tickFormatter={(v: number) => compact.format(v)} tick={axisTick} tickLine={false} axisLine={false} width={44} />
            <Tooltip content={<ChartTooltip formatLabel={(k) => paymentMethodLabel[k as PaymentMethod] ?? k} />} cursor={{ fill: 'var(--muted)', opacity: 0.6 }} />
            <Bar dataKey="amount" radius={[4, 4, 0, 0]} maxBarSize={56}>
              {rows.map((r) => (
                <Cell key={r.key} fill={methodColor[r.method]} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
