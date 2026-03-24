# Regression Test Session 2 — 2026-03-24

**Tester**: Automated (Playwright MCP)
**App**: TestApp (created from rename script)
**Backend**: http://localhost:5050
**Frontend**: http://localhost:4000
**Database**: Fresh `testappdb` (PostgreSQL)
**Services**: Mailpit (8025), MinIO (9000), Redis (6379)

---

## Changes Since Last Session

- **System Settings redesign**: 31 settings across 6 categories, tab-based UI, DataType metadata, ISettingsProvider for runtime config, tenant override support
- **Tenant Branding Suite**: 13 new fields on Tenant entity (logo, favicon, colors, business info, localized custom text), 4-tab detail page, dynamic theme injection, sidebar logo
- **Code quality**: Boolean toggle visibility fix, i18n label mapping fixes

---

## Test Results

| # | Test | Result | Notes |
|---|------|--------|-------|
| 1.1 | Landing page | PASS | "Welcome to TestApp" |
| 2.2 | SuperAdmin login | PASS | Dashboard with stats |
| 7.1 | All pages accessible (7 pages) | PASS | Users, Roles, Files, Reports, Settings, AuditLogs, Profile |
| 11.1 | Tenants list | PASS | Default Organization |
| 11.3 | Tenant detail with tabs | PASS | Overview, Branding, Business Info, Custom Text |
| 11.4 | Overview tab | PASS | Name, slug, status, Suspend/Deactivate buttons |
| 11.5 | Branding tab: logo upload area | PASS | Drag & drop (max 5MB) |
| 11.6 | Branding tab: color pickers | PASS | Hex input + native color picker for primary/secondary |
| 11.7 | Branding tab: description field | PASS | Textarea present |
| 11.8 | Business Info tab: save | PASS | Address, phone, website, taxId saved and persisted |
| 11.9 | Custom Text tab: language tabs | PASS | EN, AR, KU sub-tabs |
| 11.10 | Custom Text tab: preview | PASS | Shows tenant name + custom text |
| 4.2 | Tenant registration | PASS | Creates org + user, sends verification email |
| 5.2 | Email verification | PASS | OTP verified, redirected to login |
| 20.1 | Tenant isolation | PASS | 1 user visible, Audit Logs hidden (no permission) |
| 21.1 | Logout | PASS | Clears state, redirects to login |

---

## Settings Feature Verification

Verified in earlier session (same day):
- 31 settings across 6 categories (Application, Email, SMS, Notifications, Security, Reports)
- Tab-based UI with proper input types (text, number, boolean toggle, password, email, url)
- Boolean toggles now visible with Enabled/Disabled labels
- Save with unsaved changes badge
- Tenant override: Jane changed Currency to IQD, SuperAdmin still sees USD
- Settings persist after refresh

---

## Tenant Branding Feature Verification

| Component | Status |
|-----------|--------|
| Branding tab: logo upload area | PASS |
| Branding tab: favicon upload area | PASS (max 2MB) |
| Branding tab: primary color picker | PASS (hex + native picker) |
| Branding tab: secondary color picker | PASS |
| Branding tab: description textarea | PASS |
| Business Info: address textarea | PASS |
| Business Info: phone input | PASS |
| Business Info: website url input | PASS (with https:// placeholder) |
| Business Info: tax ID input | PASS |
| Business Info: save + persistence | PASS (verified after refresh) |
| Custom Text: EN/AR/KU language tabs | PASS |
| Custom Text: login title field | PASS |
| Custom Text: login subtitle field | PASS |
| Custom Text: email footer field | PASS |
| Custom Text: live preview section | PASS |
| Public branding endpoint | Available (AllowAnonymous) |

---

## Bugs Found

**None.** All 16 tests passed.

---

## Summary

- **Total tests executed**: 16
- **Passed**: 16 (100%)
- **Bugs found**: 0
- **New features tested**: Tenant branding (4 tabs, 13 fields), system settings (31 settings, 6 categories)
- **Build verification**: BE 0 errors, FE 0 errors
