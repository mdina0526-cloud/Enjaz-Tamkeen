import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useLocation, useNavigate, useParams } from 'react-router-dom'
import { AlertCircle, BarChart3, Building2, Eye, EyeOff, FileSpreadsheet, Link2Off, Loader2, Lock, Percent, ShieldCheck, User, Users } from 'lucide-react'
import { useAuth } from '@/context/AuthContext'
import { errorMessage, useApiQuery } from '@/hooks/useApi'
import { api } from '@/lib/api'
import type { LoginPortal } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Logo } from '@/components/Logo'

const password = z.string().min(1, 'يرجى إدخال كلمة المرور').max(128)
const mfaCode = z.union([z.literal(''), z.string().regex(/^\d{6}$/, 'أدخل رمز التحقق المكون من 6 أرقام')]).optional()
const schema = z.object({ username: z.string().trim().min(1, 'يرجى إدخال اسم المستخدم أو البريد الإلكتروني').max(150), password, mfaCode })
// On a personal link the link itself identifies the employee, so only the password is asked for.
const portalSchema = z.object({ username: z.string().optional(), password, mfaCode })
type FormValues = z.infer<typeof portalSchema>

/** Personal login page of one employee (/e/:slug). Only that employee can sign in here. */
export function EmployeeLoginPage() {
  const { slug = '' } = useParams()
  const portal = useApiQuery(['portal', slug], () => api.get<LoginPortal>(`/api/auth/portal/${encodeURIComponent(slug)}`))

  if (portal.loading)
    return (
      <div className="flex min-h-screen items-center justify-center bg-white dark:bg-card">
        <Loader2 className="size-8 animate-spin text-primary" />
      </div>
    )
  if (!portal.data)
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-white px-6 text-center dark:bg-card">
        <span className="flex size-14 items-center justify-center rounded-2xl bg-destructive/10 text-destructive"><Link2Off className="size-7" /></span>
        <h1 className="text-2xl font-bold text-navy dark:text-white">رابط الدخول غير صالح</h1>
        <p className="max-w-sm text-muted-foreground">قد يكون الرابط قديماً أو الحساب معطلاً. اطلب من المدير رابط الدخول الخاص بك.</p>
        <Button variant="outline" asChild><Link to="/login">صفحة الدخول العامة</Link></Button>
      </div>
    )
  return <LoginPage portal={{ slug, ...portal.data }} />
}

