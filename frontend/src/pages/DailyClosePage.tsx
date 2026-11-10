import { useState } from 'react'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { toastError, useApiQuery } from '@/hooks/useApi'
import { formatMoney } from '@/lib/utils'
import { PageHeader, ErrorState } from '@/components/common'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'

interface Preview { businessDate: string; expectedCash: number; expectedCard: number; ordersCount: number; rentalPaymentsCount: number; pendingOrders: number; isClosed: boolean }
interface Close extends Preview { id: number; countedCash: number; countedCard: number; notes: string | null; closedAtUtc: string }

export function DailyClosePage() {
  const [date, setDate] = useState(() => { const d = new Date(); d.setDate(d.getDate() - 1); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` })
  const [cash, setCash] = useState('')
  const [card, setCard] = useState('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)
  const preview = useApiQuery(['daily-close', 'preview', date], () => api.get<Preview>('/api/daily-close/preview', { date }), { enabled: Boolean(date) })
  const closes = useApiQuery(['daily-close', 'list'], () => api.get<Close[]>('/api/daily-close'))

  const closeDay = async () => {
    if (!preview.data) return
    setSaving(true)
    try {
      await api.post('/api/daily-close', { businessDate: date, countedCash: Number(cash), countedCard: Number(card), notes })
      toast.success('تم إقفال اليوم المالي')
      preview.reload(); closes.reload()
    } catch (e) { toastError(e) } finally { setSaving(false) }
  }

  return <div className="space-y-6">
    <PageHeader title="الإقفال اليومي" description="طابق المدفوعات الفعلية مع الطلبات والإيجارات، وسجّل أي فرق." />
    <Card>
      <CardHeader><CardTitle>مراجعة اليوم</CardTitle></CardHeader>
      <CardContent className="grid max-w-2xl gap-4">
        <Field label="تاريخ العمل"><Input type="date" value={date} onChange={e => { setDate(e.target.value); setCash(''); setCard('') }} /></Field>
        {preview.error && <ErrorState message={preview.error} onRetry={preview.reload} />}
        {preview.data && <>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-lg bg-muted p-4">المتوقع نقداً <strong className="block">{formatMoney(preview.data.expectedCash)}</strong></div>
            <div className="rounded-lg bg-muted p-4">المتوقع بطاقة <strong className="block">{formatMoney(preview.data.expectedCard)}</strong></div>
          </div>
          <p className="text-sm text-muted-foreground">{preview.data.ordersCount} طلب مكتمل · {preview.data.rentalPaymentsCount} دفعة إيجار · {preview.data.pendingOrders} طلب معلق</p>
          {preview.data.isClosed ? <p className="text-sm font-semibold text-primary">هذا اليوم مقفل بالفعل.</p> : <>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="النقد الفعلي"><Input type="number" min="0" step="0.01" value={cash} onChange={e => setCash(e.target.value)} /></Field>
              <Field label="البطاقات الفعلية"><Input type="number" min="0" step="0.01" value={card} onChange={e => setCard(e.target.value)} /></Field>
            </div>
            <Field label="سبب الفرق أو ملاحظة"><Input value={notes} maxLength={1000} onChange={e => setNotes(e.target.value)} /></Field>
            <div><Button disabled={saving || cash === '' || card === '' || preview.data.pendingOrders > 0} loading={saving} onClick={closeDay}>إقفال اليوم</Button></div>
          </>}
        </>}
      </CardContent>
    </Card>
    <Card><CardHeader><CardTitle>آخر الإقفالات</CardTitle></CardHeader><CardContent className="space-y-2">
      {closes.data?.map(c => <div key={c.id} className="flex flex-wrap justify-between gap-2 rounded-lg border p-3 text-sm">
        <strong>{c.businessDate}</strong><span>نقداً: {formatMoney(c.countedCash)} / {formatMoney(c.expectedCash)}</span><span>بطاقة: {formatMoney(c.countedCard)} / {formatMoney(c.expectedCard)}</span>
        {c.notes && <span className="w-full text-muted-foreground">{c.notes}</span>}
      </div>)}
      {closes.data?.length === 0 && <p className="text-sm text-muted-foreground">لا توجد إقفالات بعد.</p>}
    </CardContent></Card>
  </div>
}
