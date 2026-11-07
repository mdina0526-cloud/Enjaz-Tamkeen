import * as React from 'react'
import { cn } from '@/lib/utils'

const fieldBase =
  'flex w-full rounded-lg border border-input bg-card px-3 py-2 text-sm shadow-xs transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/40 focus-visible:border-ring disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:border-destructive'

export function Input({ className, type, ...props }: React.ComponentProps<'input'>) {
  return <input type={type} className={cn(fieldBase, 'h-10 file:border-0 file:bg-transparent file:text-sm file:font-medium', className)} {...props} />
}

export function Textarea({ className, ...props }: React.ComponentProps<'textarea'>) {
  return <textarea className={cn(fieldBase, 'min-h-20', className)} {...props} />
}

/** Native select styled like the inputs: dependable on mobile and in RTL. */
export function NativeSelect({ className, children, ...props }: React.ComponentProps<'select'>) {
  return (
    <select className={cn(fieldBase, 'h-10 cursor-pointer', className)} {...props}>
      {children}
    </select>
  )
}
