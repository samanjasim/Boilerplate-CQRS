/**
 * Module bootstrap codegen — Tier 2.5 Theme 5.
 *
 * One generator, three emitters. Replaces the three platform-specific module
 * discovery mechanisms (BE filesystem glob, FE manual array, mobile hardcoded
 * constructors) with a generated registry sourced from `modules.catalog.json`.
 * The codepath in source mode is identical to package mode (Tier 3 just
 * changes the catalog's input).
 *
 * Emits:
 *   - boilerplateBE/src/Starter.Api/Modularity/ModuleRegistry.g.cs
 *       static class returning instantiated IModule[] in dependency order.
 *   - boilerplateFE/src/config/modules.generated.ts
 *       enabledModules array + ModuleName union (the public modules.config.ts
 *       re-exports both).
 *   - boilerplateMobile/lib/app/modules.config.dart
 *       activeModules() returning AppModule list (this file is itself an
 *       entry-point — there is no re-export indirection).
 *   - boilerplateFE/eslint.config.modules.json
 *       generated patterns + allowlist files for the no-restricted-imports
 *       rule. eslint.config.js consumes this JSON.
 *
 * Usage:
 *   npm run generate:modules           # writes all four files
 *   npm run verify:modules             # writes to tmp + diffs (CI drift gate)
 *
 * Spec: docs/superpowers/specs/2026-04-29-modularity-tier-2-5-hardening.md §2 Theme 5
 * Plan: docs/superpowers/plans/2026-04-29-modularity-tier-2-5-theme-5.md
 */
import { execSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  CatalogEntry,
  REPO_ROOT,
  readCatalog,
  relativeFromRepo,
} from './lib/catalog.js';
import { resolveModuleClass } from './lib/parse-csharp-class.js';

// ─────────────────────────────────────────────────────────────────────
// Output paths
// ─────────────────────────────────────────────────────────────────────

const BE_OUT = join(
  REPO_ROOT,
  'boilerplateBE',
  'src',
  'Starter.Api',
  'Modularity',
  'ModuleRegistry.g.cs',
);
const FE_OUT = join(
  REPO_ROOT,
  'boilerplateFE',
  'src',
  'config',
  'modules.generated.ts',
);
const MOBILE_OUT = join(
  REPO_ROOT,
  'boilerplateMobile',
  'lib',
  'app',
  'modules.config.dart',
);
const ESLINT_OUT = join(
  REPO_ROOT,
  'boilerplateFE',
  'eslint.config.modules.json',
);

// ─────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────

function backendEntries(entries: CatalogEntry[]): CatalogEntry[] {
  return entries.filter((e) => e.supportedPlatforms.includes('backend'));
}

function webEntries(entries: CatalogEntry[]): CatalogEntry[] {
  return entries.filter((e) => e.supportedPlatforms.includes('web'));
}

function mobileEntries(entries: CatalogEntry[]): CatalogEntry[] {
  return entries.filter((e) => e.supportedPlatforms.includes('mobile'));
}

function moduleProjectDir(e: CatalogEntry): string {
  if (!e.backendModule) {
    throw new Error(
      `Catalog entry '${e.id}' is on the backend platform but has no backendModule field.`,
    );
  }
  return join(REPO_ROOT, 'boilerplateBE', 'src', 'modules', e.backendModule);
}

// ─────────────────────────────────────────────────────────────────────
// BE emitter — ModuleRegistry.g.cs
// ─────────────────────────────────────────────────────────────────────

function emitBackend(entries: CatalogEntry[]): string {
  const backend = backendEntries(entries);
  const resolved = backend.map((e) => ({
    entry: e,
    cls: resolveModuleClass(moduleProjectDir(e)),
  }));

  const lines: string[] = [];
  lines.push('// AUTO-GENERATED — DO NOT EDIT.');
  lines.push('// Regenerate with `npm run generate:modules` from the repo root.');
  lines.push('// CI fails on drift; modules.catalog.json is the single source of truth.');
  lines.push('//');
  lines.push('// Source: modules.catalog.json');
  lines.push('');
  lines.push('using Starter.Abstractions.Modularity;');
  lines.push('');
  lines.push('namespace Starter.Api.Modularity;');
  lines.push('');
  lines.push('/// <summary>');
  lines.push('/// Generated module registry. Used by the API host and out-of-process');
  lines.push('/// tooling (<c>Program.ConfigureServicesForTooling</c>) instead of the');
  lines.push('/// reflection-based <c>ModuleLoader.DiscoverModules()</c>. Discover');
  lines.push('/// remains for tests that need runtime introspection.');
  lines.push('/// </summary>');
  lines.push('public static class ModuleRegistry');
  lines.push('{');
  lines.push('    public static IReadOnlyList<IModule> All()');
  lines.push('    {');
  if (resolved.length === 0) {
    lines.push('        return System.Array.Empty<IModule>();');
  } else {
    lines.push('        return new IModule[]');
    lines.push('        {');
    for (const { cls } of resolved) {
      lines.push(`            new ${cls.fullName}(),`);
    }
    lines.push('        };');
  }
  lines.push('    }');
  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

// ─────────────────────────────────────────────────────────────────────
// FE emitter — modules.generated.ts
// ─────────────────────────────────────────────────────────────────────

function emitFrontend(entries: CatalogEntry[]): string {
  const web = webEntries(entries);

  // The catalog id (e.g. "billing", "importExport") is the canonical
  // ModuleName key. Each feature folder exports `{id}Module` — verified in
  // ModuleIsolationTests + tightened by ESLint allowlist below.
  const importVar = (e: CatalogEntry): string => `${e.id}Module`;

  const lines: string[] = [];
  lines.push('// AUTO-GENERATED — DO NOT EDIT.');
  lines.push('// Regenerate with `npm run generate:modules` from the repo root.');
  lines.push('// CI fails on drift; modules.catalog.json is the single source of truth.');
  lines.push('//');
  lines.push('// Source: modules.catalog.json');
  lines.push('');
  lines.push("import { registerWebModules, type WebModule } from '@/lib/modules';");
  for (const e of web) {
    if (!e.frontendFeature) {
      throw new Error(
        `Catalog entry '${e.id}' is on the web platform but has no frontendFeature field.`,
      );
    }
    lines.push(`import { ${importVar(e)} } from '@/features/${e.frontendFeature}';`);
  }
  lines.push('');
  lines.push('/**');
  lines.push(' * Static catalog union — does not vary by selection. Always emits all web');
  lines.push(' * modules so callers using `ModuleName` get a stable, exhaustive type even');
  lines.push(' * when a module is stripped at template generation time.');
  lines.push(' */');
  lines.push('export type ModuleName =');
  if (web.length === 0) {
    lines.push("  never;");
  } else {
    web.forEach((e, i) => {
      const sep = i === web.length - 1 ? ';' : '';
      lines.push(`  | '${e.id}'${sep}`);
    });
  }
  lines.push('');
  lines.push('/**');
  lines.push(' * Source of truth for what is active in this build. The generator emits');
  lines.push(" * only the imports for modules whose `supportedPlatforms` includes 'web'");
  lines.push(' * and which were not stripped by `rename.ps1 -Modules` at template time.');
  lines.push(' */');
  lines.push('export const enabledModules: WebModule[] = [');
  for (const e of web) {
    lines.push(`  ${importVar(e)},`);
  }
  lines.push('];');
  lines.push('');
  lines.push('const enabledIds = new Set<string>(enabledModules.map((m) => m.id));');
  lines.push('');
  lines.push('export function isModuleActive(module: ModuleName): boolean {');
  lines.push('  return enabledIds.has(module);');
  lines.push('}');
  lines.push('');
  lines.push('/**');
  lines.push(' * Frozen literal view derived from `enabledModules`. Cannot drift from');
  lines.push(' * the array because every flag is computed from `enabledIds`.');
  lines.push(' */');
  lines.push('export const activeModules: Readonly<Record<ModuleName, boolean>> = Object.freeze({');
  for (const e of web) {
    lines.push(`  ${e.id}: isModuleActive('${e.id}'),`);
  }
  lines.push('}) as Readonly<Record<ModuleName, boolean>>;');
  lines.push('');
  lines.push('export function registerAllModules(): void {');
  lines.push('  registerWebModules(enabledModules);');
  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

// ─────────────────────────────────────────────────────────────────────
// Mobile emitter — modules.config.dart
// ─────────────────────────────────────────────────────────────────────

function emitMobile(entries: CatalogEntry[]): string {
  const mobile = mobileEntries(entries);

  const lines: string[] = [];
  lines.push('// AUTO-GENERATED — DO NOT EDIT.');
  lines.push('// Regenerate with `npm run generate:modules` from the repo root.');
  lines.push('// CI fails on drift; modules.catalog.json is the single source of truth.');
  lines.push('//');
  lines.push('// Source: modules.catalog.json');
  lines.push('');
  lines.push("import 'package:boilerplate_mobile/core/modularity/app_module.dart';");
  for (const e of mobile) {
    if (!e.mobileFolder || !e.mobileModule) {
      throw new Error(
        `Catalog entry '${e.id}' is on the mobile platform but is missing mobileFolder or mobileModule.`,
      );
    }
    const path = `package:boilerplate_mobile/modules/${e.mobileFolder}/${e.mobileFolder}_module.dart`;
    lines.push(`import '${path}';`);
  }
  lines.push('');
  lines.push('/// Optional modules active in this build.');
  lines.push('///');
  lines.push('/// In generated apps, `rename.ps1` regenerates this file from');
  lines.push("/// `modules.catalog.json` based on the `-Modules` flag. When the flag");
  lines.push('/// excludes every optional module, the list is empty and the app runs');
  lines.push('/// with core features only.');
  if (mobile.length === 0) {
    lines.push('List<AppModule> activeModules() => const <AppModule>[];');
  } else {
    lines.push('List<AppModule> activeModules() => <AppModule>[');
    for (const e of mobile) {
      lines.push(`      ${e.mobileModule}(),`);
    }
    lines.push('    ];');
  }
  lines.push('');
  return lines.join('\n');
}

// ─────────────────────────────────────────────────────────────────────
// ESLint patterns — eslint.config.modules.json
// ─────────────────────────────────────────────────────────────────────

interface EslintConfigData {
  /** Restricted import patterns for `no-restricted-imports`. */
  restrictedPatterns: string[];
  /** Files allowlisted to bypass the rule. */
  allowlistFiles: string[];
}

function emitEslintData(entries: CatalogEntry[]): string {
  const web = webEntries(entries);
  const restricted: string[] = [];
  const allow: string[] = [];

  for (const e of web) {
    if (!e.frontendFeature) continue;
    restricted.push(`@/features/${e.frontendFeature}`);
    restricted.push(`@/features/${e.frontendFeature}/*`);
    allow.push(`src/features/${e.frontendFeature}/**`);
  }

  // The generated bootstrap config is allowed to reach into modules — it is
  // the seam between core and modules. main.tsx remains restricted.
  allow.push('src/config/modules.config.ts');
  allow.push('src/config/modules.generated.ts');

  const data: EslintConfigData = {
    restrictedPatterns: restricted,
    allowlistFiles: allow,
  };

  return JSON.stringify(data, null, 2) + '\n';
}

// ─────────────────────────────────────────────────────────────────────
// Driver
// ─────────────────────────────────────────────────────────────────────

interface Emits {
  be: string;
  fe: string;
  mobile: string;
  eslint: string;
  source: string;
}

function readEmits(): Emits {
  const { entries, source } = readCatalog();
  if (entries.length === 0) {
    throw new Error(
      `No catalog entries found in ${source}. The modules generator needs at least ` +
        `one optional module to emit a registry. Did the catalog file move?`,
    );
  }
  return {
    be: emitBackend(entries),
    fe: emitFrontend(entries),
    mobile: emitMobile(entries),
    eslint: emitEslintData(entries),
    source,
  };
}

function writeEmits(): void {
  const { be, fe, mobile, eslint } = readEmits();
  writeFileSync(BE_OUT, be);
  writeFileSync(FE_OUT, fe);
  writeFileSync(MOBILE_OUT, mobile);
  writeFileSync(ESLINT_OUT, eslint);
  console.log(`✓ wrote ${relativeFromRepo(BE_OUT)}`);
  console.log(`✓ wrote ${relativeFromRepo(FE_OUT)}`);
  console.log(`✓ wrote ${relativeFromRepo(MOBILE_OUT)}`);
  console.log(`✓ wrote ${relativeFromRepo(ESLINT_OUT)}`);
}

function checkEmits(): void {
  const expected = readEmits();
  const targets: Array<{ name: string; abs: string; want: string }> = [
    { name: 'be', abs: BE_OUT, want: expected.be },
    { name: 'fe', abs: FE_OUT, want: expected.fe },
    { name: 'mobile', abs: MOBILE_OUT, want: expected.mobile },
    { name: 'eslint', abs: ESLINT_OUT, want: expected.eslint },
  ];

  const drifted: Array<{ abs: string; want: string }> = [];
  for (const t of targets) {
    let current = '';
    try {
      current = readFileSync(t.abs, 'utf8');
    } catch {
      drifted.push({ abs: t.abs, want: t.want });
      continue;
    }
    if (current !== t.want) drifted.push({ abs: t.abs, want: t.want });
  }

  if (drifted.length === 0) {
    console.log('✓ module codegen is up to date');
    return;
  }

  const tmpDir = mkdtempSync(join(tmpdir(), 'starter-modules-codegen-'));
  for (const { abs, want } of drifted) {
    const baseName = abs.split('/').pop() ?? 'expected';
    const expectedPath = join(tmpDir, `${baseName}.expected`);
    writeFileSync(expectedPath, want);
    console.error(`✗ ${relativeFromRepo(abs)} drifted:`);
    try {
      execSync(`diff -u "${abs}" "${expectedPath}"`, { stdio: 'inherit' });
    } catch {
      // diff exits non-zero on differences — expected.
    }
  }
  console.error('');
  console.error(
    'Run `npm run generate:modules` from the repo root and commit the result.',
  );
  process.exit(1);
}

const isCheck = process.argv.includes('--check');
if (isCheck) {
  checkEmits();
} else {
  writeEmits();
}
