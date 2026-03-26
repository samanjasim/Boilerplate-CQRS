import { useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useUIStore, selectLanguage } from '@/stores';
import { useClickOutside } from '@/hooks';
import { LANGUAGES, type LanguageCode } from '@/constants';
import { Button } from '@/components/ui/button';

interface LanguageSwitcherProps {
  variant?: 'ghost' | 'text';
}

export function LanguageSwitcher({ variant = 'ghost' }: LanguageSwitcherProps) {
  const { i18n } = useTranslation();
  const language = useUIStore(selectLanguage);
  const setLanguage = useUIStore((state) => state.setLanguage);
  const [isOpen, setIsOpen] = useState(false);

  const close = useCallback(() => setIsOpen(false), []);
  const ref = useClickOutside<HTMLDivElement>(close);

  const changeLanguage = (code: LanguageCode) => {
    setLanguage(code);
    i18n.changeLanguage(code);
    setIsOpen(false);
  };

  if (variant === 'text') {
    return (
      <div className="relative" ref={ref}>
        <button
          onClick={() => setIsOpen(!isOpen)}
          className="flex items-center gap-1.5 rounded-xl px-3 py-2 text-sm text-muted-foreground hover:bg-secondary hover:text-foreground transition-all duration-150"
        >
          <Globe className="h-4 w-4" />
          <span>{LANGUAGES.find((l) => l.code === language)?.label}</span>
        </button>
        {isOpen && (
          <div className="absolute end-0 top-full mt-1.5 z-50 w-36 rounded-xl border border-border/30 bg-popover p-1.5 shadow-float">
            {LANGUAGES.map((lang) => (
              <button
                key={lang.code}
                onClick={() => changeLanguage(lang.code)}
                className={cn(
                  'w-full rounded-lg px-3 py-2 text-start text-sm transition-colors duration-150',
                  language === lang.code
                    ? 'bg-primary/10 text-primary font-medium'
                    : 'text-muted-foreground hover:bg-secondary hover:text-foreground'
                )}
              >
                {lang.label}
              </button>
            ))}
          </div>
        )}
      </div>
    );
  }

  return (
    <div ref={ref} className="relative">
      <Button variant="ghost" size="sm" onClick={() => setIsOpen(!isOpen)}>
        <Globe className="h-4 w-4" />
        <span className="hidden sm:inline text-xs uppercase">{language}</span>
      </Button>
      {isOpen && (
        <div className="absolute ltr:right-0 rtl:left-0 mt-1.5 w-36 rounded-xl border border-border/30 bg-popover p-1.5 shadow-float z-50">
          {LANGUAGES.map((lang) => (
            <button
              key={lang.code}
              onClick={() => changeLanguage(lang.code)}
              className={cn(
                'w-full rounded-lg px-3 py-2 text-sm text-left transition-colors duration-150',
                language === lang.code
                  ? 'bg-primary/10 text-primary font-medium'
                  : 'text-foreground hover:bg-secondary'
              )}
            >
              {lang.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
