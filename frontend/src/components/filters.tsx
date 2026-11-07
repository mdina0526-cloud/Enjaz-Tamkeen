import { CalendarRange } from 'lucide-react'
import { cn, shiftIso, startOfMonthIso, startOfWeekIso, startOfYearIso, todayIso } from '@/lib/utils'
import { Input, NativeSelect } from '@/components/ui/input'

const presets = [
  { key: 'today', label: 'اليوم', range: () => ({ from: todayIso(), to: todayIso() }) },
  { key: 'week', label: 'هذا الأسبوع', range: () => ({ from: startOfWeekIso(), to: todayIso() }) },
  { key: '7d', label: 'آخر 7 أيام', range: () => ({ from: shiftIso(todayIso(), -6), to: todayIso() }) },
  { key: 'month', label: 'هذا الشهر', range: () => ({ from: startOfMonthIso(), to: todayIso() }) },
  { key: '30d', label: 'آخر 30 يوماً', range: () => ({ from: shiftIso(todayIso(), -29), to: todayIso() }) },
  { key: '90d', label: 'آخر 90 يوماً', range: () => ({ from: shiftIso(todayIso(), -89), to: todayIso() }) },
  { key: 'year', label: 'هذه السنة', range: () => ({ from: startOfYearIso(), to: todayIso() }) },
  { key: 'all', label: 'كل الفترات', range: () => ({ from: '', to: '' }) },
]

export function DateRangeFilter({ from, to, onChange }: { from: string; to: string; onChange: (r: { from: string; to: string }) => void }) {
  const active = presets.find((p) => {
    const r = p.range()
    return r.from === from && r.to === to
  })
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="relative">
        <CalendarRange className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <NativeSelect
          aria-label="الفترة"
          className="w-40 pr-9"
          value={active?.key ?? 'custom'}
          onChange={(e) => {
            const p = presets.find((x) => x.key === e.target.value)
            if (p) onChange(p.range())
          }}
        >
          {presets.map((p) => (
            <option key={p.key} value={p.key}>{p.label}</option>
          ))}
          <option value="custom" disabled>فترة مخصصة</option>
        </NativeSelect>
      </div>
      <Input type="date" aria-label="من تاريخ" className="w-[150px]" value={from} max={to || undefined} onChange={(e) => onChange({ from: e.target.value, to })} />
      <span className="text-sm text-muted-foreground">إلى</span>
      <Input type="date" aria-label="إلى تاريخ" className="w-[150px]" value={to} min={from || undefined} onChange={(e) => onChange({ from, to: e.target.value })} />
    </div>
  )
}

const periodTabs = [
  { key: 'today', label: 'اليوم' },
  { key: 'week', label: 'الأسبوع' },
  { key: 'month', label: 'الشهر' },
  { key: 'year', label: 'السنة' },
] as const

/** Quick day / week / month / year switch; any other range shows as custom in the date inputs next to it. */
export function PeriodTabs({ from, to, onChange }: { from: string; to: string; onChange: (r: { from: string; to: string }) => void }) {
  return (
    <div role="tablist" aria-label="الفترة الزمنية" className="inline-flex rounded-lg border bg-muted/50 p-1">
      {periodTabs.map((t) => {
        const r = presets.find((p) => p.key === t.key)!.range()
        const active = r.from === from && r.to === to
        return (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(r)}
            className={cn('rounded-md px-3 py-1.5 text-sm font-medium transition-colors', active ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground')}
          >
            {t.label}
          </button>
        )
      })}
    </div>
  )
}
