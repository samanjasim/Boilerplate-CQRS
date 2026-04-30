import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';
import { PROVIDER_LOGOS, isKnownProvider } from './providerLogos';

interface ProviderLogoProps {
  provider: string;
  size?: 'sm' | 'md';
  className?: string;
}

const SIZE_CLASSES: Record<NonNullable<ProviderLogoProps['size']>, string> = {
  sm: 'size-5',
  md: 'size-8',
};

export function ProviderLogo({ provider, size = 'md', className }: ProviderLogoProps) {
  const { t } = useTranslation();

  if (isKnownProvider(provider)) {
    const svg = PROVIDER_LOGOS[provider] as string;
    return (
      <span
        role="img"
        aria-label={provider}
        className={cn(
          'inline-flex items-center justify-center rounded-md bg-[var(--active-bg)]/40 text-[var(--tinted-fg)]',
          SIZE_CLASSES[size],
          className,
        )}
        // Logo strings are static, hardcoded SVG with no user input — safe to inject
        // eslint-disable-next-line react/no-danger
        dangerouslySetInnerHTML={{ __html: svg }}
      />
    );
  }

  const providerStr = String(provider);
  const initial = providerStr.trim().charAt(0).toUpperCase() || '?';
  return (
    <span
      role="img"
      aria-label={t('communication.providers.unknown', { name: providerStr })}
      className={cn(
        'inline-flex items-center justify-center rounded-md bg-[var(--active-bg)] text-[var(--active-text)] font-semibold',
        SIZE_CLASSES[size],
        size === 'sm' ? 'text-xs' : 'text-sm',
        className,
      )}
    >
      {initial}
    </span>
  );
}
