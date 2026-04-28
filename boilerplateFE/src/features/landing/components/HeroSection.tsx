import { useEffect, useState } from 'react';
import { ArrowRight, Github, TrendingUp } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { HeroLinesBackground } from '@/components/common/backgrounds';
import { useCountUp } from '@/hooks';
import { ScrambleText } from './ScrambleText';

export function HeroSection() {
  return (
    <section className="relative isolate">
      <HeroLinesBackground />
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-28 grid lg:grid-cols-[minmax(0,1fr)_minmax(0,440px)] gap-12 items-center relative z-[1]">
        <HeroCopy />
        <div className="hidden lg:block animate-fade-up-delay-3">
          <DashboardPreviewCard />
        </div>
      </div>
    </section>
  );
}

function HeroCopy() {
  return (
    <div>
      <div className="animate-fade-up inline-flex items-center gap-2 px-3 py-1 rounded-full mb-7 text-[10px] font-bold uppercase tracking-[0.18em] bg-[color-mix(in_srgb,var(--color-accent-500)_8%,transparent)] text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] border border-[color-mix(in_srgb,var(--color-accent-500)_22%,transparent)]">
        <span className="pulse-dot" />
        Open source · Multi-tenant · Production-grade
      </div>

      {/* Headline — line 1 fades, line 2 (gradient accent) decrypts */}
      <h1 className="text-[40px] sm:text-[52px] lg:text-[64px] font-extralight tracking-[-0.04em] leading-[1.04] mb-6 font-display text-foreground">
        <span className="block animate-fade-up-delay-1">Stop rebuilding the foundation.</span>
        <em className="block not-italic font-medium gradient-text">
          <ScrambleText
            text="Build what's actually yours."
            duration={1100}
            step={28}
            delay={350}
          />
        </em>
      </h1>

      <p className="animate-fade-up-delay-2 text-[15px] sm:text-base leading-[1.65] max-w-[560px] mb-9 text-muted-foreground">
        A full-stack CQRS starter spanning <span className="text-foreground font-semibold">.NET 10</span>, <span className="text-foreground font-semibold">React 19</span>, and <span className="text-foreground font-semibold">Flutter 3</span> — with the fifteen things every SaaS quietly needs (auth, RBAC, multi-tenancy, billing, audit, webhooks, feature flags, observability) already wired together. Skip a quarter of foundation work and ship the part only your team can build.
      </p>
      <div className="animate-fade-up-delay-3 flex flex-wrap gap-3 mb-7">
        <Button size="lg" asChild>
          <a href={import.meta.env.VITE_GITHUB_URL || '#github'}>
            <Github className="h-4 w-4" />
            Clone on GitHub
            <ArrowRight className="h-4 w-4" />
          </a>
        </Button>
        <Button variant="outline" size="lg" asChild>
          <a href="#architecture">Read the architecture</a>
        </Button>
      </div>
      <div className="animate-fade-up-delay-4 text-[12px] text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1.5">
        <span><span className="text-foreground font-semibold">Three clients</span></span>
        <span className="opacity-40">·</span>
        <span>TypeScript-strict</span>
        <span className="opacity-40">·</span>
        <span>CQRS via MediatR</span>
        <span className="opacity-40">·</span>
        <span>Apache-2.0</span>
      </div>
    </div>
  );
}

/** Dashboard mockup that constructs itself in real time:
 *  outline frame draws via SVG stroke animation, then internal elements
 *  fade in in sequence (title bar → eyebrow → metric → sparkline → list rows). */
