import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { PlusCircle, Search, X } from 'lucide-react'
import { api } from '@/lib/api'
import { useApiQuery, useDebounced, useOrderTypes, useServices } from '@/hooks/useApi'
import { useAuth } from '@/context/AuthContext'
import { orderStatusLabel, paymentMethodLabel } from '@/lib/labels'
import type { EmployeeLookup, OrderListItem, PagedResult } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input, NativeSelect } from '@/components/ui/input'
import { EmptyState, ErrorState, PageHeader, Pagination, TableSkeleton } from '@/components/common'
import { DateRangeFilter } from '@/components/filters'
import { OrdersTable, type OrderSort, type OrderSortKey } from '@/components/OrdersTable'

const PAGE_SIZE = 20

export function OrdersPage() {
  const { isAdmin } = useAuth()
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState(params.get('search') ?? '')
  const debounced = useDebounced(search)

  const filters = {
    from: params.get('from') ?? '',
    to: params.get('to') ?? '',
    employeeId: params.get('employeeId') ?? '',
    status: params.get('status') ?? '',
    paymentMethod: params.get('paymentMethod') ?? '',
    orderTypeId: params.get('orderTypeId') ?? '',
    serviceTypeId: params.get('serviceTypeId') ?? '',
    sortBy: params.get('sortBy') ?? '',
    sortDir: params.get('sortDir') ?? '',
    page: Number(params.get('page') ?? 1),
  }

  const update = (patch: Record<string, string | number>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (v === '' || v === undefined) next.delete(k)
      else next.set(k, String(v))
    }
    if (!('page' in patch)) next.delete('page')
    setParams(next, { replace: true })
  }

  const employees = useApiQuery(['employees', 'lookup'], () => api.get<EmployeeLookup[]>('/api/employees/lookup'), { enabled: isAdmin })
  const orderTypes = useOrderTypes()
  const services = useServices()
  const { data, loading, fetching, error, reload } = useApiQuery(
    ['orders', 'list', params.toString(), debounced],
    () => api.get<PagedResult<OrderListItem>>('/api/orders', { ...filters, search: debounced, pageSize: PAGE_SIZE }),
    { keepPrevious: true },
  )
  const sort: OrderSort = { by: (filters.sortBy || 'date') as OrderSortKey, dir: filters.sortDir === 'asc' ? 'asc' : 'desc' }
  const hasFilters = !!(filters.from || filters.to || filters.employeeId || filters.status || filters.paymentMethod || filters.orderTypeId || filters.serviceTypeId || debounced)

  return (
    <div>
      <PageHeader
        title={isAdmin ? 'إدارة العمليات' : 'طلباتي'}
        description={isAdmin ? 'السجل المركزي لجميع عمليات الموظفين' : 'جميع الطلبات التي قمت بتسجيلها وحالتها'}
        actions={
          <Button asChild>
            <Link to="/orders/new"><PlusCircle /> طلب جديد</Link>
          </Button>
        }
      />

      <Card>
        <div className="flex flex-col gap-3 border-b p-4">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative min-w-56 flex-1">
              <Search className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input className="pr-9" placeholder="بحث برقم الطلب أو الخدمة أو الملاحظات..." value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
            <DateRangeFilter from={filters.from} to={filters.to} onChange={(r) => update(r)} />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {isAdmin && (
              <NativeSelect aria-label="الموظف" className="w-48" value={filters.employeeId} onChange={(e) => update({ employeeId: e.target.value })}>
                <option value="">كل الموظفين</option>
                {employees.data?.map((e) => (
                  <option key={e.id} value={e.id}>{e.fullName}</option>
                ))}
              </NativeSelect>
            )}
            <NativeSelect aria-label="نوع العملية" className="w-40" value={filters.orderTypeId} onChange={(e) => update({ orderTypeId: e.target.value })}>
              <option value="">كل الأنواع</option>
              {orderTypes.data?.map((t) => (
                <option key={t.id} value={t.id}>{t.name}</option>
              ))}
            </NativeSelect>
            <NativeSelect aria-label="الخدمة" className="w-44" value={filters.serviceTypeId} onChange={(e) => update({ serviceTypeId: e.target.value })}>
              <option value="">كل الخدمات</option>
              {services.data?.map((sv) => (
                <option key={sv.id} value={sv.id}>{sv.name}</option>
              ))}
            </NativeSelect>
            <NativeSelect aria-label="طريقة الدفع" className="w-44" value={filters.paymentMethod} onChange={(e) => update({ paymentMethod: e.target.value })}>
              <option value="">كل طرق الدفع</option>
              {Object.entries(paymentMethodLabel).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
            <NativeSelect aria-label="الحالة" className="w-40" value={filters.status} onChange={(e) => update({ status: e.target.value })}>
              <option value="">كل الحالات</option>
              {Object.entries(orderStatusLabel).map(([k, v]) => (
                <option key={k} value={k}>{v}</option>
              ))}
            </NativeSelect>
            {hasFilters && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSearch('')
                  setParams(new URLSearchParams(), { replace: true })
                }}
              >
                <X /> مسح الفلاتر
              </Button>
            )}
          </div>
        </div>

        {error ? (
          <ErrorState message={error} onRetry={reload} />
        ) : loading && !data ? (
          <TableSkeleton />
        ) : data && data.items.length > 0 ? (
          <div className={fetching ? 'opacity-60 transition-opacity' : 'transition-opacity'}>
            <OrdersTable orders={data.items} showEmployee={isAdmin} sort={sort} onSort={(s) => update({ sortBy: s.by, sortDir: s.dir })} />
            <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onChange={(p) => update({ page: p })} />
          </div>
        ) : (
          <EmptyState
            title={hasFilters ? 'لا توجد نتائج مطابقة' : 'لا توجد طلبات بعد'}
            description={hasFilters ? 'جرّب تعديل الفلاتر أو البحث' : 'ابدأ بتسجيل أول طلب مبيعات'}
            action={!hasFilters && <Button asChild><Link to="/orders/new"><PlusCircle /> طلب جديد</Link></Button>}
          />
        )}
      </Card>
    </div>
  )
}
