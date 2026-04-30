# FE API Envelope Cleanup — Slice 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the typed `api` helper module + envelope-protection ESLint rules + first feature migration (`comments-activity`), as PR #3 of the codebase-assessment Track 4 program.

**Architecture:** New `src/lib/api/` module wraps the existing axios `apiClient` with eight typed helpers (`api.get/post/put/patch/delete/paged/download` + thrown `ApiError`). Feature `*.api.ts` files migrate one slice at a time; lint rules forbid raw `apiClient` outside the api layer and `.data.data` chains anywhere. `comments-activity` is slice 1 — uniform Convention A, leaf feature, exercises full surface.

**Tech Stack:** React 19, TypeScript 5, axios 1.x, TanStack Query 5, ESLint 9 flat config, sonner toasts. No test runner in repo — verification gate is `tsc -b` + `eslint` + Playwright manual smoke per CLAUDE.md "Post-Feature Testing Workflow".

**Spec:** [`docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md`](../specs/2026-04-30-fe-api-envelope-cleanup-design.md). The multi-PR roadmap for slices 2–5 lives in §8 of the spec.

---

## File Map

**Create (5 files):**
- `boilerplateFE/src/lib/api/types.ts` — `ApiOptions` interface
- `boilerplateFE/src/lib/api/error.ts` — `ApiError` class + `toApiError`/`extractEnvelopeMessage` helpers
- `boilerplateFE/src/lib/api/client.ts` — the `api` namespace (eight functions)
- `boilerplateFE/src/lib/api/index.ts` — public surface re-exports
- (none for tests — repo has no FE test runner)

**Modify (8 files):**
- `boilerplateFE/src/types/api.types.ts` — add `PagedResult<T>` interface
- `boilerplateFE/src/types/index.ts` — already re-exports `api.types`, no change needed (verify in T1)
- `boilerplateFE/eslint.config.js` — add envelope rules
- `boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts` — migrate 11 methods
- `boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts` — remove local `PagedResponse<T>`, retype optimistic-update snapshots
- `boilerplateFE/src/features/comments-activity/components/WatchButton.tsx` — `?.data?.X` → `?.X`
- `boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx` — `.data.data` → `.items`
- `boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx` — `data?.data` fallback → `data?.items`
- `CLAUDE.md` — replace "Components must unwrap" rule with new helper rule

**Total:** 13 files. ~250 LOC added (helper module), ~80 LOC modified (migration), ~40 LOC removed (defensive fallbacks).

---

## Task 1: Add `PagedResult<T>` to shared types

**Files:**
- Modify: `boilerplateFE/src/types/api.types.ts`

- [ ] **Step 1.1: Add the `PagedResult<T>` export**

Append after the `PaginationParams` block (around line 35):

```ts
/** Helper return type — what `api.paged<T>(...)` resolves to.
 * Uses `items` (not `data`) by design: after the api helper unwraps the
 * envelope, the literal field name `data` does not appear in any FE consumer. */
export interface PagedResult<T> {
  items: T[];
  pagination: PaginationMeta;
}
```

- [ ] **Step 1.2: Verify the existing `src/types/index.ts` re-exports `api.types`**

Run: `grep "api.types" boilerplateFE/src/types/index.ts`
Expected: `export * from './api.types';` — no edit needed.

- [ ] **Step 1.3: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean (no new errors; this is a pure addition).

- [ ] **Step 1.4: Commit**

```bash
git add boilerplateFE/src/types/api.types.ts
git commit -m "feat(fe): add PagedResult<T> shared type for envelope helper"
```

---

## Task 2: Create `src/lib/api/types.ts` (`ApiOptions`)

**Files:**
- Create: `boilerplateFE/src/lib/api/types.ts`

- [ ] **Step 2.1: Write the file**

```ts
import type { AxiosProgressEvent } from 'axios';

/**
 * Options accepted by every `api.*` helper.
 *
 * Deliberately narrow — *not* a passthrough to `AxiosRequestConfig`.
 * Adding a field requires a design discussion: the whole point of this
 * module is that there's exactly one way to do each thing.
 *
 * - `signal` — TanStack Query passes this automatically when query
 *   functions accept the QueryFunctionContext object.
 * - `onUploadProgress` — multipart uploads only. Most callers don't need it.
 * - `suppressValidationToast` — preserves the existing axios config flag
 *   used by login/register inline-error forms. Read by `error.interceptor.ts`.
 * - `headers` — last-resort overrides. Auth + tenant headers are set by
 *   request interceptors and should never be specified by callers.
 */
export interface ApiOptions {
  signal?: AbortSignal;
  onUploadProgress?: (event: AxiosProgressEvent) => void;
  suppressValidationToast?: boolean;
  headers?: Record<string, string>;
}
```

- [ ] **Step 2.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean.

- [ ] **Step 2.3: Commit**

```bash
git add boilerplateFE/src/lib/api/types.ts
git commit -m "feat(fe): add ApiOptions for typed api helper module"
```

---

## Task 3: Create `src/lib/api/error.ts` (`ApiError` + helpers)

**Files:**
- Create: `boilerplateFE/src/lib/api/error.ts`

