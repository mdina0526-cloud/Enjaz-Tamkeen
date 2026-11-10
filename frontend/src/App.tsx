import { BrowserRouter, Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { Toaster } from 'sonner'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@/hooks/useApi'
import { Loader2, SearchX } from 'lucide-react'
import { AuthProvider, useAuth } from '@/context/AuthContext'
import { ThemeProvider, useTheme } from '@/context/ThemeContext'
import { TooltipProvider } from '@/components/ui/misc'
import { Button } from '@/components/ui/button'
import { AppLayout } from '@/components/AppLayout'
import { EmptyState } from '@/components/common'
import { EmployeeLoginPage, LoginPage } from '@/pages/LoginPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { OrdersPage } from '@/pages/orders/OrdersPage'
import { NewOrderPage } from '@/pages/orders/NewOrderPage'
import { OrderDetailPage } from '@/pages/orders/OrderDetailPage'
import { EmployeesPage } from '@/pages/employees/EmployeesPage'
import { EmployeeDetailPage } from '@/pages/employees/EmployeeDetailPage'
import { RentalsPage } from '@/pages/rentals/RentalsPage'
import { RentalDetailPage } from '@/pages/rentals/RentalDetailPage'
import { ReportsPage } from '@/pages/ReportsPage'
import { AuditLogsPage } from '@/pages/AuditLogsPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { ProfilePage } from '@/pages/ProfilePage'
import { SalesPage } from '@/pages/SalesPage'
import { DailyClosePage } from '@/pages/DailyClosePage'

function RequireAuth() {
  const { user, loading } = useAuth()
  const location = useLocation()
  if (loading)
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loader2 className="size-8 animate-spin text-primary" />
      </div>
    )
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <Outlet />
}

/** UI guard only; every admin endpoint is also protected by role checks on the server. */
function RequireRole({ roles }: { roles: string[] }) {
  const { user } = useAuth()
  return user && roles.includes(user.role) ? <Outlet /> : <Navigate to="/" replace />
}

function NotFound() {
  return (
    <EmptyState
      icon={SearchX}
      title="الصفحة غير موجودة"
      description="ربما تم نقل الصفحة أو أن الرابط غير صحيح"
      action={<Button asChild><Link to="/">العودة للرئيسية</Link></Button>}
    />
  )
}

function ThemedToaster() {
  const { theme } = useTheme()
  return <Toaster position="top-center" richColors closeButton dir="rtl" theme={theme} toastOptions={{ style: { fontFamily: 'inherit' } }} />
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
    <ThemeProvider>
      <TooltipProvider delayDuration={200}>
        <AuthProvider>
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/e/:slug" element={<EmployeeLoginPage />} />
              <Route element={<RequireAuth />}>
                <Route element={<AppLayout />}>
                  <Route index element={<DashboardPage />} />
                  <Route path="profile" element={<ProfilePage />} />
                  <Route element={<RequireRole roles={['SuperAdmin', 'OperationsManager', 'Employee']} />}>
                    <Route path="orders" element={<OrdersPage />} />
                    <Route path="orders/new" element={<NewOrderPage />} />
                    <Route path="orders/:id" element={<OrderDetailPage />} />
                  </Route>
                  <Route element={<RequireRole roles={['SuperAdmin', 'OperationsManager']} />}>
                    <Route path="sales" element={<SalesPage />} />
                  </Route>
                  <Route element={<RequireRole roles={['SuperAdmin']} />}>
                    <Route path="employees" element={<EmployeesPage />} />
                    <Route path="employees/:id" element={<EmployeeDetailPage />} />
                  </Route>
                  <Route element={<RequireRole roles={['SuperAdmin', 'OperationsManager', 'Accountant']} />}>
                    <Route path="rentals" element={<RentalsPage />} />
                    <Route path="rentals/:id" element={<RentalDetailPage />} />
                  </Route>
                  <Route element={<RequireRole roles={['SuperAdmin', 'Accountant']} />}>
                    <Route path="reports" element={<ReportsPage />} />
                    <Route path="daily-close" element={<DailyClosePage />} />
                  </Route>
                  <Route element={<RequireRole roles={['SuperAdmin']} />}>
                    <Route path="audit-logs" element={<AuditLogsPage />} />
                    <Route path="settings" element={<SettingsPage />} />
                  </Route>
                  <Route path="*" element={<NotFound />} />
                </Route>
              </Route>
            </Routes>
          </BrowserRouter>
          <ThemedToaster />
        </AuthProvider>
      </TooltipProvider>
    </ThemeProvider>
    </QueryClientProvider>
  )
}
