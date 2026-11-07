import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { KeyRound, Save } from 'lucide-react'
import { api } from '@/lib/api'
import { invalidate, toastError, useApiQuery } from '@/hooks/useApi'
import { useAuth } from '@/context/AuthContext'
import { formatDateTime, formatMoney, formatNumber } from '@/lib/utils'
import type { EmployeeDashboard, UserInfo } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Avatar } from '@/components/ui/misc'
import { InfoRow, PageHeader } from '@/components/common'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'كلمة المرور الحالية مطلوبة'),
    newPassword: z.string().min(8, 'كلمة المرور يجب ألا تقل عن 8 أحرف').max(128).regex(/[A-Za-z؀-ۿ]/, 'يجب أن تحتوي على حروف').regex(/\d/, 'يجب أن تحتوي على أرقام'),
    confirm: z.string(),
  })
  .refine((v) => v.newPassword === v.confirm, { message: 'كلمتا المرور غير متطابقتين', path: ['confirm'] })
type Values = z.infer<typeof schema>

const contactSchema = z.object({
  email: z.union([z.literal(''), z.email('البريد الإلكتروني غير صالح').max(256)]),
  phone: z.string().trim().max(32, 'الحد الأقصى 32 رقماً').regex(/^[0-9+\-\s]*$/, 'رقم الهاتف غير صالح'),
})
type ContactValues = z.infer<typeof contactSchema>

/** Only contact details are self-service; name, role and deduction rate are managed by the admin. */
function ContactForm() {
  const { user, refresh } = useAuth()
  const { register, handleSubmit, reset, formState: { errors, isSubmitting, isDirty } } = useForm<ContactValues>({
    resolver: zodResolver(contactSchema),
    values: { email: user?.email ?? '', phone: user?.phone ?? '' },
  })
  const onSubmit = async (v: ContactValues) => {
    try {
      const updated = await api.put<UserInfo>('/api/auth/me', { email: v.email || null, phone: v.phone || null })
      reset({ email: updated.email ?? '', phone: updated.phone ?? '' })
      await refresh()
      void invalidate('employees')
      toast.success('تم تحديث بيانات التواصل')
    } catch (e) {
      toastError(e)
    }
  }
  return (
    <Card>
      <CardHeader>
        <CardTitle>بيانات التواصل</CardTitle>
        <CardDescription>يمكنك تعديل بريدك الإلكتروني ورقم هاتفك. لتعديل الاسم أو المسمى الوظيفي تواصل مع المدير.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} className="grid max-w-xl gap-4 sm:grid-cols-2" noValidate>
          <Field label="البريد الإلكتروني" error={errors.email?.message}>
            <Input type="email" dir="ltr" autoComplete="email" {...register('email')} />
          </Field>
          {user?.employeeId && (
            <Field label="رقم الهاتف" error={errors.phone?.message}>
              <Input type="tel" dir="ltr" autoComplete="tel" {...register('phone')} />
            </Field>
          )}
          <div className="sm:col-span-2">
            <Button type="submit" loading={isSubmitting} disabled={!isDirty}><Save /> حفظ</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  )
}

export function ProfilePage() {
  const { user, isAdmin, logout } = useAuth()
  const stats = useApiQuery(['dashboard', 'employee'], () => api.get<EmployeeDashboard>('/api/dashboard/employee'), { enabled: !isAdmin })
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(schema) })

  const onSubmit = async (v: Values) => {
    try {
      await api.post('/api/auth/change-password', { currentPassword: v.currentPassword, newPassword: v.newPassword })
      reset()
      toast.success('تم تغيير كلمة المرور. يرجى تسجيل الدخول مجدداً')
      logout()
    } catch (e) {
      toastError(e)
    }
  }

  if (!user) return null
  return (
    <div className="space-y-6">
      <PageHeader title="الملف الشخصي" description="بيانات حسابك وإعدادات الأمان" />
      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardContent className="pt-6">
            <div className="mb-5 flex flex-col items-center gap-3 text-center">
              <Avatar name={user.fullName} className="size-20 text-2xl" />
              <div>
                <p className="text-lg font-semibold">{user.fullName}</p>
                <p className="num text-sm text-muted-foreground">@{user.username}</p>
              </div>
              <Badge>{isAdmin ? 'المدير العام' : 'موظف'}</Badge>
            </div>
            {user.employeeCode && <InfoRow label="الرقم الوظيفي"><span className="num">{user.employeeCode}</span></InfoRow>}
            {user.jobTitle && <InfoRow label="المسمى الوظيفي">{user.jobTitle}</InfoRow>}
            <InfoRow label="البريد الإلكتروني"><span className="num">{user.email ?? '—'}</span></InfoRow>
            {user.employeeId && <InfoRow label="رقم الهاتف"><span className="num" dir="ltr">{user.phone ?? '—'}</span></InfoRow>}
            <InfoRow label="آخر دخول سابق">{formatDateTime(user.lastLoginAtUtc)}</InfoRow>
          </CardContent>
        </Card>

        <div className="space-y-6 lg:col-span-2">
          {stats.data && (
            <Card>
              <CardHeader>
                <CardTitle>سجلي</CardTitle>
                <CardDescription>ملخص عملياتي منذ بداية العمل</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-4">
                {[
                  { l: 'عدد العمليات', v: formatNumber(stats.data.ordersCount) },
                  { l: 'إجمالي المبيعات', v: formatMoney(stats.data.totalSales) },
                  { l: `إجمالي ${stats.data.deductionLabel}`, v: formatMoney(stats.data.totalDeductions) },
                  { l: 'النسبة الحالية', v: `${stats.data.deductionPercentage}%` },
                ].map((s) => (
                  <div key={s.l} className="rounded-xl bg-muted/50 p-3">
                    <p className="text-xs text-muted-foreground">{s.l}</p>
                    <p className="num mt-1 font-bold">{s.v}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
          <ContactForm />
          <Card>
            <CardHeader>
              <CardTitle>تغيير كلمة المرور</CardTitle>
              <CardDescription>سيتم تسجيل خروجك من جميع الأجهزة بعد التغيير</CardDescription>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit(onSubmit)} className="grid max-w-xl gap-4" noValidate>
                <Field label="كلمة المرور الحالية" error={errors.currentPassword?.message}>
                  <Input type="password" dir="ltr" autoComplete="current-password" {...register('currentPassword')} />
                </Field>
                <Field label="كلمة المرور الجديدة" error={errors.newPassword?.message} hint="8 أحرف على الأقل، حروف وأرقام">
                  <Input type="password" dir="ltr" autoComplete="new-password" {...register('newPassword')} />
                </Field>
                <Field label="تأكيد كلمة المرور الجديدة" error={errors.confirm?.message}>
                  <Input type="password" dir="ltr" autoComplete="new-password" {...register('confirm')} />
                </Field>
                <div>
                  <Button type="submit" loading={isSubmitting}><KeyRound /> تغيير كلمة المرور</Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
