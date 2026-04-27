import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ROUTES } from '@/config';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';

const APP_NAME = import.meta.env.VITE_APP_NAME || 'Starter';

const NAV_LINKS = [
  { label: 'Product', href: '#product' },
  { label: 'Architecture', href: '#architecture' },
  { label: 'Docs', href: '#docs' },
  { label: 'GitHub', href: import.meta.env.VITE_GITHUB_URL || 'https://github.com' },
];

export function LandingNav() {
  return (
    <nav className="sticky top-0 z-50 surface-glass border-b border-border/30">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-7 py-3.5">
        <Link to={ROUTES.LANDING} className="flex items-center gap-2.5 font-semibold text-sm">
          <div className="flex h-[26px] w-[26px] items-center justify-center rounded-md btn-primary-gradient glow-primary-sm text-primary-foreground text-xs font-bold">
            {APP_NAME.charAt(0)}
          </div>
          <span className="text-foreground">{APP_NAME}</span>
        </Link>
        <div className="hidden md:flex items-center gap-7 text-xs font-medium">
          {NAV_LINKS.map((link) => (
            <a
              key={link.label}
              href={link.href}
              className="text-muted-foreground hover:text-foreground transition-colors"
            >
              {link.label}
            </a>
          ))}
        </div>
        <div className="flex items-center gap-1">
          <LanguageSwitcher />
          <ThemeToggle />
          <div className="h-5 w-px bg-border/50 mx-2" />
          <Button variant="ghost" size="sm" asChild>
            <Link to={ROUTES.LOGIN}>Sign in</Link>
          </Button>
          <Button size="sm" asChild>
            <Link to={ROUTES.REGISTER_TENANT}>Get started</Link>
          </Button>
        </div>
      </div>
    </nav>
  );
}