- [ ] **Step 3.1: Write the file**

```ts
import type { AxiosError } from 'axios';
import i18n from '@/i18n';
import type { ApiResponse, ApiError as ApiErrorBody } from '@/types';

/**
 * Single error type thrown by every `api.*` helper.
 *
 * - HTTP errors (4xx/5xx) are wrapped from the AxiosError. `cause` retains
 *   the original axios error for advanced use (e.g., reading
 *   `error.cause.config?.url`).
 * - HTTP 200 + envelope `success: false` is wrapped from the response body.
 *   `status` is `null` in this case to distinguish from real HTTP failures.
 *
 * Caller pattern:
 *   try { await api.post(...) }
 *   catch (e) {
 *     if (e instanceof ApiError && e.code === 'TENANT_INACTIVE') ...
 *     throw e;
 *   }
 */
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

/**
 * Build an `ApiError` from a rejected axios call.
 *
 * The existing response error interceptor (`src/lib/axios/interceptors/
 * error.interceptor.ts`) has already toasted the message and tacked it
 * onto the rejection as `parsedMessage`. We reuse that here so the helper
 * doesn't double-toast.
 */
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

/**
 * Build an `ApiError` from a 200-status response whose envelope reports
 * `success: false`. The interceptor never fired (status was 2xx), so we
 * are responsible for both constructing the error and toasting.
 */
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

/**
 * Extract a human-readable message from an envelope that reports failure
 * on a 200 response. Mirrors the validation-first / message / detail
 * priority used by `error.interceptor.ts:getErrorMessage`, but operates
 * on the envelope body directly (no HTTP status to switch on).
 */
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
  const keys = Object.keys(record);
  return keys.length > 0 ? keys[0] : null;
}

function firstNonEmpty(arrays: Array<string[]>): string | null {
  for (const arr of arrays) {
    if (Array.isArray(arr) && arr.length > 0 && typeof arr[0] === 'string') {
      return arr[0];
    }
  }
  return null;
}
```

- [ ] **Step 3.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean. (`ApiResponse` and `ApiError as ApiErrorBody` resolve from `src/types`.)

- [ ] **Step 3.3: Commit**

```bash
git add boilerplateFE/src/lib/api/error.ts
git commit -m "feat(fe): add ApiError class and envelope error normalization"
```

---

## Task 4: Create `src/lib/api/client.ts` (the `api` namespace)

**Files:**
- Create: `boilerplateFE/src/lib/api/client.ts`

- [ ] **Step 4.1: Write the file**

```ts
import type { AxiosError, AxiosRequestConfig, AxiosResponse } from 'axios';
import { toast } from 'sonner';
import { apiClient } from '@/lib/axios';
import type { ApiResponse, PaginatedResponse, PagedResult, ApiError as ApiErrorBody } from '@/types';
import type { ApiOptions } from './types';
import { ApiError, toApiErrorFromAxios, toApiErrorFromEnvelope } from './error';

/**
 * Typed HTTP helpers. Every helper:
 *   1. Calls the shared `apiClient` (auth, refresh, error interceptors active).
 *   2. Unwraps `ApiResponse<T>` → `T` (or `PaginatedResponse<T>` → `PagedResult<T>`).
 *   3. Throws `ApiError` on HTTP failure or envelope `success: false`.
 *
 * Feature `*.api.ts` files use this namespace exclusively. Raw `apiClient`
 * is allowed only inside `*.api.ts` during migration and SSE/streaming
 * files (with eslint-disable comment).
 */

function buildConfig(
  options: ApiOptions | undefined,
  extras?: Partial<AxiosRequestConfig>,
): AxiosRequestConfig {
  return {
    ...(options?.signal ? { signal: options.signal } : {}),
    ...(options?.onUploadProgress ? { onUploadProgress: options.onUploadProgress } : {}),
    ...(options?.headers ? { headers: options.headers } : {}),
    // suppressValidationToast is a custom axios config field consumed by
    // the existing error interceptor — pass it through unchanged.
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
  // body.data is `T`. If the BE returned an empty 204-style payload axios
  // gives us `''` here; cast to T (callers that expect void will discard).
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
    // Re-wrap as ApiResponse-shaped for the envelope error path.
    throw toApiErrorFromEnvelope(
      { success: false, message: body.message, errors: body.errors, validationErrors: body.validationErrors, data: undefined as unknown as never },
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
      const response = await apiClient.get<Blob>(url, buildConfig(options, { params, responseType: 'blob' }));
      return response.data;
    } catch (error) {
      throw toApiErrorFromAxios(error as AxiosError<ApiErrorBody>);
    }
  },
};

export { ApiError };
```

- [ ] **Step 4.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -30`
Expected: clean. If `suppressValidationToast` is flagged as an unknown config key, axios's module-augmentation should already declare it; check `src/lib/axios/types.ts`. If missing, add the augmentation in Step 4.3.

- [ ] **Step 4.3: Verify axios module augmentation for `suppressValidationToast`**

Run: `grep -rn "suppressValidationToast" boilerplateFE/src/lib/axios/`
Expected: a declaration like `declare module 'axios' { interface AxiosRequestConfig { suppressValidationToast?: boolean } }`.

If absent, add it to `boilerplateFE/src/lib/axios/types.ts`:

```ts
import 'axios';

