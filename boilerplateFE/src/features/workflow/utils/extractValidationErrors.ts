import type { AxiosError } from 'axios';

export function extractValidationErrors(err: unknown): Record<string, string> | null {
  const axiosErr = err as AxiosError<{
    validationErrors?: Record<string, string[]>;
  }>;
  const ve = axiosErr.response?.data?.validationErrors;
  if (!ve || typeof ve !== 'object') return null;

  const flat: Record<string, string> = {};
  for (const [field, messages] of Object.entries(ve)) {
    if (Array.isArray(messages) && messages.length > 0) {
      flat[field] = messages[0];
    }
  }
  return Object.keys(flat).length > 0 ? flat : null;
}
