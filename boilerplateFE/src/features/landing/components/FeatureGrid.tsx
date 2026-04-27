type IconColor = 'copper' | 'emerald' | 'violet' | 'amber';

const ICON_BG: Record<IconColor, string> = {
  copper: 'btn-primary-gradient glow-primary-sm',
  emerald: 'bg-gradient-to-br from-[var(--color-accent-400)] to-[var(--color-accent-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-accent-500)_30%,transparent)]',
  violet: 'bg-gradient-to-br from-[var(--color-violet-400)] to-[var(--color-violet-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-violet-500)_30%,transparent)]',
  amber: 'bg-gradient-to-br from-[var(--color-amber-400)] to-[var(--color-amber-700)] shadow-[0_4px_12px_color-mix(in_srgb,var(--color-amber-500)_30%,transparent)]',
};

const FEATURES: { letter: string; color: IconColor; title: string; body: string; tags: string[] }[] = [
  { letter: 'A', color: 'copper', title: 'Auth & Sessions', body: 'JWT + refresh-token rotation, TOTP/2FA, invitations, password reset, session listing, login history. Mailpit-wired for dev SMTP.', tags: ['JWT', '2FA', 'API keys'] },
  { letter: 'T', color: 'emerald', title: 'Multi-tenancy', body: 'Global EF query filters keep tenant data isolated automatically. Platform admins still see everything; tenant users see only theirs. Onboarding, branding, status, business info.', tags: ['RLS-style', 'Branding'] },
  { letter: 'R', color: 'violet', title: 'RBAC & Permissions', body: 'Role/permission matrix with policy-based authorization. Permissions mirrored across BE/FE/Mobile so adding one is a single source-of-truth edit.', tags: ['Roles', 'Policies'] },
  { letter: '$', color: 'amber', title: 'Billing & Plans', body: 'Subscription plans CRUD, plan changes with proration, usage tracking, payment records. Stripe-adapter-ready, no provider lock-in.', tags: ['Plans', 'Usage'] },
  { letter: 'L', color: 'copper', title: 'Audit Trail', body: 'Every state-changing action logged with actor, tenant, before/after diff. Filterable, immutable, exportable to CSV or PDF.', tags: ['Immutable', 'CSV/PDF'] },
  { letter: 'W', color: 'emerald', title: 'Webhooks', body: 'Endpoint CRUD, signed deliveries, retry with exponential backoff, delivery log, secret rotation, manual test-fire. Outbox pattern under the hood.', tags: ['Outbox', 'HMAC'] },
  { letter: 'F', color: 'violet', title: 'Feature flags', body: 'Tenant-scoped overrides, opt-out, enforcement modes. Ship behind a flag, ramp without redeploys.', tags: ['Tenant override'] },
  { letter: 'O', color: 'amber', title: 'Observability', body: 'OpenTelemetry → Jaeger, Prometheus metrics, Serilog structured logs with conversation-id correlation across every consumer.', tags: ['OTel', 'Jaeger', 'Prom'] },
];

export function FeatureGrid() {
  return (
    <section id="product" className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">What's already in</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Fifteen capabilities.<br />
        <em className="not-italic font-medium gradient-text">Eight you'd dread building from scratch.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Real implementations of the boring-but-load-bearing pieces — JWT rotation, RBAC matrices, transactional outbox, billing proration, audit diffs. The ones that take weeks to get right and months to clean up.
      </p>
      <div className="grid gap-3 md:grid-cols-2">
        {FEATURES.map((f) => (
          <div key={f.title} className="surface-glass rounded-[10px] p-4">
            <div className={`w-[26px] h-[26px] rounded-[7px] flex items-center justify-center text-[13px] font-bold mb-2.5 text-white ${ICON_BG[f.color]}`}>
              {f.letter}
            </div>
            <h3 className="text-[13px] font-semibold mb-1 text-foreground">{f.title}</h3>
            <p className="text-[11px] leading-[1.55] text-muted-foreground">{f.body}</p>
            <div className="mt-2 flex gap-1 flex-wrap">
              {f.tags.map((t) => (
                <span key={t} className="font-mono text-[9px] px-1.5 py-px rounded bg-[color-mix(in_srgb,var(--color-primary)_6%,transparent)] text-[var(--color-primary-700)] dark:text-[var(--color-primary-300)]">
                  {t}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
