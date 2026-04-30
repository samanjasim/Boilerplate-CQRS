import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

// Module isolation patterns are generated from modules.catalog.json by
// `npm run generate:modules` (Tier 2.5 Theme 5). The CI drift gate
// `verify:modules` fails if this JSON falls behind the catalog.
const __dirname = dirname(fileURLToPath(import.meta.url))
const moduleConfig = JSON.parse(
  readFileSync(resolve(__dirname, 'eslint.config.modules.json'), 'utf8'),
)

// Forbid core code from importing optional module folders directly.
// Cross-module composition must go through the module registry
// (`src/lib/modules`) so routes, nav, slots, and capabilities stay
// tree-shakeable AND architecturally isolated.
//
// This rule blocks `import type` as well as runtime `import`. That
// is intentional: even type-only imports couple core code to a
// module's shape, so renaming a field in the module would break
// core compilation. The spirit of the boundary is "core knows
// nothing about module internals" - type imports violate that.
// If a core file genuinely needs a shared shape, move the shape to
// `src/types/` or add it to the module registry contract.
//
// When `restrictedPatterns` is empty (e.g. `rename.ps1 -Modules None`),
// ESLint rejects a zero-length `group`, so we drop the rule entirely.
// There are no optional modules to guard against in that build.
const restrictedImportRule = moduleConfig.restrictedPatterns.length > 0
  ? {
      'no-restricted-imports': ['error', {
        patterns: [
          {
            group: moduleConfig.restrictedPatterns,
            message: 'Do not import optional module features from core. Register routes, nav, slots, and capabilities through src/config/modules.config.ts and src/lib/modules instead.',
          },
        ],
      }],
    }
  : {}

// FE API envelope cleanup — Track 4 PR #3 onward.
// See docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md.
//
// Three rules:
//   Rule 1 (global, day-one): no raw `@/lib/axios` outside the api layer.
//     Forces non-api feature code (components, queries, hooks) to go through
//     `@/lib/api`. The api layer itself is allowed.
//   Rule 2 (per-feature, scoped): no `.data.data` member chains anywhere
//     inside a migrated feature folder. Cannot be globalized until slice 5
//     ships — unmigrated features and shared hooks (useListPage.ts) still
//     read the envelope shape during the transition.
//   Rule 3 (per-feature, scoped): no raw `@/lib/axios` inside a migrated
//     feature's `*.api.ts`. Tighter than Rule 1 (which still allows raw
//     apiClient inside `*.api.ts` to support unmigrated features).
//
// Rules 2+3 share the same per-feature `files` block. Slices 2-5 append
// feature globs to the array.
const migratedFeatureGlobs = [
  'src/features/comments-activity/**/*.{ts,tsx}',
  // future slices:
  // 'src/features/auth/**/*.{ts,tsx}',
  // 'src/features/users/**/*.{ts,tsx}',
  // ...
]

const noDataDataChain = {
  selector:
    'MemberExpression[object.type="MemberExpression"][object.property.name="data"][property.name="data"]',
  message:
    "`.data.data` envelope chains are forbidden in migrated features. Use the `api` namespace from `@/lib/api`, which returns the inner payload directly. See docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md.",
}

const noRawAxiosImport = {
  selector: 'ImportDeclaration[source.value="@/lib/axios"]',
  message:
    "Do not import `@/lib/axios` here. Use the typed `api` namespace from `@/lib/api`. Raw apiClient is allowed only inside feature `*.api.ts` files (during migration) and the api module itself.",
}

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: restrictedImportRule,
  },
  {
    // Allowlist: the modules themselves (their own internal imports) and the
    // generated bootstrap config that wires them up. main.tsx is not on this
    // list — it must reach optional modules only via @/config/modules.config.
    files: moduleConfig.allowlistFiles,
    rules: {
      'no-restricted-imports': 'off',
    },
  },
  {
    // Rule 1: forbid `@/lib/axios` outside the api layer. Allowed: feature
    // `*.api.ts` files (during migration) and the api module itself.
    // Existing leaf component WorkflowPendingTaskBadge.tsx is allowlisted
    // via inline eslint-disable until slice 4 (workflow migration).
    files: ['src/**/*.{ts,tsx}'],
    ignores: [
      'src/features/**/*.api.ts',
      'src/lib/api/**',
      'src/lib/axios/**',
    ],
    rules: {
      'no-restricted-syntax': ['error', noRawAxiosImport],
    },
  },
  {
    // Rules 2+3: per-migrated-feature. Forbids both raw `@/lib/axios` and
    // `.data.data` chains inside any file in a migrated feature folder.
    // Append a glob here when a new slice lands.
    files: migratedFeatureGlobs,
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ImportDeclaration[source.value="@/lib/axios"]',
          message:
            "Migrated feature: use `@/lib/api`. Raw apiClient requires an explicit eslint-disable allowlist with rationale.",
        },
        noDataDataChain,
      ],
    },
  },
])