declare module 'axios' {
  export interface AxiosRequestConfig {
    suppressValidationToast?: boolean;
  }
  export interface InternalAxiosRequestConfig {
    suppressValidationToast?: boolean;
  }
}

export {};
```

(Only run this step if grep returned no results.)

- [ ] **Step 4.4: Commit**

```bash
git add boilerplateFE/src/lib/api/client.ts boilerplateFE/src/lib/axios/types.ts
git commit -m "feat(fe): add typed api namespace wrapping apiClient"
```

(Drop `boilerplateFE/src/lib/axios/types.ts` from the add list if Step 4.3 found the augmentation already in place.)

---

## Task 5: Create `src/lib/api/index.ts` (public surface)

**Files:**
- Create: `boilerplateFE/src/lib/api/index.ts`

- [ ] **Step 5.1: Write the file**

```ts
export { api, ApiError } from './client';
export type { ApiOptions } from './types';
export type { PagedResult } from '@/types';
```

- [ ] **Step 5.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean.

- [ ] **Step 5.3: Verify the import path resolves**

Run: `cd boilerplateFE && node -e "console.log(require.resolve('./src/lib/api/index.ts'))" 2>&1 | tail -5`
Expected: prints the absolute path. If `require.resolve` fails for `.ts`, ignore — vite's resolver handles it; the type-check from 5.2 is the real gate.

- [ ] **Step 5.4: Commit**

```bash
git add boilerplateFE/src/lib/api/index.ts
git commit -m "feat(fe): export api module public surface"
```

---

## Task 6: Add envelope ESLint rules

**Files:**
- Modify: `boilerplateFE/eslint.config.js`

Three rules. Rule 1 (no `apiClient` outside api layer) and rule 2 (no `.data.data` chains) are global day-one. Rule 3 (no `apiClient` in *migrated* `*.api.ts`) is scoped to `comments-activity/**/*.api.ts` for now and grows per slice.

We use `no-restricted-syntax` for envelope rules — not `no-restricted-imports` — because the existing module allowlist block at lines 64–72 turns `no-restricted-imports` off for module files. `no-restricted-syntax` isn't affected.

- [ ] **Step 6.1: Edit `eslint.config.js`**

Replace the existing `defineConfig` array (lines 48–73 in current file) with:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      ...restrictedImportRule,
      // FE API envelope cleanup — Track 4 PR #3 onward.
      // Rule 2 (global): forbid `.data.data` member chains. After migration
      // these never legitimately appear; this catches regressions.
      'no-restricted-syntax': [
        'error',
        {
          selector: 'MemberExpression[object.type="MemberExpression"][object.property.name="data"][property.name="data"]',
          message:
            "`.data.data` envelope chains are forbidden. Use the `api` namespace from `@/lib/api`, which returns the inner payload directly. See docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md.",
        },
      ],
    },
  },
  {
    // Module allowlist — see comment at top of this file. Modules may
    // import optional siblings; the envelope rules above still apply.
    files: moduleConfig.allowlistFiles,
    rules: {
      'no-restricted-imports': 'off',
    },
  },
  {
    // Rule 1 (global): forbid raw `@/lib/axios` (apiClient) outside the
    // api layer. Allowed locations: feature `*.api.ts` files (during the
    // multi-PR migration) and the api module itself.
    files: ['src/**/*.{ts,tsx}'],
    ignores: [
      'src/features/**/*.api.ts',
      'src/lib/api/**',
      'src/lib/axios/**',
    ],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ImportDeclaration[source.value="@/lib/axios"]',
          message:
            "Do not import `@/lib/axios` here. Use the typed `api` namespace from `@/lib/api`. Raw apiClient is allowed only inside feature `*.api.ts` files and the api module itself.",
        },
        {
          selector: 'MemberExpression[object.type="MemberExpression"][object.property.name="data"][property.name="data"]',
          message:
            "`.data.data` envelope chains are forbidden. Use the `api` namespace from `@/lib/api`, which returns the inner payload directly.",
        },
      ],
    },
  },
  {
    // Rule 3 (per-feature, scoped): once a feature is migrated, its
    // *.api.ts files lose access to raw `@/lib/axios` too. Grows with
    // each slice; see plan / spec for the schedule.
    files: ['src/features/comments-activity/**/*.api.ts'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ImportDeclaration[source.value="@/lib/axios"]',
          message:
            "comments-activity is migrated: use `@/lib/api`. Raw apiClient requires explicit allowlist + rationale comment.",
        },
        {
          selector: 'MemberExpression[object.type="MemberExpression"][object.property.name="data"][property.name="data"]',
          message:
            "`.data.data` envelope chains are forbidden. Use the `api` namespace from `@/lib/api`.",
        },
      ],
    },
  },
])
```

