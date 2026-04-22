import type { AxiosError } from 'axios';

/**
 * Pulls server-side field validation errors out of an axios error response.
 * Returns a `{ fieldName: [messages...] }` map, or null when the response
 * has no `validationErrors` payload.
 *
 * Preserves every message per field (FluentValidation may return multiple,
 * e.g. `Required` + `MaxLength`) so callers can render them all.
 */
export function extractValidationErrors(err: unknown): Record<string, string[]> | null {
  const axiosErr = err as AxiosError<{
    validationErrors?: Record<string, string[]>;
  }>;
  const ve = axiosErr.response?.data?.validationErrors;
  if (!ve || typeof ve !== 'object') return null;

  const byField: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(ve)) {
    if (Array.isArray(messages) && messages.length > 0) {
      byField[field] = messages.filter((m): m is string => typeof m === 'string' && m.length > 0);
    }
  }
  return Object.keys(byField).length > 0 ? byField : null;
}
