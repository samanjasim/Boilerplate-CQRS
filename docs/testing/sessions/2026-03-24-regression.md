# Regression Test Session — 2026-03-24

**Tester**: Automated (Playwright MCP)
**App**: TestApp (created from rename script)
**Backend**: http://localhost:5050
**Frontend**: http://localhost:4000
**Database**: Fresh `testappdb` (PostgreSQL)
**Services**: Mailpit (8025), MinIO (9000), Redis (6379)

---

## Changes Since Last Session (2026-03-23)

- **Async Report System**: Replaced synchronous export with fire-and-forget async report generation via MassTransit consumer. Reports saved to S3, notifications push via Ably (polling fallback).
- **Code Review Fixes (15 issues)**: Tenant auth checks, ForceExport permission gate, DRY storage key extraction, IMessagePublisher rename, missing validators, onError handlers, auto-polling race condition fix, download URL handling.
- **Code Quality Cleanup**: Hardcoded i18n strings fixed, missing onError mutation handlers added.
- **New permission**: `System.ForceExport` (SuperAdmin only — bypass report cache)

---

## Test Results

| # | Test | Result | Notes |
|---|------|--------|-------|
| 1.1 | Landing page (unauthenticated) | PASS | "Welcome to TestApp", Get Started + Sign In |
| 2.1 | Login page UI | PASS | Email, password, forgot password, create account |
| 2.2 | SuperAdmin login | PASS | Redirect to dashboard, welcome toast |
| 7.1 | Dashboard stats | PASS | 1 user, 3 roles, platform active |
| 7.2 | Recent activity shows entity labels | PASS | "User Updated", "Permission Created" (not integers) |
| 8.1 | Users list | PASS | Table + Export + Invite User buttons |
| 10.1 | Roles list | PASS | Admin: 16 perms, SuperAdmin: 26 perms (includes ForceExport) |
| 11.1 | Tenants list | PASS | Default Organization |
| 13.1 | Files page | PASS | Grid view, Export + Upload buttons |
| 13.4 | File category labels | PASS | Shows "Document" not integer |
| 14.1 | Profile page | PASS | All sections: info, password, prefs, 2FA, sessions, history |
| 17.1 | Reports nav in sidebar | PASS | Between Files and Audit Logs |
| 17.2 | Reports empty state | PASS | "No reports yet" with description |
| 17.3 | Export PDF from Audit Logs | PASS | Toast with "View Reports" action link |
| 17.7 | Report completed | PASS | Status: Completed, Download + Delete buttons |
| 17.8 | Download report | PASS | Signed MinIO URL opens, PDF downloads |
| 4.2 | Tenant registration | PASS | Creates org + user, sends verification email |
| 5.2 | Email verification | PASS | OTP from Mailpit, verified, redirected to login |
| 20.1 | Tenant user isolation — users | PASS | Only own tenant's user visible |
| 20.2 | Tenant user isolation — sidebar | PASS | No Audit Logs (lacks permission), Reports visible |
| 7.6 | Dashboard no 403 errors | PASS | Audit logs query gated by permission |
| 19.1 | Dark mode toggle | PASS | HTML class="dark" |
| 21.1 | Logout | PASS | Clears state, redirects to login |

---

## Async Report Pipeline Verification

Full end-to-end test of the new async report system:

```
1. SuperAdmin clicks "Export PDF" on Audit Logs page
2. POST /api/v1/Reports → returns immediately with report ID
3. Toast: "Report requested" with "View Reports" action link
4. MassTransit consumer picks up GenerateReportMessage
5. Consumer: loads audit log data → generates PDF via QuestPDF → uploads to MinIO
6. Consumer: updates ReportRequest status → Completed
7. Consumer: creates notification for user
8. Navigate to /reports → shows row: "Audit Logs | PDF | Completed"
9. Click Download → signed S3 URL opens in new tab → PDF file downloads
10. Generated PDF also visible in Files page as "AuditLogs_Report_*.pdf" (45.8 KB)
```

All 10 steps completed successfully. The async pipeline (HTTP → DB → MassTransit → Consumer → S3 → Notification) works end-to-end.

---

## Bugs Found

**None.** All 23 tests passed with zero issues. Previous session's 6 bugs have been verified fixed:
- Bug #1 (audit log enum integers) — FIXED: shows "Permission", "Created"
- Bug #2 (file category integers) — FIXED: shows "Document"
- Bug #3 (uploaded by shows "-") — FIXED: file uploader name resolved
- Bug #4 (dashboard 403 for tenant users) — FIXED: query gated by permission
- Bug #6 (React key warning) — FIXED: no console warnings

---

## Summary

- **Total tests executed**: 23
- **Passed**: 23 (100%)
- **Bugs found**: 0
- **Previous bugs verified fixed**: 5
- **New features tested**: Async reports (full pipeline), Reports page, Export button, ForceExport permission
- **Build verification**: BE 0 errors, FE 0 errors