Notes on the structure:
- **Block 1 (`files: ['**/*.{ts,tsx}']`)** keeps the original module-pattern rule and adds the global `.data.data` check.
- **Block 2** (module allowlist) is unchanged from before; it only disables `no-restricted-imports`.
- **Block 3** (`src/**/*.{ts,tsx}` minus api layer) adds the no-`@/lib/axios` rule for non-api code. Each later block REPLACES the rule key — flat config last-block-wins for same selectors.
- **Block 4** (comments-activity api glob) tightens further. Future slices append more globs to this `files` array.

- [ ] **Step 6.2: Run ESLint to confirm no pre-existing violations were introduced**

Run: `cd boilerplateFE && npm run lint 2>&1 | tail -30`
Expected: clean *before* the comments-activity migration (Tasks 7–11) is started. If clean, the new rules don't fire on any unmigrated file (because non-api feature files don't import `@/lib/axios` anyway, and `.data.data` chains live inside `*.api.ts` files where rule 3 is not yet applied to the unmigrated features).

If `npm run lint` reports hits in other features' `*.api.ts` files (they all use `r.data.data`), that means rule 3 would need to be scoped exclusively to `comments-activity/**/*.api.ts`. Verify the file glob in Block 4 is `src/features/comments-activity/**/*.api.ts` — not a broader pattern.

If hits appear from rule 1 (no `@/lib/axios` outside api layer) on existing files, list them; they should already be inside `src/features/**/*.api.ts` (allowed by ignores) or `src/lib/axios/**` (also allowed). If a non-api file imports `@/lib/axios` today, log it — it's a finding to handle in this PR (probably an exception for SSE).

- [ ] **Step 6.3: Commit**

```bash
git add boilerplateFE/eslint.config.js
git commit -m "feat(fe): lint rules forbidding raw apiClient + .data.data chains"
```

---

## Task 7: Migrate `comments-activity.api.ts`

**Files:**
- Modify: `boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts`

- [ ] **Step 7.1: Replace the entire file contents**

```ts
import { api } from '@/lib/api';
import { API_ENDPOINTS } from '@/config';
import type {
  Comment,
  CreateCommentData,
  EditCommentData,
  MentionableUser,
  ReactionSummary,
  TimelineItem,
  ToggleReactionData,
  WatchStatus,
} from '@/types/comments-activity.types';
import type { PagedResult } from '@/types';

interface TimelineParams {
  entityType: string;
  entityId: string;
  filter?: string;
  pageNumber?: number;
  pageSize?: number;
}

interface CommentsParams {
  entityType: string;
  entityId: string;
  pageNumber?: number;
  pageSize?: number;
}

interface MentionableUsersParams {
  search?: string;
  pageSize?: number;
  entityType?: string;
  entityId?: string;
}

interface WatchTargetParams {
  entityType: string;
  entityId: string;
}

export const commentsActivityApi = {
  getTimeline: (params: TimelineParams): Promise<PagedResult<TimelineItem>> =>
    api.paged<TimelineItem>(API_ENDPOINTS.COMMENTS_ACTIVITY.TIMELINE, params),

  getComments: (params: CommentsParams): Promise<PagedResult<Comment>> =>
    api.paged<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, params),

  addComment: (data: CreateCommentData): Promise<Comment> =>
    api.post<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENTS, data),

  editComment: (data: EditCommentData): Promise<Comment> =>
    api.put<Comment>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(data.id), data),

  deleteComment: (id: string): Promise<void> =>
    api.delete<void>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_DETAIL(id)),

  toggleReaction: (commentId: string, data: ToggleReactionData): Promise<ReactionSummary[]> =>
    api.post<ReactionSummary[]>(API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTIONS(commentId), data),

  removeReaction: (commentId: string, reactionType: string): Promise<ReactionSummary[]> =>
    api.delete<ReactionSummary[]>(
      API_ENDPOINTS.COMMENTS_ACTIVITY.COMMENT_REACTION(commentId, reactionType),
    ),

  getWatchStatus: (params: WatchTargetParams): Promise<WatchStatus> =>
    api.get<WatchStatus>(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS_STATUS, params),

  watch: (data: WatchTargetParams): Promise<void> =>
    api.post<void>(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS, data),

  unwatch: (params: WatchTargetParams): Promise<void> =>
    api.delete<void>(
      `${API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS}?entityType=${encodeURIComponent(params.entityType)}&entityId=${encodeURIComponent(params.entityId)}`,
    ),

  getMentionableUsers: (params: MentionableUsersParams): Promise<PagedResult<MentionableUser>> =>
    api.paged<MentionableUser>(API_ENDPOINTS.COMMENTS_ACTIVITY.MENTIONABLE_USERS, params),
};
```

Note on `unwatch`: the original used `apiClient.delete(url, { params })` to send query params with a DELETE. The new `api.delete` doesn't expose params (delete doesn't carry a body either). Since the BE endpoint reads from query string, embed the params in the URL. If a future feature needs DELETE-with-params more frequently, we add a `params?` slot to `api.delete` then; for one callsite, embedding is fine.

