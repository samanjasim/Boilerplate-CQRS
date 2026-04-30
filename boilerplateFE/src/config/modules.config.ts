/**
 * Public module-bootstrap entry point. Re-exports the generated registry from
 * `modules.generated.ts` so callers (main.tsx, route guards, slot consumers)
 * keep importing `@/config/modules.config` as before.
 *
 * The generator (`scripts/generators/modules.ts`) owns module composition:
 *   modules.catalog.json → modules.generated.ts → this file
 *
 * Add module-level human-edited config knobs in this file alongside the
 * re-exports if/when needed; do NOT edit `modules.generated.ts` directly.
 */
export {
  enabledModules,
  isModuleActive,
  activeModules,
  registerAllModules,
  type ModuleName,
} from './modules.generated';
