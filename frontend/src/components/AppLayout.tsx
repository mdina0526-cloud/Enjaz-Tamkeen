import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  ArrowLeft, BarChart3, Bell, Building2, ChevronsLeft, ChevronsRight, ClipboardCheck, ClipboardList, FileText, LayoutDashboard, LogOut, Menu, Moon, PlusCircle,
  ScrollText, Settings, Sun, TrendingUp, User, Users, X,
  type LucideIcon,
} from 'lucide-react'
import { useAuth } from '@/context/AuthContext'
import { api } from '@/lib/api'
import { useTheme } from '@/context/ThemeContext'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Avatar, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger, Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/misc'
import { Logo } from './Logo'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
}

interface NavSection {
  label: string
  items: NavItem[]
}

const adminNav: NavSection[] = [
  { label: 'الرئيسية', items: [{ to: '/', label: 'لوحة التحكم', icon: LayoutDashboard, end: true }] },
  { label: 'إدارة الأعمال', items: [
    { to: '/orders', label: 'العمليات', icon: ClipboardList },
    { to: '/sales', label: 'المبيعات', icon: TrendingUp },
    { to: '/employees', label: 'الموظفون', icon: Users },
    { to: '/rentals', label: 'الإيجارات', icon: Building2 },
  ] },
  { label: 'المتابعة والتحليل', items: [
    { to: '/reports', label: 'التقارير المالية', icon: BarChart3 },
    { to: '/daily-close', label: 'الإقفال اليومي', icon: ClipboardCheck },
    { to: '/audit-logs', label: 'سجل النشاط', icon: ScrollText },
  ] },
  { label: 'النظام', items: [{ to: '/settings', label: 'الإعدادات', icon: Settings }] },
]

const employeeNav: NavSection[] = [
  { label: 'الرئيسية', items: [{ to: '/', label: 'لوحتي', icon: LayoutDashboard, end: true }] },
  { label: 'عملي', items: [{ to: '/orders', label: 'طلباتي', icon: FileText }] },
  { label: 'الحساب', items: [{ to: '/profile', label: 'ملفي الشخصي', icon: User }] },
]

const managerNav: NavSection[] = adminNav.map(section => ({
  ...section,
  items: section.items.filter(item => !['/employees', '/reports', '/daily-close', '/audit-logs', '/settings'].includes(item.to)),
})).filter(section => section.items.length > 0)

const accountantNav: NavSection[] = [
  { label: 'الرئيسية', items: [{ to: '/', label: 'لوحتي', icon: LayoutDashboard, end: true }] },
  { label: 'الشؤون المالية', items: [
    { to: '/rentals', label: 'الإيجارات', icon: Building2 },
    { to: '/reports', label: 'التقارير المالية', icon: BarChart3 },
    { to: '/daily-close', label: 'الإقفال اليومي', icon: ClipboardCheck },
  ] },
  { label: 'الحساب', items: [{ to: '/profile', label: 'ملفي الشخصي', icon: User }] },
]

