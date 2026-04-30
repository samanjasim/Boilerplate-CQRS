// AUTO-GENERATED — DO NOT EDIT.
// Regenerate with `npm run generate:modules` from the repo root.
// CI fails on drift; modules.catalog.json is the single source of truth.
//
// Source: modules.catalog.json

import { registerWebModules, type WebModule } from '@/lib/modules';
import { billingModule } from '@/features/billing';
import { commentsActivityModule } from '@/features/comments-activity';
import { communicationModule } from '@/features/communication';
import { importExportModule } from '@/features/import-export';
import { productsModule } from '@/features/products';
import { webhooksModule } from '@/features/webhooks';
import { workflowModule } from '@/features/workflow';

/**
 * Static catalog union — does not vary by selection. Always emits all web
 * modules so callers using `ModuleName` get a stable, exhaustive type even
 * when a module is stripped at template generation time.
 */
export type ModuleName =
  | 'billing'
  | 'commentsActivity'
  | 'communication'
  | 'importExport'
  | 'products'
  | 'webhooks'
  | 'workflow';

/**
 * Source of truth for what is active in this build. The generator emits
 * only the imports for modules whose `supportedPlatforms` includes 'web'
 * and which were not stripped by `rename.ps1 -Modules` at template time.
 */
export const enabledModules: WebModule[] = [
  billingModule,
  commentsActivityModule,
  communicationModule,
  importExportModule,
  productsModule,
  webhooksModule,
  workflowModule,
];

const enabledIds = new Set<string>(enabledModules.map((m) => m.id));

export function isModuleActive(module: ModuleName): boolean {
  return enabledIds.has(module);
}

/**
 * Frozen literal view derived from `enabledModules`. Cannot drift from
 * the array because every flag is computed from `enabledIds`.
 */
export const activeModules: Readonly<Record<ModuleName, boolean>> = Object.freeze({
  billing: isModuleActive('billing'),
  commentsActivity: isModuleActive('commentsActivity'),
  communication: isModuleActive('communication'),
  importExport: isModuleActive('importExport'),
  products: isModuleActive('products'),
  webhooks: isModuleActive('webhooks'),
  workflow: isModuleActive('workflow'),
}) as Readonly<Record<ModuleName, boolean>>;

export function registerAllModules(): void {
  registerWebModules(enabledModules);
}
