import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { api, ApiError } from '@/lib/api'
import { toastError } from '@/hooks/useApi'
import type { EmployeeDetail } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'

const password = z.string().min(8, 'كلمة المرور يجب ألا تقل عن 8 أحرف').max(128).regex(/[A-Za-z؀-ۿ]/, 'يجب أن تحتوي على حروف').regex(/\d/, 'يجب أن تحتوي على أرقام')
const pct = z.coerce.number<number>({ message: 'أدخل نسبة صحيحة' }).min(0, 'الحد الأدنى 0').max(100, 'الحد الأقصى 100')

const profileFields = {
  fullName: z.string().trim().min(2, 'الاسم الكامل مطلوب').max(150),
  email: z.union([z.literal(''), z.email('البريد الإلكتروني غير صالح').max(256)]),
  phone: z.string().max(32).optional(),
  jobTitle: z.string().max(100).optional(),
  hireDate: z.string().optional(),
}

const createSchema = z.object({
  ...profileFields,
  username: z.string().trim().regex(/^[a-zA-Z0-9._-]{3,64}$/, 'اسم المستخدم: 3-64 حرفاً إنجليزياً أو أرقاماً أو (. _ -)'),
  password,
  deductionPercentage: pct,
})
const editSchema = z.object(profileFields)

type CreateValues = z.infer<typeof createSchema>
type EditValues = z.infer<typeof editSchema>

function applyServerErrors(e: unknown, setError: (name: never, err: { message: string }) => void) {
  if (e instanceof ApiError) {
    for (const [k, msgs] of Object.entries(e.fieldErrors)) {
      const name = k.charAt(0).toLowerCase() + k.slice(1)
      setError(name as never, { message: msgs[0] })
    }
  }
}

