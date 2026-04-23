# Communication Module — User Manual

This guide covers how to configure and use the Multi-Channel Communication module as a tenant administrator or platform administrator.

---

## Table of Contents

1. [Overview](#overview)
2. [Getting Started](#getting-started)
3. [Channels — Connecting Notification Providers](#channels)
4. [Templates — Customizing Messages](#templates)
5. [Trigger Rules — Automating Notifications](#trigger-rules)
6. [Integrations — Team Messaging Platforms](#integrations)
7. [Delivery Log — Tracking Messages](#delivery-log)
8. [Notification Preferences — User Settings](#notification-preferences)
9. [Required Notifications — Admin Controls](#required-notifications)
10. [Dashboard Widget](#dashboard-widget)
11. [Troubleshooting](#troubleshooting)

---

## Overview

The Communication module provides a unified messaging platform for your organization. It lets you:

- **Send notifications** to users via Email, SMS, Push, WhatsApp, and In-App channels
- **Post updates** to team platforms like Slack, Telegram, Discord, and Microsoft Teams
- **Customize message templates** with your own branding and content
- **Automate messages** based on system events (e.g., "when a user is created, send a welcome email")
- **Track delivery** with a full log of every message sent, including retries and failures
- **Respect user preferences** — users can choose which channels they receive notifications on

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Channel** | A notification delivery method (Email, SMS, Push, WhatsApp, In-App) connected to a provider |
| **Integration** | A team messaging platform (Slack, Telegram, Discord, Teams) for group notifications |
| **Template** | A reusable message template with variables like `{{userName}}` and `{{orderNumber}}` |
| **Trigger Rule** | An automation rule: "When X happens, send Y template via Z channel" |
| **Delivery Log** | A record of every message sent, with status tracking and retry history |

---

## Getting Started

### First-Time Setup (5 minutes)

1. **Connect an email provider** — Go to **Channels** and add an SMTP, SendGrid, or Amazon SES configuration
2. **Review templates** — Go to **Templates** to see the default system templates. Customize any you want
3. **Create a trigger rule** — Go to **Trigger Rules** and set up your first automation (e.g., "Welcome email on user registration")
4. **Test it** — Invite a new user. They should receive the welcome email

### Navigation

The Communication module adds these pages to the sidebar:

| Page | Purpose | Who Uses It |
|------|---------|-------------|
| **Channels** | Connect and manage notification providers | Tenant Admin |
| **Templates** | View and customize message templates | Tenant Admin |
| **Trigger Rules** | Set up automated messaging rules | Tenant Admin |
| **Integrations** | Connect Slack, Telegram, Discord, Teams | Tenant Admin |
| **Delivery Log** | View sent messages and delivery status | Tenant Admin |

Users manage their notification preferences in **Profile > Notification Preferences**.

---

## Channels

Channels are the delivery methods for person-to-person notifications. Each channel needs to be connected to a provider with your own credentials.

### Supported Channels and Providers

| Channel | Providers | What You Need |
|---------|-----------|---------------|
| **Email** | SMTP, SendGrid, Amazon SES | Server/API credentials, sender email address |
| **SMS** | Twilio | Account SID, auth token, sender phone number |
| **Push Notifications** | Firebase (FCM), Apple (APNs) | Project/app credentials |
| **WhatsApp** | Twilio, Meta Business API | Account credentials, approved sender number |
| **In-App** | Ably (Platform Managed) | No setup needed — always available |

### Adding a Channel

1. Navigate to **Channels**
2. Click **Add Channel**
3. **Step 1:** Select the channel type (Email, SMS, etc.)
4. **Step 2:** Select the provider (e.g., SMTP for Email)
5. **Step 3:** Enter your provider credentials
6. Set a display name and optionally mark as the default provider for that channel
7. Click **Save**

### Testing a Channel

After creating a channel configuration:

1. Click the **Test** button (paper plane icon) on the channel card
2. The system will validate your credentials
3. A success or failure message will be shown

### Setting a Default Channel

If you have multiple providers for the same channel (e.g., both SMTP and SendGrid for Email), you can set one as the default:

1. Click the **star icon** on the channel card you want as default
2. The previous default for that channel will be automatically unset

### Channel Statuses

| Status | Meaning |
|--------|---------|
| **Active** | Channel is working and ready to send |
| **Inactive** | Channel has been manually disabled |
| **Error** | Last connection test failed — check credentials |

---

## Templates

Templates define the content of messages. The system ships with default templates for common events (welcome emails, password resets, etc.). You can customize any template with your own branding and content.

### Viewing Templates

1. Navigate to **Templates**
2. Templates are grouped by **category** (Authentication, Security, etc.)
3. Use the category filter tabs to narrow the view
4. Each template shows:
   - **Name** — the template identifier (e.g., `auth.welcome`)
   - **Module** — which module registered this template
   - **Status** — "System" (default) or "Customized" (you've overridden it)

### Customizing a Template

1. Click **Edit** on any template
2. The editor shows:
   - **System Default** (read-only) — the original template content
   - **Variable Reference** — all available variables you can use
3. Click **Customize** to create your own version
4. Edit the **Subject** and **Body** fields
5. Use variables like `{{userName}}` — refer to the Variable Reference panel for available variables
6. Click **Preview** to see how the rendered message looks with sample data
7. Click **Save**

### Template Syntax

Templates use Mustache syntax:

| Syntax | Description | Example |
|--------|-------------|---------|
| `{{variable}}` | Insert a value | `Hi {{userName}}` |
| `{{#section}}...{{/section}}` | Conditional block (shows if value exists) | `{{#trackingUrl}}Track: {{trackingUrl}}{{/trackingUrl}}` |
| `{{^section}}...{{/section}}` | Inverted block (shows if value is empty) | `{{^trackingUrl}}Tracking not available{{/trackingUrl}}` |
| `{{#list}}...{{/list}}` | Loop over items | `{{#items}}- {{name}}: {{price}}{{/items}}` |

### Resetting to Default

To revert a customized template back to the system default:

1. Click **Edit** on the customized template
2. Click **Reset to Default**
3. Confirm the action

Your custom override will be deleted and the system default will be used again.

---

## Trigger Rules

Trigger rules automate message sending based on system events. When an event occurs (e.g., "user created"), matching trigger rules dispatch messages automatically.

### Creating a Trigger Rule

1. Navigate to **Trigger Rules**
2. Click **Create Rule**
3. Fill in:
   - **Name** — a descriptive name (e.g., "Welcome Email on Registration")
   - **Event** — select from available events (e.g., "User Created")
   - **Template** — select which message template to use
   - **Recipient** — who receives the message:
     - **Event User** — the user who triggered the event
     - **Specific User** — a fixed user ID
   - **Channel Sequence** — the delivery channels in fallback order (e.g., Email first, then Push, then In-App)
   - **Delay** — optional delay in seconds before sending
4. Click **Save**

### Channel Fallback

The channel sequence defines a fallback chain:

```
1. Email  →  2. Push  →  3. InApp (always)
```

If email delivery fails (e.g., provider is down), the system automatically tries the next channel. **In-App is always the final implicit fallback** — a message is never silently lost.

### Activating / Deactivating Rules

- Click the **toggle icon** on a rule to activate or deactivate it
- Deactivated rules are preserved but won't fire on events

### Available Events

Events are registered by installed modules. Common events include:

| Event | Description |
|-------|-------------|
| User Created | A new user account was created |
| User Updated | A user profile was modified |
| Tenant Registered | A new organization was registered |
| Invitation Accepted | A user accepted an invitation |
| Password Changed | A user changed their password |
| File Uploaded | A file was uploaded |
| File Deleted | A file was deleted |

Additional events are added by other modules when installed (e.g., Leave module adds "Leave Approved", "Leave Rejected").

---

## Integrations

Integrations send notifications to team messaging platforms — Slack channels, Telegram groups, Discord channels, or Microsoft Teams channels. Unlike notification channels, integrations target groups/channels (not individual users).

### Supported Platforms

| Platform | Connection Method | What You Need |
|----------|------------------|---------------|
| **Slack** | Webhook URL | Create an Incoming Webhook in your Slack workspace |
| **Telegram** | Bot Token + Chat ID | Create a bot via @BotFather, get the chat/group ID |
| **Discord** | Webhook URL | Create a Webhook in your Discord channel settings |
| **Microsoft Teams** | Webhook URL | Create an Incoming Webhook connector in your Teams channel |

### Adding an Integration

1. Navigate to **Integrations**
2. Click **Add Integration**
3. Select the platform (Slack, Telegram, Discord, or Teams)
4. Enter your credentials:
   - **Slack:** Paste the webhook URL from your Slack app
   - **Telegram:** Enter the bot token and target chat ID
   - **Discord:** Paste the webhook URL from Discord channel settings
   - **Teams:** Paste the incoming webhook URL from Teams
5. Set a display name
6. Click **Save**

### Testing an Integration

Click the **Test** button to send a test message to the connected platform. You should see it appear immediately in the target channel/group.

### Linking Integrations to Trigger Rules

When creating or editing a trigger rule, you can add integration targets:
- This means "when this event fires, ALSO post to these team channels"
- Integration messages are independent of the notification channel — both are sent

---

## Delivery Log

The Delivery Log tracks every message the system has sent, with full status tracking and retry history.

### Viewing the Log

1. Navigate to **Delivery Log**
2. The log shows a paginated table of all messages

### Filtering

Use the filters at the top to narrow results:

| Filter | Options |
|--------|---------|
| **Status** | All, Pending, Queued, Sending, Delivered, Failed, Bounced |
| **Channel** | All, Email, SMS, Push, WhatsApp, InApp |
| **Template** | Type a template name to search |

### Message Statuses

| Status | Meaning |
|--------|---------|
| **Pending** | Message queued but not yet processed |
| **Queued** | Message handed to the delivery pipeline |
| **Sending** | Provider is currently delivering |
| **Delivered** | Successfully delivered to the provider |
| **Failed** | Delivery failed after all retries |
| **Bounced** | Provider accepted but recipient rejected (e.g., invalid email) |

### Viewing Details

Click any row to see full details:
- Recipient, template used, rendered subject and body preview
- **Delivery Attempts** — a timeline showing each attempt:
  - Attempt number, channel, provider, status, duration
  - Provider response (for debugging)
  - Error message (if failed)

### Resending Failed Messages

For failed or bounced messages:

1. Click the row to open details
2. Click **Resend**
3. The message is re-queued through the full pipeline
4. A new delivery attempt is recorded

---

## Notification Preferences

Users can control which channels they receive notifications on, per category.

### Managing Your Preferences

1. Go to **Profile** (click your avatar in the top-right)
2. Find the **Notification Preferences** section
3. You'll see a matrix:
   - **Rows** = notification categories (Authentication, Security, etc.)
   - **Columns** = channels (Email, SMS, Push, WhatsApp, In-App)
4. Toggle channels on/off per category
5. Click **Save**

### Required Notifications

Some categories may be marked as **Required** by your organization's admin. These appear with a "Required" badge and cannot be toggled off. For example, Security Alerts via Email might be mandatory.

### In-App is Always On

The In-App channel cannot be disabled. It serves as the guaranteed fallback — every notification is always visible in your in-app notification feed, regardless of other channel preferences.

---

## Required Notifications

*For Tenant Administrators only.*

You can enforce mandatory notification channels for specific categories, preventing users from opting out.

### Setting Required Notifications

1. Navigate to **Channels** (or a dedicated Required Notifications section)
2. Click **Add Required**
3. Select a **Category** (e.g., "Security")
4. Select a **Channel** (e.g., "Email")
5. Click **Save**

This means: all users in your organization MUST receive Security notifications via Email, regardless of their personal preferences.

### Common Required Notification Configurations

| Category | Channel | Why |
|----------|---------|-----|
| Security | Email | Security alerts (login from new device, password changes) must reach users |
| Authentication | Email | Password reset and verification links need email delivery |
| Billing | Email | Payment failures and subscription changes are critical |

---

## Dashboard Widget

The Communication dashboard widget appears on the main Dashboard page, showing:

- **Messages Sent Today** — count of messages dispatched today
- **Delivery Rate** — percentage of successfully delivered messages
- **Failed** — count of failed deliveries requiring attention

Click the widget to navigate directly to the Delivery Log.

---

## Troubleshooting

### "No channels configured" but I set one up

- Verify the channel status is **Active** (not Error)
- Run the **Test** button to validate credentials
- Check that the channel is set as **Default** for its type

### Messages not being sent

1. **Check Trigger Rules** — is there an active rule for the event?
2. **Check Channel Config** — is the channel configured and active for the tenant?
3. **Check Delivery Log** — look for failed entries with error messages
4. **Check User Preferences** — has the recipient opted out of this channel/category?
5. **Check Required Notifications** — is the admin-enforced channel matching what you expect?

### Email arrives but with wrong content

- Check if you have a **Template Override** — go to Templates and look for the "Customized" badge
- **Preview** the template to verify the rendered output
- **Reset to Default** if the override has issues

### "Delivery Failed" in the log

Click the delivery log entry to see the **Delivery Attempts** section:
- **Provider Response** — the raw response from the email/SMS provider
- **Error Message** — what went wrong
- Common causes:
  - Invalid credentials → re-enter and test the channel config
  - Recipient address invalid → check the user's email/phone
  - Provider rate limit → wait and try again, or switch providers
  - Network timeout → check provider connectivity

### Integration messages not appearing in Slack/Telegram/Discord

1. **Test the integration** — click the Test button on the integration card
2. **Check webhook URL** — make sure it hasn't expired or been revoked
3. **Check channel/chat ID** — for Telegram, ensure the bot is a member of the group
4. **Check Delivery Log** — integration deliveries also appear in the log with the integration type

### Template preview shows raw `{{variables}}`

- The template uses a variable that doesn't exist in the sample data
- Go to the template editor and update the **Sample Variables** to include the missing variable
- Check variable names are spelled correctly (case-sensitive)

### Quota exceeded error

Your organization's subscription plan limits the number of messages per billing period:
- Check your current usage in the Dashboard widget
- Contact your platform administrator to upgrade your plan
- **In-App notifications are never rate-limited** — they always work regardless of quota

---

## Glossary

| Term | Definition |
|------|------------|
| **Channel** | A delivery method for person-to-person notifications (Email, SMS, Push, WhatsApp, In-App) |
| **Channel Config** | Provider credentials and settings for a specific channel |
| **Integration** | A team messaging platform connection (Slack, Telegram, Discord, Teams) |
| **Template** | A reusable message template with variable substitution |
| **Template Override** | A tenant-specific customization of a system template |
| **Trigger Rule** | An automation rule that sends a message when an event occurs |
| **Fallback Chain** | The ordered list of channels to try if the primary fails |
| **Delivery Log** | A record of every message sent with status and attempt history |
| **Required Notification** | An admin-enforced channel for a category that users cannot opt out of |
| **Event Registration** | Metadata about available system events (for the Trigger Rules UI) |
