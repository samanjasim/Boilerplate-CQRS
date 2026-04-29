interface LocaleEntry {
  name?: string;
  description?: string;
}

export type PlanTranslations = Record<string, LocaleEntry>;

/** Parse the raw JSON string stored on `SubscriptionPlan.translations`.
 *  Returns `null` for malformed JSON so callers can fall back to raw-edit
 *  mode; an empty/whitespace string returns an empty record. */
export function parseTranslations(raw: string | null | undefined): PlanTranslations | null {
  if (!raw || !raw.trim()) return {};
  try {
    const obj = JSON.parse(raw);
    if (typeof obj !== 'object' || obj === null || Array.isArray(obj)) return null;
    return obj as PlanTranslations;
  } catch {
    return null;
  }
}

/** Drop empty locale entries before serializing. Returns `''` when nothing to
 *  store, so a `value || undefined` check at the call site keeps the field
 *  absent on the wire instead of writing an empty object. */
export function serializeTranslations(map: PlanTranslations): string {
  const compact: PlanTranslations = {};
  for (const [code, entry] of Object.entries(map)) {
    if (entry && (entry.name?.trim() || entry.description?.trim())) {
      compact[code] = {
        ...(entry.name?.trim() && { name: entry.name.trim() }),
        ...(entry.description?.trim() && { description: entry.description.trim() }),
      };
    }
  }
  return Object.keys(compact).length === 0 ? '' : JSON.stringify(compact, null, 2);
}
