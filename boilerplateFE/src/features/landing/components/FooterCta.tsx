import { useState } from 'react';
import { ArrowUpRight, BookOpen, Check, Copy, Github, MessageSquare, Newspaper } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

const GITHUB_URL = import.meta.env.VITE_GITHUB_URL || 'https://github.com/<org>/boilerplate-cqrs';
const CLONE_CMD = `git clone ${GITHUB_URL.replace(/^https:\/\//, '')}`;

const NEXT_LINKS: { title: string; body: string; icon: LucideIcon; href: string }[] = [
  { title: 'Read the docs', body: 'Architecture, patterns, and recipes for shipping fast.', icon: BookOpen, href: '#docs' },
  { title: 'Star on GitHub', body: 'Track the changelog. Open an issue. Send a PR.', icon: Github, href: GITHUB_URL },
  { title: 'Join the community', body: 'Discord for the team. Office hours weekly.', icon: MessageSquare, href: '#community' },
  { title: 'Changelog', body: 'Every shipped commit, grouped by milestone.', icon: Newspaper, href: '#changelog' },
];

export function FooterCta() {
  const [copied, setCopied] = useState(false);

  const onCopy = () => {
    navigator.clipboard?.writeText(CLONE_CMD).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  return (
    <section className="relative">
      <div className="mx-auto max-w-6xl px-7 py-24 lg:py-32">
        {/* "What's next?" grid */}
        <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3 text-center">
          What's next
        </div>
        <h3 className="text-[26px] sm:text-[32px] font-light tracking-[-0.025em] leading-[1.15] mb-10 font-display text-center max-w-[640px] mx-auto">
          Take the next step.
        </h3>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 mb-20">
          {NEXT_LINKS.map((link) => {
            const Icon = link.icon;
            return (
              <a
                key={link.title}
                href={link.href}
                className="surface-glass hover-lift-card rounded-2xl p-5 border border-border/40 group block"
              >
                <div className="flex items-start justify-between mb-3">
                  <div className="w-9 h-9 rounded-xl flex items-center justify-center bg-card/60 border border-border/40 text-primary">
                    <Icon className="h-4 w-4" strokeWidth={2} />
                  </div>
                  <ArrowUpRight className="h-4 w-4 text-muted-foreground group-hover:text-primary transition-colors" />
                </div>
                <div className="text-[14px] font-semibold text-foreground mb-1 tracking-tight">
                  {link.title}
                </div>
                <div className="text-[11.5px] text-muted-foreground leading-[1.55]">{link.body}</div>
              </a>
            );
          })}
        </div>

        {/* CTA */}
        <div className="text-center">
          <h3 className="text-[36px] sm:text-[48px] font-extralight tracking-[-0.035em] leading-[1.08] mb-5 font-display">
            Ship the boring parts
            <br />
            <em className="not-italic font-medium gradient-text">before lunch.</em>
          </h3>
          <p className="text-[15px] mb-8 text-muted-foreground max-w-[520px] mx-auto leading-[1.65]">
            Clone, run{' '}
            <code className="font-mono text-[13px] px-1.5 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_10%,transparent)] text-[var(--tinted-fg)]">
              docker compose up
            </code>
            , log in as{' '}
            <code className="font-mono text-[13px] px-1.5 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_10%,transparent)] text-[var(--tinted-fg)]">
              superadmin@starter.com
            </code>{' '}
            in 60 seconds.
          </p>

          <button
            onClick={onCopy}
            className="group inline-flex items-center gap-2.5 px-4 py-2.5 rounded-xl font-mono text-[12px] mb-6 bg-[#1c1815] text-[var(--color-primary-300)] border border-white/[0.08] hover:bg-[#252018] hover:border-white/[0.14] transition-colors shadow-float"
          >
            <span className="text-[#9b8978]">$</span>
            <span>{CLONE_CMD}</span>
            <span className="ml-1 text-[#9b8978] group-hover:text-[var(--color-primary-300)]">
              {copied ? <Check className="h-3.5 w-3.5 text-[var(--color-accent-400)]" /> : <Copy className="h-3.5 w-3.5" />}
            </span>
          </button>

          <div className="text-[12px] text-muted-foreground flex flex-wrap items-center justify-center gap-x-3 gap-y-1.5 mt-8 pt-8 border-t border-border/30 max-w-[480px] mx-auto">
            <a href="#product" className="hover:text-foreground transition-colors">Product</a>
            <span className="opacity-40">·</span>
            <a href="#architecture" className="hover:text-foreground transition-colors">Architecture</a>
            <span className="opacity-40">·</span>
            <a href={GITHUB_URL} className="hover:text-foreground transition-colors">GitHub</a>
            <span className="opacity-40">·</span>
            <span>Apache-2.0</span>
            <span className="opacity-40">·</span>
            <span>© 2026</span>
          </div>
        </div>
      </div>
    </section>
  );
}