- [ ] **Step 7.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -30`
Expected: clean for this file. Errors in `comments-activity.queries.ts` and component files are expected because the return types changed; those are fixed in Tasks 8–11.

- [ ] **Step 7.3: Commit**

```bash
git add boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts
git commit -m "refactor(fe): migrate comments-activity.api.ts to typed api helpers"
```

---

## Task 8: Update `comments-activity.queries.ts` for new types

**Files:**
- Modify: `boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts`

The local `type PagedResponse<T> = { data: T[]; ... }` (line 17) was a workaround for the un-typed envelope. It's now replaced by the helper's `PagedResult<T>`. The optimistic-update `onMutate` block touches snapshots typed as `PagedResponse<Comment>` / `PagedResponse<TimelineItem>` and reaches for `old.data` — those need to switch to `old.items`.

- [ ] **Step 8.1: Replace the file's content**

The diffs are localized but interleaved; rewrite the file in full:

```ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query/keys';
import { commentsActivityApi } from './comments-activity.api';
import type { PagedResult } from '@/types';
import type {
  Comment,
  CreateCommentData,
  EditCommentData,
  ReactionSummary,
  TimelineItem,
} from '@/types/comments-activity.types';
import { toast } from 'sonner';
import i18n from '@/i18n';

// Error toasts come from the global axios interceptor — mutations only add
// side-effects (cache invalidation, optimistic updates) here.

function toggleReactionOnList(comments: Comment[] | undefined, commentId: string, reactionType: string): Comment[] | undefined {
  if (!comments) return comments;
  return comments.map((comment) => {
    const updated = toggleReactionOnComment(comment, commentId, reactionType);
    if (updated !== comment) return updated;
    if (comment.replies && comment.replies.length > 0) {
      const replies = toggleReactionOnList(comment.replies, commentId, reactionType);
      if (replies !== comment.replies) return { ...comment, replies };
    }
    return comment;
  });
}

function toggleReactionOnComment(comment: Comment, commentId: string, reactionType: string): Comment {
  if (comment.id !== commentId) return comment;
  const existing = comment.reactions.find((r) => r.reactionType === reactionType);
  let reactions: ReactionSummary[];
  if (existing) {
    if (existing.userReacted) {
      const nextCount = Math.max(0, existing.count - 1);
      reactions = nextCount === 0
        ? comment.reactions.filter((r) => r.reactionType !== reactionType)
        : comment.reactions.map((r) =>
            r.reactionType === reactionType ? { ...r, count: nextCount, userReacted: false } : r,
          );
    } else {
      reactions = comment.reactions.map((r) =>
        r.reactionType === reactionType ? { ...r, count: r.count + 1, userReacted: true } : r,
      );
    }
  } else {
    reactions = [...comment.reactions, { reactionType, count: 1, userReacted: true }];
  }
  return { ...comment, reactions };
}

// ── Queries ────────────────────────────────────────────────────────────────

export function useTimeline(
  entityType: string,
  entityId: string,
  params?: { filter?: string; pageNumber?: number; pageSize?: number },
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.timeline.list(entityType, entityId, params),
    queryFn: () => commentsActivityApi.getTimeline({ entityType, entityId, ...params }),
    enabled: !!entityType && !!entityId,
  });
}

export function useComments(
  entityType: string,
  entityId: string,
  params?: { pageNumber?: number; pageSize?: number },
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.comments.list(entityType, entityId, params),
    queryFn: () => commentsActivityApi.getComments({ entityType, entityId, ...params }),
    enabled: !!entityType && !!entityId,
  });
}

export function useWatchStatus(entityType: string, entityId: string) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.watchers.status(entityType, entityId),
    queryFn: () => commentsActivityApi.getWatchStatus({ entityType, entityId }),
    enabled: !!entityType && !!entityId,
  });
}

export function useMentionableUsers(
  search?: string,
  enabled = true,
  entityType?: string,
  entityId?: string,
) {
  return useQuery({
    queryKey: queryKeys.commentsActivity.mentionableUsers(search, entityType, entityId),
    queryFn: () =>
      commentsActivityApi.getMentionableUsers({ search, pageSize: 10, entityType, entityId }),
    enabled,
  });
}

// ── Mutations ──────────────────────────────────────────────────────────────

export function useAddComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateCommentData) => commentsActivityApi.addComment(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.commentAdded', 'Comment added'));
    },
  });
}

export function useEditComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: EditCommentData) => commentsActivityApi.editComment(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      toast.success(i18n.t('commentsActivity.commentEdited', 'Comment updated'));
    },
  });
}

export function useDeleteComment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => commentsActivityApi.deleteComment(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      toast.success(i18n.t('commentsActivity.commentDeleted', 'Comment deleted'));
    },
  });
}

