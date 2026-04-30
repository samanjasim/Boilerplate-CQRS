import type { AxiosProgressEvent } from 'axios';

// Options accepted by every `api.*` helper. Deliberately narrow — not a
// passthrough to AxiosRequestConfig. The whole point of this module is that
// there's exactly one way to do each thing; widening this type requires
// design discussion.
export interface ApiOptions {
  signal?: AbortSignal;
  onUploadProgress?: (event: AxiosProgressEvent) => void;
  // Read by error.interceptor.ts to skip the global toast when forms render
  // validation errors inline. Pre-existing axios config field.
  suppressValidationToast?: boolean;
  headers?: Record<string, string>;
}
