import * as React from 'react'
import * as LabelPrimitive from '@radix-ui/react-label'
import { cn } from '@/lib/utils'

export function Label({ className, ...props }: React.ComponentProps<typeof LabelPrimitive.Root>) {
  return <LabelPrimitive.Root className={cn('text-sm font-medium leading-none peer-disabled:opacity-70', className)} {...props} />
}

export function FieldError({ message }: { message?: string }) {
  if (!message) return null
  return (
    <p className="text-xs font-medium text-destructive" role="alert">
      {message}
    </p>
  )
}

export function Field({ label, error, children, hint, htmlFor, className }: { label: string; error?: string; hint?: string; htmlFor?: string; className?: string; children: React.ReactNode }) {
  return (
    <div className={cn('space-y-1.5', className)}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      <FieldError message={error} />
    </div>
  )
}
