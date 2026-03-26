export type ThemePresetName = 'warm-copper' | 'ocean-blue' | 'deep-indigo' | 'midnight-sapphire' | 'rose' | 'emerald';

interface ThemeMode {
  primary: string;
  primaryForeground: string;
  ring: string;
}

interface ThemePreset {
  name: ThemePresetName;
  label: string;
  light: ThemeMode;
  dark: ThemeMode;
  primaryScale: Record<string, string>;
  accentScale: Record<string, string>;
}

export const presets: Record<ThemePresetName, ThemePreset> = {
  'warm-copper': {
    name: 'warm-copper',
    label: 'Warm Copper',
    light: {
      primary: '22 51% 55%',
      primaryForeground: '0 0% 100%',
      ring: '22 51% 55%',
    },
    dark: {
      primary: '22 56% 60%',
      primaryForeground: '0 0% 100%',
      ring: '22 56% 60%',
    },
    primaryScale: {
      '50': '#fdf5ef',
      '100': '#fbe8d9',
      '200': '#f6ceb2',
      '300': '#f0ae81',
      '400': '#e9884f',
      '500': '#d4885f',
      '600': '#c67a52',
      '700': '#b56a42',
      '800': '#94522e',
      '900': '#784528',
      '950': '#412113',
    },
    /* NOTE: These values MUST match src/styles/index.css @theme block */
    accentScale: {
      '50': '#ecfdf5',
      '100': '#d1fae5',
      '200': '#a7f3d0',
      '300': '#6ee7b7',
      '400': '#34d399',
      '500': '#10b981',
      '600': '#059669',
      '700': '#047857',
      '800': '#065f46',
      '900': '#064e3b',
      '950': '#022c22',
    },
  },
  'ocean-blue': {
    name: 'ocean-blue',
    label: 'Ocean Blue',
    light: {
      primary: '221.2 83.2% 53.3%',
      primaryForeground: '210 40% 98%',
      ring: '221.2 83.2% 53.3%',
    },
    dark: {
      primary: '217.2 91.2% 59.8%',
      primaryForeground: '222.2 47.4% 11.2%',
      ring: '224.3 76.3% 48%',
    },
    primaryScale: {
      '50': '#eff6ff',
      '100': '#dbeafe',
      '200': '#bfdbfe',
      '300': '#93c5fd',
      '400': '#60a5fa',
      '500': '#3b82f6',
      '600': '#2563eb',
      '700': '#1d4ed8',
      '800': '#1e40af',
      '900': '#1e3a8a',
      '950': '#172554',
    },
    accentScale: {
      '50': '#fffbeb',
      '100': '#fef3c7',
      '200': '#fde68a',
      '300': '#fcd34d',
      '400': '#fbbf24',
      '500': '#f59e0b',
      '600': '#d97706',
      '700': '#b45309',
      '800': '#92400e',
      '900': '#78350f',
      '950': '#451a03',
    },
  },
  'deep-indigo': {
    name: 'deep-indigo',
    label: 'Deep Indigo',
    light: {
      primary: '238.7 83.5% 66.7%',
      primaryForeground: '210 40% 98%',
      ring: '238.7 83.5% 66.7%',
    },
    dark: {
      primary: '234.5 89.5% 73.9%',
      primaryForeground: '238.4 43.2% 15.3%',
      ring: '234.5 89.5% 73.9%',
    },
    primaryScale: {
      '50': '#eef2ff',
      '100': '#e0e7ff',
      '200': '#c7d2fe',
      '300': '#a5b4fc',
      '400': '#818cf8',
      '500': '#6366f1',
      '600': '#4f46e5',
      '700': '#4338ca',
      '800': '#3730a3',
      '900': '#312e81',
      '950': '#1e1b4b',
    },
    accentScale: {
      '50': '#ecfdf5',
      '100': '#d1fae5',
      '200': '#a7f3d0',
      '300': '#6ee7b7',
      '400': '#34d399',
      '500': '#10b981',
      '600': '#059669',
      '700': '#047857',
      '800': '#065f46',
      '900': '#064e3b',
      '950': '#022c22',
    },
  },
  'midnight-sapphire': {
    name: 'midnight-sapphire',
    label: 'Midnight Sapphire',
    light: {
      primary: '243.4 75.4% 58.6%',
      primaryForeground: '210 40% 98%',
      ring: '243.4 75.4% 58.6%',
    },
    dark: {
      primary: '239.7 83.8% 67.3%',
      primaryForeground: '243.5 46.7% 13.5%',
      ring: '239.7 83.8% 67.3%',
    },
    primaryScale: {
      '50': '#eef2ff',
      '100': '#e0e7ff',
      '200': '#c7d2fe',
      '300': '#a5b4fc',
      '400': '#818cf8',
      '500': '#6366f1',
      '600': '#4f46e5',
      '700': '#4338ca',
      '800': '#3730a3',
      '900': '#312e81',
      '950': '#1e1b4b',
    },
    accentScale: {
      '50': '#f0fdfa',
      '100': '#ccfbf1',
      '200': '#99f6e4',
      '300': '#5eead4',
      '400': '#2dd4bf',
      '500': '#14b8a6',
      '600': '#0d9488',
      '700': '#0f766e',
      '800': '#115e59',
      '900': '#134e4a',
      '950': '#042f2e',
    },
  },
  'rose': {
    name: 'rose',
    label: 'Rose',
    light: {
      primary: '346.8 77.2% 49.8%',
      primaryForeground: '355.7 100% 97.3%',
      ring: '346.8 77.2% 49.8%',
    },
    dark: {
      primary: '349.7 89.2% 60.2%',
      primaryForeground: '343.1 87.7% 15.5%',
      ring: '349.7 89.2% 60.2%',
    },
    primaryScale: {
      '50': '#fff1f2',
      '100': '#ffe4e6',
      '200': '#fecdd3',
      '300': '#fda4af',
      '400': '#fb7185',
      '500': '#f43f5e',
      '600': '#e11d48',
      '700': '#be123c',
      '800': '#9f1239',
      '900': '#881337',
      '950': '#4c0519',
    },
    accentScale: {
      '50': '#f8fafc',
      '100': '#f1f5f9',
      '200': '#e2e8f0',
      '300': '#cbd5e1',
      '400': '#94a3b8',
      '500': '#64748b',
      '600': '#475569',
      '700': '#334155',
      '800': '#1e293b',
      '900': '#0f172a',
      '950': '#020617',
    },
  },
  'emerald': {
    name: 'emerald',
    label: 'Emerald',
    light: {
      primary: '160.1 84.1% 39.4%',
      primaryForeground: '152.3 76% 96.5%',
      ring: '160.1 84.1% 39.4%',
    },
    dark: {
      primary: '158.1 64.4% 51.6%',
      primaryForeground: '155.3 49.7% 11.5%',
      ring: '158.1 64.4% 51.6%',
    },
    primaryScale: {
      '50': '#ecfdf5',
      '100': '#d1fae5',
      '200': '#a7f3d0',
      '300': '#6ee7b7',
      '400': '#34d399',
      '500': '#10b981',
      '600': '#059669',
      '700': '#047857',
      '800': '#065f46',
      '900': '#064e3b',
      '950': '#022c22',
    },
    accentScale: {
      '50': '#fffbeb',
      '100': '#fef3c7',
      '200': '#fde68a',
      '300': '#fcd34d',
      '400': '#fbbf24',
      '500': '#f59e0b',
      '600': '#d97706',
      '700': '#b45309',
      '800': '#92400e',
      '900': '#78350f',
      '950': '#451a03',
    },
  },
};

/*
 * ╔════════════════════════════════════════════════════════╗
 * ║  Change this value to rebrand the entire application  ║
 * ╚════════════════════════════════════════════════════════╝
 */
export const activePreset: ThemePresetName = 'warm-copper';
