import { useEffect } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Save } from 'lucide-react'
import { api } from '@/lib/api'
import { toastError, useApiQuery } from '@/hooks/useApi'
import { cn, formatMoney } from '@/lib/utils'
import { treatmentLabel } from '@/lib/labels'
import type { DeductionSettings, DeductionTreatment } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Field, Label } from '@/components/ui/label'
import { Skeleton, Switch } from '@/components/ui/misc'
import { ErrorState, PageHeader } from '@/components/common'
import { CatalogSettings } from './settings/CatalogSettings'

const schema = z.object({
  enabled: z.boolean(),
  treatment: z.enum(['SubtractFromNet', 'RecordOnly']),
  label: z.string().trim().min(2, 'المسمى مطلوب').max(50),
})

export function SettingsPage() {
  const { data, loading, error, reload } = useApiQuery(['settings', 'deduction'], () => api.get<DeductionSettings>('/api/settings/deduction'))
  const { register, control, handleSubmit, reset, formState: { errors, isSubmitting, isDirty } } = useForm<DeductionSettings>({ resolver: zodResolver(schema) })
  const values = useWatch({ control })

  useEffect(() => { if (data) reset(data) }, [data, reset])

  const onSubmit = async (v: DeductionSettings) => {
    try {
      reset(await api.put<DeductionSettings>('/api/settings/deduction', v))
      toast.success('تم حفظ الإعدادات')
    } catch (e) {
      toastError(e)
    }
  }

  if (error) return <ErrorState message={error} onRetry={reload} />

  const deduction = values.enabled ? 50 : 0
  const net = values.treatment === 'RecordOnly' ? 1000 : 1000 - deduction

  return (
    <div className="space-y-6">
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <PageHeader title="إعدادات النظام" description="سياسة الاستقطاع، وأنواع الطلبات، وقائمة الخدمات" actions={<Button type="submit" loading={isSubmitting} disabled={!isDirty}><Save /> حفظ</Button>} />
      {loading ? (
        <Skeleton className="h-80" />
      ) : (
        <div className="grid gap-6 lg:grid-cols-3">
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>سياسة الاستقطاع</CardTitle>
              <CardDescription>تُحدَّد النسبة لكل موظف من صفحة الموظفين، وهنا تحدد كيفية تطبيقها. تؤثر التغييرات على الطلبات الجديدة فقط.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="flex items-center justify-between rounded-xl border p-4">
                <div>
                  <Label htmlFor="enabled">تفعيل الاستقطاع</Label>
                  <p className="mt-1 text-xs text-muted-foreground">عند التعطيل تُسجّل الطلبات بنسبة 0%</p>
                </div>
                <Controller control={control} name="enabled" render={({ field }) => <Switch id="enabled" checked={field.value} onCheckedChange={field.onChange} />} />
              </div>
              <Field label="مسمى الاستقطاع" error={errors.label?.message} hint="مثال: استقطاع، عمولة، رسوم خدمة، حصة الشركة">
                <Input {...register('label')} />
              </Field>
              <div className="space-y-2">
                <Label>طريقة التطبيق</Label>
                <Controller
                  control={control}
                  name="treatment"
                  render={({ field }) => (
                    <div className="grid gap-3 sm:grid-cols-2">
                      {(Object.keys(treatmentLabel) as DeductionTreatment[]).map((t) => (
                        <button
                          type="button"
                          key={t}
                          onClick={() => field.onChange(t)}
                          className={cn('rounded-xl border-2 p-4 text-start text-sm transition-colors', field.value === t ? 'border-primary bg-primary/5' : 'hover:border-primary/40')}
                        >
                          <p className="font-semibold">{t === 'SubtractFromNet' ? 'يُطرح من المبلغ' : 'للتسجيل فقط'}</p>
                          <p className="mt-1 text-xs text-muted-foreground">{treatmentLabel[t]}</p>
                        </button>
                      ))}
                    </div>
                  )}
                />
              </div>
            </CardContent>
          </Card>
          <Card className="h-fit">
            <CardHeader>
              <CardTitle>معاينة</CardTitle>
              <CardDescription>عملية بقيمة 1,000 ونسبة 5%</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex justify-between"><span>المبلغ الأساسي</span><span className="num font-semibold">{formatMoney(1000)}</span></div>
              <div className="flex justify-between"><span>{values.label || 'الاستقطاع'}</span><span className="num font-semibold text-warning">{formatMoney(deduction)}</span></div>
              <div className="flex justify-between border-t pt-3"><span>المبلغ بعد الاستقطاع</span><span className="num text-base font-bold text-primary">{formatMoney(net)}</span></div>
            </CardContent>
          </Card>
        </div>
      )}
    </form>
    <CatalogSettings />
    </div>
  )
}
