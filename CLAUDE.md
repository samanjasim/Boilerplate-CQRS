# Project: Boilerplate CQRS

Full-stack boilerplate — .NET 10 backend (Clean Architecture + CQRS) and React 19 frontend (TypeScript + Tailwind CSS 4 + shadcn/ui).

## Build & Run

```bash
# Backend
cd boilerplateBE/src/Starter.Api && dotnet run --launch-profile http

# Frontend
cd boilerplateFE && npm run dev

# Build check
cd boilerplateFE && npm run build
```

## Frontend Rules — Must Always Follow

### Theme System

- **Never hardcode primary color shades** (`primary-600`, `primary-50`, etc.) in components. Use `bg-primary`, `text-primary`, or semantic tokens (`var(--active-bg)`, `var(--active-text)`, `state-active`, `state-hover`).
- **Never add `dark:` overrides for primary colors.** The theme preset system handles dark mode automatically via `useThemePreset`.
- **Active preset lives in** `src/config/theme.config.ts` → `activePreset`. Changing it rebrands the entire app.
- **Semantic tokens in CSS** (`--active-bg`, `--active-text`, `--active-border`, `--hover-bg`, `--gradient-from/to`) auto-derive from `--primary` using `color-mix()`. Components reference these, not raw shades.
- All Tailwind semantic colors (`bg-card`, `bg-popover`, `bg-background`, `text-foreground`, etc.) are registered in the `@theme` block of `index.css` AND set at runtime by `useThemePreset.ts`. Both must stay in sync.

### Components & Patterns

- **Use shared components** — `Pagination`, `PageHeader`, `EmptyState`, `UserAvatar`, `ConfirmDialog`, `ExportButton` from `@/components/common`.
- **Back navigation** — use `useBackNavigation(path, label)` hook in detail/edit pages. It renders in the header bar automatically and clears on unmount.
- **Page size persistence** — use `getPersistedPageSize()` from `@/components/common/Pagination` as the initial state for paginated lists. The `Pagination` component persists changes to localStorage.
- **Status badges** — use `STATUS_BADGE_VARIANT` from `@/constants/status` for mapping entity status to badge variants.
- **Tables** — the `Table` component includes its own `rounded-2xl bg-card shadow-card` container. Do NOT wrap it in an extra `<Card>`.
- **Empty states** — always use `<EmptyState>` component with an icon, title, and optional description/action.

### Styling Rules

- **Font** — IBM Plex Sans (loaded via Google Fonts in `index.html`). RTL uses IBM Plex Sans Arabic.
- **Radius convention** — sm=8px, md=12px, lg=16px. Cards use `rounded-2xl`, inputs/buttons `rounded-xl`, nav items `rounded-lg`.
- **No global `color` on typography tags** — the `p`, `h1`, etc. rules in `index.css` set size/weight only, not color. Use Tailwind classes.
- **RTL** — use `text-start` not `text-left`, `ltr:/rtl:` prefixes for directional borders/margins, `rtl:rotate-180` on arrow icons.
- **Buttons** — `variant="default"` is the primary action (copper fill), `variant="outline"` shows primary text, `variant="ghost"` shows primary tint on hover.

### Type Safety

- **No `as unknown as` casts** — extend the proper interface instead.
- **Shared types** live in `src/types/`. Extend them when the API returns new fields.

### Architecture

- **Feature-based structure** — each feature in `src/features/` has `api/`, `pages/`, `components/` subdirs.
- **State** — Zustand for client state (`src/stores/`), TanStack Query for server state.
- **Constants** — shared mappings (permissions, status variants, audit actions) go in `src/constants/`.
- **Hooks** — reusable logic in `src/hooks/` (permissions, back nav, theme preset, tenant branding, etc.).

## Backend Notes

- .NET 10, PostgreSQL, Redis, RabbitMQ
- EF Core migrations in `Starter.Infrastructure/Persistence/Migrations/`
- Seed data applied on startup when `DatabaseSettings.SeedDataOnStartup = true`
- Default credentials: `superadmin@starter.com` / `Admin@123456`
