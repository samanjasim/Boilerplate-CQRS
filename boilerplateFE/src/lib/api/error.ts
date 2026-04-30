import type { AxiosError } from 'axios';
import i18n from '@/i18n';
import type { ApiResponse, ApiError as ApiErrorBody } from '@/types';

// Single error type thrown by every `api.*` helper.
//
// HTTP errors (4xx/5xx) are wrapped from the AxiosError; status comes from
// response.status. HTTP 200 + envelope success:false is wrapped from the
// response body and carries status:null to distinguish from real HTTP failures.
//
// Caller pattern:
//   try { await api.post(...) }
//   catch (e) {
//     if (e instanceof ApiError && e.code === 'TENANT_INACTIVE') ...
//     throw e;
//   }
export class ApiError extends Error {
  readonly status: number | null;
  readonly code: string | null;
  readonly validationErrors: Record<string, string[]> | null;
  readonly cause: unknown;

  constructor(init: {
    message: string;
    status: number | null;
    code: string | null;
    validationErrors: Record<string, string[]> | null;
    cause: unknown;
  }) {
    super(init.message);
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code;
    this.validationErrors = init.validationErrors;
    this.cause = init.cause;
  }
}

// Build an ApiError from a rejected axios call. The existing response error
// interceptor (src/lib/axios/interceptors/error.interceptor.ts) has already
// toasted the message and tacked it onto the rejection as `parsedMessage`.
// We reuse that here so the helper doesn't double-toast.
export function toApiErrorFromAxios(error: AxiosError<ApiErrorBody>): ApiError {
  const parsed = (error as AxiosError & { parsedMessage?: string }).parsedMessage;
  const body = error.response?.data;
  const code = body?.errors ? firstKey(body.errors) : null;
  return new ApiError({
    message: parsed ?? error.message ?? i18n.t('errors.unknownError'),
    status: error.response?.status ?? null,
    code,
    validationErrors: body?.validationErrors ?? null,
    cause: error,
  });
}

// Build an ApiError from a 200-status response whose envelope reports
// success:false. The interceptor never fired (status was 2xx), so we are
// responsible for both constructing the error and toasting.
export function toApiErrorFromEnvelope<T>(
  body: ApiResponse<T>,
  toast: (message: string) => void,
): ApiError {
  const message = extractEnvelopeMessage(body);
  toast(message);
  return new ApiError({
    message,
    status: null,
    code: body.errors ? firstKey(body.errors) : null,
    validationErrors: body.validationErrors ?? null,
    cause: body,
  });
}

// Mirrors error.interceptor.ts:getErrorMessage's validation-first / message /
// detail priority, but operates on the envelope body directly (no HTTP status
// to switch on).
function extractEnvelopeMessage<T>(body: ApiResponse<T>): string {
  if (body.validationErrors) {
    const first = firstNonEmpty(Object.values(body.validationErrors));
    if (first) return first;
  }
  if (body.errors) {
    const first = firstNonEmpty(Object.values(body.errors));
    if (first) return first;
  }
  if (body.message) return body.message;
  return i18n.t('errors.unknownError');
}

function firstKey(record: Record<string, unknown>): string | null {
  for (const key of Object.keys(record)) {
    return key;
  }
  return null;
}

function firstNonEmpty(arrays: Array<string[]>): string | null {
  for (const arr of arrays) {
    if (Array.isArray(arr) && arr.length > 0 && typeof arr[0] === 'string') {
      return arr[0];
    }
  }
  return null;
}
