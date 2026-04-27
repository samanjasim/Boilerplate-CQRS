const POINTS: { num: string; title: string; body: string }[] = [
  { num: '01', title: 'Sealed primary constructors.', body: 'Less ceremony, smaller files, no DI mistakes.' },
  { num: '02', title: 'Result<T> everywhere.', body: 'No exceptions for control flow. Controllers map to HTTP via HandleResult().' },
  { num: '03', title: 'Outbox over IPublishEndpoint.', body: 'Events commit with the business row. Architecture test fails the build if you regress.' },
  { num: '04', title: 'Validators auto-discovered.', body: 'FluentValidation + a MediatR pipeline behavior — drop a class in, it runs.' },
];

export function CodeSection() {
  return (
    <section className="px-7 py-9 relative z-[2]">
      <div className="text-[10px] font-bold uppercase tracking-[0.18em] text-primary mb-2">Show, don't tell</div>
      <h2 className="text-[26px] font-light tracking-[-0.025em] leading-[1.15] mb-2 font-display">
        Real handlers.<br />
        <em className="not-italic font-medium gradient-text">Transactional outbox, by default.</em>
      </h2>
      <p className="text-[13px] leading-[1.55] max-w-[540px] mb-6 text-muted-foreground">
        Every command handler is a sealed primary-constructor record. Events are scheduled through a collector — never published mid-handler — so the row commits atomically with the business write.
      </p>

      <div className="grid gap-5 lg:grid-cols-[1.1fr_1fr] items-start">
        <div className="rounded-[10px] overflow-hidden bg-[#1c1815] text-[#d4cfc3] border border-border shadow-float font-mono text-[11px] leading-[1.65]">
          <div className="px-3 py-2 text-[10px] flex gap-1.5 items-center bg-white/5 border-b border-white/[0.06] text-muted-foreground">
            <span className="px-2 py-0.5 rounded bg-[color-mix(in_srgb,var(--color-primary)_18%,transparent)] text-[var(--color-primary-300)] font-sans font-semibold">C# · Application</span>
            <span className="ml-auto">RegisterTenantCommandHandler.cs</span>
          </div>
          <div className="p-4">
            <div><span className="text-[#a5b4fc]">internal sealed class</span> <span className="text-[#6ee7b7]">RegisterTenantCommandHandler</span>(</div>
            <div>  <span className="text-[#6ee7b7]">IApplicationDbContext</span> context,</div>
            <div>  <span className="text-[#6ee7b7]">IIntegrationEventCollector</span> events) <span className="text-muted-foreground italic">// not IPublishEndpoint</span></div>
            <div>  : <span className="text-[#6ee7b7]">IRequestHandler</span>&lt;<span className="text-[#6ee7b7]">RegisterTenantCommand</span>, <span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt;</div>
            <div>{`{`}</div>
            <div>  <span className="text-[#a5b4fc]">public async</span> <span className="text-[#6ee7b7]">Task</span>&lt;<span className="text-[#6ee7b7]">Result</span>&lt;<span className="text-[#6ee7b7]">Guid</span>&gt;&gt; <span className="text-[#fbbf24]">Handle</span>(</div>
            <div>    <span className="text-[#6ee7b7]">RegisterTenantCommand</span> cmd, <span className="text-[#6ee7b7]">CancellationToken</span> ct)</div>
            <div>  {`{`}</div>
            <div>    <span className="text-[#a5b4fc]">var</span> tenant = <span className="text-[#6ee7b7]">Tenant</span>.<span className="text-[#fbbf24]">Create</span>(cmd.<span className="text-[#fbbf24]">Name</span>, cmd.<span className="text-[#fbbf24]">Slug</span>);</div>
            <div>    context.Tenants.<span className="text-[#fbbf24]">Add</span>(tenant);</div>
            <div>    events.<span className="text-[#fbbf24]">Schedule</span>(<span className="text-[#a5b4fc]">new</span> <span className="text-[#6ee7b7]">TenantRegisteredEvent</span>(tenant.<span className="text-[#fbbf24]">Id</span>));</div>
            <div>    <span className="text-[#a5b4fc]">await</span> context.<span className="text-[#fbbf24]">SaveChangesAsync</span>(ct); <span className="text-muted-foreground italic">// atomic</span></div>
            <div>    <span className="text-[#a5b4fc]">return</span> <span className="text-[#6ee7b7]">Result</span>.<span className="text-[#fbbf24]">Success</span>(tenant.<span className="text-[#fbbf24]">Id</span>);</div>
            <div>  {`}`}</div>
            <div>{`}`}</div>
          </div>
        </div>

        <div className="flex flex-col gap-3 pt-1">
          {POINTS.map((p) => (
            <div key={p.num} className="flex gap-2.5">
              <span className="font-mono text-[10px] font-bold pt-px min-w-[18px] text-primary">{p.num}</span>
              <span className="text-[12px] leading-[1.55] text-foreground">
                <strong className="font-semibold">{p.title}</strong> {p.body}
              </span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
