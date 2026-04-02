# Tenant Self-Service Portal — Manual Test Session

**Date**: 2026-04-02  
**Test App**: TestOrg  
**Test Instance**: http://localhost:3100 (FE) / http://localhost:5100 (BE)

---

## Credentials

| User | Email | Password | Role | Notes |
|------|-------|----------|------|-------|
| SuperAdmin | `superadmin@testorg.com` | `Admin@123456` | SuperAdmin | Platform admin, no tenantId |

**Note**: To test tenant self-service features, you need to create a tenant admin via the registration flow (Test 2) or by inviting a user (Test 6).

---

## URLs

| Service | URL |
|---------|-----|
| Frontend | http://localhost:3100 |
| Backend API | http://localhost:5100 |
| Swagger | http://localhost:5100/swagger |

**Docker services needed** (start Docker Desktop first):
- Mailpit: `docker run -d --name starter-mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit`
- Redis: `docker run -d --name Bookify.Redis -p 6379:6379 redis:7-alpine` (or existing)
- MinIO (for file uploads): `docker run -d --name starter-minio -p 9000:9000 -p 9001:9001 -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin minio/minio server /data --console-address ":9001"`

After MinIO starts, create bucket:
```python
python -c "
import boto3
s3 = boto3.client('s3', endpoint_url='http://127.0.0.1:9000', aws_access_key_id='minioadmin', aws_secret_access_key='minioadmin', region_name='us-east-1')
s3.create_bucket(Bucket='testorg-files')
print('Bucket created')
"
```

---

## Test 1: SuperAdmin — Platform Admin Experience

**Login as**: `superadmin@testorg.com` / `Admin@123456`

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 1.1 | Navigate to http://localhost:3100/login | Login page with copper theme | |
| 1.2 | Login with SuperAdmin credentials | Redirect to /dashboard | |
| 1.3 | Check sidebar | Should show: Dashboard, Users, Roles, **Tenants** (NOT Organization), Files, Reports, Audit Logs, API Keys, Feature Flags, Billing Plans, Subscriptions, Settings | |
| 1.4 | Click "Tenants" in sidebar | Tenants list page with "Default Organization" | |
| 1.5 | Click "Default Organization" | Tenant detail page with tabs: Overview, Branding, Business Info, Custom Text, Activity, Feature Flags, **Subscription** | |
| 1.6 | Click "Activity" tab | Shows audit log entries (seeded data) | |
| 1.7 | Click "Subscription" tab | Shows subscription management (may show empty if no subscription assigned) | |
| 1.8 | Click "Audit Logs" in sidebar | Full audit logs page with filters | |
| 1.9 | Navigate to http://localhost:3100/organization | Should redirect to /dashboard (platform admin has no tenantId) | |

---

