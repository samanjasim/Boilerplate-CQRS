import {
  Activity,
  Check,
  CreditCard,
  Flag,
  KeyRound,
  ScrollText,
  Shield,
  Users,
  Webhook,
  type LucideIcon,
} from 'lucide-react';
import { useReveal } from './useReveal';

type IconColor = 'copper' | 'emerald' | 'violet' | 'amber';

const ICON_BG: Record<IconColor, string> = {
  copper: 'btn-primary-gradient glow-primary-sm',
  emerald:
    'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  violet:
    'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber:
    'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
};

interface Feature {
  icon: LucideIcon;
  color: IconColor;
  title: string;
  body: string;
  tags: string[];
}

const FEATURES: Feature[] = [
  {
    icon: KeyRound,
    color: 'copper',
    title: 'Auth & Sessions',
    body: 'JWT + refresh-token rotation, TOTP/2FA, invitations, password reset, session listing, login history. Mailpit-wired for dev SMTP.',
    tags: ['JWT', '2FA', 'API keys'],
  },
  {
    icon: Users,
    color: 'emerald',
    title: 'Multi-tenancy',
    body: 'Global EF query filters keep tenant data isolated automatically. Platform admins still see everything; tenant users see only theirs.',
    tags: ['RLS-style', 'Branding'],
  },
  {
    icon: Shield,
    color: 'violet',
    title: 'RBAC & Permissions',
    body: 'Role/permission matrix with policy-based authorization. Permissions mirrored across BE/FE/Mobile so adding one is a single source-of-truth edit.',
    tags: ['Roles', 'Policies'],
  },
  {
    icon: CreditCard,
    color: 'amber',
    title: 'Billing & Plans',
    body: 'Subscription plans CRUD, plan changes with proration, usage tracking, payment records. Stripe-adapter-ready, no provider lock-in.',
    tags: ['Plans', 'Usage'],
  },
  {
    icon: ScrollText,
    color: 'copper',
    title: 'Audit Trail',
    body: 'Every state-changing action logged with actor, tenant, before/after diff. Filterable, immutable, exportable to CSV or PDF.',
    tags: ['Immutable', 'CSV/PDF'],
  },
  {
    icon: Webhook,
    color: 'emerald',
    title: 'Webhooks',
    body: 'Endpoint CRUD, signed deliveries, retry with exponential backoff, delivery log, secret rotation, manual test-fire. Outbox pattern under the hood.',
    tags: ['Outbox', 'HMAC'],
  },
  {
    icon: Flag,
    color: 'violet',
    title: 'Feature flags',
    body: 'Tenant-scoped overrides, opt-out, enforcement modes. Ship behind a flag, ramp without redeploys.',
    tags: ['Tenant override'],
  },
  {
    icon: Activity,
    color: 'amber',
    title: 'Observability',
    body: 'OpenTelemetry → Jaeger, Prometheus metrics, Serilog structured logs with conversation-id correlation across every consumer.',
    tags: ['OTel', 'Jaeger', 'Prom'],
  },
];

export function FeatureGrid() {
  const head = useReveal<HTMLDivElement>();
  const grid = useReveal<HTMLDivElement>();
  return (
    <section id="product" className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24">
        <div ref={head.ref} data-revealed={head.revealed} className="reveal-up">
          <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
            What's already in
          </div>
          <h2 className="text-[34px] sm:text-[40px] font-light tracking-[-0.025em] leading-[1.12] mb-4 font-display max-w-[720px]">
            Fifteen capabilities.
            <br />
            <em className="not-italic font-medium gradient-text">Eight you'd dread building from scratch.</em>
          </h2>
          <p className="text-[15px] leading-[1.6] max-w-[600px] mb-12 text-muted-foreground">
            Real implementations of the boring-but-load-bearing pieces — JWT rotation, RBAC matrices,
            transactional outbox, billing proration, audit diffs. The ones that take weeks to get right and
            months to clean up.
          </p>
        </div>

        <div ref={grid.ref} data-revealed={grid.revealed} className="reveal-snap grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map((f) => {
            const Icon = f.icon;
            return (
              <div
                key={f.title}
                className="surface-glass hover-lift-card rounded-2xl p-5 group relative"
              >
                <div
                  className={`relative w-10 h-10 rounded-xl flex items-center justify-center mb-4 text-white ${ICON_BG[f.color]} transition-transform duration-200 group-hover:scale-110`}
                >
                  <Icon className="h-[18px] w-[18px]" strokeWidth={2} />
                  {/* Verified flash — appears once on reveal */}
                  {grid.revealed && (
                    <span className="feature-check absolute -top-1 -right-1 h-4 w-4 rounded-full bg-[var(--color-accent-500)] flex items-center justify-center shadow-[0_0_8px_color-mix(in_srgb,var(--color-accent-500)_60%,transparent)]">
                      <Check className="h-2.5 w-2.5 text-white" strokeWidth={3} />
                    </span>
                  )}
                </div>
                <h3 className="text-[14px] font-semibold mb-1.5 text-foreground tracking-tight">
                  {f.title}
                </h3>
                <p className="text-[12px] leading-[1.6] text-muted-foreground mb-3">{f.body}</p>
                <div className="flex gap-1 flex-wrap">
                  {f.tags.map((t) => (
                    <span
                      key={t}
                      className="font-mono text-[9px] px-1.5 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)] text-[var(--tinted-fg)]"
                    >
                      {t}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
