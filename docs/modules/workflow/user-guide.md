# Workflow & Approvals — User Manual

The Workflow module lets you submit requests that need approval, track their progress, and act on tasks assigned to you. This guide is for end users (not developers).

## Concepts

- **Request (Workflow Instance)** — a single submitted workflow. For example, "Expense report #42" or "Purchase order for laptop."
- **Task (Pending Task)** — a step in a request that's waiting for someone to act. You see tasks assigned to you in the **Inbox**.
- **Definition** — the template that shapes the workflow (the steps, who approves each step, what forms show up). Admins author definitions; you pick one when starting a new request.
- **Step** — a single state in the workflow (e.g. "Draft," "Manager Review," "Approved").

## Starting a new request

1. Go to **Workflow → Inbox** from the sidebar.
2. Click **New Request** (top right).
3. Choose a workflow definition from the dropdown. The description shows what the workflow is for.
4. Fill in the initial form if the definition requires one. Required fields are marked with `*`.
5. Click **Submit**. Your request appears under **Workflow → My Requests** and automatically advances to the first approval step.

## Approving, rejecting, or returning a task

Your **Inbox** shows every task assigned to you.

1. Click **Approve** (or **Reject**) next to the task.
2. If the step has a form, fill in the required fields.
3. Add an optional comment — useful for explaining your decision.
4. Click **Confirm**.

Three main actions:

- **Approve** — advances the request to the next step.
- **Reject** — ends the request in a rejected state. The originator sees the rejection reason from your comment.
- **Return for revision** — sends the request back to the originator. They can edit the submission and resubmit.

## Resubmitting after return for revision

If a reviewer sends your request back:

1. You'll receive a notification.
2. Open the request from **My Requests**.
3. Edit the initial form fields (or attached entity data).
4. Click **Resubmit**.

The request re-enters the first approval step.

## Delegating your tasks (coverage while on leave)

When you're away, you can delegate your pending tasks to another user.

1. From the **Inbox**, click **Delegate** (top right).
2. Pick the user to delegate to.
3. Choose a start date and end date.
4. Click **Confirm**.

During the delegation period, any new tasks that would normally land in your inbox go to the delegate instead. The delegated user sees a "Delegated from {your name}" badge on each task.

To cancel an active delegation early, click **Cancel Delegation** from the banner at the top of your inbox.

## Viewing request history and current status

From **Workflow → My Requests**, click any row to open the request detail page. You'll see:

- **Status banner** — current state, who started the request, when.
- **Step timeline** — every step the request has passed through, who acted, what comment they left, any form data they submitted. Steps not yet visited are shown in muted grey.
- **Comments + Activity** — free-text discussion and system events (delegations, escalations, reassignments) in chronological order.
- **Cancel** — only available to the originator while the request is still active.

## Reading the Dashboard widget

On your dashboard, the **Pending Approvals** widget shows:

- **Count** — total tasks waiting for you.
- **Shortcuts** — quick links to the Inbox.

A red overdue badge indicates one or more tasks have passed their SLA. Click through to the Inbox to see which.

## Forms and form fields

Some steps require structured data, not just a free-text comment. Common field types:

- **Text** — free-form single line.
- **Number** — numeric; may have min/max.
- **Select** — pick one from a dropdown.
- **Date** — calendar picker.
- **Checkbox** — yes/no.

Required fields have a red `*` next to the label. You cannot submit until all required fields are filled.

## Notifications

You'll receive notifications when:

- A task is assigned to you (or to a user who has delegated to you).
- A task you submitted is approved, rejected, or returned for revision.
- A task assigned to you is overdue (SLA reminder).
- A task assigned to you is escalated to someone else.

Manage notification channels (email, in-app, etc.) from **Settings → Notification Preferences**.

## Frequently asked questions

**Q: I don't see a "New Request" button.**
A: You need the `Workflows.Start` permission. Ask your tenant admin.

**Q: Can I edit a request after submitting it?**
A: Only after a reviewer returns it for revision. While it's in approval, the content is locked.

**Q: Can I see all of my tenant's requests?**
A: Only if you have the `Workflows.ViewAll` permission. Otherwise you see your own requests plus tasks assigned to you.

**Q: My delegate approved a task — who's recorded as the actor?**
A: The delegate is the actor. The original assignee (you) is recorded as "delegated from" in the step history.
