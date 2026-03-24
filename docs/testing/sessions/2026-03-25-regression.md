# Regression Test Session — 2026-03-25

**Tester**: Automated (Playwright MCP + curl API tests)
**App**: TestApp (fresh from rename script)
**Backend**: http://localhost:5050 | **Frontend**: http://localhost:4000
**Database**: Fresh `testappdb` (PostgreSQL)
**Services**: Mailpit (8025), MinIO (9000), Redis (6379)

---

## Context

Full regression after 4 rounds of code review fixes. Includes deep testing of the file lifecycle system (3-origin model: UserUpload, SystemGenerated, ProcessUpload).

---

## Core Regression Results

| # | Test | Result |
|---|------|--------|
| 1 | Landing page | PASS |
| 2 | SuperAdmin login | PASS |
| 3 | Dashboard | PASS |
| 4 | Users page | PASS |
| 5 | Roles page | PASS |
| 6 | Tenants page | PASS |
| 7 | Files page | PASS |
| 8 | Reports page | PASS |
| 9 | Audit Logs page | PASS |
| 10 | Settings page | PASS |
| 11 | Profile page | PASS |
| 12 | Tenant registration (Acme Corp) | PASS |
| 13 | Email verification (OTP from Mailpit) | PASS |
| 14 | Tenant user login | PASS |
| 15 | Tenant isolation (1 user visible) | PASS |

---

## File System Deep Test Results

| # | Test | Result | Details |
|---|------|--------|---------|
| F1 | Upload permanent file (Files page) | PASS | Status=Permanent, Origin=UserUpload |
| F2 | Upload temp file (upload-temp endpoint) | PASS | Status=Temporary, Origin=ProcessUpload, ExpiresAt set |
| F3 | Files list excludes temp files | PASS | Only 1 file shown (permanent), temp not visible |
| F4 | Export CSV from Audit Logs | PASS | Report created, status=Pending→Completed |
| F5 | System files origin filter | PASS | 1 file: AuditLogs_Report.csv, Origin=SystemGenerated, Category=Report |
| F6 | Download report (signed URL) | PASS | HTTP protocol, valid MinIO URL |
| F7 | Delete report | PASS | Report removed, 0 remaining |

---

## Bugs Found

**None.** All 22 tests passed.

---

## Summary

- **Total tests**: 22 (15 core + 7 file deep tests)
- **Passed**: 22 (100%)
- **Bugs found**: 0
- **File lifecycle verified**: 3-origin model working (UserUpload, SystemGenerated, ProcessUpload)
- **Tenant isolation verified**: Tenant user sees only own data
- **Report generation verified**: Async MassTransit consumer creates system file with correct category
