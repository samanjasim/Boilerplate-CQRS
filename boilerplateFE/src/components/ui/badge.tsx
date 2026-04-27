import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const badgeVariants = cva(
  "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors",
  {
    variants: {
      variant: {
        default:
          "[background:var(--active-bg)] [color:var(--active-text)]",
        secondary:
          "bg-secondary text-muted-foreground",
        destructive:
          "bg-destructive/10 text-destructive dark:bg-destructive/20",
        outline: "bg-secondary/60 text-muted-foreground",
        // J4 status pills — emerald (healthy), amber (pending), destructive (failed), violet (info)
        healthy:
          "bg-[color-mix(in_srgb,var(--color-accent-500)_10%,transparent)] text-accent-700 dark:text-accent-300 border border-[color-mix(in_srgb,var(--color-accent-500)_20%,transparent)]",
        pending:
          "bg-[color-mix(in_srgb,var(--color-amber-500)_12%,transparent)] text-amber-700 dark:text-amber-300 border border-[color-mix(in_srgb,var(--color-amber-500)_25%,transparent)]",
        failed:
          "bg-destructive/10 text-destructive border border-destructive/20",
        info:
          "bg-[color-mix(in_srgb,var(--color-violet-500)_10%,transparent)] text-violet-700 dark:text-violet-300 border border-[color-mix(in_srgb,var(--color-violet-500)_20%,transparent)]",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export { Badge, badgeVariants }
