# QA Test Session — Plan 4b-8 (Per-document ACLs + unified file/access)

**Date:** 2026-04-22
**Feature branch:** `feature/ai-integration`
**Test app name:** `_testAcl`
**Ports:** BE `5102`, FE `3102` (5100/3100 occupied by stale `_testWfPh` zombie)
**Credentials:** `superadmin@testacl.com` / `Admin@123456` (underscore stripped per trap #2)

---

## Scenarios

Every scenario is a **live Playwright MCP** execution. Each has: preconditions, steps, observations (UI + network + console), pass/fail criteria.

### Phase 1 — Core ACL UX flows (§5.9 of design spec)

| # | Flow | Expected outcome |
|---|---|---|
| 1.1 | **Owner shares file with user** — Upload file as superadmin, open row menu → Share → pick user → Viewer → grant | Toast "Access granted"; grant row appears in dialog; audit log has `ResourceGrantCreated` |
| 1.2 | **Owner shares file with role** — Share dialog → Roles tab → pick role → Editor → grant | Toast; grant row with role chip; no notification sent (role grants don't notify per Task 19 test) |
| 1.3 | **Grantee discovers shared file** — Log out, log in as grantee → Files → "Shared with Me" tab | File visible; NotificationBell has unread badge |
| 1.4 | **Visibility → Public (admin)** — Share dialog → click Public button → confirm dialog "I understand" → confirm | Toast "Visibility updated"; badge on row becomes green Globe; audit writes 2 rows (Changed + MadePublic) |
| 1.5 | **Copy public link** — Share dialog (now Public) → "Copy link" button | Copy icon changes to check; clipboard has `/files/{id}` URL |
| 1.6 | **Revoke grant** — Share dialog → Remove button on grant row → confirm dialog | Toast "Access revoked"; grant disappears; audit has `ResourceGrantRevoked` |
| 1.7 | **Transfer ownership** — Row menu → Transfer ownership → pick new user → Transfer | Toast "Ownership transferred"; audit has `ResourceOwnershipTransferred` |
| 1.8 | **Storage summary panel** — Header "Storage" button → dialog | Total bytes formatted; byCategory bars render; topUploaders list shows avatars; allTenants checkbox visible (superadmin is platform admin) |

### Phase 2 — Visibility permutations + permission guards

| # | Flow | Expected outcome |
|---|---|---|
| 2.1 | Non-admin user tries Public — login as tenant user without `Files.Manage` | Public button in share dialog is disabled |
| 2.2 | Private visibility default — new upload | VisibilityBadge shows Lock/"Private" |
| 2.3 | Tenant visibility — change via share dialog | VisibilityBadge shows Building/"Tenant" |
| 2.4 | View tabs filter correctly — switch All → Mine → Shared → Public | List refreshes; URL or state reflects the view; counts change accordingly |
| 2.5 | Row-action visibility per permission — owner sees Share+Transfer+Download+Delete; non-owner sees only Download (+Share if ShareOwn) | Dropdown menu items shown/hidden correctly |

### Phase 3 — Notifications + audit logs

| # | Flow | Expected outcome |
|---|---|---|
| 3.1 | NotificationBell — grant creates a `ResourceShared` notification | Share icon (Share2), badge increments, clicking navigates to `/files?view=shared` |
| 3.2 | NotificationsPage — full list | ResourceShared row has icon + message |
| 3.3 | Audit logs page — filter by `ResourceGrant` | Rows for `ResourceGrantCreated`, `ResourceGrantRevoked` visible |
| 3.4 | Audit logs — `ResourceVisibilityChanged` + `ResourceVisibilityMadePublic` pair | Both rows present for the Public change from 1.4 |

### Phase 4 — i18n + RTL + UI/UX polish

| # | Flow | Expected outcome |
|---|---|---|
| 4.1 | Switch language → Arabic | Header/buttons/tabs translate; share dialog uses Arabic labels (`مشاركة`, `الرؤية`) |
| 4.2 | RTL layout — share dialog in Arabic | Icons/buttons mirror; text right-aligned; copy-link icon position flips |
| 4.3 | VisibilityBadge — all three variants rendered | Lock+outline for Private, Building+secondary for Tenant, Globe+primary for Public |
| 4.4 | SubjectPicker — search filters users/roles | Typing narrows list; tabs switch between Users/Roles |
| 4.5 | SubjectStack — grant row with ≤3 entries vs >3 | Overflow text "+N more" appears only when overflowed |

### Phase 5 — AI module assistant ACL + cross-tenant

| # | Flow | Expected outcome |
|---|---|---|
| 5.1 | Create AI assistant — existing create flow | Assistant saved with `CreatedByUserId` = current user, `Visibility=Private`, `AccessMode=CallerPrincipal` |
| 5.2 | Try set assistant visibility to Public via API | 400 `Access.VisibilityNotAllowedForResourceType` (max is TenantWide) |
| 5.3 | Assistant visibility → TenantWide | Succeeds when caller is owner |
| 5.4 | SetAssistantAccessMode by non-owner | 403/404 failure — blocked by Manager grant check |
| 5.5 | Assistant chat with acl-resolve stage | Response returns; if Qdrant unavailable, degraded mode works (no crash) |
| 5.6 | Cross-tenant — tenant B user tries to access tenant A's assistant | 404 (not "forbidden" — masks existence) |

### Phase 6 — Regression (must not break)

| # | Flow | Expected outcome |
|---|---|---|
| 6.1 | Files — upload + download + delete + edit metadata | Works unchanged |
| 6.2 | Users — create + activate + suspend + delete | Works; DeleteRole doesn't break unrelated user ops |
| 6.3 | Roles — create + update perms + delete | Delete cascades role grants (new behavior from Task 15) without breaking |
| 6.4 | Tenants — list + detail | Works |
| 6.5 | Navigation — sidebar links across all modules | Routes load without errors |
| 6.6 | Settings — edit | Works |
| 6.7 | AI module — list assistants + documents + upload document | Works; DeleteDocument cascades grants (Task 15) |

---

## Execution order

1. Phase 1 (happy path, sanity checks)
2. Phase 6 (regression — catch breakage early)
3. Phase 2 (permutations)
4. Phase 3 (async artifacts: audit + notifications)
5. Phase 5 (AI-specific)
6. Phase 4 (polish)

## Reporting format

Each phase produces a short log:

```
=== Phase N: Name ===
[PASS] N.1 — observation
[FAIL] N.2 — observation + screenshot ref
[SKIP] N.3 — reason
```

All findings are fixed in **worktree source**, not the test copy. Re-run rename if fixes are needed.
