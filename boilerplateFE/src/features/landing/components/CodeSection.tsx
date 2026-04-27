import { useReveal } from './useReveal';

const POINTS: { num: string; title: string; body: string }[] = [
  { num: '01', title: 'Sealed primary constructors.', body: 'Less ceremony, smaller files, no DI mistakes.' },
  { num: '02', title: 'Result<T> everywhere.', body: 'No exceptions for control flow. Controllers map to HTTP via HandleResult().' },
  { num: '03', title: 'Outbox over IPublishEndpoint.', body: 'Events commit with the business row. Architecture test fails the build if you regress.' },
  { num: '04', title: 'Validators auto-discovered.', body: 'FluentValidation + a MediatR pipeline behavior — drop a class in, it runs.' },
];

export function CodeSection() {
  const head = useReveal<HTMLDivElement>();
  const body = useReveal<HTMLDivElement>();
  return (
    <section className="relative">
      <div className="mx-auto max-w-6xl px-7 py-20 lg:py-24">
        <div ref={head.ref} data-revealed={head.revealed} className="reveal-up">
          <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-3">
            Show, don't tell
          </div>
          <h2 className="text-[34px] sm:text-[40px] font-light tracking-[-0.025em] leading-[1.12] mb-4 font-display max-w-[720px]">
            Real handlers.
            <br />
            <em className="not-italic font-medium gradient-text">Transactional outbox, by default.</em>
          </h2>
          <p className="text-[15px] leading-[1.6] max-w-[600px] mb-12 text-muted-foreground">
            Every command handler is a sealed primary-constructor record. Events are scheduled through a
            collector — never published mid-handler — so the row commits atomically with the business write.
          </p>
        </div>

        <div ref={body.ref} data-revealed={body.revealed} className="reveal-stagger grid gap-8 lg:grid-cols-[1.15fr_1fr] items-start">
          <div className="rounded-2xl overflow-hidden bg-[#1c1815] text-[#d4cfc3] border border-border/40 shadow-float">
            <div className="px-4 py-2.5 text-[10px] flex gap-2 items-center bg-white/[0.04] border-b border-white/[0.06] text-[#9b8978]">
              <span className="flex gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-[#c4574c]/70" />
                <span className="h-2.5 w-2.5 rounded-full bg-[#d97706]/70" />
                <span className="h-2.5 w-2.5 rounded-full bg-[#10b981]/70" />
              </span>
              <span className="px-2 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_22%,transparent)] text-[var(--color-primary-300)] font-sans font-semibold ml-2">
                C# · Application
              </span>
              <span className="ml-auto font-mono">RegisterTenantCommandHandler.cs</span>
            </div>
            <div className="p-5 font-mono text-[12px] leading-[1.7] code-typing">
              <div><span className="text-[#a5b4fc]">internal sealed class</span> <span className="text-[#6ee7b7]">RegisterTenantCommandHandler</span>(</div>
              <div>  <span className="text-[#6ee7b7]">IApplicationDbContext</span> context,</div>
              <div>  <span className="text-[#6ee7b7]">IIntegrationEventCollector</span> events) <span className="text-[#6b6b6b] italic">// not IPublishEndpoint</span></div>
              <div>  : <span className="text-[#6ee7b7]">IRequestHandler</span>&lt;<span className="text-[#6ee7b7]">RegisterTenantCommand</span>, <span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt;</div>
              <div>{'{'}</div>
              <div>  <span className="text-[#a5b4fc]">public async</span> <span className="text-[#6ee7b7]">Task</span>&lt;<span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt; <span className="text-[#fbbf24]">Handle</span>(</div>
              <div>    <span className="text-[#6ee7b7]">RegisterTenantCommand</span> cmd, <span className="text-[#6ee7b7]">CancellationToken</span> ct)</div>
              <div>  {'{'}</div>
              <div>    <span className="text-[#a5b4fc]">var</span> tenant = <span className="text-[#6ee7b7]">Tenant</span>.<span className="text-[#fbbf24]">Create</span>(cmd.<span className="text-[#fbbf24]">Name</span>, cmd.<span className="text-[#fbbf24]">Slug</span>);</div>
              <div>    context.Tenants.<span className="text-[#fbbf24]">Add</span>(tenant);</div>
              <div>    events.<span className="text-[#fbbf24]">Schedule</span>(<span className="text-[#a5b4fc]">new</span> <span className="text-[#6ee7b7]">TenantRegisteredEvent</span>(tenant.<span className="text-[#fbbf24]">Id</span>));</div>
              <div>    <span className="text-[#a5b4fc]">await</span> context.<span className="text-[#fbbf24]">SaveChangesAsync</span>(ct); <span className="text-[#6b6b6b] italic">// atomic</span></div>
              <div>    <span className="text-[#a5b4fc]">return</span> <span className="text-[#6ee7b7]">Result</span>.<span className="text-[#fbbf24]">Success</span>(tenant.<span className="text-[#fbbf24]">Id</span>); <span className="caret-blink text-[#f0ae81]" /></div>
              <div>  {'}'}</div>
              <div>{'}'}</div>
            </div>
          </div>

          <div className="flex flex-col gap-5 lg:pt-3">
            {POINTS.map((p) => (
              <div key={p.num} className="flex gap-4">
                <span className="font-mono text-[11px] font-bold pt-0.5 min-w-[26px] text-primary">
                  {p.num}
                </span>
                <span className="text-[13px] leading-[1.6] text-foreground">
                  <strong className="font-semibold">{p.title}</strong>{' '}
                  <span className="text-muted-foreground">{p.body}</span>
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
