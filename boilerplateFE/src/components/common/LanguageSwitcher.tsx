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
          className="flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
        >
          <Globe className="h-4 w-4" />
          <span>{LANGUAGES.find((l) => l.code === language)?.label}</span>
        </button>
        {isOpen && (
          <div className="absolute end-0 top-full mt-1 z-50 w-36 rounded-lg border bg-popover py-1 shadow-lg">
            {LANGUAGES.map((lang) => (
              <button
                key={lang.code}
                onClick={() => changeLanguage(lang.code)}
                className={cn(
                  'w-full px-4 py-2 text-start text-sm transition-colors hover:bg-accent',
                  language === lang.code
                    ? 'text-primary font-medium'
                    : 'text-muted-foreground'
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
        <div className="absolute ltr:right-0 rtl:left-0 mt-2 w-36 rounded-lg border bg-popover py-1 shadow-lg z-50">
          {LANGUAGES.map((lang) => (
            <button
              key={lang.code}
              onClick={() => changeLanguage(lang.code)}
              className={cn(
                'w-full px-3 py-2 text-sm text-left transition-colors',
                language === lang.code
                  ? 'bg-accent text-primary'
                  : 'text-foreground hover:bg-muted'
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
