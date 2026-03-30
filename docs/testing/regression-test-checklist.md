# Regression Test Checklist

This checklist covers every user-facing feature in the boilerplate. Run through it before each release or after significant changes. Update this file when new features are added.

---

## How to Use

1. Create a fresh test app: `.\scripts\rename.ps1 -Name "TestApp"`
2. Create a fresh database
3. Run migration: `dotnet ef migrations add InitialCreate --project src/TestApp.Infrastructure --startup-project src/TestApp.Api`
4. Start backend: `dotnet run --project src/TestApp.Api`
5. Start frontend: `cd TestApp-FE && npm install && npm run dev`
6. Ensure Mailpit is running on port 8025 (for email verification)
7. Ensure MinIO is running on port 9000 (for file uploads)
8. Walk through each section below, marking pass/fail

---

## 1. Landing Page

| # | Test Case | Expected |
|---|-----------|----------|
| 1.1 | Navigate to `/` as unauthenticated user | Landing page with "Get Started" and "Sign In" links |
| 1.2 | Click "Sign In" | Navigates to `/login` |
| 1.3 | Click "Get Started" | Navigates to `/register-tenant` |
| 1.4 | Navigate to `/` as authenticated user | Redirects to `/dashboard` |

## 2. Authentication — Login

| # | Test Case | Expected |
|---|-----------|----------|
| 2.1 | Login page shows email + password fields | Form renders with placeholders |
| 2.2 | Login with valid SuperAdmin credentials | Redirect to `/dashboard`, welcome toast |
| 2.3 | Login with invalid credentials | Error toast, stays on login page |
| 2.4 | Login with unverified email | Error: account not active |
| 2.5 | "Forgot Password" link | Navigates to `/forgot-password` |
| 2.6 | "Create one" link | Navigates to `/register` |
| 2.7 | Login with 2FA enabled account | Shows 2FA code input after credentials |

## 3. Authentication — Register (Individual)

| # | Test Case | Expected |
|---|-----------|----------|
| 3.1 | Register page shows all fields | First name, last name, username, email, password, confirm |
| 3.2 | Submit with valid data | Toast "Account created", redirects to `/login` or `/verify-email` |
| 3.3 | Submit with existing email | Error: email already exists |
| 3.4 | Submit with weak password | Validation error (min 8 chars, uppercase, lowercase, digit, special) |
| 3.5 | Submit with mismatched passwords | Validation error: passwords don't match |

## 4. Authentication — Tenant Registration (Get Started)

| # | Test Case | Expected |
|---|-----------|----------|
| 4.1 | Form shows company name + personal fields | All fields render |
| 4.2 | Submit with valid data | Creates tenant + user, sends verification email, redirects to `/verify-email` |
| 4.3 | Check Mailpit for verification email | Email arrives with 6-digit OTP |
| 4.4 | Submit with existing email | Error: email already exists |

## 5. Authentication — Email Verification

| # | Test Case | Expected |
|---|-----------|----------|
| 5.1 | Verify email page shows code input | 6-digit code field with "Verify Email" button |
| 5.2 | Enter valid OTP from Mailpit | Toast "Email verified", redirects to `/login` |
| 5.3 | Enter invalid OTP | Error: invalid or expired code |
| 5.4 | Click "Resend Code" | New email sent, 60-second cooldown starts |
| 5.5 | "Back to Login" link works | Navigates to `/login` |

## 6. Authentication — Forgot / Reset Password

| # | Test Case | Expected |
|---|-----------|----------|
| 6.1 | Forgot password page shows email field | "Send Reset Code" button |
| 6.2 | Submit valid email | Toast "If email exists, reset code sent", advances to step 2 |
| 6.3 | Submit non-existent email | Same success message (prevents user enumeration) |
| 6.4 | Step 2: Enter valid OTP + new password | Toast "Password reset successfully", redirects to `/login` |
| 6.5 | Step 2: Enter expired/invalid OTP | Error: invalid or expired code |
| 6.6 | Login with new password | Success |

## 7. Dashboard

| # | Test Case | Expected |
|---|-----------|----------|
| 7.1 | SuperAdmin sees full stats | Total Users, Active Roles, Total Roles, Platform Status |
| 7.2 | Recent Activity section shows audit entries | Latest changes with timestamps |
| 7.3 | Recent Users section shows user list | Clickable links to user detail |
| 7.4 | Quick Overview cards link to correct pages | Users, Roles, System Settings |
| 7.5 | Tenant user sees only their own data | No SuperAdmin or other tenant data |
| 7.6 | Tenant user without audit permission | No 403 errors in console |

