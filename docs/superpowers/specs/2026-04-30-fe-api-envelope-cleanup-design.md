# Frontend API Envelope Cleanup — Design

**Created:** 2026-04-30
**Branch:** `codex/fe-api-envelope-cleanup` (off latest `origin/main`, b1e2e6e6, with Theme 5 module bootstrap merged)
**Roadmap reference:** [`docs/architecture/codebase-assessment.md`](../../architecture/codebase-assessment.md) §Track 4 — "API and Frontend Contract Reliability." The PR sequence table lists this as **PR #3: Frontend API envelope cleanup design + first migration slice**.
**Risk register:** D1 (High, Open) — *"FE API envelope unwrapping remains inconsistent."*

This spec covers the full multi-PR program. The immediate PR ships the helper module, lint guardrails, and the first migration slice. Subsequent PRs migrate the remaining 18 feature `*.api.ts` files in slices.

---

## 1. Goal

Eliminate the `r.data.data` / `?.data?.X` / `data ?? response` family of patterns from the React frontend by introducing a single typed helper module that fully encapsulates `ApiResponse<T>` envelope handling. After migration, no feature code touches the envelope: feature `*.api.ts` files return `Promise<T>` (or `Promise<{ items, pagination }>`), and components consume `T` directly.

**Concrete deliverables for PR #3 (this branch):**
1. New module `src/lib/api/` exposing the `api` namespace, `ApiError` class, and `ApiOptions` type.
2. Three ESLint rules (two global day-one, one per-feature) preventing regression.
3. Migration of `comments-activity` (11 methods, uniform Convention A, leaf feature) as the proof-of-concept slice.
4. Updated `CLAUDE.md` frontend rules pointing at the new helpers.
5. This design doc + slice-1 plan committed.

**Subsequent PRs** (planned in §8) migrate the remaining 18 features in 4–5 slices.

---

## 2. Non-goals

Locked in at brainstorm; listed up front so they don't get relitigated:

- **No bundle-size budget gate.** That's PR #9 per the assessment sequence.
- **No i18n parity script.** Same — PR #9. Currently `ar` is missing 44 keys, `ku` 115; that's tracked separately.
- **No OpenAPI codegen.** Right long-term direction; the helper *is* the seam codegen would target. Out of scope for this program.
- **No changes to error-toast UX for HTTP 4xx/5xx.** The existing `error.interceptor.ts` keeps owning toast behavior for HTTP failures, including the `suppressValidationToast` opt-out used by login/register inline-error forms.
- **No changes to the auth/refresh interceptors.** They sit at the `apiClient` layer, beneath the helper, and stay untouched.
- **No migration of features outside the slice's scope per PR.** Each PR migrates exactly the features it names.
- **No new BE endpoints, no DTO changes.** This is a pure FE refactor against a fixed API contract.
- **No replacement of the existing `apiClient` axios instance.** The helper wraps it. `apiClient` stays exported (allowlist-only after migration).
- **No retry/circuit-breaker logic in the helpers.** Out of scope; if needed later, it lives in the helper layer.
- **No request/response logging hooks.** Existing OpenTelemetry trace propagation continues to work via axios; no new instrumentation.

---

## 3. Architecture decisions

Four decisions were locked during brainstorm. Each had alternatives explicitly considered and rejected.

### 3.1 Helper-module-with-private-interceptor (chosen) over global response interceptor

The helper module owns envelope unwrapping. The shared `apiClient` axios instance and its three interceptors (auth, refresh, error) remain unchanged. Consequence: features migrate file-by-file, unmigrated features keep working unchanged, and the rollout is reversible per-feature.

The rejected alternative was a global response interceptor that strips `ApiResponse<T>` for every callsite at once. That would have changed behavior at 184 callsites simultaneously and broken the 35 sites today expecting the envelope. Big-bang migrations of this size belong in their own PR; this approach lets us validate the pattern on one feature first.

### 3.2 Paged endpoints normalize to `{ items, pagination }` over preserving `{ data, pagination }`

After the helper, the literal field name `data` does not appear in any FE consumer. This is the central anti-confusion lever — the whole reason the cleanup exists is that `data.data.data` is unreadable, and a paged result with `.data` for items keeps the ambiguity alive.

Rejected alternatives: preserve wire shape (cheaper migration but defeats the goal); flatten pagination meta into seven loose fields (loses the conceptual grouping that `<Pagination>` consumes as a unit).

