# Webhook Feature — Testing Guide

Complete testing guide covering every user journey, edge case, and integration point.

## Prerequisites

1. Test app running (backend :5100, frontend :3100)
2. Docker services: PostgreSQL, Redis, RabbitMQ (for MassTransit delivery)
3. A tenant registered with a paid plan (Starter+ for webhooks.enabled=true)
4. A public webhook receiver URL for delivery tests (use https://webhook.site for free temporary endpoints)

## Test Environment Setup

### Create Test App
```bash
# From worktree root
powershell -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name "_testWebhooks" -OutputDir "."
# Apply port config (5100/3100), fix seed email, create migration
# See post-feature-testing.md for full steps
```

### Prepare Accounts
- **SuperAdmin**: `superadmin@testwebhooks.com` / `Admin@123456` (platform admin, no tenant)
- **Tenant Admin**: Register a new tenant "WebhookTest Inc" via `/register-tenant`
- **SuperAdmin upgrades tenant** to Starter/Pro plan (webhooks enabled) via Subscriptions page

### Get a Webhook Receiver URL
Go to https://webhook.site — copy your unique URL (e.g., `https://webhook.site/abc-123`). This will receive and display webhook payloads.

---

## Test 1: Feature Flag Gating

**Goal:** Verify webhooks are disabled on Free plan and enabled on paid plans.

### 1.1 — Free Plan: Cannot Create Webhook
1. Login as tenant admin (on Free plan)
2. Navigate to Webhooks page → should see the page (has View permission)
3. Click "Add Endpoint"
4. Fill in URL: `https://webhook.site/your-id`
5. Select events: `user.created`
6. Click Create
7. **Expected:** Error toast — "Webhooks feature is disabled" (or similar)
8. **Verify via API:**
   ```bash
   curl -X POST http://localhost:5100/api/v1/Webhooks \
     -H "Authorization: Bearer $TENANT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"url":"https://webhook.site/test","events":["user.created"],"isActive":true}'
   ```
   Expected: `{"success": false, "message": "Webhooks feature is disabled..."}`

### 1.2 — Upgrade to Starter: Can Create Webhook
1. Login as SuperAdmin
2. Go to Subscriptions → change tenant plan to Starter (webhooks.enabled=true, max=3)
3. Logout, login as tenant admin
4. Navigate to Webhooks → click "Add Endpoint"
5. Fill in URL, select events, Create
6. **Expected:** Success — secret displayed

### 1.3 — Quota Limit: Cannot Exceed Max
1. Create 3 webhook endpoints (Starter plan max=3)
2. Try to create a 4th
3. **Expected:** Error — "Webhook endpoint quota exceeded (max: 3)"
4. Delete one endpoint → create succeeds again

---

## Test 2: CRUD Operations

### 2.1 — Create Webhook Endpoint
1. Click "Add Endpoint"
2. Fill:
   - URL: `https://webhook.site/your-id`
   - Description: "Test webhook for user events"
   - Events: check `user.created`, `user.updated`
   - Active: checked
3. Click Create
4. **Expected:**
   - Secret shown once in a modal (64 hex characters)
   - Copy button works (clipboard)
   - After closing modal, endpoint appears in the list
   - Status badge: "Active"
   - Events shown: "user.created, user.updated"
5. **Verify:** Copy the secret — you'll need it for signature verification later

### 2.2 — Create with Invalid URL
1. Click "Add Endpoint"
2. Enter URL: `http://example.com` (HTTP, not HTTPS)
3. Click Create
4. **Expected:** Validation error — "Webhook URL must be a valid HTTPS URL"
5. Try: empty URL → "Webhook URL is required"
6. Try: `not-a-url` → validation error
7. Try: no events selected → "At least one event type is required"

### 2.3 — Edit Webhook Endpoint
1. Click Edit on an existing endpoint
2. Change URL to `https://webhook.site/new-id`
3. Add more events (e.g., `file.uploaded`)
4. Toggle Active off
5. Save
6. **Expected:**
   - URL updated in the list
   - Events column shows 3 events
   - Status badge: "Inactive"

### 2.4 — Edit with Invalid URL (Regression)
1. Edit an endpoint
2. Change URL to `http://insecure.com`
3. Save
4. **Expected:** Validation error — same HTTPS check as create

### 2.5 — Delete Webhook Endpoint
1. Click Delete on an endpoint
2. **Expected:** Confirmation dialog appears
3. Confirm deletion
4. **Expected:** Endpoint removed from list, success toast
5. **Verify:** Usage tracker decremented (can create up to quota again)

### 2.6 — Regenerate Secret
1. Click on an endpoint's actions menu
2. Select "Regenerate Secret" (if button exists in UI — may need API test)
3. **Expected:** New secret displayed, old secret invalidated
4. **API test:**
   ```bash
   curl -X POST http://localhost:5100/api/v1/Webhooks/{id}/regenerate-secret \
     -H "Authorization: Bearer $TENANT_TOKEN"
   ```
   Expected: `{"data": "new-64-char-hex-secret"}`

---

## Test 3: Webhook Delivery

### 3.1 — Trigger Real Delivery via User Creation
1. Create a webhook subscribed to `user.created`
2. Make sure it's Active and URL points to webhook.site
3. Go to Users → Invite User (or register a new user in the tenant)
4. Wait 5-10 seconds
5. Check webhook.site — **Expected:**
   - POST request received
   - Headers include:
     - `X-Webhook-Signature-256: t=<timestamp>,v1=<hex-hash>`
     - `X-Webhook-Event: user.created`
     - `Content-Type: application/json`
   - Body contains: `userId`, `email`, `fullName`

### 3.2 — Verify HMAC Signature
Using the secret from creation:
```python
import hmac, hashlib, json

secret = "your-64-char-hex-secret"
payload = '{"userId":"...","email":"...","fullName":"..."}'  # exact body from webhook.site
timestamp = "1234567890"  # from t= in X-Webhook-Signature-256 header

signed_payload = f"{timestamp}.{payload}"
expected_sig = hmac.new(
    bytes.fromhex(secret),
    signed_payload.encode(),
    hashlib.sha256
).hexdigest()

# Compare with v1= value from the header
print(f"Expected: {expected_sig}")
```

### 3.3 — Trigger File Upload Delivery
1. Create webhook subscribed to `file.uploaded`
2. Go to Files → Upload a file
3. Check webhook.site
4. **Expected:** Payload with `fileId`, `fileName`, `size`, `contentType`

### 3.4 — Trigger Subscription Changed
1. Create webhook subscribed to `subscription.changed`
2. SuperAdmin changes the tenant's plan
3. Check webhook.site
4. **Expected:** Payload with `tenantId`, `oldPlanId`, `newPlanId`

### 3.5 — Inactive Endpoint Skipped
1. Edit a webhook → set Active to false
2. Trigger an event (e.g., create a user)
3. Check webhook.site
4. **Expected:** No delivery received (inactive endpoints are skipped)

### 3.6 — Unsubscribed Event Skipped
1. Create webhook subscribed only to `file.uploaded`
2. Create a user (triggers `user.created`)
3. Check webhook.site
4. **Expected:** No delivery received (event not in subscription)

### 3.7 — Delivery Failure (Bad URL)
1. Create webhook with URL `https://httpstat.us/500` (always returns 500)
2. Trigger an event
3. Check delivery log in the app
4. **Expected:** Delivery recorded as "Failed" with status code 500

### 3.8 — Delivery Timeout
1. Create webhook with URL `https://httpstat.us/200?sleep=35000` (delays 35 seconds)
2. Trigger an event
3. Check delivery log
4. **Expected:** Delivery recorded as "Failed" — timeout (30s limit exceeded)

---

## Test 4: Test Webhook

### 4.1 — Send Test Ping
1. Click "Test" button on an active endpoint
2. **Expected:**
   - Success toast: "Test sent"
   - Check webhook.site: POST received
   - Event type: `webhook.test`
   - Payload: `{"id":"evt_...","type":"webhook.test","data":{"message":"This is a test webhook delivery"}}`

### 4.2 — Test Inactive Endpoint
1. Deactivate an endpoint (edit → Active=false)
2. Click "Test"
3. **Expected:** Error — endpoint is not active

### 4.3 — Test Non-Existent Endpoint
```bash
curl -X POST http://localhost:5100/api/v1/Webhooks/00000000-0000-0000-0000-000000000000/test \
  -H "Authorization: Bearer $TENANT_TOKEN"
```
**Expected:** 404 — endpoint not found

---

## Test 5: Delivery History

### 5.1 — View Delivery Log
1. Generate several deliveries (create users, upload files)
2. Click the delivery log icon on a webhook endpoint
3. **Expected:**
   - Modal opens with delivery list
   - Columns: Time, Event Type, Status, Response Code, Duration, Attempts
   - Recent deliveries shown

### 5.2 — Expand Delivery Details
1. Click on a delivery row to expand
2. **Expected:**
   - Request Payload shown (pretty-printed JSON)
   - Response Body shown (if any)
   - Error Message shown (if failed, red background)

### 5.3 — Filter by Status
1. Use the status dropdown filter
2. Select "Success" → only successful deliveries shown
3. Select "Failed" → only failed deliveries
4. Select "All" → all deliveries

### 5.4 — Pagination
1. Generate 25+ deliveries (trigger events repeatedly)
2. Open delivery log
3. **Expected:** Pagination controls at bottom
4. Navigate to page 2 → shows next batch
5. Change page size → resets to page 1

---

## Test 6: Event Types

### 6.1 — View Available Events
1. Open Create Webhook dialog
2. **Expected:** Events grouped by resource:
   - **Users**: user.created, user.updated, invitation.accepted
   - **Files**: file.uploaded, file.deleted
   - **Roles**: role.created, role.updated
   - **Billing**: subscription.changed

### 6.2 — Group Selection
1. Check the "Users" group header checkbox
2. **Expected:** All 3 user events selected
3. Uncheck one individual event
4. **Expected:** Group header becomes indeterminate (partial selection)
5. Uncheck group header → all user events deselected

### 6.3 — API Endpoint
```bash
curl http://localhost:5100/api/v1/Webhooks/events \
  -H "Authorization: Bearer $TENANT_TOKEN"
```
**Expected:** 8 event types with type, resource, description

---

## Test 7: Permissions

### 7.1 — View Permission (Webhooks.View)
User with View permission can:
- See Webhooks nav item in sidebar
- List endpoints (GET /webhooks)
- View endpoint detail (GET /webhooks/{id})
- View deliveries (GET /webhooks/{id}/deliveries)
- View event types (GET /webhooks/events)
- Test an endpoint (POST /webhooks/{id}/test)

### 7.2 — Create Permission (Webhooks.Create)
User with Create permission can:
- See "Add Endpoint" button
- Create new endpoints (POST /webhooks)

### 7.3 — Update Permission (Webhooks.Update)
User with Update permission can:
- See "Edit" button on endpoints
- Update endpoints (PUT /webhooks/{id})
- Regenerate secrets (POST /webhooks/{id}/regenerate-secret)

### 7.4 — Delete Permission (Webhooks.Delete)
User with Delete permission can:
- See "Delete" button on endpoints
- Delete endpoints (DELETE /webhooks/{id})

### 7.5 — No Permission
User without Webhooks.View:
- No "Webhooks" in sidebar
- API returns 403 on all webhook endpoints

### 7.6 — Role-Based Access
- **Admin role**: Has all 4 webhook permissions
- **User role**: Has only Webhooks.View
- Test: Login as User role → can see list, cannot create/edit/delete

---

## Test 8: Multi-Tenancy Isolation

### 8.1 — Tenant A Cannot See Tenant B's Webhooks
1. Create webhooks as Tenant A admin
2. Login as Tenant B admin
3. Go to Webhooks page
4. **Expected:** Empty list (no endpoints from Tenant A visible)

### 8.2 — Cross-Tenant API Access
```bash
# Get Tenant A's webhook ID
# Try to access it as Tenant B
curl http://localhost:5100/api/v1/Webhooks/{tenantA-webhook-id} \
  -H "Authorization: Bearer $TENANT_B_TOKEN"
```
**Expected:** 404 (not found, not 403 — don't leak existence)

### 8.3 — Platform Admin Cannot See Tenant Webhooks
1. Login as SuperAdmin (no tenantId)
2. Go to Webhooks page
3. **Expected:** Empty list or page not accessible (webhooks are tenant-scoped)

---

## Test 9: Edge Cases

### 9.1 — Very Long URL (2000 chars)
Create with a URL at exactly 2000 characters → should succeed.
Create with 2001 characters → should fail validation.

### 9.2 — Special Characters in Description
Create with description containing `<script>alert('xss')</script>`
**Expected:** Stored and displayed safely (HTML escaped in frontend)

### 9.3 — Duplicate URLs
Create two webhooks with the same URL but different events.
**Expected:** Both created successfully (duplicates allowed)

### 9.4 — All Events Selected
Create webhook subscribed to ALL 8 events.
**Expected:** Created successfully. Triggers on every event type.

### 9.5 — Rapid Event Triggering
Create 5 users quickly in succession.
**Expected:** 5 delivery records created, all delivered (MassTransit handles concurrency)

### 9.6 — Large Response Body
Point webhook to an endpoint that returns a 10KB response body.
**Expected:** Response body truncated to 4096 characters in delivery log.

---

## Test 10: UI/UX Flow Completeness

### 10.1 — Empty State
1. New tenant with no webhooks
2. Navigate to Webhooks page
3. **Expected:** Empty state with icon, title "No webhook endpoints", and "Add Endpoint" CTA button

### 10.2 — Loading State
1. Slow network or large dataset
2. **Expected:** Spinner shown while data loads

### 10.3 — Error State
1. Kill the backend while on webhooks page
2. **Expected:** Error state with message and retry option

### 10.4 — Secret Display Flow
1. Create endpoint → secret modal appears
2. Copy secret → button changes to checkmark briefly
3. Close modal → secret is gone forever
4. Re-open endpoint → no secret visible (only shown once)

### 10.5 — Sidebar Navigation
- **Tenant user**: Sees "Webhooks" in sidebar (if has View permission)
- **Platform admin**: Does NOT see "Webhooks" (tenant-only feature)

---

## Quick API Test Script

```bash
# Login
TOKEN=$(curl -s http://localhost:5100/api/v1/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"alice@techcorp.com","password":"Test@123456"}' \
  | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["accessToken"])')

# Get event types
curl -s http://localhost:5100/api/v1/Webhooks/events \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Create webhook
RESULT=$(curl -s -X POST http://localhost:5100/api/v1/Webhooks \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://webhook.site/YOUR-ID",
    "description": "Test webhook",
    "events": ["user.created", "file.uploaded"],
    "isActive": true
  }')
echo $RESULT | python3 -m json.tool
WEBHOOK_ID=$(echo $RESULT | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["id"])')
SECRET=$(echo $RESULT | python3 -c 'import sys,json; print(json.load(sys.stdin)["data"]["secret"])')

# Test webhook
curl -s -X POST "http://localhost:5100/api/v1/Webhooks/$WEBHOOK_ID/test" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Get deliveries
curl -s "http://localhost:5100/api/v1/Webhooks/$WEBHOOK_ID/deliveries" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Regenerate secret
curl -s -X POST "http://localhost:5100/api/v1/Webhooks/$WEBHOOK_ID/regenerate-secret" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool

# Update webhook
curl -s -X PUT "http://localhost:5100/api/v1/Webhooks/$WEBHOOK_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"id\": \"$WEBHOOK_ID\",
    \"url\": \"https://webhook.site/YOUR-ID\",
    \"description\": \"Updated description\",
    \"events\": [\"user.created\", \"user.updated\", \"file.uploaded\"],
    \"isActive\": false
  }" | python3 -m json.tool

# Delete webhook
curl -s -X DELETE "http://localhost:5100/api/v1/Webhooks/$WEBHOOK_ID" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

---

## Test Checklist Summary

| # | Category | Tests | Priority |
|---|----------|-------|----------|
| 1 | Feature Flag Gating | 3 | Critical |
| 2 | CRUD Operations | 6 | Critical |
| 3 | Webhook Delivery | 8 | Critical |
| 4 | Test Webhook | 3 | High |
| 5 | Delivery History | 4 | High |
| 6 | Event Types | 3 | Medium |
| 7 | Permissions | 6 | High |
| 8 | Multi-Tenancy | 3 | Critical |
| 9 | Edge Cases | 6 | Medium |
| 10 | UI/UX Flow | 5 | Medium |
| **Total** | | **47** | |
