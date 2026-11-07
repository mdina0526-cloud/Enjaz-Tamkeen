import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  BarChart3, Building2, ChevronsLeft, ChevronsRight, ClipboardList, FileText, LayoutDashboard, LogOut, Menu, Moon, PlusCircle,
  ScrollText, Settings, Sun, TrendingUp, User, Users, X,
} from 'lucide-react'
import { useAuth } from '@/context/AuthContext'
import { useTheme } from '@/context/ThemeContext'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Avatar, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger, Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/misc'
import { Logo } from './Logo'

interface NavItem {
  to: string
  label: string
  icon: typeof LayoutDashboard
  end?: boolean
}

const adminNav: NavItem[] = [
  { to: '/', label: 'لوحة التحكم', icon: LayoutDashboard, end: true },
  { to: '/employees', label: 'الموظفون', icon: Users },
  { to: '/orders', label: 'العمليات', icon: ClipboardList },
  { to: '/sales', label: 'المبيعات', icon: TrendingUp },
  { to: '/orders/new', label: 'طلب جديد', icon: PlusCircle },
  { to: '/rentals', label: 'الإيجارات', icon: Building2 },
  { to: '/reports', label: 'التقارير المالية', icon: BarChart3 },
  { to: '/audit-logs', label: 'سجل النشاط', icon: ScrollText },
  { to: '/settings', label: 'الإعدادات', icon: Settings },
]

const employeeNav: NavItem[] = [
  { to: '/', label: 'لوحتي', icon: LayoutDashboard, end: true },
  { to: '/orders/new', label: 'طلب جديد', icon: PlusCircle },
  { to: '/orders', label: 'طلباتي', icon: FileText },
  { to: '/profile', label: 'ملفي الشخصي', icon: User },
]

function readCollapsed() {
  try {
    return localStorage.getItem('sidebar_collapsed') === '1'
  } catch {
    return false
  }
}

export function AppLayout() {
  const { user, isAdmin, logout } = useAuth()
  const { theme, toggle } = useTheme()
  const navigate = useNavigate()
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [mobileOpen, setMobileOpen] = useState(false)
  const nav = isAdmin ? adminNav : employeeNav
  const current = [...nav].sort((a, b) => b.to.length - a.to.length).find((n) => (n.end ? location.pathname === n.to : location.pathname.startsWith(n.to)))

  useEffect(() => setMobileOpen(false), [location.pathname])

  const toggleCollapsed = () => {
    setCollapsed((c) => {
      try {
        localStorage.setItem('sidebar_collapsed', c ? '0' : '1')
      } catch {
        /* ignore */
      }
      return !c
    })
  }

  const sidebar = (mobile: boolean) => (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <div className={cn('flex h-16 items-center gap-3 border-b border-white/10 px-4', collapsed && !mobile && 'justify-center px-2')}>
        <Logo className="size-9 shrink-0" />
        {(!collapsed || mobile) && (
          <div className="min-w-0">
            <p className="truncate text-sm font-bold text-white">إنجاز وتمكين</p>
            <p className="truncate text-[11px] text-sidebar-foreground/70">إدارة المبيعات والعمليات</p>
          </div>
        )}
        {mobile && (
          <button onClick={() => setMobileOpen(false)} className="ms-auto rounded-md p-1.5 hover:bg-white/10" aria-label="إغلاق القائمة">
            <X className="size-5" />
          </button>
        )}
      </div>
      <nav className="flex-1 space-y-1 overflow-y-auto p-3">
        {nav.map((item) => {
          const link = (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end ?? item.to === '/orders'}
              className={({ isActive }) =>
                cn(
                  'group flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                  isActive ? 'bg-sidebar-active text-white shadow-lg shadow-black/20' : 'hover:bg-white/8 hover:text-white',
                  collapsed && !mobile && 'justify-center px-2',
                )
              }
            >
              <item.icon className="size-[18px] shrink-0" />
              {(!collapsed || mobile) && <span className="truncate">{item.label}</span>}
            </NavLink>
          )
          return collapsed && !mobile ? (
            <Tooltip key={item.to}>
              <TooltipTrigger asChild>{link}</TooltipTrigger>
              <TooltipContent side="left">{item.label}</TooltipContent>
            </Tooltip>
          ) : (
            link
          )
        })}
      </nav>
      {!mobile && (
        <button onClick={toggleCollapsed} className="m-3 flex items-center justify-center gap-2 rounded-lg border border-white/10 py-2 text-xs hover:bg-white/8" aria-label={collapsed ? 'توسيع القائمة' : 'طي القائمة'}>
          {collapsed ? <ChevronsLeft className="size-4" /> : <><ChevronsRight className="size-4" /> طي القائمة</>}
        </button>
      )}
    </div>
  )

  return (
    <div className="flex min-h-screen">
      <aside className={cn('no-print sticky top-0 hidden h-screen shrink-0 transition-[width] duration-200 lg:block', collapsed ? 'w-[72px]' : 'w-64')}>{sidebar(false)}</aside>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-navy/60" onClick={() => setMobileOpen(false)} />
          <aside className="absolute inset-y-0 right-0 w-72 shadow-2xl">{sidebar(true)}</aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="no-print sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-card/85 px-4 backdrop-blur md:px-6">
          <Button variant="ghost" size="icon" className="lg:hidden" onClick={() => setMobileOpen(true)} aria-label="فتح القائمة">
            <Menu />
          </Button>
          <h1 className="truncate text-base font-semibold">{current?.label ?? ''}</h1>
          <div className="ms-auto flex items-center gap-1.5">
            <Button variant="ghost" size="icon" onClick={toggle} aria-label={theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن'}>
              {theme === 'dark' ? <Sun /> : <Moon />}
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger className="flex cursor-pointer items-center gap-2 rounded-lg p-1 ps-2 hover:bg-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                <div className="hidden text-end sm:block">
                  <p className="text-sm font-medium leading-tight">{user?.fullName}</p>
                  <p className="text-[11px] text-muted-foreground">{isAdmin ? 'المدير العام' : (user?.jobTitle ?? 'موظف')}</p>
                </div>
                <Avatar name={user?.fullName ?? '?'} />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuLabel>
                  <span className="block">{user?.fullName}</span>
                  <span className="num block text-end text-xs font-normal text-muted-foreground">@{user?.username}</span>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => navigate('/profile')}>
                  <User /> الملف الشخصي
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={toggle}>
                  {theme === 'dark' ? <Sun /> : <Moon />} {theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن'}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onSelect={() => {
                    navigate(logout(), { replace: true })
                  }}
                >
                  <LogOut /> تسجيل الخروج
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>
        <main className="flex-1 p-4 md:p-6">
          <div className="mx-auto w-full max-w-[1400px]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