## 8. Users Management

| # | Test Case | Expected |
|---|-----------|----------|
| 8.1 | Users list shows table with name, email, roles, created | Correct data |
| 8.2 | Click user name → detail page | Shows user info, roles, permissions |
| 8.3 | User detail: Edit User button | Opens edit modal, saves changes |
| 8.4 | User detail: Assign Role | Opens role picker, assigns role |
| 8.5 | User detail: Remove Role | Confirmation dialog, removes role |
| 8.6 | User detail: Suspend/Deactivate/Activate | Status changes correctly |
| 8.7 | Tenant user only sees own tenant's users | SuperAdmin and other tenants hidden |

## 9. Invite User

| # | Test Case | Expected |
|---|-----------|----------|
| 9.1 | "Invite User" button opens dialog | Email + role dropdown |
| 9.2 | Send invitation with valid data | Toast "Invitation sent", pending invitations table appears |
| 9.3 | Check Mailpit for invitation email | Email with invitation link |
| 9.4 | Revoke pending invitation | Invitation removed from table |
| 9.5 | Accept invitation via link | Registration form, creates user in tenant |

## 10. Roles & Permissions

| # | Test Case | Expected |
|---|-----------|----------|
| 10.1 | Roles list shows all roles as cards | Name, description, user count, permission count |
| 10.2 | Click role card → detail page | Shows role info with grouped permissions |
| 10.3 | System roles show "System" badge | Cannot edit or delete system roles |
| 10.4 | "Create Role" → 2-step form | Step 1: name/description, Step 2: permission matrix |
| 10.5 | Permission matrix: select/deselect individual | Checkbox toggles |
| 10.6 | Permission matrix: select/deselect module | All permissions in module toggle |
| 10.7 | Edit role: update name + permissions | Saves both changes |
| 10.8 | Delete non-system role | Confirmation dialog, deletes role |
| 10.9 | Delete role with assigned users | Error: role in use |

## 11. Tenants

| # | Test Case | Expected |
|---|-----------|----------|
| 11.1 | SuperAdmin sees all tenants | List with name, slug, status, created |
| 11.2 | Tenant user sees only their own tenant | Single row in list |
| 11.3 | Click tenant → detail page with tabs | Overview, Branding, Business Info, Custom Text tabs |
| 11.4 | Overview tab: status management | Suspend, Deactivate, Activate buttons work |
| 11.5 | Branding tab: upload logo | Logo preview shown, saved |
| 11.6 | Branding tab: set primary color | Color picker + hex input, swatch preview |
| 11.7 | Branding tab: set description | Textarea, saves |
| 11.8 | Business Info tab: save address/phone/website/taxId | All fields save correctly |
| 11.9 | Custom Text tab: language sub-tabs (en/ar/ku) | Per-language input fields |
| 11.10 | Custom Text tab: save login page title | JSON stored, login page shows custom title |
| 11.11 | Sidebar shows tenant logo after branding update | Logo replaces app icon |
| 11.12 | Primary color applies to theme | CSS variables updated dynamically |
| 11.13 | Logout resets branding to platform default | App icon + default colors restored |
| 11.14 | Public branding endpoint (anonymous) | GET /Tenants/branding?slug=xxx returns branding |

## 12. Audit Logs

| # | Test Case | Expected |
|---|-----------|----------|
| 12.1 | Page shows audit entries table | Entity type, action, performed by, date, IP |
| 12.2 | Entity Type filter works | Filters by User, Role, etc. |
| 12.3 | Action filter works | Filters by Created, Updated, Deleted |
| 12.4 | Search filter works | Searches by text |
| 12.5 | Click expandable row | Shows old/new values JSON diff |
| 12.6 | Pagination works | Next/Previous, showing X of Y |
| 12.7 | Entity types show as labels not numbers | "User" not "3" |
| 12.8 | Actions show as labels not numbers | "Created" not "1" |

## 13. File Management

| # | Test Case | Expected |
|---|-----------|----------|
| 13.1 | Files page shows empty state when no files | "No files found" message |
| 13.2 | Upload file via dialog | Select file, set category/description/tags, upload |
| 13.3 | File appears in grid after upload | Thumbnail or icon, name, size, category label |
| 13.4 | Category shows as label not number | "Document" not "3" |
| 13.5 | Switch between grid and list view | Both views render correctly |
| 13.6 | Click file → detail dialog | Shows metadata, download/copy URL/edit/delete buttons |
| 13.7 | Copy URL | Toast "URL copied", valid URL in clipboard |
| 13.8 | Download | Opens file in new tab or downloads |
| 13.9 | Edit file metadata | Update description/category/tags |
| 13.10 | Delete file | Confirmation, file removed |
| 13.11 | Category filter works | Filters file list |
| 13.12 | Search filter works | Filters by filename |
| 13.13 | Uploaded By shows user name | Not "-" or raw GUID |
| 13.14 | Tenant isolation | Users only see their own tenant's files |

