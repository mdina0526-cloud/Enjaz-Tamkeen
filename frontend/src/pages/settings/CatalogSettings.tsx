import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { Pencil, Plus } from 'lucide-react'
import { api } from '@/lib/api'
import { invalidate, toastError, useOrderTypes, useServices } from '@/hooks/useApi'
import type { OrderTypeItem, ServiceItem } from '@/types/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input, NativeSelect, Textarea } from '@/components/ui/input'
import { Field, Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/misc'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { EmptyState, ErrorState, Money, TableSkeleton } from '@/components/common'

/** Admin management of order types and the service catalog used by the new-order form. */
export function CatalogSettings() {
  return (
    <div className="grid gap-6 xl:grid-cols-5">
      <div className="xl:col-span-2"><OrderTypesCard /></div>
      <div className="xl:col-span-3"><ServicesCard /></div>
    </div>
  )
}

function StatusBadge({ active }: { active: boolean }) {
  return <Badge variant={active ? 'success' : 'secondary'}>{active ? 'مفعّل' : 'معطّل'}</Badge>
}

// ---------- Order types ----------

const typeSchema = z.object({
  code: z.string().trim().regex(/^[A-Za-z][A-Za-z0-9_-]{1,31}$/, 'رمز إنجليزي من 2-32 حرفاً يبدأ بحرف'),
  name: z.string().trim().min(2, 'الاسم مطلوب').max(100),
  sortOrder: z.coerce.number<number>().int().min(0).max(1000),
  isActive: z.boolean(),
})
type TypeValues = z.infer<typeof typeSchema>

function OrderTypesCard() {
  const types = useOrderTypes(true)
  const [editing, setEditing] = useState<OrderTypeItem | 'new' | null>(null)

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <div>
          <CardTitle>أنواع الطلبات</CardTitle>
          <CardDescription>تظهر في نموذج الطلب الجديد وفي الفلاتر والتقارير</CardDescription>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}><Plus /> نوع جديد</Button>
      </CardHeader>
      {types.error ? (
        <ErrorState message={types.error} onRetry={types.reload} />
      ) : types.loading ? (
        <TableSkeleton rows={3} cols={3} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>الاسم</TableHead>
              <TableHead>الرمز</TableHead>
              <TableHead>الحالة</TableHead>
              <TableHead className="w-12"><span className="sr-only">تعديل</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {types.data?.map((t) => (
              <TableRow key={t.id}>
                <TableCell className="font-medium">{t.name}</TableCell>
                <TableCell className="num text-muted-foreground" dir="ltr">{t.code}</TableCell>
                <TableCell><StatusBadge active={t.isActive} /></TableCell>
                <TableCell>
                  <Button variant="ghost" size="icon" aria-label={`تعديل ${t.name}`} onClick={() => setEditing(t)}><Pencil /></Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <OrderTypeDialog item={editing} onClose={() => setEditing(null)} />
    </Card>
  )
}

function OrderTypeDialog({ item, onClose }: { item: OrderTypeItem | 'new' | null; onClose: () => void }) {
  const isNew = item === 'new'
  const { register, handleSubmit, reset, setValue, watch, formState: { errors, isSubmitting } } = useForm<TypeValues>({ resolver: zodResolver(typeSchema) })
  useEffect(() => {
    if (item) reset(isNew ? { code: '', name: '', sortOrder: 0, isActive: true } : { code: item.code, name: item.name, sortOrder: item.sortOrder, isActive: item.isActive })
  }, [item, isNew, reset])

  const onSubmit = async (v: TypeValues) => {
    try {
      if (isNew) await api.post('/api/catalog/order-types', v)
      else if (item) await api.put(`/api/catalog/order-types/${item.id}`, v)
      await invalidate('catalog')
      toast.success(isNew ? 'تمت إضافة نوع الطلب' : 'تم حفظ التعديلات')
      onClose()
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={item !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
          <DialogHeader>
            <DialogTitle>{isNew ? 'إضافة نوع طلب' : 'تعديل نوع الطلب'}</DialogTitle>
            <DialogDescription>الأنواع المعطلة لا تظهر للموظفين، وتبقى العمليات السابقة كما هي.</DialogDescription>
          </DialogHeader>
          <Field label="الاسم" error={errors.name?.message}><Input placeholder="مثال: صيانة" {...register('name')} /></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="الرمز (إنجليزي)" error={errors.code?.message}><Input dir="ltr" placeholder="Maintenance" {...register('code')} /></Field>
            <Field label="ترتيب العرض" error={errors.sortOrder?.message}><Input type="number" className="num" {...register('sortOrder')} /></Field>
          </div>
          <div className="flex items-center justify-between rounded-lg border p-3">
            <Label htmlFor="type-active">مفعّل</Label>
            <Switch id="type-active" checked={watch('isActive') ?? true} onCheckedChange={(c) => setValue('isActive', c)} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>إلغاء</Button>
            <Button type="submit" loading={isSubmitting}>حفظ</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ---------- Services ----------

const serviceSchema = z.object({
  name: z.string().trim().min(2, 'اسم الخدمة مطلوب').max(200),
  description: z.string().max(500).optional(),
  defaultPrice: z.union([z.literal(''), z.coerce.number<number>().gt(0, 'السعر يجب أن يكون أكبر من صفر').max(100_000_000)]).optional(),
  orderTypeId: z.string().optional(),
  sortOrder: z.coerce.number<number>().int().min(0).max(1000),
  isActive: z.boolean(),
})
type ServiceValues = z.infer<typeof serviceSchema>

function ServicesCard() {
  const services = useServices(true)
  const [editing, setEditing] = useState<ServiceItem | 'new' | null>(null)

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between">
        <div>
          <CardTitle>الخدمات</CardTitle>
          <CardDescription>قائمة "نوع الخدمة" في نموذج الطلب، مع سعر مقترح اختياري</CardDescription>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}><Plus /> خدمة جديدة</Button>
      </CardHeader>
      {services.error ? (
        <ErrorState message={services.error} onRetry={services.reload} />
      ) : services.loading ? (
        <TableSkeleton rows={4} cols={4} />
      ) : services.data?.length ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>الخدمة</TableHead>
              <TableHead>نوع الطلب</TableHead>
              <TableHead className="text-end">السعر المقترح</TableHead>
              <TableHead>الحالة</TableHead>
              <TableHead className="w-12"><span className="sr-only">تعديل</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {services.data.map((s) => (
              <TableRow key={s.id}>
                <TableCell className="whitespace-normal">
                  <p className="font-medium">{s.name}</p>
                  {s.description && <p className="text-xs text-muted-foreground">{s.description}</p>}
                </TableCell>
                <TableCell className="text-muted-foreground">{s.orderTypeName ?? 'كل الأنواع'}</TableCell>
                <TableCell className="text-end">{s.defaultPrice ? <Money value={s.defaultPrice} /> : '—'}</TableCell>
                <TableCell><StatusBadge active={s.isActive} /></TableCell>
                <TableCell>
                  <Button variant="ghost" size="icon" aria-label={`تعديل ${s.name}`} onClick={() => setEditing(s)}><Pencil /></Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <EmptyState title="لا توجد خدمات" description="أضف أول خدمة ليتمكن الموظفون من تسجيل الطلبات" />
      )}
      <ServiceDialog item={editing} onClose={() => setEditing(null)} />
    </Card>
  )
}

function ServiceDialog({ item, onClose }: { item: ServiceItem | 'new' | null; onClose: () => void }) {
  const isNew = item === 'new'
  const types = useOrderTypes(true)
  const { register, handleSubmit, reset, setValue, watch, formState: { errors, isSubmitting } } = useForm<ServiceValues>({ resolver: zodResolver(serviceSchema) })
  useEffect(() => {
    if (!item) return
    reset(isNew
      ? { name: '', description: '', defaultPrice: '', orderTypeId: '', sortOrder: 0, isActive: true }
      : { name: item.name, description: item.description ?? '', defaultPrice: item.defaultPrice ?? '', orderTypeId: item.orderTypeId ? String(item.orderTypeId) : '', sortOrder: item.sortOrder, isActive: item.isActive })
  }, [item, isNew, reset])

  const onSubmit = async (v: ServiceValues) => {
    const body = {
      name: v.name,
      description: v.description?.trim() || null,
      defaultPrice: v.defaultPrice === '' || v.defaultPrice === undefined ? null : v.defaultPrice,
      orderTypeId: v.orderTypeId ? Number(v.orderTypeId) : null,
      sortOrder: v.sortOrder,
      isActive: v.isActive,
    }
    try {
      if (isNew) await api.post('/api/catalog/services', body)
      else if (item) await api.put(`/api/catalog/services/${item.id}`, body)
      await invalidate('catalog')
      toast.success(isNew ? 'تمت إضافة الخدمة' : 'تم حفظ التعديلات')
      onClose()
    } catch (e) {
      toastError(e)
    }
  }

  return (
    <Dialog open={item !== null} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
          <DialogHeader>
            <DialogTitle>{isNew ? 'إضافة خدمة' : 'تعديل الخدمة'}</DialogTitle>
            <DialogDescription>تغيير اسم الخدمة لا يغيّر أسماءها في الطلبات المسجلة سابقاً.</DialogDescription>
          </DialogHeader>
          <Field label="اسم الخدمة" error={errors.name?.message}><Input {...register('name')} /></Field>
          <Field label="الوصف" error={errors.description?.message}><Textarea rows={2} {...register('description')} /></Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="السعر المقترح (اختياري)" error={errors.defaultPrice?.message}>
              <Input type="number" step="0.01" min={0} className="num" {...register('defaultPrice')} />
            </Field>
            <Field label="نوع الطلب" hint="اتركه فارغاً لكل الأنواع">
              <NativeSelect {...register('orderTypeId')}>
                <option value="">كل الأنواع</option>
                {types.data?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </NativeSelect>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="ترتيب العرض" error={errors.sortOrder?.message}><Input type="number" className="num" {...register('sortOrder')} /></Field>
            <div className="flex items-center justify-between self-end rounded-lg border p-3">
              <Label htmlFor="service-active">مفعّلة</Label>
              <Switch id="service-active" checked={watch('isActive') ?? true} onCheckedChange={(c) => setValue('isActive', c)} />
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>إلغاء</Button>
            <Button type="submit" loading={isSubmitting}>حفظ</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
