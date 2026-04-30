import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

const Sheet = DialogPrimitive.Root;
const SheetTrigger = DialogPrimitive.Trigger;
const SheetClose = DialogPrimitive.Close;
const SheetPortal = DialogPrimitive.Portal;

const SheetOverlay = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px]',
      'data-[state=open]:animate-in data-[state=closed]:animate-out',
      'data-[state=open]:fade-in-0 data-[state=closed]:fade-out-0',
      className,
    )}
    {...props}
  />
));
SheetOverlay.displayName = DialogPrimitive.Overlay.displayName;

type SheetSide = 'end' | 'bottom';
type SheetWidth = 'sm' | 'md' | 'lg';

// Drawer floats off the screen edges (top/bottom/trailing) so it reads as a
// panel rather than a flush slide-out. The inline-end inset uses a screen-edge
// gap that survives RTL via `inset-inline-end-*`. Closed state translates by
// 100% + the gap so the drawer fully clears the viewport during the slide-out.
const SIDE_CLASSES: Record<SheetSide, string> = {
  end: [
    'top-3 bottom-3 inset-inline-end-3 sm:top-4 sm:bottom-4 sm:inset-inline-end-4',
    'rounded-2xl border border-[var(--border-strong)]',
    'data-[state=open]:translate-x-0',
    'data-[state=closed]:translate-x-[calc(100%+1.5rem)]',
    'rtl:data-[state=closed]:-translate-x-[calc(100%+1.5rem)]',
  ].join(' '),
  bottom: [
    'inset-x-3 bottom-3 max-h-[calc(90vh-1.5rem)]',
    'rounded-2xl border border-[var(--border-strong)]',
    'data-[state=open]:translate-y-0',
    'data-[state=closed]:translate-y-[calc(100%+1.5rem)]',
  ].join(' '),
};

const WIDTH_CLASSES: Record<SheetWidth, string> = {
  // sm: viewport-minus-gap on phones, fixed width above the breakpoint.
  sm: 'w-[calc(100vw-1.5rem)] sm:w-[400px]',
  md: 'w-[calc(100vw-1.5rem)] sm:w-[480px]',
  lg: 'w-[calc(100vw-1.5rem)] sm:w-[560px]',
};

interface SheetContentProps
  extends React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> {
  side?: SheetSide;
  width?: SheetWidth;
  /** Show the X close button in the top-end corner. Default true. */
  showClose?: boolean;
}

const SheetContent = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Content>,
  SheetContentProps
>(
  (
    { side = 'end', width = 'lg', showClose = true, className, children, ...props },
    ref,
  ) => (
    <SheetPortal>
      <SheetOverlay />
      <DialogPrimitive.Content
        ref={ref}
        className={cn(
          'fixed z-50 flex flex-col gap-4 surface-glass-strong shadow-float overflow-hidden',
          'transition-transform duration-300 ease-out',
          'p-6',
          SIDE_CLASSES[side],
          side === 'end' && WIDTH_CLASSES[width],
          'data-[state=open]:animate-in data-[state=closed]:animate-out',
          className,
        )}
        {...props}
      >
        {/* Copper accent stripe along the leading edge — subtle J4 brand cue */}
        {side === 'end' && (
          <span
            aria-hidden
            className="pointer-events-none absolute inset-y-0 inset-inline-start-0 w-[2px] bg-gradient-to-b from-[var(--active-bg)] via-[var(--active-border)] to-transparent"
          />
        )}
        {children}
        {showClose && (
          <SheetClose
            className={cn(
              'absolute top-4 inline-end-4 z-10 rounded-lg p-1.5 text-muted-foreground transition-colors',
              'hover:text-[var(--active-text)] hover:bg-[var(--active-bg)]',
              'focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:pointer-events-none',
            )}
          >
            <X className="h-4 w-4" />
            <span className="sr-only">Close</span>
          </SheetClose>
        )}
      </DialogPrimitive.Content>
    </SheetPortal>
  ),
);
SheetContent.displayName = DialogPrimitive.Content.displayName;

const SheetHeader = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('flex flex-col gap-1.5 text-start pe-10', className)}
    {...props}
  />
);
SheetHeader.displayName = 'SheetHeader';

const SheetTitle = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn('text-lg font-semibold leading-none tracking-tight gradient-text', className)}
    {...props}
  />
));
SheetTitle.displayName = DialogPrimitive.Title.displayName;

const SheetDescription = React.forwardRef<
  React.ComponentRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn('text-sm text-muted-foreground', className)}
    {...props}
  />
));
SheetDescription.displayName = DialogPrimitive.Description.displayName;

const SheetBody = ({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn('flex-1 overflow-y-auto -mx-6 px-6', className)}
    {...props}
  />
);
SheetBody.displayName = 'SheetBody';

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetBody,
};