## Test 2: Create a Tenant via "Get Started" Registration

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 2.1 | Logout from SuperAdmin | Redirect to login | |
| 2.2 | Navigate to http://localhost:3100/ (landing page) | Landing page with "Get Started" and "Sign In" buttons visible | |
| 2.3 | Click "Get Started" | Navigate to /register-tenant | |
| 2.4 | Fill form: Company Name: "Acme Corp", Email: `admin@acme.test`, Password: `Admin@123456`, First Name: "John", Last Name: "Doe" | Form validates successfully | |
| 2.5 | Submit registration | Success → redirects to verify email page | |
| 2.6 | Open Mailpit (http://localhost:8025) | Verification email received for admin@acme.test | |
| 2.7 | Click verification link or copy OTP code | Email verified | |
| 2.8 | Login with `admin@acme.test` / `Admin@123456` | Redirect to /dashboard | |

---

## Test 3: Onboarding Wizard (New Tenant First Login)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 3.1 | After first login as tenant admin | Onboarding wizard appears (full screen) | |
| 3.2 | Step 1: Organization Profile | Shows company name (pre-filled "Acme Corp"), logo upload, description field | |
| 3.3 | Add a description, click "Next" | Saves profile, advances to Step 2 | |
| 3.4 | Step 2: Invite Your Team | Shows email + role input fields | |
| 3.5 | Click "Skip for now" | Advances to Step 3 | |
| 3.6 | Step 3: You're All Set | Shows completion message with "Go to Dashboard" button | |
| 3.7 | Click "Go to Dashboard" | Wizard dismisses, dashboard loads | |
| 3.8 | Refresh page | Wizard does NOT reappear (dismissed via localStorage) | |

**Alternative**: Click "Skip setup" on Step 1 to bypass entire wizard.

---

## Test 4: Tenant Admin — Organization Self-Service

**Login as**: The tenant admin created in Test 2 (`admin@acme.test` / `Admin@123456`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 4.1 | Check sidebar | Should show: Dashboard, Users, Roles, **Organization** (NOT Tenants), Files, Reports, Audit Logs, API Keys, Feature Flags, Billing, Settings | |
| 4.2 | Click "Organization" | /organization page loads with own tenant detail | |
| 4.3 | Check header | Back navigation shows "Dashboard" (not "Back to Tenants") | |
| 4.4 | Check tabs | Should show: Overview, Branding, Business Info, Custom Text, Activity, Feature Flags. NO "Subscription" tab (tenant admin, not platform admin). | |
| 4.5 | Click "Branding" tab | Can edit logo, colors, description | |
| 4.6 | Click "Business Info" tab | Can edit address, phone, website, tax ID | |
| 4.7 | Click "Activity" tab | Shows audit logs scoped to own tenant | |
| 4.8 | Click "Feature Flags" tab | Shows feature flag values, can opt-out of boolean non-system flags | |

---

## Test 5: Tenant Admin — Role Permission Management

**Login as**: Tenant admin (`admin@acme.test`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 5.1 | Navigate to Roles | Shows roles list (Admin, User — system roles visible) | |
| 5.2 | Click "Create Role" | Role creation page | |
| 5.3 | Create role "Editor" with description | Role created, redirects to detail | |
| 5.4 | Click "Edit" on the Editor role | Edit page with permission matrix visible | |
| 5.5 | Check permission matrix | Shows all modules with checkboxes. Permissions the admin doesn't have should still be visible but assignable within ceiling. | |
| 5.6 | Select: Users.View, Files.View, Files.Upload | Permissions selected | |
| 5.7 | Save | Success toast — permissions saved | |
| 5.8 | Try to assign a permission the admin doesn't have (e.g., Billing.ManagePlans) | Should fail with permission ceiling error | |

---

## Test 6: Tenant Admin — Invite User & Team Management

**Login as**: Tenant admin (`admin@acme.test`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 6.1 | Navigate to Users | Shows user list (only own tenant users) | |
| 6.2 | Click "Invite User" | Invite modal opens | |
| 6.3 | Enter email: `editor@acme.test`, Role: "Editor" (from Test 5) | Form filled | |
| 6.4 | Send invitation | Success toast | |
| 6.5 | Check Mailpit | Invitation email received at editor@acme.test | |
| 6.6 | Open invitation link | Accept invite page | |
| 6.7 | Set password and submit | Account created | |
| 6.8 | Login as `editor@acme.test` | Dashboard loads with limited access based on Editor role permissions | |

---

## Test 7: Tenant Dashboard Scoping

**Login as**: Tenant admin (`admin@acme.test`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 7.1 | Navigate to Dashboard | Shows "My Users" (not "Total Users"), "My Organization" stat card | |
| 7.2 | Check user count | Should reflect tenant user count (2-3 users from Tests 2+6) | |
| 7.3 | Check Recent Activity | Shows only this tenant's activity | |
| 7.4 | Check Recent Users | Shows only this tenant's users | |

Now login as SuperAdmin (`superadmin@testorg.com`):

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 7.5 | Navigate to Dashboard | Shows "Total Users" (all users), "Platform Status" | |
| 7.6 | Check Recent Activity | Shows ALL audit logs across tenants | |

---

## Test 8: Audit Logs — Tenant Scoping

**Login as**: Tenant admin (`admin@acme.test`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 8.1 | Click "Audit Logs" in sidebar | Audit logs page loads | |
| 8.2 | Check entries | Only shows logs from own tenant (no other tenant data visible) | |
| 8.3 | Navigate to Organization → Activity tab | Shows same tenant-scoped logs in simplified table | |

---

## Test 9: Billing Page (Tenant Self-Service)

**Login as**: Tenant admin (`admin@acme.test`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 9.1 | Click "Billing" in sidebar | Billing page loads | |
| 9.2 | Check current plan | Shows subscription status (or empty if no plan assigned) | |
| 9.3 | Check usage bars | Shows users, storage, API keys, reports usage vs limits | |

---

## Test 10: SuperAdmin — Subscription Management on Tenant Detail

**Login as**: SuperAdmin (`superadmin@testorg.com`)

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 10.1 | Navigate to Tenants → click "Acme Corp" | Tenant detail page | |
| 10.2 | Click "Subscription" tab | Shows subscription management (current plan, usage, payments) | |
| 10.3 | Click "Change Plan" | Plan selector dialog opens with Free/Starter/Pro/Enterprise options | |
| 10.4 | Select "Starter" plan and confirm | Plan changed, usage limits update | |

---

## Test 11: Theme & Dark Mode

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 11.1 | Check landing page colors | Copper/warm gradient visible | |
| 11.2 | Check "Get Started" button | White bg with dark text (readable) | |
| 11.3 | Check language/theme controls on landing | White text, visible on gradient | |
| 11.4 | Login and toggle dark mode | All pages adapt, warm dark tones | |
| 11.5 | Check sidebar active state | Copper tint on active item | |
| 11.6 | Check page titles | Copper colored (primary) | |

---

## Test 12: Sidebar Collapse/Expand

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 12.1 | Click collapse button (next to logo) | Sidebar collapses to icon-only (64px) | |
| 12.2 | Click expand button (bottom of collapsed sidebar) | Sidebar expands back (240px) | |

---

## Test 13: Pagination

| # | Step | Expected Result | Pass/Fail |
|---|------|----------------|-----------|
| 13.1 | Navigate to Audit Logs | Pagination shows with page numbers, page size selector | |
| 13.2 | Change page size to 10 | Table updates, page size persists | |
| 13.3 | Navigate to Users | Same page size (10) applied (localStorage persistence) | |

---

## Notes

- **File uploads require Docker** with MinIO running on port 9000
- **Email verification requires Docker** with Mailpit running on port 8025  
- **Redis** is needed for caching (feature flags, OTP)
- If Docker isn't available, skip file upload and email verification tests
- The SuperAdmin user has NO tenantId — they see "Tenants" not "Organization"
- Tenant users see "Organization" not "Tenants" — this is the key UX difference