### 3.3 Helpers throw a typed `ApiError`; existing error interceptor unchanged

Helpers resolve with `T` on success and throw `ApiError` on either HTTP failure (wrapping the AxiosError) or HTTP 200 + `success: false` (building from the envelope). The error interceptor's existing toast behavior for HTTP 4xx/5xx continues unchanged, preserving today's UX. The HTTP-200-but-`success:false` case — silently broken today — gets toasted by the helper using the same i18n message extraction.

Rejected alternatives: move all error UX into the helpers (touches existing UX with regression risk; out of scope); rethrow raw AxiosError (no `instanceof` discrimination, callers can't write typed handlers).

### 3.4 Tight, opinionated helper surface — no parallel ways to do the same thing

Eight call shapes total (verb × payload × envelope-shape), one `ApiError` class, one `ApiOptions` type. No `apiUpload` helper (FormData passes through `api.post`); no flag-on-`api.get` to switch into blob mode (`api.download` is its own helper because blobs have no envelope to unwrap); no arbitrary `AxiosRequestConfig` passthrough.

Rationale: the failure mode this PR fixes — three coexisting conventions across 19 files — is exactly what happens when a surface has multiple equivalent ways to do the same thing. The helper is deliberately narrow. If a callsite needs anything outside `ApiOptions`, that's a signal it belongs on raw `apiClient` with an allowlist comment, not a reason to widen the helper.

---

## 4. Helper module — `src/lib/api/`

### 4.1 Surface

```ts
// src/lib/api/index.ts
import { api, ApiError, type ApiOptions } from '@/lib/api';

api.get<T>(url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<T>;
api.post<T>(url: string, body?: unknown, options?: ApiOptions): Promise<T>;
api.put<T>(url: string, body?: unknown, options?: ApiOptions): Promise<T>;
api.patch<T>(url: string, body?: unknown, options?: ApiOptions): Promise<T>;
api.delete<T>(url: string, options?: ApiOptions): Promise<T>;
api.paged<T>(url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<PagedResult<T>>;
api.download(url: string, params?: Record<string, unknown>, options?: ApiOptions): Promise<Blob>;
```

```ts
// src/types/api.types.ts (additions)
export interface PagedResult<T> {
  items: T[];
  pagination: PaginationMeta;
}
```

`PaginationMeta` already exists in `api.types.ts` and is unchanged.

### 4.2 `ApiOptions`

```ts
export interface ApiOptions {
  signal?: AbortSignal;                                // cancellation; TanStack Query passes this automatically
  onUploadProgress?: (e: AxiosProgressEvent) => void;  // multipart uploads only
  suppressValidationToast?: boolean;                   // forms with inline errors (preserves existing UX flag)
  headers?: Record<string, string>;                    // rare overrides only — auth + tenant headers handled by interceptors
}
```

Not `AxiosRequestConfig`. The set is finite, named, and additive only when a real callsite requires a new field. Adding fields requires explicit design discussion.

### 4.3 `ApiError`

```ts
export class ApiError extends Error {
  readonly status: number | null;            // null when HTTP 200 + success:false
  readonly code: string | null;              // first key of envelope.errors, else null
  readonly validationErrors: Record<string, string[]> | null;
  readonly cause: unknown;                   // original AxiosError when HTTP; envelope body otherwise

  constructor(init: { message: string; status: number | null; code: string | null;
                      validationErrors: Record<string, string[]> | null; cause: unknown });
}
```

Caller pattern:

```ts
try {
  await api.post<TenantDto>('/tenants', body);
} catch (e) {
  if (e instanceof ApiError && e.code === 'TENANT_INACTIVE') {
    // typed handler
  }
  throw e;
}
```

TanStack Query's `error` is automatically `ApiError | null` when mutation/query type parameters declare it (see §4.7 example).

### 4.4 Implementation contract — what the helper does internally

1. **Build the axios call.** Use existing `apiClient`. Map `ApiOptions` → `AxiosRequestConfig` (rename `headers` directly; pass `signal`, `onUploadProgress`, `suppressValidationToast` through unchanged — the last is already a custom axios config field consumed by `error.interceptor.ts`).
2. **For `get`/`post`/`put`/`patch`/`delete`:** await the call. The response body is `ApiResponse<T>`.
   - If `response.data.success === false`, build `ApiError` from `response.data` (status `null`, code = first key of `errors`, message = first message or fallback i18n key), toast it via the same `getErrorMessage` logic the interceptor uses, and throw.
   - Otherwise return `response.data.data`.
3. **For `paged`:** same as above but body is `PaginatedResponse<T>` (no nested `data: ApiResponse<T>` — paged endpoints are flat). Return `{ items: response.data.data, pagination: response.data.pagination }`.
4. **For `download`:** request with `responseType: 'blob'`. No envelope. Return `response.data` (Blob).
5. **Error path (catch on the axios await):** the existing `error.interceptor.ts` already toasts and tacks on `parsedMessage`. Wrap the rejected `AxiosError` in `ApiError` (status from response, code from `response.data.errors` first key if present, validationErrors from `response.data.validationErrors`, message from `parsedMessage` or fallback) and throw. The toast has already fired.

The helper is ~80 lines. A focused unit; no logic beyond unwrap + error normalization.

### 4.5 Module layout

```
src/lib/api/
├── index.ts          # public surface — re-exports `api`, `ApiError`, `ApiOptions`, `PagedResult`
├── client.ts         # the `api` namespace (the eight functions)
├── error.ts          # `ApiError` class + `toApiError()` helpers
└── types.ts          # `ApiOptions`
```

`PagedResult<T>` lives in `src/types/api.types.ts` (alongside `PaginationMeta`) so feature DTO files can import it without crossing into `@/lib/api` types.

### 4.6 Why this shape eliminates the existing failure modes

- **Three competing conventions → one.** Every `*.api.ts` file uses `api.<verb>`; nothing else compiles after rule 3 ships.
- **`r.data.data` is structurally impossible.** `api.get<User>(...)` returns `Promise<User>`. There's no envelope to dereference. Lint rule 2 catches any regression where someone reaches for `.data.data` anyway (e.g., on a manually-shaped response from raw `apiClient`).
- **`?.data?.X` chains in components disappear.** TanStack Query's `data` is `T`, not `ApiResponse<T>`. WatchButton becomes `watchStatus.isWatching` — no chain, no nullish coalescing on a field that always exists when the query has resolved.
- **`success: false` becomes a typed throw.** Today it's silently a "successful" response that turns into runtime `undefined` access. After migration, it's an `ApiError` that the global query-error boundary catches and toasts (same UX as HTTP errors).
- **Type safety is end-to-end.** Currently many api methods are inferred `Promise<any>` because callers do `.then((r) => r.data)` without a generic — `ApiResponse<unknown>.data` is `unknown`, not `T`. After migration, the generic on `api.<verb><T>(...)` is mandatory at the seam, and TS enforces propagation through queries to components.

### 4.7 Worked example — pre/post for `WatchButton`

**Today:**

```ts
// comments-activity.api.ts
getWatchStatus: (params) =>
  apiClient.get(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS_STATUS, { params })
    .then((r) => r.data),                          // returns ApiResponse<WatchStatus>

// WatchButton.tsx
const { data: watchStatus } = useWatchStatus(entityType, entityId);
const isCurrentlyWatching = watchStatus?.data?.isWatching ?? false;
const watcherCount = watchStatus?.data?.watcherCount ?? 0;
```

**After migration:**

```ts
// comments-activity.api.ts
getWatchStatus: (params: { entityType: string; entityId: string }): Promise<WatchStatus> =>
  api.get<WatchStatus>(API_ENDPOINTS.COMMENTS_ACTIVITY.WATCHERS_STATUS, params),

// WatchButton.tsx
const { data: watchStatus } = useWatchStatus(entityType, entityId);
const isCurrentlyWatching = watchStatus?.isWatching ?? false;
const watcherCount = watchStatus?.watcherCount ?? 0;
```

Same call shape, one fewer level of nesting, types correct end-to-end.

---

## 5. Lint rules — `eslint.config.js`

Three rules. Rules 1 and 2 ship day one. Rule 3 ships per-feature as each migration lands (so unmigrated features don't fail CI).

### 5.1 Rule 1 — No raw `apiClient` outside the api layer (day one)

`no-restricted-imports` config:

```js
{
  files: ['src/features/**/!(*.api).{ts,tsx}', 'src/components/**', 'src/pages/**', 'src/hooks/**'],
  rules: {
    'no-restricted-imports': ['error', {
      paths: [{
        name: '@/lib/axios',
        message: 'Use the typed `api` namespace from `@/lib/api`. Raw `apiClient` is allowed only inside `*.api.ts` (during migration) and SSE/streaming files (with override).',
      }],
    }],
  },
}
```

### 5.2 Rule 2 — No `.data.data` member chains (day one)

`no-restricted-syntax`:

```js
{
  rules: {
    'no-restricted-syntax': ['error', {
      selector: 'MemberExpression[object.type="MemberExpression"][object.property.name="data"][property.name="data"]',
      message: '`.data.data` envelope chains are forbidden. Use the `api` namespace from `@/lib/api`, which returns the inner payload directly.',
    }],
  },
}
```

This covers `r.data.data`, `response.data.data`, `result.data.data`, etc. The selector's `object.type="MemberExpression"` ensures we match a chain of two `.data`s, not just any property called `data.data` (which is unlikely anyway).

### 5.3 Rule 3 — No `apiClient` in migrated `*.api.ts` files (per-feature, ships with each slice)

A scoped `no-restricted-imports` block applied to a growing list of migrated feature globs:

```js
{
  files: [
    'src/features/comments-activity/**/*.api.ts',
    // future slices append here
  ],
  rules: {
    'no-restricted-imports': ['error', {
      paths: [{ name: '@/lib/axios', message: 'Migrated feature: use `@/lib/api`. Raw apiClient is allowlist-only.' }],
    }],
  },
}
```

When the final feature migrates, the file glob narrows to the SSE allowlist (currently expected to be empty) and a top-level rule adds `@/lib/axios` to the global forbidden list.

### 5.4 Allowlist mechanism

Files genuinely needing raw `apiClient` (e.g., a future SSE consumer) can opt out by adding a single-line override at the top:

```ts
// eslint-disable-next-line no-restricted-imports -- SSE: see src/lib/api/README.md §raw-axios
import { apiClient } from '@/lib/axios';
```

Each allowlist comment must include a brief rationale. The expected count after full migration is 0–2 files.

---

## 6. First migration slice — `comments-activity`

11 methods, all uniform Convention A (`.then((r) => r.data)` returning the envelope), 3 component consumers (`WatchButton`, `EntityTimeline`, `MentionAutocomplete`), no cross-feature dependencies. The most surgical possible first slice — uniform input, contained blast radius, exercises the GET / POST / PUT / DELETE / paged surface in one file.

### 6.1 Methods to migrate

From [`src/features/comments-activity/api/comments-activity.api.ts`](../../../boilerplateFE/src/features/comments-activity/api/comments-activity.api.ts):

| # | Method | New helper | Notes |
|---|---|---|---|
| 1 | `getTimeline` | `api.paged<TimelineItem>` | Returns `{ items, pagination }` — currently typed as plain envelope, queries reach for `.data` |
| 2 | `getComments` | `api.paged<Comment>` | Same |
| 3 | `addComment` | `api.post<Comment>` | Returns the created comment |
| 4 | `editComment` | `api.put<Comment>` | Returns the edited comment |
| 5 | `deleteComment` | `api.delete<void>` | Discard return |
| 6 | `toggleReaction` | `api.post<ReactionSummary[]>` | Body: `ToggleReactionData` |
| 7 | `removeReaction` | `api.delete<ReactionSummary[]>` | |
| 8 | `getWatchStatus` | `api.get<WatchStatus>` | The `WatchButton` smell-fix |
| 9 | `watch` | `api.post<void>` | |
| 10 | `unwatch` | `api.delete<void>` | |
| 11 | `getMentionableUsers` | `api.paged<MentionableUser>` | |

### 6.2 Component changes

- [`WatchButton.tsx:19-20`](../../../boilerplateFE/src/features/comments-activity/components/WatchButton.tsx#L19-L20): `watchStatus?.data?.isWatching` → `watchStatus?.isWatching`; same for `watcherCount`.
- [`EntityTimeline.tsx`](../../../boilerplateFE/src/features/comments-activity/components/EntityTimeline.tsx): query result `data` was previously `PaginatedResponse<TimelineItem>`; becomes `{ items: TimelineItem[]; pagination: PaginationMeta }`. Replace `data?.data?.map(...)` (if any) with `data?.items?.map(...)`. Pagination meta access is unchanged.
- [`MentionAutocomplete.tsx`](../../../boilerplateFE/src/features/comments-activity/components/MentionAutocomplete.tsx): same `.data.X` → `.items` shift.
- [`comments-activity.queries.ts`](../../../boilerplateFE/src/features/comments-activity/api/comments-activity.queries.ts): the local `type PagedResponse<T> = { data: T[]; ... }` (line ~17) is removed; queries use the helper's `PagedResult<T>` directly.

### 6.3 The product-status-bug investigation (deferred)

The survey flagged a likely real bug in `products.getStatusCounts` where the type and runtime shape disagree. This is **not** fixed in slice 1 because products is a separate slice. Logged here so it doesn't get lost; it'll be picked up in the products slice (PR #6).

### 6.4 Verification gate for slice 1

- `npm run lint` clean (rule 1, rule 2, scoped rule 3 for comments-activity).
- `npm run build` clean (`tsc -b && vite build`).
- BE arch tests pass (`dotnet test boilerplateBE/Starter.sln --filter MessagingArchitectureTests` plus the AiTool acid test) — proves the FE-side change didn't accidentally touch the BE contract.
- Manual QA via Playwright MCP per CLAUDE.md "Post-Feature Testing Workflow": comments timeline loads, comment add/edit/delete works, reaction toggle works, watch/unwatch works, mention autocomplete works. Three roles: superadmin, tenant admin, regular user.

---

## 7. CLAUDE.md update

Replace the current "Components must unwrap: `const item = response?.data ?? response`" passage in §"Components & Patterns" with a forward-looking rule:

> **API responses are typed payloads, never envelopes.** All feature code uses the `api` namespace from `@/lib/api` — never raw `apiClient`. Helpers return `T` (or `{ items, pagination }` for paged endpoints) and throw `ApiError` on failure. Components and queries never see `ApiResponse<T>`. The `data.data` pattern is forbidden (ESLint rule 2). Migration is per-feature; check `eslint.config.js` for the migrated-features allowlist.

Add a new short subsection §"Error handling" pointing at `ApiError` with the typed-handler example from §4.3.

---

## 8. Multi-PR roadmap — finishing the program

This PR (#3) ships the helper module, lint rules, and one feature. The remaining 18 features migrate in 4 follow-up PRs, ordered by **risk × value**: prove the worst case early, then sweep the rest in increasing-uniformity batches.

| PR | Slice | Features | Method count | Convention mix | Why this batch |
|---|---|---|---|---|---|
| **#3 (this)** | Slice 1 — proof | `comments-activity` | 11 | Uniform A | Leaf, uniform input, exercises full surface |
| #4 | Slice 2 — worst-case | `auth` | 14 | All three (7A + 1B + 6C) | Mixed conventions in one file; pre-auth load path; high traffic. Earn confidence on the hardest case. |
| #5 | Slice 3 — admin core | `users`, `roles`, `tenants` | ~30 | Mostly C + some A | High-traffic admin surfaces; enables most platform-admin paths. |
| #6 | Slice 4 — monetization & data | `billing`, `products`, `workflow` | ~50 | Workflow nearly modern (B); products mixed; billing untouched | Highest-revenue surfaces; uncovers the products status-counts bug noted in §6.3. |
| #7 | Slice 5 — long tail + final hardening | `api-keys`, `audit-logs`, `files`, `reports`, `notifications`, `settings`, `webhooks`, `communication`, `feature-flags`, `import-export`, `access` | ~80 | Mostly C | Sweep + flip lint rule 3 to global. After this PR, raw `apiClient` is allowlist-only. |
| **#9** (sequence-separate) | i18n parity + bundle budget | n/a | n/a | n/a | Track 4's other half — already scheduled separately per the assessment. Independent of this program. |

### 8.1 Slice ordering rationale

- **Slice 1 (uniform / leaf):** validates the helper module against a real feature with zero risk. If the helper has a design flaw, we discover it on a feature where rollback is one revert.
- **Slice 2 (worst-case):** auth has all three conventions in one file *and* is loaded before any other feature. Doing it second (rather than last) means the helper gets battle-tested while we still have time and motivation to tighten its contract — leaving the worst feature for the end is a smell.
- **Slices 3–4 (high-value):** clears the daily-driver admin / billing / products pages. Each slice is one PR and one feature-area cluster, sized for review.
- **Slice 5 (sweep):** by this point the pattern is mechanical and reviewers have seen it five times. The remaining 11 features mostly share Convention C (explicit `response.data.data`), which is the easiest mechanical translation. Final action: rule 3 flips global, with at most a 1–2-file allowlist (currently zero).

### 8.2 Per-slice PR checklist (re-used by each follow-up)

Each follow-up PR follows this template:

1. Edit each `*.api.ts` to use `api.<verb><T>` helpers; remove explicit `r.data.data` / `then((r) => r.data)` patterns.
2. Update `*.queries.ts` types: `useQuery<ApiResponse<T>>` → `useQuery<T>`, `useQuery<PaginatedResponse<T>>` → `useQuery<PagedResult<T>>`. Most call sites need no change because TanStack Query infers from `queryFn`.
3. Update component consumers: `data?.data?.X` → `data?.X` or `data?.items?.X` (paged).
4. Add the feature glob to ESLint rule 3 in `eslint.config.js`.
5. Verify: `npm run lint` clean, `npm run build` clean, BE arch tests clean, Playwright manual smoke.
6. PR description references this design doc and the slice number.

### 8.3 Acceptance criteria for the program

The program is complete when:

- **Zero** non-allowlisted `*.api.ts` files import `@/lib/axios`.
- **Zero** `.data.data` member chains compile-clean across the FE.
- **Zero** components reach into a query/mutation result with a `.data?.X` envelope chain (the chain may still appear for legitimately optional fields on `T` itself — that's not the same pattern).
- ESLint rule 3 is global (not per-glob) with at most 2 allowlisted files.
- The CLAUDE.md "API Response Envelope" rule is replaced by the §7 wording.
- Track 4 D1 risk register entry moves from Open → Closed.

### 8.4 Rollback plan

Each slice is independently revertible. Reverting any slice removes its feature glob from rule 3 and restores the previous `*.api.ts` contents — no migrations, no schema changes, no shared state. The helper module itself can stay even if all slices revert; it's pure addition.

If the helper module needs redesign mid-program: pause new slices, open a follow-up design spec, and the migrated slices stay on the current helper until the redesigned helper ships. Worst case is a temporary co-existence of two helpers, which is still an improvement on the current three-conventions state.

---

## 9. Risks

| ID | Risk | Likelihood | Mitigation |
|---|---|---|---|
| R1 | Helper handles a non-envelope response shape we missed (e.g., a 204 No Content endpoint) | Medium | The slice-1 verification covers GET/POST/PUT/DELETE/paged. Any 204-style response returning empty body is handled by `axios.response.data === ""` → helper returns `undefined as T`; `void` generics make this explicit. |
| R2 | A feature's BE endpoint actually returns flat (non-envelope) data; the helper's unwrap turns it into garbage | Medium | The `products.getStatusCounts` survey finding suggests this exists. Each slice's manual QA must verify the smoke path. The helper throws a clear `ApiError` (rather than silently corrupting) when `response.data.success` is `undefined` — which catches non-envelope shapes loudly. |
| R3 | TanStack Query's automatic-error-boundary doesn't pick up `ApiError` correctly | Low | `ApiError extends Error`, so anything that `instanceof Error`-checks works. Slice 1 manual QA includes triggering an envelope `success: false` (e.g., by attempting an unauthorized action) and confirming the toast fires once. |
| R4 | `suppressValidationToast` flag is silently dropped because the helper doesn't forward it | High if untested | Helper explicitly maps `options.suppressValidationToast` → `AxiosRequestConfig.suppressValidationToast`. Slice-1 plan includes a check on `comments-activity.addComment` with form validation to confirm. |
| R5 | A future contributor adds a new feature using raw `apiClient` because they don't know about `@/lib/api` | High over time | Lint rule 1 (day one) catches it in CI. CLAUDE.md update points new code at the helper. |
| R6 | The 184-callsite final tally is undercounted; the program takes more PRs than planned | Medium | Each slice ships independently; the roadmap is a plan, not a contract. If slice 4 grows too large, split into 4a/4b at PR-time. |

---

## 10. Open questions

None remaining. All architecture decisions locked during brainstorm.

---

## 11. Spec self-review

- **Placeholder scan:** No TBDs, TODOs, or vague requirements. Section 6.3 explicitly defers the products-status-counts bug investigation to its slice and labels the deferral.
- **Internal consistency:** Architecture (§3) → surface (§4) → first slice (§6) → roadmap (§8) is one consistent narrative. Lint rule 3 in §5.3 matches the per-slice checklist in §8.2 step 4.
- **Scope check:** This PR scope (helper + lint + one feature) is plan-sized. The roadmap (§8) is documented but each follow-up PR will get its own focused plan.
- **Ambiguity check:** "First key of `errors`" in §4.4 is deterministic only if the BE returns errors as an ordered map; in practice JS object key order is insertion order for string keys, so this is well-defined. Documented in §4.3 / §4.4 to make the intent explicit.
