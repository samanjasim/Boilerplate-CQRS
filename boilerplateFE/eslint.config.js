import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

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
            group: [
              '@/features/billing',
              '@/features/billing/*',
              '@/features/webhooks',
              '@/features/webhooks/*',
              '@/features/import-export',
              '@/features/import-export/*',
              '@/features/products',
              '@/features/products/*',
              '@/features/comments-activity',
              '@/features/comments-activity/*',
              '@/features/communication',
              '@/features/communication/*',
              '@/features/workflow',
              '@/features/workflow/*',
            ],
            message: 'Do not import optional module features from core. Register routes, nav, slots, and capabilities through src/config/modules.config.ts and src/lib/modules instead.',
          },
        ],
      }],
    },
  },
  {
    // Allowlist: the modules themselves (their own internal imports), the
    // bootstrap config that wires them up, and the app entry point.
    files: [
      'src/features/billing/**',
      'src/features/webhooks/**',
      'src/features/import-export/**',
      'src/features/products/**',
      'src/features/comments-activity/**',
      'src/features/communication/**',
      'src/features/workflow/**',
      'src/config/modules.config.ts',
      'src/app/main.tsx',
    ],
    rules: {
      'no-restricted-imports': 'off',
    },
  },
])
