import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Archive, ArchiveRestore, ArrowRight, Ban, Paperclip, Pencil, Plus, Receipt } from 'lucide-react'
import { api } from '@/lib/api'
import { toastError, useApiQuery } from '@/hooks/useApi'
import { formatDate, formatDateTime, formatMoney } from '@/lib/utils'
import { paymentMethodLabel, propertyTypeLabel } from '@/lib/labels'
import type { RentalDetail, RentalPayment } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ConfirmDialog, EmptyState, ErrorState, InfoRow, Money, PageHeader, RentalStatusBadge } from '@/components/common'
import { RentalFormDialog, RentalPaymentDialog } from './RentalDialogs'

export function RentalDetailPage() {
  const { id } = useParams()
  const { data: r, loading, error, reload, setData } = useApiQuery(['rentals', 'detail', id], () => api.get<RentalDetail>(`/api/rentals/${id}`))
  const [dialog, setDialog] = useState<'edit' | 'pay' | 'archive' | null>(null)
  const [voiding, setVoiding] = useState<RentalPayment | null>(null)

  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !r) return <Skeleton className="h-96" />
  const progress = Math.min(100, (r.paidAmount / r.amount) * 100)

  const download = async (p: RentalPayment) => {
    try {
      await api.download(`/api/rentals/${r.id}/payments/${p.id}/attachment`, undefined, p.attachmentName ?? 'receipt')
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={r.title}
        description={`${propertyTypeLabel[r.propertyType]}${r.location ? ` · ${r.location}` : ''}`}
        actions={
          <>
            <Button variant="outline" asChild><Link to="/rentals"><ArrowRight /> الإيجارات</Link></Button>
            <Button variant="outline" onClick={() => setDialog('edit')}><Pencil /> تعديل</Button>
            <Button variant="outline" onClick={() => setDialog('archive')}>{r.isArchived ? <><ArchiveRestore /> إلغاء الأرشفة</> : <><Archive /> أرشفة</>}</Button>
            {!r.isArchived && r.remainingAmount > 0 && <Button onClick={() => setDialog('pay')}><Plus /> تسجيل دفعة</Button>}
          </>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardContent className="pt-6">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm text-muted-foreground">نسبة السداد</p>
                <p className="num text-3xl font-bold">{progress.toFixed(0)}%</p>
              </div>
              <RentalStatusBadge status={r.status} />
            </div>
            <div className="h-3 overflow-hidden rounded-full bg-muted" role="progressbar" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100}>
              <div className="h-full rounded-full bg-gradient-to-l from-success to-emerald-400 transition-all" style={{ width: `${progress}%` }} />
            </div>
            <div className="mt-5 grid grid-cols-3 gap-4 text-center">
              <div className="rounded-xl bg-muted/50 p-3">
                <p className="text-xs text-muted-foreground">قيمة الإيجار</p>
                <p className="num mt-1 font-bold">{formatMoney(r.amount)}</p>
              </div>
              <div className="rounded-xl bg-success/8 p-3">
                <p className="text-xs text-muted-foreground">المدفوع</p>
                <p className="num mt-1 font-bold text-success">{formatMoney(r.paidAmount)}</p>
              </div>
              <div className="rounded-xl bg-warning/8 p-3">
                <p className="text-xs text-muted-foreground">المتبقي</p>
                <p className="num mt-1 font-bold text-warning">{formatMoney(r.remainingAmount)}</p>
              </div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>بيانات العقد</CardTitle></CardHeader>
          <CardContent>
            <InfoRow label="المستفيد">{r.beneficiaryName}</InfoRow>
            <InfoRow label="جوال المستفيد"><span className="num">{r.beneficiaryPhone ?? '—'}</span></InfoRow>
            <InfoRow label="تاريخ الاستحقاق">{formatDate(r.dueDate)}</InfoRow>
            <InfoRow label="تاريخ الإضافة">{formatDateTime(r.createdAtUtc)}</InfoRow>
            {r.isArchived && <InfoRow label="الأرشفة"><Badge variant="muted">مؤرشف</Badge></InfoRow>}
            {r.notes && <p className="mt-3 whitespace-pre-wrap text-sm text-muted-foreground">{r.notes}</p>}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle>سجل الدفعات</CardTitle></CardHeader>
        {r.payments.length ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>تاريخ السداد</TableHead>
                <TableHead className="text-end">المبلغ</TableHead>
                <TableHead>الطريقة</TableHead>
                <TableHead>المرجع</TableHead>
                <TableHead>الإيصال</TableHead>
                <TableHead>الحالة</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {r.payments.map((p) => (
                <TableRow key={p.id} className={p.isVoided ? 'opacity-60' : ''}>
                  <TableCell>{formatDate(p.paidOn)}</TableCell>
                  <TableCell className="text-end font-semibold"><Money value={p.amount} className={p.isVoided ? 'line-through' : ''} /></TableCell>
                  <TableCell>{paymentMethodLabel[p.method]}</TableCell>
                  <TableCell className="num">{p.reference ?? '—'}</TableCell>
                  <TableCell>
                    {p.hasAttachment ? (
                      <Button variant="link" size="sm" className="h-auto p-0" onClick={() => download(p)}>
                        <Paperclip /> {p.attachmentName}
                      </Button>
                    ) : '—'}
                  </TableCell>
                  <TableCell>{p.isVoided ? <Badge variant="destructive" title={p.voidReason ?? ''}>ملغاة</Badge> : <Badge variant="success">مسجلة</Badge>}</TableCell>
                  <TableCell>
                    {!p.isVoided && (
                      <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => setVoiding(p)}>
                        <Ban /> إلغاء
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <EmptyState icon={Receipt} title="لا توجد دفعات مسجلة" action={!r.isArchived && <Button onClick={() => setDialog('pay')}><Plus /> تسجيل دفعة</Button>} />
        )}
      </Card>

      <RentalFormDialog open={dialog === 'edit'} onOpenChange={(o) => setDialog(o ? 'edit' : null)} rental={r} onSaved={setData} />
      <RentalPaymentDialog open={dialog === 'pay'} onOpenChange={(o) => setDialog(o ? 'pay' : null)} rental={r} onSaved={setData} />
      <ConfirmDialog
        open={dialog === 'archive'}
        onOpenChange={(o) => setDialog(o ? 'archive' : null)}
        title={r.isArchived ? 'إلغاء أرشفة الإيجار' : 'أرشفة الإيجار'}
        description={r.isArchived ? 'سيعود الإيجار إلى القائمة النشطة.' : 'يُخفى الإيجار من القائمة النشطة مع الاحتفاظ بكامل سجله ودفعاته.'}
        onConfirm={async () => {
          try {
            await api.post(`/api/rentals/${r.id}/archive?archived=${!r.isArchived}`)
            toast.success(r.isArchived ? 'تم إلغاء الأرشفة' : 'تمت الأرشفة')
            reload()
          } catch (e) {
            toastError(e)
            throw e
          }
        }}
      />
      <ConfirmDialog
        open={!!voiding}
        onOpenChange={(o) => !o && setVoiding(null)}
        title="إلغاء الدفعة"
        description={voiding ? `ستُلغى الدفعة بمبلغ ${formatMoney(voiding.amount)} وتبقى محفوظة في السجل.` : ''}
        confirmLabel="إلغاء الدفعة"
        destructive
        requireReason
        onConfirm={async (reason) => {
          try {
            setData(await api.post<RentalDetail>(`/api/rentals/${r.id}/payments/${voiding!.id}/void`, { reason }))
            toast.success('تم إلغاء الدفعة')
          } catch (e) {
            toastError(e)
            throw e
          }
        }}
      />
    </div>
  )
}
