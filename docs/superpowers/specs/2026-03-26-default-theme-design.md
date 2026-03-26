# Default Theme & Preset System Design

## Context

The boilerplate's current UI is functional but visually flat — standard blue primary, minimal gradients, basic shadows. The goal is to ship a polished, immersive default theme (Ocean Blue) with a preset system that lets developers rebrand the entire app by changing one line in a config file. The visual style targets "subtle polish" — gentle gradients on key elements, refined shadows, clean surfaces with depth hints. No new dependencies; everything built on the existing Tailwind CSS 4 + CSS variable architecture.

## Theme Preset System

### Preset Config (`src/config/theme.config.ts`)

A single file defines all available presets and selects the active one:

```ts
export const activePreset = 'ocean-blue';
```

Each preset contains:
- **Light mode HSL values**: primary, primary-foreground, ring
- **Dark mode HSL values**: same keys with dark-appropriate values
- **Primary color scale** (50-950): full Tailwind-compatible hex scale for gradients, badges, backgrounds
- **Accent color scale** (50-950): secondary brand color hex scale

#### Shipped Presets

| Preset | Primary | Accent | Vibe |
|--------|---------|--------|------|
| **ocean-blue** (default) | Deep navy-blue (#1e40af / #3b82f6) | Warm amber (#d97706) | Trustworthy, clean |
| deep-indigo | Purple-blue (#4338ca / #6366f1) | Emerald (#059669) | Confident, modern |
| midnight-sapphire | Indigo-violet (#3730a3 / #4f46e5) | Teal (#0d9488) | Premium, sophisticated |
| rose | Rose-pink (#be123c / #f43f5e) | Slate (#475569) | Warm, distinctive |
| emerald | Deep green (#047857 / #10b981) | Amber (#d97706) | Fresh, natural |

### Preset Application Hook (`src/hooks/useThemePreset.ts`)

- Called on app mount in `App.tsx`, **before** `useTenantBranding`
- Reads `activePreset` from config
- Sets CSS variables on `document.documentElement`:
  - `--primary`, `--primary-foreground`, `--ring` (HSL values)
  - `--color-primary-50` through `--color-primary-950` (hex scale)
  - `--color-accent-50` through `--color-accent-950` (hex scale)
- Respects current dark/light mode — applies the correct set of values
- Re-applies when theme mode changes (listens to the `.dark` class)

### Integration with Tenant Branding

`useTenantBranding` fires after `useThemePreset`. Tenant colors override `--primary` via the existing hex-to-HSL conversion. This means:
- Preset = default brand for the boilerplate
- Tenant branding = runtime override per tenant
- No conflicts; tenant always wins

## Visual Styling Changes

### Default Palette: Ocean Blue

**Light mode CSS variables:**
- `--primary`: 221.2 83.2% 53.3% (stays close to current, slightly richer)
- `--background`: 210 20% 98% (subtle cool tint instead of pure neutral)
- `--card`: 0 0% 100%
- Ring and focus colors match primary

**Color scales:**
- Primary 50-950: Blue scale from #eff6ff to #172554 (refined from current)
- Accent 50-950: Amber scale from #fffbeb to #451a03 (replacing emerald)

### Sidebar (`Sidebar.tsx`)

- Background: subtle top-to-bottom gradient from `card` to a slightly darker shade using `bg-gradient-to-b from-card to-card/95`
- Active nav item: add a 3px start border in primary color (`ltr:border-l-3 rtl:border-r-3 border-primary`), stronger background tint (`bg-primary/15` instead of `/10`)
- Logo area: faint gradient bottom border using `border-b border-gradient` effect via pseudo-element or transparent-to-border gradient

### Header (`Header.tsx`)

- Increase backdrop blur: `backdrop-blur-md` (up from `backdrop-blur-sm`)
- Add subtle bottom border gradient: thin line fading from primary/20 to transparent
- User avatar: gradient ring from primary to accent instead of flat `bg-primary/10`

### Cards (`card.tsx` + usage sites)

- Refined shadow: use `shadow-soft-sm` by default, `shadow-soft-md` on hover for interactive cards
- Interactive cards (dashboard stats, quick overview): add `transition-shadow hover:shadow-soft-md` and subtle `hover:-translate-y-0.5` lift
- Border: `hover:border-primary/20` on interactive cards

### Buttons (`button.tsx`)

- Default variant: gradient background `bg-gradient-to-b from-primary-500 to-primary-600` instead of flat `bg-primary`
- Hover: `hover:from-primary-600 hover:to-primary-700`
- Keep shadow-sm, add subtle inner highlight via `shadow-[inset_0_1px_0_rgba(255,255,255,0.1)]`
- Ghost, outline, secondary, link variants: unchanged

### Dashboard Welcome Banner (`DashboardPage.tsx`)

- Richer gradient: `bg-gradient-to-br from-primary-500 via-primary-600 to-primary-800`
- Decorative blur circles: add CSS animation for subtle pulse (`animate-pulse` with reduced opacity, or custom keyframe with 4s duration)

### Auth Layout (`AuthLayout.tsx`)

- Left panel gradient: `bg-gradient-to-br from-primary-600 via-primary-700 to-primary-900` with a hint of accent: add a subtle accent-colored radial gradient overlay
- Add dot pattern texture: CSS `radial-gradient` background creating a subtle dot grid at low opacity (0.05-0.1)

### Landing Page (`LandingPage.tsx`)

- Match auth layout gradient upgrade
- "Get Started" button: ensure strong contrast — `bg-white text-primary-700` stays, add subtle shadow
- "Sign In" outline button: `border-white/40` (up from `/30`) for better visibility

### Loading Screen (`LoadingScreen.tsx`)

- Add subtle animated gradient background: `bg-gradient-to-br from-background via-primary-50/30 to-background` with slow CSS animation shifting the gradient position

## CSS Changes (`src/styles/index.css`)

### Updated Default Variables

Replace current `:root` values with Ocean Blue palette. Key changes:
- `--background`: shift from pure neutral `0 0% 98%` to subtle cool `210 20% 98%`
- Accent scale: swap emerald for amber

### New Utility Classes

```css
@layer utilities {
  /* Existing */
  .focus-ring { ... }
  .focus-ring-inset { ... }

  /* New */
  .hover-lift {
    @apply transition-all duration-200 hover:-translate-y-0.5 hover:shadow-soft-md;
  }
  .glass {
    @apply bg-card/80 backdrop-blur-md border-b border-border/50;
  }
  .gradient-text {
    @apply bg-clip-text text-transparent bg-gradient-to-r from-primary-500 to-accent-500;
  }
}
```

### Animated Gradient Keyframe

```css
@keyframes gradient-shift {
  0%, 100% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
}
```

## Files Changed

### New Files
| File | Purpose |
|------|---------|
| `src/config/theme.config.ts` | Preset definitions + active preset selector |
| `src/hooks/useThemePreset.ts` | Applies active preset CSS variables on mount |

### Modified Files
| File | Change |
|------|--------|
| `src/styles/index.css` | Ocean Blue defaults, amber accent scale, new utilities, keyframes |
| `src/app/App.tsx` | Call `useThemePreset()` on mount |
| `src/components/layout/MainLayout/Sidebar.tsx` | Gradient background, improved active state with left border |
| `src/components/layout/MainLayout/Header.tsx` | Enhanced glass effect, gradient avatar ring |
| `src/components/layout/AuthLayout/AuthLayout.tsx` | Richer gradient, dot pattern texture |
| `src/components/ui/button.tsx` | Gradient default variant |
| `src/components/ui/card.tsx` | Refined shadow defaults |
| `src/features/landing/pages/LandingPage.tsx` | Upgraded gradient, button contrast |
| `src/features/dashboard/pages/DashboardPage.tsx` | Richer welcome banner, hover-lift on stat cards |
| `src/components/common/LoadingScreen.tsx` | Subtle animated gradient background |

### Untouched
- All Radix UI component internals
- State management (Zustand stores, selectors)
- Routing, API layer, i18n translations
- Dark/light/system mode toggle logic
- `useTenantBranding` hook (works as before, overrides preset)
- All other pages (users, roles, tenants, files, reports, audit-logs, etc.)

## How to Switch Themes

After creating a project from this boilerplate:

1. Open `src/config/theme.config.ts`
2. Change `activePreset` value:
   ```ts
   export const activePreset = 'midnight-sapphire'; // was 'ocean-blue'
   ```
3. The entire app rebrands on next dev server refresh

To create a custom preset: add a new entry to the presets object following the existing structure, using tools like https://uicolors.app to generate color scales.

## Verification

1. Run `npm run dev` from `boilerplateFE/`
2. Verify Ocean Blue palette renders correctly in light mode
3. Toggle to dark mode — verify colors adapt properly
4. Check all pages: landing, login, register, dashboard, users list, roles list
5. Test sidebar collapsed/expanded states
6. Test RTL mode (switch to Arabic)
7. Change `activePreset` to each of the 5 presets, verify each one rebrands the app correctly
8. Verify tenant branding still overrides the preset color (if tenant has custom color configured)
9. Run `npm run build` — verify no build errors
