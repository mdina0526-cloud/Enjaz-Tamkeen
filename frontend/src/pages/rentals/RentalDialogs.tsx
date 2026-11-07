import { useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { toastError } from '@/hooks/useApi'
import { formatMoney, newIdempotencyKey, todayIso } from '@/lib/utils'
import { propertyTypeLabel } from '@/lib/labels'
import type { RentalDetail } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input, NativeSelect, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'

const rentalSchema = z.object({
  title: z.string().trim().min(2, 'اسم العقار/الموقع مطلوب').max(200),
  propertyType: z.enum(['Property', 'Office', 'Site', 'Other']),
  location: z.string().max(300).optional(),
  beneficiaryName: z.string().trim().min(2, 'اسم المستفيد مطلوب').max(150),
  beneficiaryPhone: z.string().max(32).optional(),
  amount: z.coerce.number<number>({ message: 'أدخل مبلغاً' }).gt(0, 'قيمة الإيجار يجب أن تكون أكبر من صفر').max(100_000_000),
  dueDate: z.string().min(1, 'تاريخ الاستحقاق مطلوب'),
  notes: z.string().max(1000).optional(),
})
type RentalValues = z.infer<typeof rentalSchema>

export function RentalFormDialog({ open, onOpenChange, rental, onSaved }: { open: boolean; onOpenChange: (o: boolean) => void; rental?: RentalDetail; onSaved: (r: RentalDetail) => void }) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<RentalValues>({ resolver: zodResolver(rentalSchema) })

  useEffect(() => {
    if (open)
      reset({
        title: rental?.title ?? '',
        propertyType: rental?.propertyType ?? 'Property',
        location: rental?.location ?? '',
        beneficiaryName: rental?.beneficiaryName ?? '',
        beneficiaryPhone: rental?.beneficiaryPhone ?? '',
        amount: rental?.amount ?? 0,
        dueDate: rental?.dueDate ?? todayIso(),
        notes: rental?.notes ?? '',
      })
  }, [open, rental, reset])

  const onSubmit = async (v: RentalValues) => {
    const body = { ...v, location: v.location || null, beneficiaryPhone: v.beneficiaryPhone || null, notes: v.notes || null }
    try {
      const saved = rental ? await api.put<RentalDetail>(`/api/rentals/${rental.id}`, body) : await api.post<RentalDetail>('/api/rentals', body)
      toast.success(rental ? 'تم تحديث بيانات الإيجار' : 'تمت إضافة الإيجار')
      onSaved(saved)
      onOpenChange(false)
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{rental ? 'تعديل الإيجار' : 'إضافة إيجار'}</DialogTitle>
          <DialogDescription>عقار أو مكتب أو موقع مستأجر مع قيمة الإيجار وتاريخ استحقاقه.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-2" noValidate>
          <Field label="اسم العقار / المكتب / الموقع *" error={errors.title?.message} className="sm:col-span-2">
            <Input aria-invalid={!!errors.title} {...register('title')} />
          </Field>
          <Field label="النوع" error={errors.propertyType?.message}>
            <NativeSelect {...register('propertyType')}>
              {Object.entries(propertyTypeLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </NativeSelect>
          </Field>
          <Field label="الموقع / العنوان" error={errors.location?.message}>
            <Input {...register('location')} />
          </Field>
          <Field label="اسم المستفيد (المؤجر) *" error={errors.beneficiaryName?.message}>
            <Input aria-invalid={!!errors.beneficiaryName} {...register('beneficiaryName')} />
          </Field>
          <Field label="جوال المستفيد" error={errors.beneficiaryPhone?.message}>
            <Input dir="ltr" inputMode="tel" {...register('beneficiaryPhone')} />
          </Field>
          <Field label="قيمة الإيجار (ر.س) *" error={errors.amount?.message}>
            <Input type="number" step="0.01" min={0} className="num" aria-invalid={!!errors.amount} {...register('amount')} />
          </Field>
          <Field label="تاريخ الاستحقاق *" error={errors.dueDate?.message}>
            <Input type="date" aria-invalid={!!errors.dueDate} {...register('dueDate')} />
          </Field>
          <Field label="ملاحظات" error={errors.notes?.message} className="sm:col-span-2">
            <Textarea rows={2} {...register('notes')} />
          </Field>
          <DialogFooter className="sm:col-span-2">
            <Button type="submit" loading={isSubmitting}>{rental ? 'حفظ التعديلات' : 'إضافة'}</Button>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>إلغاء</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

const MAX_FILE = 5 * 1024 * 1024

export function RentalPaymentDialog({ open, onOpenChange, rental, onSaved }: { open: boolean; onOpenChange: (o: boolean) => void; rental: RentalDetail; onSaved: (r: RentalDetail) => void }) {
  const idempotencyKey = useRef(newIdempotencyKey())
  const schema = z.object({
    amount: z.coerce.number<number>({ message: 'أدخل مبلغاً' }).gt(0, 'المبلغ يجب أن يكون أكبر من صفر').max(rental.remainingAmount, `المبلغ يتجاوز المتبقي (${formatMoney(rental.remainingAmount)})`),
    method: z.enum(['Cash', 'Card']),
    paidOn: z.string().min(1, 'تاريخ السداد مطلوب').refine((d) => d <= todayIso(), 'لا يمكن أن يكون التاريخ في المستقبل'),
    reference: z.string().max(100).optional(),
    notes: z.string().max(500).optional(),
    attachment: z
      .custom<FileList>()
      .optional()
      .refine((f) => !f || f.length === 0 || f[0].size <= MAX_FILE, 'حجم الملف يتجاوز 5 ميجابايت')
      .refine((f) => !f || f.length === 0 || /\.(pdf|jpe?g|png|webp)$/i.test(f[0].name), 'الأنواع المسموحة: PDF, JPG, PNG, WEBP'),
  })
  type Values = z.infer<typeof schema>
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (open) {
      idempotencyKey.current = newIdempotencyKey()
      reset({ amount: rental.remainingAmount, method: 'Card', paidOn: todayIso(), reference: '', notes: '' })
    }
  }, [open, rental, reset])

  const onSubmit = async (v: Values) => {
    const fd = new FormData()
    fd.set('amount', String(v.amount))
    fd.set('method', v.method)
    fd.set('paidOn', v.paidOn)
    if (v.reference) fd.set('reference', v.reference)
    if (v.notes) fd.set('notes', v.notes)
    if (v.attachment && v.attachment.length > 0) fd.set('attachment', v.attachment[0])
    try {
      const saved = await api.post<RentalDetail>(`/api/rentals/${rental.id}/payments`, fd, { 'Idempotency-Key': idempotencyKey.current })
      toast.success('تم تسجيل الدفعة')
      onSaved(saved)
      onOpenChange(false)
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>تسجيل دفعة إيجار</DialogTitle>
          <DialogDescription>
            {rental.title} — المتبقي: <span className="num font-semibold">{formatMoney(rental.remainingAmount)}</span>
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-2" noValidate>
          <Field label="المبلغ *" error={errors.amount?.message}>
            <Input type="number" step="0.01" min={0} className="num" aria-invalid={!!errors.amount} {...register('amount')} />
          </Field>
          <Field label="طريقة الدفع" error={errors.method?.message}>
            <NativeSelect {...register('method')}>
              <option value="Card">تحويل / شبكة</option>
              <option value="Cash">نقداً</option>
            </NativeSelect>
          </Field>
          <Field label="تاريخ السداد *" error={errors.paidOn?.message}>
            <Input type="date" max={todayIso()} {...register('paidOn')} />
          </Field>
          <Field label="رقم المرجع / الحوالة" error={errors.reference?.message}>
            <Input dir="ltr" {...register('reference')} />
          </Field>
          <Field label="إيصال الدفع (اختياري)" error={errors.attachment?.message as string | undefined} hint="PDF أو صورة، حتى 5 ميجابايت" className="sm:col-span-2">
            <Input type="file" accept=".pdf,.jpg,.jpeg,.png,.webp" className="cursor-pointer pt-1.5" {...register('attachment')} />
          </Field>
          <Field label="ملاحظات" error={errors.notes?.message} className="sm:col-span-2">
            <Textarea rows={2} {...register('notes')} />
          </Field>
          <DialogFooter className="sm:col-span-2">
            <Button type="submit" loading={isSubmitting}>تسجيل الدفعة</Button>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>إلغاء</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