## 14. Profile Page

| # | Test Case | Expected |
|---|-----------|----------|
| 14.1 | Access via user dropdown → "My Profile" | Profile page renders |
| 14.2 | User info card shows name, email, username, roles | Correct data |
| 14.3 | Edit Profile button → modal | Update name, email, phone |
| 14.4 | Change Password form | Current + new + confirm fields, saves |
| 14.5 | Notification preferences toggle | Email and in-app toggles for each type |
| 14.6 | Save notification preferences | Toast confirmation |

## 15. Two-Factor Authentication (2FA)

| # | Test Case | Expected |
|---|-----------|----------|
| 15.1 | 2FA section shows "Disabled" status | "Enable 2FA" button |
| 15.2 | Click Enable 2FA | QR code + manual key displayed |
| 15.3 | Enter valid TOTP code | 2FA enabled, backup codes shown |
| 15.4 | Backup codes displayed (copy button) | Can copy all codes |
| 15.5 | Logout and login with 2FA | Prompted for TOTP code after credentials |
| 15.6 | Login with backup code | Succeeds, backup code consumed |
| 15.7 | Disable 2FA from profile | 2FA removed |

## 16. Sessions & Login History

| # | Test Case | Expected |
|---|-----------|----------|
| 16.1 | Active Sessions shows current session | Marked as "Current Session" |
| 16.2 | Other sessions show "Revoke" button | Current session has no revoke button |
| 16.3 | Revoke a session | Session removed from list |
| 16.4 | "Revoke All Others" button | All non-current sessions removed |
| 16.5 | Login History table | Date, IP, device, status, reason columns |
| 16.6 | Failed login shows "Failed" status | With failure reason |

## 17. Reports (Async Export)

| # | Test Case | Expected |
|---|-----------|----------|
| 17.1 | Reports nav in sidebar | Visible for users with System.ExportData permission |
| 17.2 | Reports page shows empty state | "No reports yet" with description |
| 17.3 | Export CSV from Audit Logs page | Toast "Report requested" with "View Reports" link |
| 17.4 | Export PDF from Users page | Toast "Report requested" with "View Reports" link |
| 17.5 | Export from Files page | Toast "Report requested" with "View Reports" link |
| 17.6 | Reports page shows Pending status | Yellow badge with spinner |
| 17.7 | Report transitions to Completed | Green badge, download button appears |
| 17.8 | Download completed report | Signed URL opens, file downloads |
| 17.9 | Delete report | Confirmation dialog, report removed |
| 17.10 | Retry failed report | Creates new report with forceRefresh=true |
| 17.11 | Cache dedup — same report twice | Returns existing report, no new generation |
| 17.12 | Force Refresh (SuperAdmin) | Generates fresh report even if cached |
| 17.13 | Auto-polling when pending reports | List refreshes every 5 seconds |
| 17.14 | Auto-polling stops when all done | No more polling after Completed/Failed |
| 17.15 | Report type filter works | Filters by AuditLogs, Users, Files |
| 17.16 | Status filter works | Filters by Pending, Processing, Completed, Failed |
| 17.17 | Tenant isolation | Tenant user only sees own reports |
| 17.18 | Notification on report completion | Bell icon updates, toast if Ably enabled |

## 18. System Settings

| # | Test Case | Expected |
|---|-----------|----------|
| 18.1 | Navigate to `/settings` as admin | Settings page with category tabs |
| 18.2 | Click each category tab | Shows settings for that category |
| 18.3 | Update a text setting value | Value saved, success toast |
| 18.4 | Toggle a boolean setting | Toggle state changes, persists on reload |
| 18.5 | Update a secret/password field | Field masked, value saved |
| 18.6 | View settings as non-admin (no ManageSettings permission) | Access denied, redirect |
| 18.7 | Reload page after changes | All values persist correctly |
| 18.8 | Save button disabled when no changes made | Button only active after edits |

## 19. API Key Management