export function EmployeeFormDialog({ open, onOpenChange, employee, onSaved }: { open: boolean; onOpenChange: (o: boolean) => void; employee?: EmployeeDetail; onSaved: (e: EmployeeDetail) => void }) {
  const isEdit = !!employee
  const form = useForm<CreateValues>({
    resolver: zodResolver((isEdit ? editSchema : createSchema) as typeof createSchema),
    defaultValues: { fullName: '', username: '', email: '', password: '', phone: '', jobTitle: '', hireDate: '', deductionPercentage: 0 },
  })
  const { register, handleSubmit, reset, setError, formState: { errors, isSubmitting } } = form

  useEffect(() => {
    if (open)
      reset({
        fullName: employee?.fullName ?? '',
        username: employee?.username ?? '',
        email: employee?.email ?? '',
        password: '',
        phone: employee?.phone ?? '',
        jobTitle: employee?.jobTitle ?? '',
        hireDate: employee?.hireDate ?? '',
        deductionPercentage: employee?.deductionPercentage ?? 0,
      })
  }, [open, employee, reset])

  const onSubmit = async (v: CreateValues | EditValues) => {
    const profile = { fullName: v.fullName, email: v.email || null, phone: v.phone || null, jobTitle: v.jobTitle || null, hireDate: v.hireDate || null }
    try {
      const saved = isEdit
        ? await api.put<EmployeeDetail>(`/api/employees/${employee.id}`, profile)
        : await api.post<EmployeeDetail>('/api/employees', { ...profile, username: (v as CreateValues).username, password: (v as CreateValues).password, deductionPercentage: (v as CreateValues).deductionPercentage })
      toast.success(isEdit ? 'تم تحديث بيانات الموظف' : `تم إنشاء حساب الموظف ${saved.employeeCode}`)
      onSaved(saved)
      onOpenChange(false)
    } catch (e) {
      applyServerErrors(e, setError as never)
      toastError(e)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'تعديل بيانات الموظف' : 'إضافة موظف جديد'}</DialogTitle>
          <DialogDescription>{isEdit ? 'تحديث البيانات الأساسية. لتغيير كلمة المرور استخدم "إعادة تعيين كلمة المرور".' : 'سيتمكن الموظف من الدخول بحسابه المستقل فور الإنشاء.'}</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-2" noValidate>
          <Field label="الاسم الكامل *" error={errors.fullName?.message} className="sm:col-span-2">
            <Input aria-invalid={!!errors.fullName} {...register('fullName')} />
          </Field>
          {!isEdit && (
            <>
              <Field label="اسم المستخدم *" error={errors.username?.message}>
                <Input dir="ltr" autoComplete="off" aria-invalid={!!errors.username} {...register('username')} />
              </Field>
              <Field label="كلمة المرور *" error={errors.password?.message} hint="8 أحرف على الأقل، حروف وأرقام">
                <Input type="password" dir="ltr" autoComplete="new-password" aria-invalid={!!errors.password} {...register('password')} />
              </Field>
            </>
          )}
          <Field label="البريد الإلكتروني" error={errors.email?.message}>
            <Input type="email" dir="ltr" aria-invalid={!!errors.email} {...register('email')} />
          </Field>
          <Field label="رقم الجوال" error={errors.phone?.message}>
            <Input dir="ltr" inputMode="tel" {...register('phone')} />
          </Field>
          <Field label="المسمى الوظيفي" error={errors.jobTitle?.message}>
            <Input {...register('jobTitle')} />
          </Field>
          <Field label="تاريخ التعيين" error={errors.hireDate?.message}>
            <Input type="date" {...register('hireDate')} />
          </Field>
          {!isEdit && (
            <Field label="نسبة الاستقطاع %" error={errors.deductionPercentage?.message} hint="يمكن تعديلها لاحقاً مع الاحتفاظ بالسجل">
              <Input type="number" step="0.01" min={0} max={100} className="num" aria-invalid={!!errors.deductionPercentage} {...register('deductionPercentage')} />
            </Field>
          )}
          <DialogFooter className="sm:col-span-2">
            <Button type="submit" loading={isSubmitting}>{isEdit ? 'حفظ التعديلات' : 'إنشاء الحساب'}</Button>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>إلغاء</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

const resetSchema = z.object({ newPassword: password, confirm: z.string() }).refine((v) => v.newPassword === v.confirm, { message: 'كلمتا المرور غير متطابقتين', path: ['confirm'] })

export function ResetPasswordDialog({ open, onOpenChange, employee }: { open: boolean; onOpenChange: (o: boolean) => void; employee: EmployeeDetail }) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<z.infer<typeof resetSchema>>({ resolver: zodResolver(resetSchema) })
  useEffect(() => { if (open) reset({ newPassword: '', confirm: '' }) }, [open, reset])

  const onSubmit = async (v: z.infer<typeof resetSchema>) => {
    try {
      await api.post(`/api/employees/${employee.id}/reset-password`, { newPassword: v.newPassword })
      toast.success('تم تعيين كلمة المرور الجديدة وإنهاء الجلسات السابقة للموظف')
      onOpenChange(false)
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>إعادة تعيين كلمة المرور</DialogTitle>
          <DialogDescription>للموظف {employee.fullName}. سيتم تسجيل خروجه من جميع الأجهزة.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <Field label="كلمة المرور الجديدة" error={errors.newPassword?.message}>
            <Input type="password" dir="ltr" autoComplete="new-password" {...register('newPassword')} />
          </Field>
          <Field label="تأكيد كلمة المرور" error={errors.confirm?.message}>
            <Input type="password" dir="ltr" autoComplete="new-password" {...register('confirm')} />
          </Field>
          <DialogFooter>
            <Button type="submit" loading={isSubmitting}>تعيين</Button>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>إلغاء</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

const deductionSchema = z.object({ percentage: pct, notes: z.string().max(500).optional() })

export function DeductionDialog({ open, onOpenChange, employee, onSaved }: { open: boolean; onOpenChange: (o: boolean) => void; employee: EmployeeDetail; onSaved: () => void }) {
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<z.infer<typeof deductionSchema>>({ resolver: zodResolver(deductionSchema) })
  useEffect(() => { if (open) reset({ percentage: employee.deductionPercentage, notes: '' }) }, [open, employee, reset])

  const onSubmit = async (v: z.infer<typeof deductionSchema>) => {
    try {
      await api.put(`/api/employees/${employee.id}/deduction`, { percentage: v.percentage, notes: v.notes || null })
      toast.success('تم تحديث نسبة الاستقطاع')
      onSaved()
      onOpenChange(false)
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>تعديل نسبة الاستقطاع</DialogTitle>
          <DialogDescription>تُطبَّق النسبة الجديدة على الطلبات القادمة فقط؛ الطلبات السابقة تحتفظ بنسبتها وقيمتها.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <Field label="النسبة %" error={errors.percentage?.message}>
            <Input type="number" step="0.01" min={0} max={100} className="num" {...register('percentage')} />
          </Field>
          <Field label="ملاحظة (سبب التعديل)" error={errors.notes?.message}>
            <Textarea rows={2} {...register('notes')} />
          </Field>
          <DialogFooter>
            <Button type="submit" loading={isSubmitting}>حفظ</Button>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>إلغاء</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
