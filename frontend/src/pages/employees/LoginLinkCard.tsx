import { useState } from 'react'
import { toast } from 'sonner'
import { Copy, ExternalLink, Link2, RefreshCw } from 'lucide-react'
import { api } from '@/lib/api'
import { invalidate, toastError } from '@/hooks/useApi'
import type { EmployeeDetail } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { ConfirmDialog } from '@/components/common'

function employeeLoginUrl(slug: string) {
  return `${window.location.origin}/e/${slug}`
}

/** The employee's own login page link, for the admin to share; regenerating it revokes the old one. */
export function LoginLinkCard({ employee, onChange }: { employee: EmployeeDetail; onChange: (e: EmployeeDetail) => void }) {
  const [confirm, setConfirm] = useState(false)
  const url = employee.loginSlug ? employeeLoginUrl(employee.loginSlug) : null

  const copy = async () => {
    if (!url) return
    try {
      await navigator.clipboard.writeText(url)
      toast.success('تم نسخ رابط الدخول')
    } catch {
      toast.error('تعذر النسخ تلقائياً، انسخ الرابط يدوياً')
    }
  }

  const regenerate = async () => {
    try {
      const updated = await api.post<EmployeeDetail>(`/api/employees/${employee.id}/login-link`)
      onChange(updated)
      void invalidate('employees')
      toast.success('تم إنشاء رابط جديد وإيقاف الرابط السابق')
    } catch (e) {
      toastError(e)
      throw e
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2"><Link2 className="size-4 text-primary" /> صفحة الدخول الخاصة بالموظف</CardTitle>
        <CardDescription>رابط مستقل لهذا الموظف وحده؛ يفتح صفحة دخول باسمه ولا يقبل حساب أي شخص آخر. أرسله له مع كلمة المرور الأولية.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {url ? (
          <>
            <div className="num truncate rounded-lg border border-dashed bg-muted/40 px-3 py-2.5 text-sm" dir="ltr" title={url}>{url}</div>
            <div className="flex flex-wrap gap-2">
              <Button size="sm" onClick={copy}><Copy /> نسخ الرابط</Button>
              <Button size="sm" variant="outline" asChild>
                <a href={url} target="_blank" rel="noreferrer"><ExternalLink /> فتح</a>
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setConfirm(true)}><RefreshCw /> رابط جديد</Button>
            </div>
            {!employee.isActive && <p className="text-xs text-destructive">الحساب معطل، لذلك لا تعمل صفحة الدخول حتى يُعاد تفعيله.</p>}
          </>
        ) : (
          <Button size="sm" onClick={() => setConfirm(true)}><Link2 /> إنشاء رابط دخول</Button>
        )}
      </CardContent>
      <ConfirmDialog
        open={confirm}
        onOpenChange={setConfirm}
        title="إنشاء رابط دخول جديد"
        description="سيتوقف الرابط الحالي فوراً، وعليك إرسال الرابط الجديد للموظف."
        confirmLabel="إنشاء رابط جديد"
        onConfirm={regenerate}
      />
    </Card>
  )
}