| # | Test Case | Expected |
|---|-----------|----------|
| 19.1 | Navigate to `/api-keys` as admin | API keys page with tenant/platform tabs |
| 19.2 | Create a tenant API key with name and expiration | Key created, secret shown once |
| 19.3 | Copy secret to clipboard | Clipboard contains the secret |
| 19.4 | Close creation dialog and reopen list | New key appears, secret no longer visible |
| 19.5 | Revoke an API key | Key status changes to Revoked |
| 19.6 | Try to use revoked key via API | 401 Unauthorized |
| 19.7 | Create platform API key (as SuperAdmin) | Key created with isPlatformKey flag |
| 19.8 | Emergency revoke a tenant key (as SuperAdmin) | Key revoked across tenants |
| 19.9 | View expired key | Shows expired status badge |
| 19.10 | Update key name | Name updated, success toast |
| 19.11 | Non-admin cannot access API keys page | Access denied, redirect |
| 19.12 | Keys from one tenant not visible to another | Tenant isolation verified |

## 20. Feature Flags

| # | Test Case | Expected |
|---|-----------|----------|
| 20.1 | Navigate to `/feature-flags` as SuperAdmin | Feature flags list with all seeded flags |
| 20.2 | Search flags by name | Filtered list updates |
| 20.3 | Filter flags by category | Only matching category shown |
| 20.4 | Create a new feature flag | Flag appears in list with default value |
| 20.5 | Update flag name and description | Changes saved, success toast |
| 20.6 | Update flag default value | New default applies to tenants without overrides |
| 20.7 | Delete a non-system flag | Flag removed from list |
| 20.8 | Try to delete a system flag | Error: system flags cannot be deleted |
| 20.9 | Set tenant override value on a flag | Override saved, resolved value changes for that tenant |
| 20.10 | Remove tenant override | Tenant falls back to default value |
| 20.11 | Opt out of a non-system boolean flag | Opt-out recorded for current tenant |
| 20.12 | Remove opt-out | Flag re-enabled for tenant |
| 20.13 | Enforcement: set users.max_count to 1, try creating a second user | Error: user limit reached |
| 20.14 | Enforcement: disable api_keys.enabled, try creating API key | Error: feature disabled |
| 20.15 | Non-admin cannot access feature flags page | Access denied, redirect to dashboard |
| 20.16 | Tenant admin with ManageTenantOverrides sees only their tenant's overrides | Cross-tenant data not visible |

## 21. Notifications

| # | Test Case | Expected |
|---|-----------|----------|
| 21.1 | Bell icon in header | Shows unread count badge |
| 21.2 | Click bell → dropdown | Shows recent notifications |
| 21.3 | Click notification → marks as read | Unread count decrements |
| 21.4 | "View All" link → notifications page | Full paginated list |
| 21.5 | Mark all as read | All notifications marked read |
| 21.6 | Polling fallback (no Ably) | Unread count refreshes every 30s |

## 22. Theme & Language

| # | Test Case | Expected |
|---|-----------|----------|
| 22.1 | Toggle dark mode | HTML gets `class="dark"`, UI colors change |
| 22.2 | Toggle back to light mode | `class="light"`, UI reverts |
| 22.3 | Switch to Arabic | All text in Arabic, `dir="rtl"`, `lang="ar"` |
| 22.4 | Switch to Kurdish | All text in Kurdish, RTL layout |
| 22.5 | Switch back to English | All text in English, `dir="ltr"` |
| 22.6 | Theme persists on page reload | Theme stays after F5 |
| 22.7 | Language persists on page reload | Language stays after F5 |

## 23. Tenant Data Isolation

| # | Test Case | Expected |
|---|-----------|----------|
| 23.1 | Tenant user cannot see SuperAdmin | Not in users list |
| 23.2 | Tenant user cannot see other tenants' users | Only own tenant's users |
| 23.3 | Tenant user sees only own tenant in tenants list | Single row |
| 23.4 | Tenant user files isolated | Only own tenant's files |
| 23.5 | Tenant user audit logs isolated | Only own tenant's logs |
| 23.6 | Tenant user reports isolated | Only own tenant's reports |
| 23.7 | SuperAdmin sees all data across tenants | Full access |

## 24. Logout

| # | Test Case | Expected |
|---|-----------|----------|
| 24.1 | Click user dropdown → Logout | Clears tokens, redirects to `/login` |
| 24.2 | After logout, cannot access protected routes | Redirects to `/login` |
| 24.3 | After logout, API calls return 401 | No stale data |

---

## Notes

- **Mailpit** must be running at `http://localhost:8025` for email-related tests
- **MinIO** must be running at `http://localhost:9000` with bucket created for file tests
- **SuperAdmin credentials**: `superadmin@{appname}.com` / `Admin@123456`
- Update this checklist when adding new features to the boilerplate
- Jaeger should be running on port 16686 for tracing verification
