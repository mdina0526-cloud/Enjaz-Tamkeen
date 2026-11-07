import { useMemo, useRef, useState } from 'react'
import { useFieldArray, useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { Banknote, CreditCard, Hash, Info, Plus, Save, Trash2, UserRound } from 'lucide-react'
import { api } from '@/lib/api'
import { invalidate, toastError, useApiQuery, useOrderTypes, usePaymentMethods, useServices } from '@/hooks/useApi'
import { useAuth } from '@/context/AuthContext'
import { cn, formatMoney, newIdempotencyKey, shiftIso, todayIso } from '@/lib/utils'
import type { DeductionInfo, EmployeeLookup, OrderDetail } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input, NativeSelect, Textarea } from '@/components/ui/input'
import { Field, FieldError, Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/misc'
import { EmptyState, PageHeader } from '@/components/common'

/** Mirrors OrderService.EmployeeBackdateDays on the server, which is the real check. */
const EMPLOYEE_BACKDATE_DAYS = 7

const itemSchema = z.object({
  serviceTypeId: z.string().min(1, 'اختر نوع الخدمة'),
  description: z.string().max(500, 'الحد الأقصى 500 حرف').optional(),
  unitPrice: z.coerce.number<number>({ message: 'أدخل مبلغاً' }).gt(0, 'المبلغ يجب أن يكون أكبر من صفر').max(100_000_000, 'المبلغ كبير جداً'),
})

const schema = z.object({
  employeeId: z.string().optional(),
  orderTypeId: z.string().min(1, 'اختر نوع الطلب'),
  operationDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'أدخل تاريخاً صحيحاً'),
  paymentMethod: z.enum(['Cash', 'Card'], { message: 'اختر طريقة الدفع' }),
  notes: z.string().max(1000, 'الحد الأقصى 1000 حرف').optional(),
  items: z.array(itemSchema).min(1, 'أضف خدمة واحدة على الأقل').max(50),
})
type FormValues = z.infer<typeof schema>

const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100
const emptyItem = { serviceTypeId: '', description: '', unitPrice: 0 }

