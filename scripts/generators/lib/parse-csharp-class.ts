/**
 * Resolve the *Module class name (and namespace) declared in a backend module
 * project. The generator emits `new {Namespace}.{ClassName}()` instances; if a
 * future contributor renames a class away from the `XModule` convention, the
 * registry-vs-discovered architecture test catches the drift before merge.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { safeIsDir, safeIsFile } from './catalog.js';

const NAMESPACE_RE = /^\s*namespace\s+([A-Za-z0-9_.]+)\s*;?/m;
const CLASS_RE = /\bpublic(?:\s+(?:sealed|partial|abstract|static))*\s+class\s+([A-Za-z_][A-Za-z0-9_]*)\s*[:{(\s]/g;

export interface ResolvedModuleClass {
  /** Fully-qualified type name, e.g. `Starter.Module.Billing.BillingModule`. */
  fullName: string;
  /** Project root absolute path. */
  projectDir: string;
  /** Source file we resolved the class from. */
  source: string;
}

/**
 * Find the `XModule` class for a backend module project.
 *
 * Convention: every module project contains a single root-level `XModule.cs`
 * file (sibling to the `.csproj`) that defines a `public sealed class XModule
 * : IModule`. The plan calls this out as an explicit convention and the
 * registry-vs-discovered architecture test guards it.
 */
export function resolveModuleClass(projectDir: string): ResolvedModuleClass {
  if (!safeIsDir(projectDir)) {
    throw new Error(
      `Module project directory does not exist: ${projectDir}. ` +
        `Did the catalog's backendModule field point at a removed module?`,
    );
  }

  // Look for `*Module.cs` at the project root only — module projects always
  // declare their entry-point class at the root, sibling to the .csproj.
  // Sub-folders may contain other classes that happen to end in "Module" but
  // are not IModule implementations (e.g. domain entities).
  const rootFiles = readdirSync(projectDir).filter(
    (f) => f.endsWith('Module.cs') && safeIsFile(join(projectDir, f)),
  );

  if (rootFiles.length === 0) {
    throw new Error(
      `No *Module.cs found at the root of ${projectDir}. ` +
        `Backend modules must declare their IModule entry-point class in a root-level file.`,
    );
  }

  // If multiple root *Module.cs exist, parse all and pick the one declaring
  // an IModule (heuristic: the file whose class doesn't have generics or
  // open-paren on the public class line). Keep deterministic by sort.
  rootFiles.sort();

  for (const file of rootFiles) {
    const abs = join(projectDir, file);
    const text = readFileSync(abs, 'utf8');
    const ns = NAMESPACE_RE.exec(text)?.[1];
    if (!ns) continue;
    CLASS_RE.lastIndex = 0;
    let match: RegExpExecArray | null;
    while ((match = CLASS_RE.exec(text)) !== null) {
      const className = match[1];
      if (className.endsWith('Module')) {
        return { fullName: `${ns}.${className}`, projectDir, source: abs };
      }
    }
  }

  throw new Error(
    `Could not find a public class ending in "Module" inside any of ` +
      `[${rootFiles.join(', ')}] in ${projectDir}. The backend module emitter ` +
      `relies on the {Project}.{ProjectShortName}Module convention.`,
  );
}
