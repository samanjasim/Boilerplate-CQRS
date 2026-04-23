# Module Audit — Comments & Activity ↔ Communication

**Date:** 2026-04-16
**Branch:** `feature/module-hardening`
**Scope:** Side-by-side readiness review of the two Wave 1 modules merged to `main`, plus cross-pollination fixes and isolation verification.

---

## Why this audit

Both modules went from merged to production candidate within days of each other. The user asked to pause, compare them side by side, let them learn from each other, and prove one can run without the other — the last point matters because a future feature ("email-on-mention when the Communication module is installed") will depend entirely on the Null-Object composition seam. A broken seam at merge time is a broken feature at request time.

Secondary goal: snapshot program-level status so the next session can pick up without rediscovery.

---

## Side-by-side readiness

| Dimension | Comments & Activity | Communication | Notes |
|---|---|---|---|
| Module plumbing (`IModule`, isolated DbContext + migration table, auto-discovery) | ✅ [`CommentsActivityModule.cs:13`](../../../boilerplateBE/src/modules/Starter.Module.CommentsActivity/CommentsActivityModule.cs) | ✅ [`CommunicationModule.cs:14`](../../../boilerplateBE/src/modules/Starter.Module.Communication/CommunicationModule.cs) | Both correct |
| `Dependencies` empty (cross-module coupling must be capabilities + events only) | ✅ | ✅ [line 19](../../../boilerplateBE/src/modules/Starter.Module.Communication/CommunicationModule.cs) | Pinned by a new test |
| Capability contracts in `Starter.Abstractions` | 4 (`ICommentableEntityRegistry`, `ICommentService`, `IActivityService`, `IEntityWatcherService`) | 3 (`IMessageDispatcher`, `ICommunicationEventNotifier`, `ITemplateRegistrar`) | Both pair with Null Objects in `Starter.Infrastructure/Capabilities/NullObjects` |
| Null-Object fallbacks registered in core `Starter.Infrastructure` via `TryAddScoped` | ✅ | ✅ [`DependencyInjection.cs:79-80`](../../../boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs) | Verified by tests + code-path inspection |
| Integration events | 5 (Created / Edited / Deleted / ReactionToggled / ActivityRecorded) | N/A — dispatches via capability API | Different architectures for different roles |
| Multi-tenancy (global EF filters) | ✅ | ✅ | |
| Permissions (BE + FE mirror) | 6 perms, 3 roles | 8 perms, 3 roles | Admin role in Communication deliberately excludes `ManageQuotas` |
| Maintainer-facing `ROADMAP.md` | ✅ already present | ✅ **added** [`Starter.Module.Communication/ROADMAP.md`](../../../boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md) | Now 8 entries: outbox, SMS, Push, WhatsApp, Ably, provider tests, SendGrid/SES, typed template schemas |
| Integrator-facing developer docs | ✅ [`DEVELOPER_GUIDE.md`](../../../boilerplateBE/src/modules/Starter.Module.CommentsActivity/DEVELOPER_GUIDE.md) (402 lines) | ✅ [`docs/developer-guide.md`](../../../boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md) + [`user-manual.md`](../../../boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md) | Both substantial |
| Tests | ✅ 9 test files (pre-existing) | ✅ **4 new files, 22 new tests** — was zero | See "Tests added" below |
| Registered in `scripts/modules.json` | ❌ → ✅ added | ❌ → ✅ added | `rename.ps1` now strips both cleanly |
| Doc errata | — | Earlier audit claimed Handlebars mismatch; **false positive** — docs say Mustache/Stubble correctly at [dev guide line 504](../../../boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md) | No change needed |

**Readiness before this PR:** Comments ≈ 85%, Communication ≈ 75%.
**Readiness after this PR:** both ≈ 90% against v1 scope.

---

## Work performed

### 1. `scripts/modules.json` — register both modules

Critical gap: both modules merged to `main` without `modules.json` entries. That made the "strip one module, keep the other" test impossible because the rename script can only operate on modules it knows about. Added `commentsActivity` and `communication` entries and introduced a new optional `testsFolder` field so the rename script can also delete orphan test subfolders.

Commit: file [`scripts/modules.json`](../../../scripts/modules.json).

### 2. Communication → `ROADMAP.md`

Mirrored the style of the existing Comments ROADMAP (What / Why deferred / Pick this up when / Starting points). Eight entries documenting every known deferred item found during the audit — transactional outbox, the three stub channel providers (SMS, Push, WhatsApp), Ably real-time push, provider connection-testing, SendGrid/SES native integrations, strongly-typed template variable schemas.

Commit: [`boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md`](../../../boilerplateBE/src/modules/Starter.Module.Communication/ROADMAP.md).

### 3. Doc errata — no-op

