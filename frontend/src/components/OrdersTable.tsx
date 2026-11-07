import { useNavigate } from 'react-router-dom'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import type { OrderListItem } from '@/types/api'
import { cn, formatDate } from '@/lib/utils'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Money, OrderStatusBadge, PaymentBadge } from './common'

export type OrderSortKey = 'date' | 'number' | 'amount' | 'deduction' | 'net' | 'employee' | 'status'
export interface OrderSort {
  by: OrderSortKey
  dir: 'asc' | 'desc'
}

function SortHead({ label, k, sort, onSort, end }: { label: string; k: OrderSortKey; sort?: OrderSort; onSort?: (s: OrderSort) => void; end?: boolean }) {
  if (!onSort) return <TableHead className={end ? 'text-end' : undefined}>{label}</TableHead>
  const active = sort?.by === k
  const Icon = !active ? ArrowUpDown : sort.dir === 'asc' ? ArrowUp : ArrowDown
  return (
    <TableHead className={end ? 'text-end' : undefined} aria-sort={active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button
        type="button"
        className={cn('inline-flex items-center gap-1 rounded hover:text-foreground', active && 'text-foreground')}
        onClick={() => onSort({ by: k, dir: active && sort.dir === 'desc' ? 'asc' : 'desc' })}
      >
        {label}
        <Icon className={cn('size-3.5', !active && 'opacity-40')} />
      </button>
    </TableHead>
  )
}

export function OrdersTable({
  orders, showEmployee = true, compact = false, sort, onSort,
}: { orders: OrderListItem[]; showEmployee?: boolean; compact?: boolean; sort?: OrderSort; onSort?: (s: OrderSort) => void }) {
  const navigate = useNavigate()
  const p = { sort, onSort }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <SortHead label="رقم العملية" k="number" {...p} />
          <SortHead label="التاريخ" k="date" {...p} />
          {showEmployee && <SortHead label="الموظف" k="employee" {...p} />}
          {!compact && <TableHead>النوع</TableHead>}
          <TableHead>الخدمة</TableHead>
          <TableHead>طريقة الدفع</TableHead>
          <SortHead label="المبلغ" k="amount" end {...p} />
          {!compact && <TableHead className="text-end">النسبة</TableHead>}
          {!compact && <SortHead label="الاستقطاع" k="deduction" end {...p} />}
          {!compact && <SortHead label="الصافي" k="net" end {...p} />}
          <SortHead label="الحالة" k="status" {...p} />
        </TableRow>
      </TableHeader>
      <TableBody>
        {orders.map((o) => (
          <TableRow
            key={o.id}
            className="cursor-pointer"
            tabIndex={0}
            onClick={() => navigate(`/orders/${o.id}`)}
            onKeyDown={(e) => e.key === 'Enter' && navigate(`/orders/${o.id}`)}
          >
            <TableCell className="num font-medium text-primary">{o.orderNumber}</TableCell>
            <TableCell className="whitespace-nowrap text-muted-foreground">{formatDate(o.operationDate)}</TableCell>
            {showEmployee && <TableCell className="whitespace-nowrap">{o.employeeName}</TableCell>}
            {!compact && <TableCell className="whitespace-nowrap">{o.orderTypeName}</TableCell>}
            <TableCell className="max-w-48 truncate">
              {o.serviceName ?? '—'}
              {o.itemsCount > 1 && <span className="num ms-1 text-xs text-muted-foreground">(+{o.itemsCount - 1})</span>}
            </TableCell>
            <TableCell><PaymentBadge method={o.paymentMethod} /></TableCell>
            <TableCell className="text-end font-semibold"><Money value={o.subtotal} /></TableCell>
            {!compact && <TableCell className="num text-end text-muted-foreground">{o.deductionPercentage}%</TableCell>}
            {!compact && <TableCell className="text-end text-warning"><Money value={o.deductionAmount} /></TableCell>}
            {!compact && <TableCell className="text-end font-medium"><Money value={o.netAmount} /></TableCell>}
            <TableCell><OrderStatusBadge status={o.status} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
