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
    rules: {
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
      'no-restricted-imports': ['error', {
        patterns: [
          {
            group: moduleConfig.restrictedPatterns,
            message: 'Do not import optional module features from core. Register routes, nav, slots, and capabilities through src/config/modules.config.ts and src/lib/modules instead.',
          },
        ],
      }],
    },
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
])