export function LoginPage({ portal }: { portal?: LoginPortal & { slug: string } } = {}) {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [showPassword, setShowPassword] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)
  const [needsMfa, setNeedsMfa] = useState(false)
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({ resolver: zodResolver(portal ? portalSchema : schema) })

  if (user) return <Navigate to="/" replace />

  const onSubmit = async (v: FormValues) => {
    setServerError(null)
    try {
      await login(v.username ?? '', v.password, portal?.slug, v.mfaCode || undefined)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : '/', { replace: true })
    } catch (e) {
      const message = errorMessage(e)
      if (message.includes('رمز التحقق')) setNeedsMfa(true)
      setServerError(message)
    }
  }

  return (
    <div className="grid min-h-screen bg-white lg:grid-cols-2 dark:bg-card">
      {/* Form half */}
      <div className="flex items-center justify-center px-5 py-10 sm:px-10">
        <div className="animate-fade-up w-full max-w-md">
          <div className="mb-10 flex items-center gap-3">
            <Logo className="size-12" />
            <div>
              <p className="text-lg font-bold text-navy dark:text-white">إنجاز وتمكين</p>
              <p className="text-xs text-muted-foreground">نظام إدارة المبيعات والعمليات المالية</p>
            </div>
          </div>

          {portal ? (
            <>
              <p className="mb-3 inline-flex items-center gap-2 rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
                <User className="size-3.5" /> صفحة الدخول الخاصة بك
              </p>
              <h1 className="text-3xl font-bold tracking-tight text-navy dark:text-white">مرحباً، {portal.fullName}</h1>
              <p className="mt-2 text-muted-foreground">{portal.jobTitle ? `${portal.jobTitle} — ` : ''}أدخل كلمة المرور للدخول إلى لوحتك وتسجيل طلباتك.</p>
            </>
          ) : (
            <>
              <h1 className="text-3xl font-bold tracking-tight text-navy dark:text-white">مرحباً بعودتك</h1>
              <p className="mt-2 text-muted-foreground">سجّل الدخول للوصول إلى لوحة التحكم ومتابعة عملياتك.</p>
            </>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="mt-8 space-y-5" noValidate>
            {serverError && (
              <div className="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/8 px-3 py-2.5 text-sm text-destructive" role="alert">
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                <span>{serverError}</span>
              </div>
            )}

            {!portal && (
              <Field label="اسم المستخدم أو البريد الإلكتروني" htmlFor="username" error={errors.username?.message}>
                <div className="relative">
                  <User className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input id="username" autoComplete="username" autoFocus dir="auto" className="h-12 pr-10" placeholder="example@company.com" aria-invalid={!!errors.username} {...register('username')} />
                </div>
              </Field>
            )}

            <Field label="كلمة المرور" htmlFor="password" error={errors.password?.message}>
              <div className="relative">
                <Lock className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="password" type={showPassword ? 'text' : 'password'} autoComplete="current-password" autoFocus={!!portal} className="h-12 px-10" placeholder="••••••••" aria-invalid={!!errors.password} {...register('password')} />
                <button
                  type="button"
                  onClick={() => setShowPassword((s) => !s)}
                  className="absolute left-2 top-1/2 -translate-y-1/2 rounded-md p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground"
                  aria-label={showPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
                >
                  {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </button>
              </div>
            </Field>

            {needsMfa && (
              <Field label="رمز التحقق بخطوتين" htmlFor="mfaCode" error={errors.mfaCode?.message}>
                <Input id="mfaCode" inputMode="numeric" autoComplete="one-time-code" maxLength={6} dir="ltr" placeholder="000000" {...register('mfaCode')} />
              </Field>
            )}

            <Button type="submit" size="lg" className="h-12 w-full bg-gradient-to-l from-primary to-navy-2 text-base shadow-lg shadow-primary/25 hover:opacity-95" loading={isSubmitting}>
              تسجيل الدخول
            </Button>
          </form>

          <div className="mt-8 flex items-center gap-2 rounded-lg bg-muted/60 px-3 py-2.5 text-xs text-muted-foreground">
            <ShieldCheck className="size-4 shrink-0 text-success" />
            اتصال آمن — تُحفظ كلمات المرور مشفّرة وتُسجّل جميع العمليات في سجل النشاط.
          </div>
          <p className="mt-6 text-center text-xs text-muted-foreground">© {new Date().getFullYear()} إنجاز وتمكين. جميع الحقوق محفوظة.</p>
        </div>
      </div>

      {/* Brand half */}
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-[#0B1F4B] via-[#0F2A63] to-[#1E40AF] lg:block">
        <BrandArt />
        <div className="relative z-10 flex h-full flex-col justify-between p-12 text-white">
          <div className="flex items-center gap-2 text-sm text-blue-100/80">
            <span className="size-2 rounded-full bg-emerald-400 shadow-[0_0_12px] shadow-emerald-400" /> منصة إدارة أعمال متكاملة
          </div>

          <div className="mx-auto grid w-full max-w-md gap-3">
            {[
              { icon: BarChart3, title: 'لوحات تحكم لحظية', text: 'المبيعات والتحصيل ومقارنة الفترات من بيانات النظام الفعلية' },
              { icon: Users, title: 'حساب مستقل لكل موظف', text: 'صفحة دخول خاصة ولوحة لا ترى إلا عملياته' },
              { icon: Percent, title: 'استقطاعات تلقائية', text: 'تُحسب في الخادم وتُحفظ النسبة وقت كل عملية' },
              { icon: Building2, title: 'إدارة الإيجارات', text: 'المستحق والمدفوع والمتأخر مع سجل الدفعات' },
              { icon: FileSpreadsheet, title: 'تقارير قابلة للتصدير', text: 'Excel وPDF لكل تقرير مالي' },
            ].map((f, i) => (
              <div key={f.title} className="animate-fade-up flex items-start gap-3 rounded-xl border border-white/15 bg-white/8 p-4 backdrop-blur" style={{ animationDelay: `${0.1 + i * 0.08}s` }}>
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-white/10"><f.icon className="size-4.5 text-blue-200" /></span>
                <div>
                  <p className="font-semibold">{f.title}</p>
                  <p className="text-sm text-blue-100/75">{f.text}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="max-w-md">
            <h2 className="text-3xl font-bold leading-snug">تحكّم كامل بمبيعاتك وموظفيك وعملياتك المالية</h2>
            <p className="mt-3 text-blue-100/80">لوحات تحكم لحظية، صلاحيات دقيقة، تقارير قابلة للتصدير، وسجل تدقيق لكل عملية.</p>
          </div>
        </div>
      </div>
    </div>
  )
}

function BrandArt() {
  return (
    <svg className="absolute inset-0 h-full w-full" preserveAspectRatio="xMidYMid slice" viewBox="0 0 800 900" aria-hidden>
      <defs>
        <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
          <path d="M40 0H0V40" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="1" />
        </pattern>
        <radialGradient id="glow" cx="0.7" cy="0.3" r="0.6">
          <stop offset="0" stopColor="#60A5FA" stopOpacity="0.45" />
          <stop offset="1" stopColor="#60A5FA" stopOpacity="0" />
        </radialGradient>
      </defs>
      <rect width="800" height="900" fill="url(#grid)" />
      <rect width="800" height="900" fill="url(#glow)" />
      <circle cx="680" cy="120" r="180" fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth="1.5" />
      <circle cx="680" cy="120" r="260" fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth="1.5" />
      <circle cx="90" cy="820" r="220" fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="1.5" />
      <polygon points="110,140 190,95 270,140 270,230 190,275 110,230" fill="none" stroke="rgba(147,197,253,0.25)" strokeWidth="1.5" />
      <polygon points="600,640 660,605 720,640 720,710 660,745 600,710" fill="rgba(147,197,253,0.08)" stroke="rgba(147,197,253,0.25)" strokeWidth="1.5" />
      <path d="M0 640 C 160 560, 260 700, 420 610 S 680 520, 800 560" fill="none" stroke="rgba(147,197,253,0.35)" strokeWidth="2" strokeDasharray="6 8" />
      <path d="M0 700 C 180 640, 300 760, 460 680 S 700 600, 800 640" fill="none" stroke="rgba(255,255,255,0.12)" strokeWidth="1.5" />
      {[[140, 420], [320, 300], [520, 380], [700, 280], [240, 760], [560, 820]].map(([x, y], i) => (
        <circle key={i} cx={x} cy={y} r="3.5" fill="#93C5FD" opacity="0.7" />
      ))}
    </svg>
  )
}
