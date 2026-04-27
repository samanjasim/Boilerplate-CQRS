import { Link } from 'react-router-dom';
import { TokensSection } from '../components/sections/TokensSection';
import { TypographySection } from '../components/sections/TypographySection';
import { ButtonsSection } from '../components/sections/ButtonsSection';
import { FormsSection } from '../components/sections/FormsSection';
import { CardsSection } from '../components/sections/CardsSection';
import { BadgesSection } from '../components/sections/BadgesSection';
import { TablesSection } from '../components/sections/TablesSection';
import { DialogsSection } from '../components/sections/DialogsSection';
import { DropdownsSection } from '../components/sections/DropdownsSection';
import { AvatarsSection } from '../components/sections/AvatarsSection';
import { CommonSection } from '../components/sections/CommonSection';
import { FunctionalSection } from '../components/sections/FunctionalSection';

const SECTIONS: { id: string; label: string }[] = [
  { id: 'tokens', label: 'Tokens' },
  { id: 'typography', label: 'Typography' },
  { id: 'buttons', label: 'Buttons' },
  { id: 'forms', label: 'Forms' },
  { id: 'cards', label: 'Cards' },
  { id: 'badges', label: 'Badges' },
  { id: 'tables', label: 'Tables' },
  { id: 'dialogs', label: 'Dialogs' },
  { id: 'dropdowns', label: 'Dropdowns' },
  { id: 'avatars', label: 'Avatars + small' },
  { id: 'common', label: 'Common' },
  { id: 'functional', label: 'Functional UI' },
];

export default function StyleguidePage() {
  return (
    <div className="aurora-canvas min-h-screen bg-background text-foreground">
      <div className="mx-auto flex max-w-6xl gap-10 px-6 py-10">
        <aside className="sticky top-10 hidden h-fit w-44 shrink-0 lg:block">
          <div className="text-[10px] font-bold uppercase tracking-[0.2em] text-muted-foreground mb-3">
            Sections
          </div>
          <nav className="flex flex-col gap-1">
            {SECTIONS.map((s) => (
              <a
                key={s.id}
                href={`#${s.id}`}
                className="rounded-md px-2 py-1.5 text-sm text-muted-foreground hover:bg-secondary hover:text-foreground transition-colors"
              >
                {s.label}
              </a>
            ))}
          </nav>
          <div className="mt-6 border-t border-border pt-4 text-xs text-muted-foreground">
            <Link to="/" className="hover:text-foreground">← Back to app</Link>
          </div>
        </aside>

        <main className="min-w-0 flex-1">
          <header className="mb-8">
            <div className="text-[11px] font-bold uppercase tracking-[0.2em] text-primary mb-2">
              J4 Spectrum · dev-only
            </div>
            <h1 className="text-4xl font-extralight tracking-tight text-foreground">
              <span className="gradient-text">Style Reference</span>
            </h1>
            <p className="mt-3 max-w-prose text-sm text-muted-foreground">
              Live render of every primitive and shared component in the J4 Spectrum design system.
              This page is registered only in dev builds (<code>import.meta.env.DEV</code>).
            </p>
          </header>

          <div className="space-y-0">
            <TokensSection />
            <TypographySection />
            <ButtonsSection />
            <FormsSection />
            <CardsSection />
            <BadgesSection />
            <TablesSection />
            <DialogsSection />
            <DropdownsSection />
            <AvatarsSection />
            <CommonSection />
            <FunctionalSection />
          </div>
        </main>
      </div>
    </div>
  );
}
