import { useEffect, useState } from 'react'
import { keepPreviousData, QueryClient, useQuery, type QueryKey } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, ApiError } from '@/lib/api'
import type { OrderTypeItem, PaymentMethodItem, ServiceItem } from '@/types/api'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Financial data: always revalidate when a page mounts, but avoid refetch storms on focus.
      staleTime: 0,
      refetchOnWindowFocus: false,
      retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
    },
  },
})

/**
 * Thin wrapper around TanStack Query's useQuery with the shape the pages use.
 * `keepPrevious` keeps the last page/filter result on screen while the next one loads (tables, dashboards).
 */
export function useApiQuery<T>(key: QueryKey, fetcher: () => Promise<T>, opts: { enabled?: boolean; keepPrevious?: boolean } = {}) {
  const q = useQuery({
    queryKey: key,
    queryFn: fetcher,
    enabled: opts.enabled ?? true,
    placeholderData: opts.keepPrevious ? keepPreviousData : undefined,
  })
  return {
    data: q.data ?? null,
    /** True only while there is nothing to show yet (first load or a new key without placeholder). */
    loading: q.isPending && q.fetchStatus !== 'idle',
    fetching: q.isFetching,
    error: q.error ? errorMessage(q.error) : null,
    reload: () => void q.refetch(),
    setData: (v: T) => queryClient.setQueryData(key, v),
  }
}

/** Invalidates cached data after a write so every screen showing it refetches. */
export function invalidate(...prefixes: string[]) {
  if (prefixes.length === 0) return queryClient.invalidateQueries()
  return Promise.all(prefixes.map((p) => queryClient.invalidateQueries({ queryKey: [p] })))
}

// ---------- Shared lookups (order types, services, payment methods) ----------

export function useOrderTypes(includeInactive = false) {
  return useApiQuery(['catalog', 'order-types', includeInactive], () => api.get<OrderTypeItem[]>('/api/catalog/order-types', { includeInactive }))
}

export function useServices(includeInactive = false) {
  return useApiQuery(['catalog', 'services', includeInactive], () => api.get<ServiceItem[]>('/api/catalog/services', { includeInactive }))
}

export function usePaymentMethods() {
  return useApiQuery(['catalog', 'payment-methods'], () => api.get<PaymentMethodItem[]>('/api/catalog/payment-methods'))
}

export function errorMessage(e: unknown) {
  return e instanceof ApiError || e instanceof Error ? e.message : 'حدث خطأ غير متوقع'
}

export function toastError(e: unknown) {
  toast.error(errorMessage(e))
}

/** Debounces a fast-changing value (search boxes). */
export function useDebounced<T>(value: T, ms = 350) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}
