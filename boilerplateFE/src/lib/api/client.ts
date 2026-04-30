import type { AxiosError, AxiosRequestConfig, AxiosResponse } from 'axios';
import { toast } from 'sonner';
import { apiClient } from '@/lib/axios';
import type { ApiResponse, PaginatedResponse, PagedResult, ApiError as ApiErrorBody } from '@/types';
import type { ApiOptions } from './types';
import { ApiError, toApiErrorFromAxios, toApiErrorFromEnvelope } from './error';

// Typed HTTP helpers. Every helper:
//   1. Calls the shared `apiClient` (auth, refresh, error interceptors active).
//   2. Unwraps ApiResponse<T> -> T (or PaginatedResponse<T> -> PagedResult<T>).
//   3. Throws ApiError on HTTP failure or envelope success:false.
//
// Feature *.api.ts files use this namespace exclusively. Raw apiClient is
// allowed only inside *.api.ts during multi-PR migration and SSE/streaming
// files (with eslint-disable comment + rationale).

function buildConfig(
  options: ApiOptions | undefined,
  extras?: Partial<AxiosRequestConfig>,
): AxiosRequestConfig {
  return {
    ...(options?.signal ? { signal: options.signal } : {}),
    ...(options?.onUploadProgress ? { onUploadProgress: options.onUploadProgress } : {}),
    ...(options?.headers ? { headers: options.headers } : {}),
    ...(options?.suppressValidationToast !== undefined
      ? { suppressValidationToast: options.suppressValidationToast }
      : {}),
    ...extras,
  };
}

async function unwrapJson<T>(
  promise: Promise<AxiosResponse<ApiResponse<T>>>,
): Promise<T> {
  let response: AxiosResponse<ApiResponse<T>>;
  try {
    response = await promise;
  } catch (error) {
    throw toApiErrorFromAxios(error as AxiosError<ApiErrorBody>);
  }
  const body = response.data;
  if (body && body.success === false) {
    throw toApiErrorFromEnvelope(body, (m) => toast.error(m));
  }
  // body.data is T. Empty 204-style payloads return undefined; void callers
  // discard. Cast required because ApiResponse<T>.data is T (not T|undefined),
  // but axios may give us a non-envelope body for 204.
  return (body?.data ?? (undefined as unknown as T));
}

async function unwrapPaged<T>(
  promise: Promise<AxiosResponse<PaginatedResponse<T>>>,
): Promise<PagedResult<T>> {
  let response: AxiosResponse<PaginatedResponse<T>>;
  try {
    response = await promise;
  } catch (error) {
    throw toApiErrorFromAxios(error as AxiosError<ApiErrorBody>);
  }
  const body = response.data;
  if (body && body.success === false) {
    throw toApiErrorFromEnvelope(
      {
        success: false,
        message: body.message,
        errors: body.errors,
        validationErrors: body.validationErrors,
        data: undefined as unknown as never,
      },
      (m) => toast.error(m),
    );
  }
  return { items: body.data ?? [], pagination: body.pagination };
}

export const api = {
  get: <T>(url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<T> =>
    unwrapJson<T>(apiClient.get<ApiResponse<T>>(url, buildConfig(options, { params }))),

  post: <T>(url: string, body?: unknown, options?: ApiOptions): Promise<T> =>
    unwrapJson<T>(apiClient.post<ApiResponse<T>>(url, body, buildConfig(options))),

  put: <T>(url: string, body?: unknown, options?: ApiOptions): Promise<T> =>
    unwrapJson<T>(apiClient.put<ApiResponse<T>>(url, body, buildConfig(options))),

  patch: <T>(url: string, body?: unknown, options?: ApiOptions): Promise<T> =>
    unwrapJson<T>(apiClient.patch<ApiResponse<T>>(url, body, buildConfig(options))),

  delete: <T>(url: string, options?: ApiOptions): Promise<T> =>
    unwrapJson<T>(apiClient.delete<ApiResponse<T>>(url, buildConfig(options))),

  paged: <T>(url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<PagedResult<T>> =>
    unwrapPaged<T>(apiClient.get<PaginatedResponse<T>>(url, buildConfig(options, { params }))),

  download: async (url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<Blob> => {
    try {
      const response = await apiClient.get<Blob>(
        url,
        buildConfig(options, { params, responseType: 'blob' }),
      );
      return response.data;
    } catch (error) {
      throw toApiErrorFromAxios(error as AxiosError<ApiErrorBody>);
    }
  },
};

export { ApiError };
