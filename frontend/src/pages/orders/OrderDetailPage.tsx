import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { ArrowRight, Ban, CheckCircle2, Printer } from 'lucide-react'
import { api } from '@/lib/api'
import { invalidate, toastError, useApiQuery } from '@/hooks/useApi'
import { useAuth } from '@/context/AuthContext'
import { formatDate, formatDateTime, formatMoney } from '@/lib/utils'
import { paymentMethodLabel, treatmentLabel } from '@/lib/labels'
import type { OrderDetail } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ConfirmDialog, ErrorState, InfoRow, Money, OrderStatusBadge, PageHeader } from '@/components/common'

export function OrderDetailPage() {
  const { id } = useParams()
  const { isAdmin } = useAuth()
  const { data: order, loading, error, reload, setData } = useApiQuery(['orders', 'detail', id], () => api.get<OrderDetail>(`/api/orders/${id}`))
  const [confirm, setConfirm] = useState<'approve' | 'cancel' | null>(null)

  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !order) return <Skeleton className="h-[480px]" />

  const act = async (kind: 'approve' | 'cancel', reason: string) => {
    try {
      const updated = kind === 'approve' ? await api.post<OrderDetail>(`/api/orders/${order.id}/approve`) : await api.post<OrderDetail>(`/api/orders/${order.id}/cancel`, { reason })
      setData(updated)
      void invalidate('orders', 'dashboard', 'employees', 'reports', 'sales')
      toast.success(kind === 'approve' ? 'تم اعتماد الطلب' : 'تم إلغاء الطلب وتسجيل العملية في سجل النشاط')
    } catch (e) {
      toastError(e)
      throw e
    }
  }

  return (
    <div className="print-area">
      <PageHeader
        title={`الطلب ${order.orderNumber}`}
        description={`تاريخ العملية ${formatDate(order.operationDate)} — سُجّل في ${formatDateTime(order.createdAtUtc)}`}
        actions={
          <>
            <Button variant="outline" asChild>
              <Link to="/orders"><ArrowRight /> العودة</Link>
            </Button>
            <Button variant="outline" onClick={() => window.print()}><Printer /> طباعة</Button>
            {isAdmin && order.status === 'Pending' && (
              <Button variant="success" onClick={() => setConfirm('approve')}><CheckCircle2 /> اعتماد</Button>
            )}
            {isAdmin && order.status !== 'Cancelled' && (
              <Button variant="destructive" onClick={() => setConfirm('cancel')}><Ban /> إلغاء الطلب</Button>
            )}
          </>
        }
      />

      {order.status === 'Cancelled' && (
        <div className="mb-6 rounded-xl border border-destructive/30 bg-destructive/8 p-4 text-sm">
          <p className="font-semibold text-destructive">هذا الطلب ملغي</p>
          <p className="mt-1 text-muted-foreground">
            السبب: {order.cancellationReason} — بتاريخ {formatDateTime(order.cancelledAtUtc)}. يبقى السجل محفوظاً ولا يُحتسب في الإجماليات.
          </p>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>الخدمات</CardTitle>
          </CardHeader>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>#</TableHead>
                <TableHead>الخدمة</TableHead>
                <TableHead className="text-end">الكمية</TableHead>
                <TableHead className="text-end">المبلغ</TableHead>
                <TableHead className="text-end">الإجمالي</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {order.items.map((i, idx) => (
                <TableRow key={i.id}>
                  <TableCell className="num text-muted-foreground">{idx + 1}</TableCell>
                  <TableCell className="whitespace-normal">
                    <p className="font-medium">{i.serviceName}</p>
                    {i.description && <p className="text-xs text-muted-foreground">{i.description}</p>}
                  </TableCell>
                  <TableCell className="num text-end">{i.quantity}</TableCell>
                  <TableCell className="text-end"><Money value={i.unitPrice} /></TableCell>
                  <TableCell className="text-end font-semibold"><Money value={i.lineTotal} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell colSpan={4}>الإجمالي</TableCell>
                <TableCell className="text-end"><Money value={order.subtotal} /></TableCell>
              </TableRow>
            </TableFooter>
          </Table>
          {order.notes && (
            <CardContent className="border-t pt-4">
              <p className="text-sm text-muted-foreground">ملاحظات</p>
              <p className="mt-1 whitespace-pre-wrap text-sm">{order.notes}</p>
            </CardContent>
          )}
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>الملخص المالي</CardTitle>
            </CardHeader>
            <CardContent>
              <InfoRow label="المبلغ الأساسي"><span className="num">{formatMoney(order.subtotal)}</span></InfoRow>
              <InfoRow label={`قيمة الاستقطاع (${order.deductionPercentage}%)`}><span className="num text-warning">{formatMoney(order.deductionAmount)}</span></InfoRow>
              <InfoRow label="المبلغ بعد الاستقطاع"><span className="num text-base font-bold text-primary">{formatMoney(order.netAmount)}</span></InfoRow>
              {order.deductionTreatment && <p className="mt-3 text-xs text-muted-foreground">طريقة التطبيق: {treatmentLabel[order.deductionTreatment]}. النسبة محفوظة وقت إنشاء الطلب.</p>}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>التفاصيل</CardTitle>
            </CardHeader>
            <CardContent>
              <InfoRow label="الحالة"><OrderStatusBadge status={order.status} /></InfoRow>
              <InfoRow label="الموظف المسؤول">{order.employeeName} <span className="num text-xs text-muted-foreground">({order.employeeCode})</span></InfoRow>
              <InfoRow label="نوع الطلب">{order.orderTypeName}</InfoRow>
              <InfoRow label="تاريخ العملية">{formatDate(order.operationDate)}</InfoRow>
              <InfoRow label="طريقة الدفع">{paymentMethodLabel[order.paymentMethod]}</InfoRow>
              {order.completedAtUtc && <InfoRow label="تاريخ الاعتماد">{formatDateTime(order.completedAtUtc)}</InfoRow>}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>المدفوعات</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {order.payments.map((p) => (
                <div key={p.id} className="flex items-center justify-between rounded-lg border p-3 text-sm">
                  <div>
                    <p className="font-medium">{paymentMethodLabel[p.method]}</p>
                    <p className="text-xs text-muted-foreground">{formatDateTime(p.paidAtUtc)}</p>
                  </div>
                  <div className="text-end">
                    <p className="num font-semibold">{formatMoney(p.amount)}</p>
                    <Badge variant={p.status === 'Completed' ? 'success' : 'destructive'}>{p.status === 'Completed' ? 'مكتملة' : 'ملغاة'}</Badge>
                  </div>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>

      <ConfirmDialog
        open={confirm === 'approve'}
        onOpenChange={(o) => !o && setConfirm(null)}
        title="اعتماد الطلب"
        description={`سيتم اعتماد الطلب ${order.orderNumber} بقيمة ${formatMoney(order.subtotal)} واحتسابه ضمن المبالغ المحصلة.`}
        confirmLabel="اعتماد"
        onConfirm={(r) => act('approve', r)}
      />
      <ConfirmDialog
        open={confirm === 'cancel'}
        onOpenChange={(o) => !o && setConfirm(null)}
        title="إلغاء الطلب"
        description="لن يُحذف الطلب؛ سيُعلَّم كملغي وتُلغى دفعته ويُسجَّل الإجراء في سجل النشاط. لا يمكن التراجع."
        confirmLabel="تأكيد الإلغاء"
        destructive
        requireReason
        onConfirm={(r) => act('cancel', r)}
      />
    </div>
  )
}
