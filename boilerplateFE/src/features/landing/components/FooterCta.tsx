const GITHUB_URL = import.meta.env.VITE_GITHUB_URL || 'https://github.com/<org>/boilerplate-cqrs';

export function FooterCta() {
  return (
    <section className="px-7 py-9 text-center relative z-[2]">
      <h3 className="text-[24px] font-light tracking-[-0.025em] leading-[1.2] mb-2.5 font-display">
        Ship the boring parts<br />
        <em className="not-italic font-medium gradient-text">before lunch.</em>
      </h3>
      <p className="text-[13px] mb-4 text-muted-foreground">
        Clone, run <code className="font-mono text-[12px] px-1 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)]">docker compose up</code>, log in as <code className="font-mono text-[12px] px-1 rounded bg-[color-mix(in_srgb,var(--color-primary)_8%,transparent)]">superadmin@starter.com</code> in 60 seconds.
      </p>
      <a
        href={GITHUB_URL}
        className="inline-block px-3.5 py-2 rounded-lg font-mono text-[11px] mb-3.5 bg-[#1c1815] text-[var(--color-primary-300)] border border-white/[0.08] hover:bg-[#252018] transition-colors"
      >
        git clone {GITHUB_URL.replace(/^https:\/\//, '')}
      </a>
      <div className="text-[11px] text-muted-foreground space-x-2">
        <a href="#product" className="hover:text-foreground">Product</a>
        <span>·</span>
        <a href="#docs" className="hover:text-foreground">Docs</a>
        <span>·</span>
        <a href={GITHUB_URL} className="hover:text-foreground">GitHub</a>
        <span>·</span>
        <span>License</span>
        <span>·</span>
        <span>© 2026</span>
      </div>
    </section>
  );
}