const newOrderItem: NavItem = { to: '/orders/new', label: 'طلب جديد', icon: PlusCircle }
interface Notice { id: string; title: string; detail: string; link: string; severity: string }

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
  const notices = useQuery({ queryKey: ['notifications'], queryFn: () => api.get<Notice[]>('/api/notifications'), enabled: Boolean(user), refetchInterval: 60000 })
  const nav = user?.role === 'SuperAdmin' ? adminNav : user?.role === 'OperationsManager' ? managerNav : user?.role === 'Accountant' ? accountantNav : employeeNav
  const current = [newOrderItem, ...nav.flatMap((section) => section.items)]
    .sort((a, b) => b.to.length - a.to.length)
    .find((item) => (item.end ? location.pathname === item.to : location.pathname.startsWith(item.to)))

  useEffect(() => setMobileOpen(false), [location.pathname])

  useEffect(() => {
    if (!mobileOpen) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileOpen(false)
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', closeOnEscape)
    }
  }, [mobileOpen])

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

  const sidebar = (mobile: boolean) => {
    const compact = collapsed && !mobile
    const renderItem = (item: NavItem, prominent = false) => {
      const link = (
        <NavLink
          to={item.to}
          end={item.end ?? item.to === '/orders'}
          aria-label={compact ? item.label : undefined}
          className={({ isActive }) => cn(
            'group relative flex min-h-11 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300',
            compact && 'justify-center px-2',
            prominent
              ? 'bg-gradient-to-l from-[#3776ea] to-[#2458c5] text-white shadow-lg shadow-blue-950/30 hover:brightness-110'
              : isActive
                ? 'bg-white/12 text-white before:absolute before:inset-y-2 before:right-0 before:w-1 before:rounded-l-full before:bg-sky-300'
                : 'text-sidebar-foreground hover:bg-white/8 hover:text-white',
            prominent && isActive && 'ring-1 ring-white/35',
          )}
        >
          <item.icon className={cn('size-[19px] shrink-0', !prominent && 'group-hover:text-sky-200')} aria-hidden />
          {!compact && <span className="min-w-0 flex-1 truncate">{item.label}</span>}
          {!compact && prominent && <ArrowLeft className="size-4 opacity-75" aria-hidden />}
        </NavLink>
      )
      return compact ? (
        <Tooltip key={item.to}>
          <TooltipTrigger asChild>{link}</TooltipTrigger>
          <TooltipContent side="left">{item.label}</TooltipContent>
        </Tooltip>
      ) : <div key={item.to}>{link}</div>
    }

    return (
      <div className="relative flex h-full flex-col overflow-hidden bg-sidebar text-sidebar-foreground">
        <div className="pointer-events-none absolute -right-20 -top-28 size-64 rounded-full bg-blue-500/10 blur-3xl" aria-hidden />
        <div className={cn('relative flex h-20 items-center gap-3 border-b border-white/10 px-4', compact && 'justify-center px-2')}>
          <span className="flex size-11 shrink-0 items-center justify-center rounded-2xl border border-white/10 bg-white/10 shadow-inner shadow-white/5">
            <Logo className="size-8" />
          </span>
          {!compact && (
            <div className="min-w-0">
              <p className="truncate text-sm font-bold tracking-wide text-white">إنجاز وتمكين</p>
              <p className="truncate text-[11px] text-sidebar-foreground/70">إدارة المبيعات والعمليات</p>
            </div>
          )}
          {mobile && (
            <button onClick={() => setMobileOpen(false)} className="ms-auto rounded-lg p-2 hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300" aria-label="إغلاق القائمة">
              <X className="size-5" />
            </button>
          )}
        </div>

        {user?.role !== 'Accountant' && <div className={cn('relative px-3 pb-3 pt-5', compact && 'px-2')}>
          {renderItem(newOrderItem, true)}
        </div>}

        <nav aria-label="التنقل الرئيسي" className={cn('relative flex-1 space-y-5 overflow-y-auto px-3 pb-4 pt-2', compact && 'px-2')}>
          {nav.map((section) => (
            <div key={section.label} className="space-y-1">
              {compact ? <div className="mx-2 mb-3 border-t border-white/10" /> : (
                <p className="px-3 pb-1 text-[11px] font-semibold tracking-wide text-sidebar-foreground/55">{section.label}</p>
              )}
              {section.items.map((item) => renderItem(item))}
            </div>
          ))}
        </nav>

        <div className={cn('relative space-y-2 border-t border-white/10 p-3', compact && 'px-2')}>
          {!compact && (
            <button onClick={() => navigate('/profile')} className="flex w-full items-center gap-3 rounded-xl border border-white/10 bg-white/5 p-2.5 text-right transition-colors hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300">
              <Avatar name={user?.fullName ?? '?'} />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-xs font-semibold text-white">{user?.fullName}</span>
                <span className="block truncate text-[11px] text-sidebar-foreground/70">{isAdmin ? 'المدير العام' : (user?.jobTitle ?? 'موظف')}</span>
              </span>
              <ArrowLeft className="size-4 text-sidebar-foreground/70" aria-hidden />
            </button>
          )}
          {!mobile && (
            <button onClick={toggleCollapsed} className="flex w-full items-center justify-center gap-2 rounded-xl px-3 py-2 text-xs text-sidebar-foreground/70 transition-colors hover:bg-white/8 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-300" aria-label={collapsed ? 'توسيع القائمة' : 'طي القائمة'}>
              {collapsed ? <ChevronsLeft className="size-4" /> : <><ChevronsRight className="size-4" /> طي القائمة</>}
            </button>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen">
      <aside className={cn('no-print sticky top-0 hidden h-screen shrink-0 transition-[width] duration-200 lg:block', collapsed ? 'w-[76px]' : 'w-[272px]')}>{sidebar(false)}</aside>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" className="absolute inset-0 bg-navy/60" onClick={() => setMobileOpen(false)} aria-label="إغلاق القائمة" />
          <aside id="mobile-navigation" role="dialog" aria-modal="true" aria-label="القائمة الجانبية" className="absolute inset-y-0 right-0 w-[min(19rem,86vw)] shadow-2xl">{sidebar(true)}</aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="no-print sticky top-0 z-30 flex h-16 items-center gap-3 border-b bg-card/85 px-4 backdrop-blur md:px-6">
          <Button variant="ghost" size="icon" className="lg:hidden" onClick={() => setMobileOpen(true)} aria-label="فتح القائمة" aria-expanded={mobileOpen} aria-controls="mobile-navigation">
            <Menu />
          </Button>
          <h1 className="truncate text-base font-semibold">{current?.label ?? ''}</h1>
          <div className="ms-auto flex items-center gap-1.5">
            <DropdownMenu>
              <DropdownMenuTrigger className="relative rounded-lg p-2 hover:bg-accent" aria-label={`التنبيهات ${notices.data?.length ?? 0}`}>
                <Bell className="size-5" />
                {Boolean(notices.data?.length) && <span className="absolute -top-1 -left-1 min-w-4 rounded-full bg-destructive px-1 text-[10px] text-white">{notices.data?.length}</span>}
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-72">
                <DropdownMenuLabel>التنبيهات</DropdownMenuLabel>
                <DropdownMenuSeparator />
                {notices.data?.length ? notices.data.map(n => <DropdownMenuItem key={n.id} onSelect={() => navigate(n.link)}>
                  <span className="block"><strong className="block text-sm">{n.title}</strong><span className="text-xs text-muted-foreground">{n.detail}</span></span>
                </DropdownMenuItem>) : <p className="p-3 text-sm text-muted-foreground">لا توجد تنبيهات حالياً</p>}
              </DropdownMenuContent>
            </DropdownMenu>
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
