import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ROUTES } from '@/config';

export function HeroSection() {
  return (
    <section className="px-7 pt-8 pb-10 max-w-[760px] relative z-[2]">
      <div className="inline-flex items-center gap-2 px-2.5 py-1 rounded-full mb-4 text-[10px] font-bold uppercase tracking-[0.18em] bg-[color-mix(in_srgb,var(--color-accent-500)_8%,transparent)] text-[var(--color-accent-700)] border border-[color-mix(in_srgb,var(--color-accent-500)_20%,transparent)]">
        Open source · Multi-tenant · Production-grade
      </div>
      <h1 className="text-[44px] font-extralight tracking-[-0.04em] leading-[1.05] mb-3.5 font-display text-foreground">
        Stop rebuilding the foundation.<br />
        <em className="not-italic font-medium gradient-text">Build what's actually yours.</em>
      </h1>
      <p className="text-sm leading-[1.6] max-w-[540px] mb-5 text-muted-foreground">
        A full-stack CQRS starter spanning .NET 10, React 19, and Flutter 3 — with the fifteen things every SaaS quietly needs (auth, RBAC, multi-tenancy, billing, audit, webhooks, feature flags, observability) already wired together. Skip a quarter of foundation work and ship the part only your team can build.
      </p>
      <div className="flex flex-wrap gap-2.5 mb-4">
        <Button asChild>
          <a href={import.meta.env.VITE_GITHUB_URL || '#github'}>Clone on GitHub →</a>
        </Button>
        <Button variant="outline" asChild>
          <Link to={ROUTES.LOGIN}>Read the architecture</Link>
        </Button>
      </div>
      <div className="text-[11px] text-muted-foreground">
        <span className="text-foreground font-semibold">Three clients</span> · TypeScript-strict · CQRS via MediatR · Apache-2.0
      </div>
    </section>
  );
}
