/**
 * Shared helpers for catalog-driven generators (Tier 2.5 Theme 5+).
 *
 * Reads `modules.catalog.json` from the repo root and exposes a typed,
 * deterministic view (entries sorted by id) that every emitter consumes.
 *
 * The catalog comment field is dropped here — only data fields are surfaced.
 */
import { readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
export const REPO_ROOT = resolve(SCRIPT_DIR, '..', '..', '..');

export type Platform = 'backend' | 'web' | 'mobile';

export interface CatalogEntry {
  /** Catalog id, e.g. "billing" — used as the canonical lower-camel key for ModuleName. */
  id: string;
  displayName: string;
  version: string;
  supportedPlatforms: Platform[];
  /** `Starter.Module.X` project name. Optional only for modules not on the backend. */
  backendModule?: string;
  /** Frontend feature folder under `boilerplateFE/src/features/`. */
  frontendFeature?: string;
  /** Dart class name for the mobile entry point, e.g. "BillingModule". */
  mobileModule?: string;
  /** Mobile folder under `boilerplateMobile/lib/modules/`. */
  mobileFolder?: string;
  configKey: string;
  required: boolean;
  dependencies: string[];
  description?: string;
  testsFolder?: string;
  coreCompat?: string;
  packageId?: { nuget?: string; npm?: string; pub?: string };
}

export interface Catalog {
  entries: CatalogEntry[];
  source: string;
}

const CATALOG_PATH = join(REPO_ROOT, 'modules.catalog.json');

export function readCatalog(): Catalog {
  const raw = JSON.parse(readFileSync(CATALOG_PATH, 'utf8')) as Record<
    string,
    unknown
  >;

  const entries: CatalogEntry[] = [];
  for (const [id, value] of Object.entries(raw)) {
    if (id.startsWith('_')) continue; // _comment etc.
    if (typeof value !== 'object' || value === null) continue;
    const v = value as Record<string, unknown>;
    entries.push({
      id,
      displayName: String(v.displayName ?? id),
      version: String(v.version ?? '0.0.0'),
      supportedPlatforms: (v.supportedPlatforms as Platform[]) ?? [],
      backendModule: v.backendModule as string | undefined,
      frontendFeature: v.frontendFeature as string | undefined,
      mobileModule: v.mobileModule as string | undefined,
      mobileFolder: v.mobileFolder as string | undefined,
      configKey: String(v.configKey ?? id),
      required: Boolean(v.required ?? false),
      dependencies: (v.dependencies as string[]) ?? [],
      description: v.description as string | undefined,
      testsFolder: v.testsFolder as string | undefined,
      coreCompat: v.coreCompat as string | undefined,
      packageId: v.packageId as CatalogEntry['packageId'],
    });
  }

  entries.sort((a, b) => a.id.localeCompare(b.id));
  return { entries, source: CATALOG_PATH };
}

export function relativeFromRepo(absPath: string): string {
  return absPath.startsWith(REPO_ROOT)
    ? absPath.slice(REPO_ROOT.length + 1).replace(/\\/g, '/')
    : absPath;
}

export function safeIsDir(p: string): boolean {
  try {
    return statSync(p).isDirectory();
  } catch {
    return false;
  }
}

export function safeIsFile(p: string): boolean {
  try {
    return statSync(p).isFile();
  } catch {
    return false;
  }
}
