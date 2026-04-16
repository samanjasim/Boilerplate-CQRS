import { billingModule } from '@/features/billing';
import { webhooksModule } from '@/features/webhooks';
import { importExportModule } from '@/features/import-export';
import { productsModule } from '@/features/products';
import { commentsActivityModule } from '@/features/comments-activity';
import { communicationModule } from '@/features/communication';

/**
 * Optional module registry. To remove a module from a build, comment out
 * its import + the corresponding entry in `enabledModules`. Vite will
 * tree-shake the entire feature folder.
 *
 * The 6 core features (Files, Notifications, FeatureFlags, ApiKeys,
 * AuditLogs, Reports) are NOT in this list — they ship with every build.
 */
export const activeModules = {
  billing: true,
  webhooks: true,
  importExport: true,
  products: true,
  commentsActivity: true,
  communication: true,
} as const;

export type ModuleName = keyof typeof activeModules;

export function isModuleActive(module: ModuleName): boolean {
  return activeModules[module];
}

/**
 * Shape every optional module exports from its `index.ts`. The explicit
 * interface keeps `enabledModules` typed as `AppModule[]` even when the
 * array is emptied (e.g. when `rename.ps1 -Modules None` strips every
 * entry), avoiding the TS7034 implicit-any inference.
 */
interface AppModule {
  name: string;
  register(): void;
}

const enabledModules: AppModule[] = [
  billingModule,
  webhooksModule,
  importExportModule,
  productsModule,
  commentsActivityModule,
  communicationModule,
];

/**
 * Bootstrap entry: each module's `register()` runs before React mounts so
 * the slot/capability registries are populated by the time any component
 * tries to read them.
 */
export function registerAllModules(): void {
  for (const mod of enabledModules) {
    mod.register();
  }
}
