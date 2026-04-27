import { Toaster as Sonner, type ToasterProps } from "sonner"

/**
 * J4 Spectrum-styled Sonner Toaster wrapper.
 *
 * - Toast surface uses `surface-glass` + `shadow-float` for the glass card look.
 * - Each variant gets a 4px left accent strip:
 *     success → accent-500 · error → destructive · info → violet-500 · warning → amber-500.
 *
 * Note: the live application currently mounts `Toaster` from `sonner` directly in
 * `src/app/providers/index.tsx`. Swap that to import from this module to roll the
 * J4 styling out app-wide.
 */
function Toaster(props: ToasterProps) {
  return (
    <Sonner
      position="top-right"
      toastOptions={{
        unstyled: false,
        classNames: {
          toast:
            "group toast surface-glass shadow-float rounded-xl border border-border/60 px-4 py-3 text-foreground",
          title: "text-sm font-medium",
          description: "text-xs text-muted-foreground",
          actionButton:
            "rounded-md bg-primary px-2.5 py-1 text-xs font-medium text-primary-foreground",
          cancelButton:
            "rounded-md bg-secondary px-2.5 py-1 text-xs font-medium text-secondary-foreground",
          success: "border-l-4 border-l-[var(--color-accent-500)]",
          error: "border-l-4 border-l-destructive",
          info: "border-l-4 border-l-[var(--color-violet-500)]",
          warning: "border-l-4 border-l-[var(--color-amber-500)]",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }
