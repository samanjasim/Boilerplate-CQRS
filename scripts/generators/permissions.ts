/**
 * Permission codegen — Tier 2.5 Theme 4.
 *
 * Reads C# permission constants from:
 *   - boilerplateBE/src/Starter.Shared/Constants/Permissions.cs (core)
 *   - boilerplateBE/src/modules/<Module>/Constants/*Permissions.cs (modules)
 *
 * Emits:
 *   - boilerplateFE/src/constants/permissions.generated.ts (nested object + Permission union)
 *   - boilerplateMobile/lib/core/permissions/permissions.generated.dart (flat lowerCamelCase)
 *
 * Grouping rule: the permission's *value* is "Group.Action". Everything is
 * grouped by the first dot-segment, the property/identifier is the rest.
 * The C# constant name is intentionally ignored — see Tier 2.5 audit.
 *
 * Usage:
 *   npm run generate:permissions       # writes both files
 *   npm run verify:permissions         # writes to tmp + diffs (CI drift gate)
 *
 * Spec: docs/superpowers/specs/2026-04-29-modularity-tier-2-5-hardening.md §2 Theme 4
 * Plan: docs/superpowers/plans/2026-04-29-modularity-tier-2-5-theme-4.md
 */
import { execSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(SCRIPT_DIR, '..', '..');

// ─────────────────────────────────────────────────────────────────────
// Parser
// ─────────────────────────────────────────────────────────────────────

interface PermissionEntry {
  /** C# constant value, e.g. "Users.View". */
  value: string;
  /** First dot-segment of value, e.g. "Users". Used as the group key. */
  group: string;
  /** Remainder after the first dot, e.g. "View" or "ManageRoles". */
  action: string;
  /** Source file the constant was parsed from (for diagnostics). */
  source: string;
}

/** Match `public const string FooBar = "Group.Action";` allowing optional whitespace. */
const PERMISSION_CONST_RE =
  /public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;/g;

export function parsePermissionFile(absPath: string): PermissionEntry[] {
  const text = readFileSync(absPath, 'utf8');
  const entries: PermissionEntry[] = [];
  let match: RegExpExecArray | null;
  PERMISSION_CONST_RE.lastIndex = 0;
  while ((match = PERMISSION_CONST_RE.exec(text)) !== null) {
    const value = match[2];
    const dotAt = value.indexOf('.');
    if (dotAt <= 0 || dotAt === value.length - 1) {
      throw new Error(
        `${absPath}: permission value '${value}' must be of the form 'Group.Action' ` +
          `(both segments non-empty). The {Module}.{Action} 2-part naming convention is ` +
          `enforced by Tier 2.5 Theme 3 ModulePermissionTests.`,
      );
    }
    entries.push({
      value,
      group: value.slice(0, dotAt),
      action: value.slice(dotAt + 1),
      source: absPath,
    });
  }
  return entries;
}

function findCorePermissionFile(): string {
  return join(
    REPO_ROOT,
    'boilerplateBE',
    'src',
    'Starter.Shared',
    'Constants',
    'Permissions.cs',
  );
}

function findModulePermissionFiles(): string[] {
  const modulesRoot = join(REPO_ROOT, 'boilerplateBE', 'src', 'modules');
  if (!safeIsDir(modulesRoot)) return [];

  const out: string[] = [];
  for (const moduleDir of readdirSync(modulesRoot)) {
    const constantsDir = join(modulesRoot, moduleDir, 'Constants');
    if (!safeIsDir(constantsDir)) continue;
    for (const f of readdirSync(constantsDir)) {
      if (/Permissions?\.cs$/.test(f)) {
        out.push(join(constantsDir, f));
      }
    }
  }
  return out.sort();
}

function safeIsDir(p: string): boolean {
  try {
    return statSync(p).isDirectory();
  } catch {
    return false;
  }
}

// ─────────────────────────────────────────────────────────────────────
// Group + sort
// ─────────────────────────────────────────────────────────────────────

interface GroupedPermissions {
  /** Group → action → value. Sorted alphabetically at every level for determinism. */
  byGroup: Map<string, Map<string, string>>;
  /** Sorted list of unique permission strings. */
  allValues: string[];
}

function groupPermissions(entries: PermissionEntry[]): GroupedPermissions {
  const seen = new Map<string, string>();
  for (const e of entries) {
    if (seen.has(e.value) && seen.get(e.value) !== e.source) {
      // Same string declared in two source files. Could be intentional (Webhooks.View
      // duplicated in core + module historically) but Theme 3 tests should already
      // have flagged this. Keep first-wins; CatalogConsistencyTests & ModulePermissionTests
      // are the ground truth on uniqueness.
    }
    seen.set(e.value, e.source);
  }

  const byGroup = new Map<string, Map<string, string>>();
  for (const e of entries) {
    if (!byGroup.has(e.group)) byGroup.set(e.group, new Map());
    byGroup.get(e.group)!.set(e.action, e.value);
  }

  // Stable sort — alphabetical groups, alphabetical actions within each group.
  const sortedByGroup = new Map(
    [...byGroup.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([g, m]) => [
        g,
        new Map([...m.entries()].sort(([a], [b]) => a.localeCompare(b))),
      ]),
  );

  const allValues = [...new Set(entries.map((e) => e.value))].sort();
  return { byGroup: sortedByGroup, allValues };
}

// ─────────────────────────────────────────────────────────────────────
// TypeScript emitter
// ─────────────────────────────────────────────────────────────────────

function emitTypeScript(grouped: GroupedPermissions, sourceFiles: string[]): string {
  const lines: string[] = [];
  lines.push('/**');
  lines.push(' * AUTO-GENERATED — DO NOT EDIT.');
  lines.push(' * Regenerate with `npm run generate:permissions` from the repo root.');
  lines.push(' * CI fails on drift; the BE permission constants are the single source of truth.');
  lines.push(' *');
  lines.push(' * Source files:');
  for (const sf of sourceFiles) {
    lines.push(` *   - ${relativeFromRepo(sf)}`);
  }
  lines.push(' */');
  lines.push('export const PERMISSIONS = {');
  for (const [group, actions] of grouped.byGroup) {
    lines.push(`  ${quoteIdent(group)}: {`);
    for (const [action, value] of actions) {
      lines.push(`    ${quoteIdent(action)}: '${value}',`);
    }
    lines.push('  },');
  }
  lines.push('} as const;');
  lines.push('');
  lines.push('type PermissionMap = typeof PERMISSIONS;');
  lines.push('export type Permission = {');
  lines.push('  [M in keyof PermissionMap]: PermissionMap[M][keyof PermissionMap[M]];');
  lines.push('}[keyof PermissionMap];');
  lines.push('');
  return lines.join('\n');
}

/** TypeScript identifier rules are looser than we need; the C# constants are
 * always alphanumeric PascalCase, so a bare property is fine. We only quote
 * defensively if a future contributor sneaks in a leading digit. */
function quoteIdent(name: string): string {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(name) ? name : JSON.stringify(name);
}

// ─────────────────────────────────────────────────────────────────────
// Dart emitter
// ─────────────────────────────────────────────────────────────────────

function emitDart(grouped: GroupedPermissions, sourceFiles: string[]): string {
  const lines: string[] = [];
  lines.push('// AUTO-GENERATED — DO NOT EDIT.');
  lines.push('// Regenerate with `npm run generate:permissions` from the repo root.');
  lines.push('// CI fails on drift; the BE permission constants are the single source of truth.');
  lines.push('//');
  lines.push('// Source files:');
  for (const sf of sourceFiles) {
    lines.push(`//   - ${relativeFromRepo(sf)}`);
  }
  lines.push('');
  lines.push('// ignore_for_file: constant_identifier_names, lines_longer_than_80_chars');
  lines.push('');
  lines.push('abstract final class Permissions {');
  for (const [group, actions] of grouped.byGroup) {
    lines.push('');
    lines.push(`  // ─── ${group} ───`);
    for (const [action, value] of actions) {
      const ident = toDartIdent(group, action);
      lines.push(`  static const ${ident} = '${value}';`);
    }
  }
  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

/** "Users.View" → "usersView". The convention preserves casing of multi-word
 * actions (e.g. "ManageRoles" → "usersManageRoles"). Compatible with Dart's
 * `lowerCamelCase` lint and the existing manual mobile file's naming. */
function toDartIdent(group: string, action: string): string {
  const head = group.slice(0, 1).toLowerCase() + group.slice(1);
  return head + action;
}

// ─────────────────────────────────────────────────────────────────────
// Driver
// ─────────────────────────────────────────────────────────────────────

const TS_OUT = join(
  REPO_ROOT,
  'boilerplateFE',
  'src',
  'constants',
  'permissions.generated.ts',
);
const DART_OUT = join(
  REPO_ROOT,
  'boilerplateMobile',
  'lib',
  'core',
  'permissions',
  'permissions.generated.dart',
);

function relativeFromRepo(absPath: string): string {
  return absPath.startsWith(REPO_ROOT)
    ? absPath.slice(REPO_ROOT.length + 1).replace(/\\/g, '/')
    : absPath;
}

function readEmits(): { ts: string; dart: string; sources: string[] } {
  const sources = [findCorePermissionFile(), ...findModulePermissionFiles()];
  const allEntries: PermissionEntry[] = [];
  for (const file of sources) {
    allEntries.push(...parsePermissionFile(file));
  }
  if (allEntries.length === 0) {
    throw new Error('No permission constants found. Did source paths move?');
  }
  const grouped = groupPermissions(allEntries);
  const sourcesForHeader = sources.map(relativeFromRepo);
  return {
    ts: emitTypeScript(grouped, sourcesForHeader),
    dart: emitDart(grouped, sourcesForHeader),
    sources,
  };
}

function writeEmits(): void {
  const { ts, dart } = readEmits();
  writeFileSync(TS_OUT, ts);
  writeFileSync(DART_OUT, dart);
  console.log(`✓ wrote ${relativeFromRepo(TS_OUT)}`);
  console.log(`✓ wrote ${relativeFromRepo(DART_OUT)}`);
}

function checkEmits(): void {
  const { ts, dart } = readEmits();
  const tsCurrent = readFileSync(TS_OUT, 'utf8');
  const dartCurrent = readFileSync(DART_OUT, 'utf8');

  const tsDrift = tsCurrent !== ts;
  const dartDrift = dartCurrent !== dart;

  if (!tsDrift && !dartDrift) {
    console.log('✓ permission codegen is up to date');
    return;
  }

  // Materialise the would-be-emitted files in a temp dir and run `diff -u` so
  // the CI log shows the human-readable drift instead of just "files differ".
  const tmpDir = mkdtempSync(join(tmpdir(), 'starter-perm-codegen-'));
  if (tsDrift) {
    const expected = join(tmpDir, 'permissions.generated.ts.expected');
    writeFileSync(expected, ts);
    console.error(`✗ ${relativeFromRepo(TS_OUT)} drifted:`);
    try {
      execSync(`diff -u ${TS_OUT} ${expected}`, { stdio: 'inherit' });
    } catch {
      // diff exits non-zero when there are differences; that's expected.
    }
  }
  if (dartDrift) {
    const expected = join(tmpDir, 'permissions.generated.dart.expected');
    writeFileSync(expected, dart);
    console.error(`✗ ${relativeFromRepo(DART_OUT)} drifted:`);
    try {
      execSync(`diff -u ${DART_OUT} ${expected}`, { stdio: 'inherit' });
    } catch {
      // diff exits non-zero when there are differences; that's expected.
    }
  }
  console.error('');
  console.error(
    'Run `npm run generate:permissions` from the repo root and commit the result.',
  );
  process.exit(1);
}

const isCheck = process.argv.includes('--check');
if (isCheck) {
  checkEmits();
} else {
  writeEmits();
}