export function NewOrderPage() {
  const { user, isAdmin } = useAuth()
  const navigate = useNavigate()
  // One key per form instance: double-clicks and network retries never create a second order.
  const idempotencyKey = useRef(newIdempotencyKey())
  const [today] = useState(todayIso)
  const minDate = isAdmin ? undefined : shiftIso(today, -EMPLOYEE_BACKDATE_DAYS)

  const { register, control, handleSubmit, setValue, getValues, formState: { errors, isSubmitting }, setError } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { paymentMethod: 'Cash', orderTypeId: '1', operationDate: today, items: [emptyItem], notes: '', employeeId: '' },
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'items' })
  const items = useWatch({ control, name: 'items' })
  const paymentMethod = useWatch({ control, name: 'paymentMethod' })
  const employeeId = useWatch({ control, name: 'employeeId' })
  const orderTypeId = useWatch({ control, name: 'orderTypeId' })

  const orderTypes = useOrderTypes()
  const services = useServices()
  const paymentMethods = usePaymentMethods()
  const employees = useApiQuery(['employees', 'lookup'], () => api.get<EmployeeLookup[]>('/api/employees/lookup'), { enabled: isAdmin })
  const deduction = useApiQuery(
    ['orders', 'deduction-info', isAdmin ? employeeId : 'self'],
    () => api.get<DeductionInfo>('/api/orders/deduction-info', { employeeId: isAdmin ? employeeId : undefined }),
    { enabled: !isAdmin || !!employeeId },
  )

  // A service bound to an order type is only offered with that type.
  const availableServices = useMemo(
    () => (services.data ?? []).filter((s) => s.orderTypeId === null || String(s.orderTypeId) === orderTypeId),
    [services.data, orderTypeId],
  )

  const onOrderTypeChange = (value: string) => {
    setValue('orderTypeId', value, { shouldValidate: true })
    getValues('items').forEach((it, idx) => {
      const sv = services.data?.find((s) => String(s.id) === it.serviceTypeId)
      if (sv && sv.orderTypeId !== null && String(sv.orderTypeId) !== value) setValue(`items.${idx}.serviceTypeId`, '')
    })
  }

  const onServiceChange = (idx: number, value: string) => {
    setValue(`items.${idx}.serviceTypeId`, value, { shouldValidate: true })
    const sv = services.data?.find((s) => String(s.id) === value)
    if (sv?.defaultPrice && !(Number(getValues(`items.${idx}.unitPrice`)) > 0)) setValue(`items.${idx}.unitPrice`, sv.defaultPrice, { shouldValidate: true })
  }

  const totals = useMemo(() => {
    const subtotal = round2((items ?? []).reduce((s, i) => s + round2(Number(i?.unitPrice) || 0), 0))
    const info = deduction.data
    const pct = info?.enabled ? info.percentage : 0
    const amount = round2((subtotal * pct) / 100)
    const net = info?.treatment === 'RecordOnly' ? subtotal : round2(subtotal - amount)
    return { subtotal, pct, amount, net }
  }, [items, deduction.data])

  const onSubmit = async (v: FormValues) => {
    if (isAdmin && !v.employeeId) {
      setError('employeeId', { message: 'اختر الموظف المسؤول عن الطلب' })
      return
    }
    try {
      const order = await api.post<OrderDetail>(
        '/api/orders',
        {
          orderTypeId: Number(v.orderTypeId),
          operationDate: v.operationDate,
          items: v.items.map((i) => ({ serviceTypeId: Number(i.serviceTypeId), description: i.description?.trim() || null, quantity: 1, unitPrice: i.unitPrice })),
          paymentMethod: v.paymentMethod,
          notes: v.notes?.trim() || null,
          employeeId: isAdmin ? Number(v.employeeId) : undefined,
        },
        { 'Idempotency-Key': idempotencyKey.current },
      )
      void invalidate('orders', 'dashboard', 'employees', 'reports', 'sales')
      toast.success(`تم حفظ الطلب ${order.orderNumber} بنجاح`)
      navigate(`/orders/${order.id}`, { replace: true })
    } catch (e) {
      toastError(e)
    }
  }

  const lookupsLoading = orderTypes.loading || services.loading || paymentMethods.loading
  if (!lookupsLoading && services.data?.length === 0)
    return <EmptyState title="لا توجد خدمات مفعّلة" description="يجب أن يضيف المدير خدمة واحدة على الأقل من صفحة الإعدادات قبل تسجيل الطلبات" />

  const methodIcon = { Cash: Banknote, Card: CreditCard } as const

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <PageHeader title="إنشاء طلب جديد" description="اختر نوع الطلب والخدمة وأدخل المبلغ؛ يُحسب الاستقطاع والصافي تلقائياً" />

      <div className="grid gap-6 xl:grid-cols-3">
        <div className="space-y-6 xl:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>بيانات الطلب</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <ReadOnly icon={Hash} label="رقم الطلب" value="يُولَّد تلقائياً عند الحفظ" />
              <Field label="تاريخ العملية" htmlFor="operationDate" error={errors.operationDate?.message} hint={isAdmin ? undefined : `حتى ${EMPLOYEE_BACKDATE_DAYS} أيام سابقة`}>
                <Input id="operationDate" type="date" max={today} min={minDate} aria-invalid={!!errors.operationDate} {...register('operationDate')} />
              </Field>
              {isAdmin ? (
                <Field label="الموظف المسؤول" htmlFor="employeeId" error={errors.employeeId?.message}>
                  <NativeSelect id="employeeId" aria-invalid={!!errors.employeeId} {...register('employeeId')}>
                    <option value="">اختر الموظف...</option>
                    {employees.data?.filter((e) => e.isActive).map((e) => (
                      <option key={e.id} value={e.id}>{e.fullName} ({e.employeeCode})</option>
                    ))}
                  </NativeSelect>
                </Field>
              ) : (
                <ReadOnly icon={UserRound} label="الموظف المسؤول" value={user?.fullName ?? ''} />
              )}
              <Field label="نوع الطلب" htmlFor="orderTypeId" error={errors.orderTypeId?.message}>
                {orderTypes.loading ? (
                  <Skeleton className="h-10" />
                ) : (
                  <NativeSelect id="orderTypeId" value={orderTypeId} onChange={(e) => onOrderTypeChange(e.target.value)} aria-invalid={!!errors.orderTypeId}>
                    {orderTypes.data?.map((t) => (
                      <option key={t.id} value={t.id}>{t.name}</option>
                    ))}
                  </NativeSelect>
                )}
              </Field>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex-row items-center justify-between">
              <div>
                <CardTitle>الخدمة</CardTitle>
                <CardDescription>يمكن إضافة أكثر من خدمة في الطلب نفسه</CardDescription>
              </div>
              <Button type="button" variant="outline" size="sm" onClick={() => append(emptyItem)} disabled={fields.length >= 50}>
                <Plus /> إضافة خدمة
              </Button>
            </CardHeader>
            <CardContent className="space-y-3">
              {fields.map((f, idx) => {
                const e = errors.items?.[idx]
                return (
                  <div key={f.id} className="rounded-xl border bg-muted/30 p-4">
                    {fields.length > 1 && (
                      <div className="mb-3 flex items-center justify-between">
                        <span className="num flex size-7 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">{idx + 1}</span>
                        <Button type="button" variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => remove(idx)}>
                          <Trash2 /> حذف
                        </Button>
                      </div>
                    )}
                    <div className="grid gap-3 md:grid-cols-12">
                      <Field className="md:col-span-7" label="نوع الخدمة" error={e?.serviceTypeId?.message}>
                        <NativeSelect value={items?.[idx]?.serviceTypeId ?? ''} onChange={(ev) => onServiceChange(idx, ev.target.value)} aria-invalid={!!e?.serviceTypeId}>
                          <option value="">اختر الخدمة...</option>
                          {availableServices.map((s) => (
                            <option key={s.id} value={s.id}>{s.name}{s.defaultPrice ? ` — ${formatMoney(s.defaultPrice)}` : ''}</option>
                          ))}
                        </NativeSelect>
                      </Field>
                      <Field className="md:col-span-5" label="المبلغ (ر.س)" error={e?.unitPrice?.message}>
                        <Input type="number" min={0} step="0.01" inputMode="decimal" className="num" aria-invalid={!!e?.unitPrice} {...register(`items.${idx}.unitPrice`)} />
                      </Field>
                      <Field className="md:col-span-12" label="وصف الخدمة" error={e?.description?.message}>
                        <Input placeholder="تفاصيل الخدمة المقدمة للعميل (اختياري)" {...register(`items.${idx}.description`)} />
                      </Field>
                    </div>
                  </div>
                )
              })}
              <FieldError message={errors.items?.root?.message ?? errors.items?.message} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>طريقة الدفع والملاحظات</CardTitle>
              <CardDescription>طريقة دفع واحدة لكل عملية</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-label="طريقة الدفع">
                {(paymentMethods.data ?? []).map((m) => {
                  const Icon = methodIcon[m.id] ?? CreditCard
                  return (
                    <label
                      key={m.id}
                      className={cn(
                        'flex cursor-pointer items-center gap-3 rounded-xl border-2 p-4 transition-colors focus-within:ring-2 focus-within:ring-ring',
                        paymentMethod === m.id ? 'border-primary bg-primary/5' : 'border-border hover:border-primary/40',
                      )}
                    >
                      <input type="radio" value={m.id} className="sr-only" {...register('paymentMethod')} />
                      <span className={cn('flex size-10 items-center justify-center rounded-lg', paymentMethod === m.id ? 'bg-primary text-white' : 'bg-muted text-muted-foreground')}>
                        <Icon className="size-5" />
                      </span>
                      <span className="font-medium">{m.name}</span>
                    </label>
                  )
                })}
                {paymentMethods.loading && <><Skeleton className="h-[76px]" /><Skeleton className="h-[76px]" /></>}
              </div>
              <FieldError message={errors.paymentMethod?.message} />
              <Field label="ملاحظات إضافية" error={errors.notes?.message}>
                <Textarea rows={3} placeholder="أي ملاحظات تخص الطلب (اختياري)" {...register('notes')} />
              </Field>
            </CardContent>
          </Card>
        </div>

        <div>
          <Card className="sticky top-20 overflow-hidden">
            <div className="bg-gradient-to-l from-primary to-navy p-5 text-white">
              <p className="text-sm text-blue-100">إجمالي الطلب</p>
              <p className="num mt-1 text-3xl font-bold">{formatMoney(totals.subtotal)}</p>
            </div>
            <CardContent className="space-y-3 pt-5">
              {deduction.loading ? (
                <Skeleton className="h-24" />
              ) : deduction.data ? (
                <>
                  <SummaryRow label="المبلغ الأساسي" value={formatMoney(totals.subtotal)} />
                  <SummaryRow label={`${deduction.data.label} (${totals.pct}%)`} value={`− ${formatMoney(totals.amount)}`} muted />
                  <div className="border-t pt-3">
                    <SummaryRow label="صافي المبلغ" value={formatMoney(totals.net)} strong />
                  </div>
                  {deduction.data.treatment === 'RecordOnly' && (
                    <p className="flex gap-2 rounded-lg bg-muted p-2.5 text-xs text-muted-foreground">
                      <Info className="size-4 shrink-0" /> يُسجَّل الاستقطاع للمتابعة فقط ولا يُطرح من الصافي حسب إعدادات النظام.
                    </p>
                  )}
                  {!deduction.data.enabled && <p className="text-xs text-muted-foreground">الاستقطاع معطل في إعدادات النظام.</p>}
                </>
              ) : (
                <p className="text-sm text-muted-foreground">اختر الموظف لعرض نسبة الاستقطاع.</p>
              )}
              <p className="text-xs text-muted-foreground">القيم النهائية يحسبها الخادم ويحفظ النسبة المطبقة وقت العملية.</p>
              <Button type="submit" size="lg" className="w-full" loading={isSubmitting} disabled={lookupsLoading}>
                <Save /> حفظ الطلب
              </Button>
              <Button type="button" variant="outline" className="w-full" onClick={() => navigate(-1)} disabled={isSubmitting}>
                إلغاء
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </form>
  )
}

function ReadOnly({ label, value, icon: Icon }: { label: string; value: string; icon?: typeof Hash }) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <div className="flex h-10 items-center gap-2 rounded-lg border border-dashed bg-muted/40 px-3 text-sm text-muted-foreground">
        {Icon && <Icon className="size-4 shrink-0" />}
        <span className="truncate">{value}</span>
      </div>
    </div>
  )
}

function SummaryRow({ label, value, muted, strong }: { label: string; value: string; muted?: boolean; strong?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className={cn('text-sm', muted ? 'text-muted-foreground' : '')}>{label}</span>
      <span className={cn('num text-sm', strong ? 'text-lg font-bold text-primary' : 'font-semibold', muted && 'text-warning')}>{value}</span>
    </div>
  )
}
