import { useEffect } from 'react';
import { activePreset, presets } from '@/config/theme.config';
import { useUIStore, selectTheme } from '@/stores';

export function useThemePreset() {
  const theme = useUIStore(selectTheme);

  useEffect(() => {
    const preset = presets[activePreset];
    if (!preset) return;

    const applyPreset = () => {
      const isDark = document.documentElement.classList.contains('dark');
      const mode = isDark ? preset.dark : preset.light;
      const root = document.documentElement.style;

      root.setProperty('--primary', mode.primary);
      root.setProperty('--primary-foreground', mode.primaryForeground);
      root.setProperty('--ring', mode.ring);

      // Set bare --color-primary for bg-primary/text-primary utilities
      root.setProperty('--color-primary', preset.primaryScale['600']);
      root.setProperty('--color-primary-foreground', '#ffffff');
      root.setProperty('--color-ring', preset.primaryScale['600']);

      for (const [shade, hex] of Object.entries(preset.primaryScale)) {
        root.setProperty(`--color-primary-${shade}`, hex);
      }
      for (const [shade, hex] of Object.entries(preset.accentScale)) {
        root.setProperty(`--color-accent-${shade}`, hex);
      }

      // Set semantic surface colors for bg-card, bg-popover, etc.
      if (isDark) {
        root.setProperty('--color-background', '#17150f');
        root.setProperty('--color-foreground', '#f0ede8');
        root.setProperty('--color-card', '#211f1a');
        root.setProperty('--color-card-foreground', '#f0ede8');
        root.setProperty('--color-popover', '#211f1a');
        root.setProperty('--color-popover-foreground', '#f0ede8');
        root.setProperty('--color-secondary', '#1c1a16');
        root.setProperty('--color-secondary-foreground', '#f0ede8');
        root.setProperty('--color-muted', '#1c1a16');
        root.setProperty('--color-muted-foreground', '#9a9690');
        root.setProperty('--color-accent', '#1c1a16');
        root.setProperty('--color-accent-foreground', '#f0ede8');
        root.setProperty('--color-destructive', '#d4675c');
        root.setProperty('--color-destructive-foreground', '#ffffff');
        root.setProperty('--color-border', '#3a3632');
        root.setProperty('--color-input', '#3a3632');
      } else {
        root.setProperty('--color-background', '#f9f7f3');
        root.setProperty('--color-foreground', '#2c2c2c');
        root.setProperty('--color-card', '#ffffff');
        root.setProperty('--color-card-foreground', '#2c2c2c');
        root.setProperty('--color-popover', '#ffffff');
        root.setProperty('--color-popover-foreground', '#2c2c2c');
        root.setProperty('--color-secondary', '#f5f2ec');
        root.setProperty('--color-secondary-foreground', '#2c2c2c');
        root.setProperty('--color-muted', '#f5f2ec');
        root.setProperty('--color-muted-foreground', '#6b6b6b');
        root.setProperty('--color-accent', '#f5f2ec');
        root.setProperty('--color-accent-foreground', '#2c2c2c');
        root.setProperty('--color-destructive', '#c4574c');
        root.setProperty('--color-destructive-foreground', '#ffffff');
        root.setProperty('--color-border', '#e5e2dc');
        root.setProperty('--color-input', '#e5e2dc');
      }
    };

    applyPreset();

    // Re-apply when system preference changes (for 'system' mode)
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = () => {
      if (theme === 'system') applyPreset();
    };
    mq.addEventListener('change', handleChange);

    // Also observe the dark class for explicit toggles
    const observer = new MutationObserver(() => applyPreset());
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    });

    return () => {
      mq.removeEventListener('change', handleChange);
      observer.disconnect();
    };
  }, [theme]);
}
