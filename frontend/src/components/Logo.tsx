import { cn } from '@/lib/utils'

export function Logo({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 64 64" className={cn('size-10', className)} aria-hidden>
      <defs>
        <linearGradient id="logo-g" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#2A5BD7" />
          <stop offset="1" stopColor="#0B1F4B" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="14" fill="url(#logo-g)" />
      <path d="M18 44V30M28 44V22M38 44V34M48 44V18" stroke="#fff" strokeWidth="5" strokeLinecap="round" />
      <circle cx="48" cy="12" r="3" fill="#93C5FD" />
    </svg>
  )
}
