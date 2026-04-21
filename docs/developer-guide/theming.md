# Theming Guide

## Architecture Overview

The theme system has three layers:

1. **CSS variables** (`src/styles/index.css`) — fallback values in `:root` and `.dark`
2. **Theme presets** (`src/config/theme.config.ts`) — named color palettes
3. **Runtime application** (`src/hooks/useThemePreset.ts`) — applies the active preset's CSS variables on mount

When the app loads, `useThemePreset` reads `activePreset` from the config and sets all `--color-*` CSS custom properties on `document.documentElement`. This overrides the CSS fallbacks, so changing `activePreset` rebrands the entire app.

## Switching the Active Theme

Open `src/config/theme.config.ts` and change:

```ts
export const activePreset: ThemePresetName = 'warm-copper'; // change this
```

Available presets: `warm-copper`, `ocean-blue`, `deep-indigo`, `midnight-sapphire`, `rose`, `emerald`.

## Creating a New Theme Preset

### Step 1: Generate your color scale

Go to [uicolors.app](https://uicolors.app) and generate a 50-950 scale from your primary color. You need hex values for shades 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950.

Pick the **600** shade as your main brand color.

### Step 2: Convert your main color to HSL

Convert your 600 shade hex to HSL (use any converter). You need the space-separated format: `H S% L%` (e.g., `22 51% 55%`).

For dark mode, lighten it slightly (increase L by ~5%).

### Step 3: Add the preset to `theme.config.ts`

```ts
// 1. Add to the union type
export type ThemePresetName = 'warm-copper' | 'ocean-blue' | ... | 'your-theme';

// 2. Add the preset object
'your-theme': {
  name: 'your-theme',
  label: 'Your Theme',
  light: {
    primary: '200 60% 45%',          // HSL of your 600 shade
    primaryForeground: '0 0% 100%',  // white text on primary
    ring: '200 60% 45%',             // same as primary
  },
  dark: {
    primary: '200 65% 55%',          // lighter for dark mode
    primaryForeground: '0 0% 100%',
    ring: '200 65% 55%',
  },
  primaryScale: {
    '50':  '#...', // from uicolors.app
    '100': '#...',
    '200': '#...',
    '300': '#...',
    '400': '#...',
    '500': '#...',
    '600': '#...', // your main brand color
    '700': '#...',
    '800': '#...',
    '900': '#...',
    '950': '#...',
  },
  accentScale: {
    // pick a complementary color scale (emerald, amber, etc.)
    '50': '#...', '100': '#...', ...
  },
},

// 3. Set it as active
export const activePreset: ThemePresetName = 'your-theme';
```

### Step 4: Update CSS fallbacks (optional)

Update `src/styles/index.css` `@theme` block with your primary scale so the CSS fallbacks match. This isn't strictly required (the runtime hook overrides them) but keeps the CSS source of truth in sync.

### Step 5: Update semantic surface colors in `useThemePreset.ts`

The hook's `applyPreset` function sets surface colors (card, popover, background, border) for light and dark modes. If your theme has a warm/cool tone that differs from the default warm cream, update the hex values in the light/dark branches.

## Semantic Design Tokens

Components should NEVER hardcode primary color shades. Instead, use these semantic tokens:

### CSS Variables (use in arbitrary values)

| Token | Purpose | Example |
|-------|---------|---------|
| `var(--active-bg)` | Active item background (10% primary) | `[background:var(--active-bg)]` |
| `var(--active-text)` | Active item text color | `[color:var(--active-text)]` |
| `var(--active-border)` | Active indicator border | `border-color via state-active-border` |
| `var(--hover-bg)` | Hover background (secondary) | Handled by `state-hover` class |
| `var(--gradient-from/to)` | Hero/banner gradient | `gradient-hero` class |

### Utility Classes

| Class | Renders as |
|-------|-----------|
| `state-active` | Primary-tinted bg + primary text + semibold |
| `state-hover` | Muted text, hover shows secondary bg |
| `state-active-border` | Sets border-color to primary |
| `gradient-hero` | Linear gradient from dark to light primary |
| `hover-lift` | Translate-up + shadow on hover |

### Tailwind Utilities That Work

| Class | Maps to |
|-------|---------|
| `bg-primary` | `--color-primary` (the 600 shade) |
| `text-primary` | Same |
| `bg-primary-50` through `bg-primary-950` | Full scale |
| `bg-card`, `bg-popover`, `bg-background` | Surface colors |
| `text-foreground`, `text-muted-foreground` | Text colors |
| `border-border` | Border color |
| `bg-secondary` | Secondary surface |
| `bg-destructive` | Error/destructive color |

## What NOT to Do

- **Never use `primary-600` or `primary-50` directly** — use `bg-primary` or semantic tokens
- **Never add `dark:` overrides for primary colors** — the theme system handles dark mode
- **Never hardcode hex colors** — always use CSS variables or Tailwind classes
- **Never set `color` in global CSS rules** (like on `p` or `h1` tags) — it overrides Tailwind utilities
