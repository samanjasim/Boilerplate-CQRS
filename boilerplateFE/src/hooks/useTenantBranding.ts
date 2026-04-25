import { useEffect } from 'react';
import { useAuthStore, selectUser } from '@/stores';

function hexToHSL(hex: string): string {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return '';
  const [, rHex, gHex, bHex] = result;
  if (!rHex || !gHex || !bHex) return '';

  const r = parseInt(rHex, 16) / 255;
  const g = parseInt(gHex, 16) / 255;
  const b = parseInt(bHex, 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  let h = 0;
  let s = 0;
  const l = (max + min) / 2;

  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

    switch (max) {
      case r:
        h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
        break;
      case g:
        h = ((b - r) / d + 2) / 6;
        break;
      case b:
        h = ((r - g) / d + 4) / 6;
        break;
    }
  }

  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}

export function useTenantBranding() {
  const user = useAuthStore(selectUser);

  useEffect(() => {
    if (!user?.tenantId) return;

    const color = user.tenantPrimaryColor;

    if (color) {
      const hsl = hexToHSL(color);
      if (hsl) {
        document.documentElement.style.setProperty('--primary', hsl);
      }
    }

    return () => {
      document.documentElement.style.removeProperty('--primary');
    };
  }, [user]);
}