Grep for `Handlebars|Liquid|{{#if|{{#each` across the repo returned zero hits. The exploration agent's earlier claim that "the dev guide says Handlebars" did not hold up under verification; both [`developer-guide.md:504`](../../../boilerplateBE/src/modules/Starter.Module.Communication/docs/developer-guide.md) and [`user-manual.md:148`](../../../boilerplateBE/src/modules/Starter.Module.Communication/docs/user-manual.md) already say Mustache. Skipped.

### 4. Communication tests — 22 added

Communication shipped with zero test coverage. Mirrored the Comments testing convention (shared `Starter.Api.Tests` project with a per-module subfolder) instead of creating a separate test project — reuses existing test infra, no solution changes.

Tests added:

- [`StubbleTemplateEngineTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Communication/Infrastructure/StubbleTemplateEngineTests.cs) — variable substitution, missing-variable-as-empty, `{{#section}}`/`{{^section}}` conditionals, `{{#list}}` loops, validator happy path + unclosed-section failure.
- [`CredentialEncryptionServiceTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Communication/Infrastructure/CredentialEncryptionServiceTests.cs) — encrypt/decrypt round-trip using `EphemeralDataProtectionProvider`, mask behaviour for long / short / empty values.
- [`CommunicationModulePermissionsTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Communication/CommunicationModulePermissionsTests.cs) — invariant tests pinning the permission surface (8 perms), role mapping (SuperAdmin: all, Admin: all-except-ManageQuotas, User: View + ViewDeliveryLog), and `Dependencies` empty.
- [`NullMessageDispatcherTests.cs`](../../../boilerplateBE/tests/Starter.Api.Tests/Capabilities/NullMessageDispatcherTests.cs) — **the keystone test for the future email-on-mention feature**. Proves `NullMessageDispatcher` is resolvable when the module is absent, that a real `IMessageDispatcher` registration wins over the Null Object via `TryAddScoped` + `AddScoped` ordering, and that `NullCommunicationEventNotifier` completes silently. If this test ever breaks, the Null-Object composition seam is broken — no cross-module feature that consumes these capabilities can land safely.

Also added [`AssemblyInfo.cs`](../../../boilerplateBE/src/modules/Starter.Module.Communication/AssemblyInfo.cs) with `[InternalsVisibleTo("Starter.Api.Tests")]` — mirrors the existing Comments setup and the only way to test the internal `StubbleTemplateEngine` and `CredentialEncryptionService` without changing their access level.

**Result:** 53 total tests pass (31 pre-existing Comments + core + 22 new).

### 5. Comments `DEVELOPER_GUIDE.md` — no-op

Audit flagged it as potentially missing. It exists (402 lines). Skipped.

### 6. Null-Object path verification

Confirmed the registration order in [`Program.cs:47-52`](../../../boilerplateBE/src/Starter.Api/Program.cs):

1. `AddInfrastructure` runs first, registers `NullMessageDispatcher` via `TryAddScoped`.
2. Module loader iterates `orderedModules` and calls each `ConfigureServices`, which runs `AddScoped<IMessageDispatcher, MessageDispatcher>`.
3. `AddScoped` appends; last registration wins at resolve time. When the Communication module is absent, step 2 never registers a replacement, so the Null Object is what callers resolve.

Comments' [`NotifyMentionedUsersOnCommentCreatedHandler.cs`](../../../boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/NotifyMentionedUsersOnCommentCreatedHandler.cs) today uses `INotificationService` (core in-app) only. This is the natural extension point for the future email-on-mention feature — no refactor needed, just an additional `IMessageDispatcher.SendAsync(...)` call alongside the existing in-app notification.

### 7. `rename.ps1` — delete orphan test folders

The first scenario-1 build failed because `rename.ps1` removed the Communication module but left the new `tests/Starter.Api.Tests/Communication/` folder referencing deleted types. Added step 7 in the per-module removal loop: if `testsFolder` is set in `modules.json`, also delete `tests/{Name}.Api.Tests/{testsFolder}/`. Retested — clean build. This gap would have broken Comments test-removal too (same shape) but went unnoticed because no one had tried stripping Comments from a generated app. Fixed now.

Commit: [`scripts/rename.ps1`](../../../scripts/rename.ps1).

---

## Isolation test results

### Scenario A — Comments only, no Communication

```
pwsh scripts/rename.ps1 -Name "_testCommentsOnly" -OutputDir "." -Modules "commentsActivity" -IncludeMobile:$false
```

Rename output:
```
    - Removed: Billing
    - Removed: Webhooks
    - Removed: Import / Export
    - Removed: Products
    - Removed: Communication
```

| Step | Result |
|---|---|
| Rename script | ✅ clean — Communication BE folder, FE `features/communication/`, `ProjectReference`, solution entry, `modules.config.ts` flag/import/enabledModules entry, `routes.tsx` lazy imports, and new `tests/.../Communication/` subfolder all stripped |
| `dotnet build` on the generated app | ✅ 0 errors, 4 warnings (unrelated transitive NuGet security warnings) |
| `dotnet test` on the generated app | ✅ 36/36 pass (the Communication-specific 17 tests removed, 36 core + Comments + NullObject tests remain) |

### Scenario B — Both modules installed (regression)

```
pwsh scripts/rename.ps1 -Name "_testBothModules" -OutputDir "." -Modules "All" -IncludeMobile:$false
```

| Step | Result |
|---|---|
| Rename script | ✅ no exclusions |
| `dotnet build` on the generated app | ✅ 0 errors |
| `dotnet test` on the generated app | ✅ 53/53 pass |

### Worktree regression

| Step | Result |
|---|---|
| BE `dotnet build` in worktree | ✅ 0 errors |
| BE `dotnet test` in worktree | ✅ 53/53 pass |
| FE `npm run build` in worktree | ✅ clean (standard large-chunk warning only) |

---

## Program-level status snapshot

- **Wave 0 (core)** — done. 6 features always ship: Files, Notifications, FeatureFlags, ApiKeys, AuditLogs, Reports.
- **Wave 1 (cross-domain engines)** — 2 of 4 merged to main (Comments ✅, Communication ✅). AI module is ~30 commits deep on [`feature/ai-integration`](../../../) (RAG ingestion + Qdrant + streaming chat); not merged. Workflow module not started.
- **Composability proof** — end-to-end verified by Scenario A above. Infrastructure is in place: `IModule`, Null-Object fallbacks with `TryAddScoped`, `scripts/modules.json` registry, `rename.ps1 -Modules` selection, integration events + capabilities instead of cross-module project references.
- **Test coverage** — both Wave 1 modules now have coverage. Standard going forward: a module does not merge without tests AND a `modules.json` entry.
- **Doc discipline** — every module should carry both `ROADMAP.md` (maintainer-facing, deferred-work contract) and `DEVELOPER_GUIDE.md` (integrator-facing, how-to-consume-capabilities). This audit closed the gap for Communication.

---

## Explicit deferrals (NOT in this PR)

- **Email-on-mention via Communication module** — user explicitly deferred. [`NotifyMentionedUsersOnCommentCreatedHandler`](../../../boilerplateBE/src/modules/Starter.Module.CommentsActivity/Application/EventHandlers/NotifyMentionedUsersOnCommentCreatedHandler.cs) is the extension point; Null-Object seam is verified to catch the "Communication absent" case silently.
- **Transactional outbox** on either DbContext — documented in both ROADMAPs; no at-least-once consumer yet.
- **Stub channel implementations** (SMS/Push/WhatsApp) — documented in Communication ROADMAP.
- **Ably real-time push for In-App** — waits on the planned Realtime module.
- **AI module merge** — separate branch, separate session.
- **Wave 2 modules** (Scheduling / Reporting / Payments / Search) — separate sessions.
- **Migrations** — never in boilerplate; each generated app creates its own.

---

## Suggested follow-ups

In priority order; raise as issues or pick up next session:

1. **AI module merge prep** — parity check on `modules.json` registration, `ROADMAP.md`, test coverage, and Null-Object contracts before it merges. Apply the norm established here.
2. **Comments event-publishing test round 2** — integration-level assertions that `CommentCreatedIntegrationEvent` reaches subscribers (today only unit-level publish assertions exist).
3. **Communication trigger-rule end-to-end tests** — mock channel provider, full `TriggerRuleEvaluator` path across event → rule match → recipient resolution → fallback chain. Skipped in this PR to keep the scope tight.
4. **Architecture purity test extension** — `AbstractionsPurityTests` already reflection-checks that Abstractions gains no project references; extend to check that no module project references another module project (only `Starter.Abstractions*`). Would have caught the empty-dependencies violation early if it ever regresses.
5. **Wave 1 closer** — decide whether Workflow module happens before or after AI, and whether AI lands first to unlock automation features.

---

## References

- Original modules specs: [Comments](../../../docs/superpowers/specs/2026-04-13-comments-activity-module-design.md), [Communication](../../../docs/superpowers/specs/2026-04-13-multi-channel-communication-design.md).
- Composable catalog spec: [`2026-04-09-composable-module-catalog-design.md`](../../../docs/superpowers/specs/2026-04-09-composable-module-catalog-design.md).
- Architecture docs: [system design](../../../docs/architecture/system-design.md), [module development guide](../../../docs/architecture/module-development-guide.md), [cross-module communication](../../../docs/architecture/cross-module-communication.md).
- Roadmap: [`docs/future-roadmap.md`](../../../docs/future-roadmap.md).