export function useToggleReaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, reactionType }: { commentId: string; reactionType: string }) =>
      commentsActivityApi.toggleReaction(commentId, { reactionType }),
    // Optimistic update: mutate every cached comments and timeline page so the
    // reaction badge flips instantly. Snapshot for rollback on error.
    onMutate: async ({ commentId, reactionType }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.commentsActivity.comments.all });
      await queryClient.cancelQueries({ queryKey: queryKeys.commentsActivity.timeline.all });

      const commentSnapshots = queryClient.getQueriesData<PagedResult<Comment>>({
        queryKey: queryKeys.commentsActivity.comments.all,
      });
      const timelineSnapshots = queryClient.getQueriesData<PagedResult<TimelineItem>>({
        queryKey: queryKeys.commentsActivity.timeline.all,
      });

      queryClient.setQueriesData<PagedResult<Comment>>(
        { queryKey: queryKeys.commentsActivity.comments.all },
        (old) => {
          if (!old?.items) return old;
          const next = toggleReactionOnList(old.items, commentId, reactionType);
          return next === old.items ? old : { ...old, items: next ?? [] };
        },
      );

      queryClient.setQueriesData<PagedResult<TimelineItem>>(
        { queryKey: queryKeys.commentsActivity.timeline.all },
        (old) => {
          if (!old?.items) return old;
          let changed = false;
          const next = old.items.map((item) => {
            if (item.type !== 'comment' || !item.comment) return item;
            const updated = toggleReactionOnComment(item.comment, commentId, reactionType);
            if (updated === item.comment) return item;
            changed = true;
            return { ...item, comment: updated };
          });
          return changed ? { ...old, items: next } : old;
        },
      );

      return { commentSnapshots, timelineSnapshots };
    },
    onError: (_error, _vars, context) => {
      context?.commentSnapshots.forEach(([key, value]) => queryClient.setQueryData(key, value));
      context?.timelineSnapshots.forEach(([key, value]) => queryClient.setQueryData(key, value));
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.timeline.all });
      queryClient.invalidateQueries({ queryKey: queryKeys.commentsActivity.comments.all });
    },
  });
}

export function useWatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { entityType: string; entityId: string }) => commentsActivityApi.watch(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.watching', 'You are now watching this item'));
    },
  });
}

export function useUnwatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { entityType: string; entityId: string }) => commentsActivityApi.unwatch(data),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: queryKeys.commentsActivity.watchers.status(variables.entityType, variables.entityId),
      });
      toast.success(i18n.t('commentsActivity.unwatched', 'You stopped watching this item'));
    },
  });
}
```

Changes from before:
1. Removed local `type PagedResponse<T>` (line 17 in the original).
2. Imported `PagedResult` from `@/types`.
3. `getQueriesData<PagedResult<...>>` instead of `<PagedResponse<...>>`.
4. Inside `setQueriesData` callbacks: `old?.data` → `old?.items`, `old.data.map` → `old.items.map`, `{ ...old, data: next }` → `{ ...old, items: next }`.

- [ ] **Step 8.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -30`
Expected: clean for `comments-activity.queries.ts`. Component files (Tasks 9–11) may still have errors.

- [ ] **Step 8.3: Commit**

```bash
git add boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts
git commit -m "refactor(fe): retype comments-activity queries for PagedResult helper"
```

---

## Task 9: Migrate `WatchButton.tsx`

**Files:**
- Modify: `boilerplateFE/src/features/comments-activity/components/WatchButton.tsx`

- [ ] **Step 9.1: Edit lines 19–20**

Find:
```tsx
  const isCurrentlyWatching = watchStatus?.data?.isWatching ?? false;
  const watcherCount = watchStatus?.data?.watcherCount ?? 0;
```

Replace with:
```tsx
  const isCurrentlyWatching = watchStatus?.isWatching ?? false;
  const watcherCount = watchStatus?.watcherCount ?? 0;
```

- [ ] **Step 9.2: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean for `WatchButton.tsx`.

- [ ] **Step 9.3: Commit**

```bash
git add boilerplateFE/src/features/comments-activity/components/WatchButton.tsx
git commit -m "refactor(fe): unwrap WatchButton watchStatus to typed payload"
```

---

## Task 10: Migrate `EntityTimeline.tsx`

**Files:**
- Modify: `boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx`

The component reads `data?.data` (the items array) on line 55, 56, 59 and `data?.pagination?.totalPages` on line 80. After migration `data` is `PagedResult<TimelineItem> | undefined` so `data?.data` becomes `data?.items`. The `data?.pagination` access is unchanged (PagedResult preserves the pagination field).

- [ ] **Step 10.1: Edit the data-access lines**

Find lines 54–59:
```tsx
  if (
    data?.data &&
    (lastSync.data !== data.data || lastSync.filter !== filter || lastSync.page !== pageNumber)
  ) {
    const filterChanged = lastSync.filter !== filter;
    const incoming = data.data;
```

Replace with:
```tsx
  if (
    data?.items &&
    (lastSync.data !== data.items || lastSync.filter !== filter || lastSync.page !== pageNumber)
  ) {
    const filterChanged = lastSync.filter !== filter;
    const incoming = data.items;
```

- [ ] **Step 10.2: Verify no other `.data.data` or `data?.data` accesses remain**

Run: `grep -nE "data\?\.data|data\.data" boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx`
Expected: no output.

(`data?.pagination?.totalPages` on line 80 is fine — `pagination` is still on `PagedResult<T>`.)

- [ ] **Step 10.3: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean.

- [ ] **Step 10.4: Commit**

```bash
git add boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx
git commit -m "refactor(fe): EntityTimeline reads PagedResult items"
```

---

## Task 11: Migrate `MentionAutocomplete.tsx`

**Files:**
- Modify: `boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx`