function DashboardPreviewCard() {
  const [reducedMotion] = useState(
    () => typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches,
  );
  // Track time since mount so we can stagger the contents after the outline draws
  const [phase, setPhase] = useState(() => (reducedMotion ? 99 : 0));

  useEffect(() => {
    if (reducedMotion) return;
    const timers: ReturnType<typeof setTimeout>[] = [];
    timers.push(setTimeout(() => setPhase(1), 400));   // title bar
    timers.push(setTimeout(() => setPhase(2), 750));   // eyebrow + metric label
    timers.push(setTimeout(() => setPhase(3), 1050));  // hero metric counts up
    timers.push(setTimeout(() => setPhase(4), 1450));  // sparkline draws
    timers.push(setTimeout(() => setPhase(5), 1850));  // list rows fade in
    timers.push(setTimeout(() => setPhase(99), 2400)); // settle: ambient motion only
    return () => timers.forEach((t) => clearTimeout(t));
  }, [reducedMotion]);

  return (
    <div className="relative">
      {/* Outline frame that draws itself */}
      <svg
        aria-hidden
        className="absolute inset-0 w-full h-full pointer-events-none"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
      >
        <rect
          x="0.5"
          y="0.5"
          width="99"
          height="99"
          rx="2"
          ry="2"
          fill="none"
          stroke="color-mix(in srgb, var(--color-primary) 70%, transparent)"
          strokeWidth="0.4"
          vectorEffect="non-scaling-stroke"
          className="draw-outline"
        />
      </svg>

      <div className="surface-glass-strong rounded-2xl shadow-float overflow-hidden border border-border/50">
        {/* Title bar — phase 1 */}
        <div
          className={`flex items-center gap-1.5 px-4 py-2.5 border-b border-border/30 bg-[color-mix(in_srgb,var(--color-primary)_4%,transparent)] transition-opacity duration-500 ${phase >= 1 ? 'opacity-100' : 'opacity-0'}`}
        >
          <span className="h-2.5 w-2.5 rounded-full bg-destructive/60" />
          <span className="h-2.5 w-2.5 rounded-full bg-[var(--color-amber-400)]" />
          <span className="h-2.5 w-2.5 rounded-full bg-[var(--color-accent-500)]" />
          <span className="ml-3 font-mono text-[10px] text-muted-foreground">tenants · overview</span>
        </div>

        <div className="p-5 space-y-4">
          {/* Eyebrow + metric label — phase 2 */}
          <div
            className={`transition-all duration-500 ${phase >= 2 ? 'opacity-100 translate-y-0' : 'opacity-0 -translate-y-2'}`}
          >
            <div className="text-[9px] font-bold uppercase tracking-[0.18em] text-primary mb-1 inline-flex items-center gap-1.5">
              <span className="pulse-dot" />
              Live
            </div>
          </div>

          {/* Hero metric — phase 3 (count up from 0 to 142) */}
          <div className={`-mt-1 transition-opacity duration-500 ${phase >= 2 ? 'opacity-100' : 'opacity-0'}`}>
            <CountUpDisplay target={142} active={phase >= 3} />
            <div
              className={`text-[12px] text-muted-foreground mt-1.5 inline-flex items-center gap-1 transition-opacity duration-500 ${phase >= 3 ? 'opacity-100' : 'opacity-0'}`}
            >
              <TrendingUp className="h-3 w-3 text-[var(--color-accent-600)]" />
              <span className="text-[var(--color-accent-700)] dark:text-[var(--color-accent-300)] font-medium font-mono">+8.2%</span>
              <span>this month</span>
            </div>
          </div>

          {/* Sparkline — phase 4 (path stroke draws in) */}
          <SparklineAnim active={phase >= 4} />

          {/* List rows — phase 5 (fade-up stagger) */}
          <div className="space-y-px rounded-md overflow-hidden">
            {[
              { name: 'Acme Corporation', val: '42 · +8.2%' },
              { name: 'Globex Industries', val: '31 · +2.1%' },
              { name: 'Initech Systems', val: '28 · +1.4%' },
            ].map((row, i) => (
              <div
                key={row.name}
                style={{ transitionDelay: phase >= 5 ? `${i * 80}ms` : '0ms' }}
                className={`flex items-center justify-between px-3 py-2 bg-card/50 text-[12px] transition-all duration-400 ${phase >= 5 ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-1.5'}`}
              >
                <span className="text-foreground font-medium">{row.name}</span>
                <span className="font-mono text-[11px] text-muted-foreground">{row.val}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function CountUpDisplay({ target, active }: { target: number; active: boolean }) {
  const { value } = useCountUp(target, { duration: 900, active });
  return (
    <div className="font-display text-5xl font-extralight tracking-[-0.04em] leading-none text-foreground font-feature-settings">
      {value}
    </div>
  );
}

function SparklineAnim({ active }: { active: boolean }) {
  return (
    <svg viewBox="0 0 280 50" className="w-full h-[50px]" preserveAspectRatio="none">
      <defs>
        <linearGradient id="hero-spark-fill-2" x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="var(--color-primary)" stopOpacity="0.4" />
          <stop offset="100%" stopColor="var(--color-primary)" stopOpacity="0" />
        </linearGradient>
        <linearGradient id="hero-spark-stroke-2" x1="0" x2="1" y1="0" y2="0">
          <stop offset="0%" stopColor="var(--color-primary-700)" />
          <stop offset="100%" stopColor="var(--color-violet-500)" />
        </linearGradient>
      </defs>
      <path
        d="M0,40 L20,38 L40,32 L60,35 L80,28 L100,30 L120,22 L140,24 L160,18 L180,20 L200,12 L220,14 L240,8 L260,10 L280,4 L280,50 L0,50 Z"
        fill="url(#hero-spark-fill-2)"
        style={{
          opacity: active ? 1 : 0,
          transition: 'opacity 0.6s ease 0.3s',
        }}
      />
      <path
        d="M0,40 L20,38 L40,32 L60,35 L80,28 L100,30 L120,22 L140,24 L160,18 L180,20 L200,12 L220,14 L240,8 L260,10 L280,4"
        fill="none"
        stroke="url(#hero-spark-stroke-2)"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
        style={{
          strokeDasharray: 600,
          strokeDashoffset: active ? 0 : 600,
          transition: 'stroke-dashoffset 1.2s cubic-bezier(0.4, 0, 0.2, 1)',
        }}
      />
    </svg>
  );
}