Line 59 has a defensive `Array.isArray(data) ? data : (data?.data ?? [])` — after migration, `data` is always `PagedResult<MentionableUser> | undefined`, never an array, so the `Array.isArray` branch is dead. Replace with the items field directly.

- [ ] **Step 11.1: Edit line 58–61**

Find:
```tsx
    const users: MentionableUser[] = useMemo(() => {
      const raw = Array.isArray(data) ? data : (data?.data ?? []);
      return raw as MentionableUser[];
    }, [data]);
```

Replace with:
```tsx
    const users: MentionableUser[] = useMemo(() => data?.items ?? [], [data]);
```

The `as MentionableUser[]` cast is also dropped — types now flow correctly from the helper.

- [ ] **Step 11.2: Verify no other defensive envelope accesses remain**

Run: `grep -nE "data\?\.data|data\.data|Array\.isArray\(data\)" boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx`
Expected: no output.

- [ ] **Step 11.3: Type-check**

Run: `cd boilerplateFE && npx tsc -b --pretty false 2>&1 | tail -20`
Expected: clean.

- [ ] **Step 11.4: Commit**

```bash
git add boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx
git commit -m "refactor(fe): MentionAutocomplete reads PagedResult items"
```

---

## Task 12: Update `CLAUDE.md` frontend rules

**Files:**
- Modify: `CLAUDE.md`

Replace the "API Response Envelope" bullet under §"Frontend Rules — Must Always Follow" → "Components & Patterns" with the new helper-first rule.

- [ ] **Step 12.1: Find the existing bullet**

Run: `grep -n "API Response Envelope\|Components must unwrap" CLAUDE.md`
Expected: matches a line under "Components & Patterns" (around line 250–260).

- [ ] **Step 12.2: Replace the bullet**

Find:
```
- **API Response Envelope** — Backend wraps all responses in `ApiResponse<T>`: `{ data: T, success, errors }`. Frontend API methods return `r.data` (axios), giving `{ data: T, success }`. Components must unwrap: `const item = response?.data ?? response`. This is the #1 source of "data not showing" bugs.
```

Replace with:
```
- **API Response Envelope** — All FE feature code uses the typed `api` namespace from `@/lib/api` — never raw `apiClient`. Helpers return `T` (or `{ items, pagination }` for paged endpoints) and throw `ApiError` on failure. Components and queries never see `ApiResponse<T>`; the `data.data` pattern is forbidden by ESLint. Migration is per-feature; check `eslint.config.js` for the migrated-features allowlist. See [docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md](docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md).
- **API Errors** — Helpers throw a typed `ApiError` (`status`, `code`, `validationErrors`, `cause`) on HTTP failure or envelope `success: false`. Use `e instanceof ApiError && e.code === '...'` for typed handlers. The error toast already fired (HTTP errors via the existing axios interceptor; envelope errors via the helper) — don't toast again in the catch.
```

- [ ] **Step 12.3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update FE rules — typed api namespace and ApiError"
```

---

## Task 13: Full verification gate

- [ ] **Step 13.1: ESLint clean**

Run: `cd boilerplateFE && npm run lint 2>&1 | tail -30`
Expected: zero errors. Any envelope-rule hit is a regression — investigate before proceeding.

- [ ] **Step 13.2: Build clean**

Run: `cd boilerplateFE && npm run build 2>&1 | tail -30`
Expected: `tsc -b` clean, vite build emits dist with no type errors.

- [ ] **Step 13.3: BE arch tests clean**

Run: `cd boilerplateBE && dotnet test Starter.sln --filter "FullyQualifiedName~MessagingArchitectureTests|FullyQualifiedName~Plan5eAcidTests" --nologo --verbosity quiet 2>&1 | tail -20`
Expected: all green. (FE-only PRs shouldn't break BE arch tests; running them confirms the FE-side change didn't accidentally bleed into BE contracts.)

- [ ] **Step 13.4: Sanity grep — no `r.data.data` / `?.data?.X` left in comments-activity**

Run: `grep -rnE "r\.data\.data|response\.data\.data|\?\.data\?\." boilerplateFE/src/features/comments-activity/`
Expected: no output.

Run: `grep -rn "@/lib/axios" boilerplateFE/src/features/comments-activity/`
Expected: no output (the migrated feature no longer touches raw axios).

- [ ] **Step 13.5: Manual smoke via Playwright (CLAUDE.md "Post-Feature Testing Workflow")**

Per the workflow doc: spin up the dev BE+FE on test ports, navigate to a page that embeds `EntityTimeline` (workflow instance detail is the canonical consumer — `/workflows/instances/:id`), and verify:

- Timeline loads and shows comments + activity items (mixed).
- Filter tabs (All / Comments / Activity) switch the list and reset pagination.
- "Load more" appends a new page without duplicating items.
- Comment composer: add a comment → it appears via real-time channel + timeline invalidates → `commentAdded` toast.
- Edit a comment → `commentEdited` toast → list updates.
- Delete a comment → `commentDeleted` toast → list updates.
- Toggle a reaction → optimistic flip is instant; server-side roundtrip leaves it stable.
- Click `WatchButton` → "You are now watching this item" toast; badge count updates.
- Click again → "You stopped watching" toast.
- Mention autocomplete (type `@` in composer) → user list appears; selection inserts mention.
- Trigger a deliberate failure (e.g., delete a comment you don't own) → toast shows BE error message; UI doesn't break.

If any path errors or shows an undefined value where data should be, capture the console + network and stop the slice — likely indicates a missed `.data` → `.items` migration.

---

## Task 14: Final cleanup commit

- [ ] **Step 14.1: Verify branch state**

Run: `git status && git log origin/main..HEAD --oneline`
Expected: working tree clean, commits in order (Task 1 → Task 12).

- [ ] **Step 14.2: Push branch**

Run: `git push -u origin codex/fe-api-envelope-cleanup`

- [ ] **Step 14.3: Open PR (only when user requests)**

PR title: `feat(fe): API envelope cleanup — typed helper module + comments-activity slice (PR #3)`

PR body (HEREDOC template — see CLAUDE.md guidance):

```
## Summary

- Adds `src/lib/api/` typed helper module (`api.get/post/put/patch/delete/paged/download` + `ApiError`) — single import surface eliminating the three coexisting envelope conventions.
- Adds two ESLint rules global day-one (no `@/lib/axios` outside api layer; no `.data.data` chains) plus per-feature scoped rule for migrated features.
- Migrates `comments-activity` (11 methods, 3 components) as the first slice — proves the pattern end-to-end on a uniform leaf feature.
- Updates `CLAUDE.md` FE rules to direct contributors at the typed surface.
- Documents the multi-PR roadmap (slices 2–5 covering the remaining 18 features) in the design spec §8.

## Background

Per `docs/architecture/codebase-assessment.md` Track 4, this is PR #3 of the program. Risk register entry D1 (FE API envelope unwrapping inconsistency, High, Open). Pre-migration survey found 19 feature `*.api.ts` files using three competing conventions; 7 chained `?.data?.X` accesses in components; documented bug-source #1 in CLAUDE.md.

## Design

`docs/superpowers/specs/2026-04-30-fe-api-envelope-cleanup-design.md`

## Test plan

- [x] `npm run lint` clean
- [x] `npm run build` clean
- [x] BE arch tests clean
- [ ] Playwright smoke (per CLAUDE.md "Post-Feature Testing Workflow") on workflow instance detail page — comments timeline, watch button, mentions, optimistic reactions

## Follow-up PRs (documented in spec §8)

| PR | Slice | Features |
|---|---|---|
| #4 | Slice 2 — worst-case | auth |
| #5 | Slice 3 — admin core | users, roles, tenants |
| #6 | Slice 4 — monetization & data | billing, products, workflow |
| #7 | Slice 5 — long tail + final lint hardening | api-keys, audit-logs, files, reports, notifications, settings, webhooks, communication, feature-flags, import-export, access |

Note: `origin/main` d986e057 added Phase 5b Communication after this branch was cut. Those files are merged here, but their envelope cleanup is intentionally deferred to the existing Slice 5 `communication` entry; slice 1 remains limited to `comments-activity`.

After PR #7, ESLint rule 3 flips global and Track 4 D1 closes.
```

(Per memory: do not include `Co-Authored-By` lines.)

---

## Self-Review

**Spec coverage:**
- Spec §3.1 (helper-with-private-interceptor) → Tasks 2–5
- Spec §3.2 (PagedResult `{ items, pagination }`) → Task 1, used in Tasks 4, 7, 8, 10, 11
- Spec §3.3 (ApiError with envelope handling) → Tasks 3, 4
- Spec §3.4 (tight surface) → Task 4 (eight functions, named, no parallel paths)
- Spec §4.1–4.7 (helper internals) → Tasks 2–5
- Spec §5.1–5.4 (lint rules) → Task 6
- Spec §6 (comments-activity migration) → Tasks 7–11
- Spec §7 (CLAUDE.md update) → Task 12
- Spec §6.4 (verification gate) → Task 13
- Spec §8 (multi-PR roadmap) → Task 14.3 (PR body); spec §8 itself documents the future

**Placeholder scan:** No TBDs, "TODO later", or "implement appropriate X". Each task has the actual code + commands. Step 4.3 is conditional but explicitly says "only if grep returned no results."

**Type consistency:** `PagedResult<T>` defined in T1 with `items: T[]; pagination: PaginationMeta`. Used unchanged in T4 (helper), T7 (api), T8 (queries), T10/T11 (components). `api.paged<T>` return type matches across all callsites. `ApiError` fields (`status`, `code`, `validationErrors`, `cause`) match between T3 (definition), T4 (helper construction), T12 (CLAUDE.md docs).

**Scope check:** Single PR scope. Multi-PR roadmap is explicitly out-of-scope-for-this-plan but documented in spec §8 and in the PR description (T14.3) so reviewers see the program shape.

**Ambiguity check:** Step 4.3 conditional on grep result → explicit. Step 6.2 about pre-existing violations → explicit fallback if rule 1 hits other features. Step 7.1 unwatch URL embedding → explicitly justified inline. Step 13.5 manual QA → explicit pass criteria per checkbox.

No unresolved gaps.
