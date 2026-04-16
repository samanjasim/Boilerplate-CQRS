# Communication Module — Developer Integration Guide

This guide explains how other modules can integrate with the Communication module to send messages, register events, and leverage the full messaging pipeline — all without taking a direct dependency on the Communication module.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Three Capability Interfaces](#three-capability-interfaces)
3. [Sending Messages Directly](#sending-messages-directly)
4. [Event-Driven Messaging via Trigger Rules](#event-driven-messaging-via-trigger-rules)
5. [Registering Templates and Events](#registering-templates-and-events)
6. [Complete Integration Walkthrough](#complete-integration-walkthrough)
7. [Real-World Integration Examples](#real-world-integration-examples)
8. [Template Syntax Reference](#template-syntax-reference)
9. [Integration Checklist](#integration-checklist)
10. [API Reference](#api-reference)

---

## Architecture Overview

```
Your Module                              Starter.Abstractions
─────────────                            ────────────────────
                                         
1. Seed templates & events ──────────►   ITemplateRegistrar
                                              │
2. Domain event fires       ─────────►   ICommunicationEventNotifier
     (via INotificationHandler)               │
                                              ▼
3. Or send directly         ─────────►   IMessageDispatcher
                                              │
                                         ─────┼───────────────
                                              │
                                    Communication Module (if installed)
                                    ─────────────────────────────────
                                              │
                                         Query TriggerRules
                                              │
                                         Resolve template + render
                                              │
                                         MassTransit async dispatch
                                              │
                                         Channel Provider (SMTP/SMS/Push)
                                              │
                                         DeliveryLog + Attempts
```

**Key principle:** Your module references ONLY `Starter.Abstractions`. All three capability interfaces have Null Object fallbacks — your code runs perfectly whether the Communication module is installed or not.

---

## Three Capability Interfaces

The Communication module exposes three capabilities in `Starter.Abstractions.Capabilities`:

| Interface | Purpose | When to Use |
|-----------|---------|-------------|
| `IMessageDispatcher` | Send a specific message to a specific user | System-critical messages (password reset, security alert) |
| `ICommunicationEventNotifier` | Notify that an event occurred — trigger rules decide what to send | Business-process messages (leave approved, order shipped) |
| `ITemplateRegistrar` | Register templates and events during module seeding | Module initialization (`SeedDataAsync`) |

All three follow the Null Object pattern:
- **Communication installed:** Real implementations process messages, evaluate rules, register templates
- **Communication not installed:** Silent no-ops — your code runs without errors, messages are simply not sent

---

## Sending Messages Directly

Use `IMessageDispatcher` when you need to send a specific message programmatically, regardless of whether the tenant has configured trigger rules.

```csharp
using Starter.Abstractions.Capabilities;

internal sealed class ApproveLeaveCommandHandler(
    LeaveDbContext dbContext,
    IMessageDispatcher messageDispatcher,  // From Abstractions — no Communication reference needed
    ICurrentUserService currentUserService)
    : IRequestHandler<ApproveLeaveCommand, Result>
{
    public async Task<Result> Handle(ApproveLeaveCommand request, CancellationToken ct)
    {
        var leave = await dbContext.LeaveRequests.FindAsync(request.Id);
        leave.Approve();
        await dbContext.SaveChangesAsync(ct);

        // Send notification — Communication module handles everything
        await messageDispatcher.SendAsync(
            templateName: "leave.approved",        // Must match a registered template name
            recipientUserId: leave.EmployeeUserId, // Who receives the message
            variables: new Dictionary<string, object>
            {
                ["employeeName"] = leave.EmployeeName,
                ["leaveType"] = leave.LeaveType,
                ["startDate"] = leave.StartDate.ToString("MMM dd, yyyy"),
                ["endDate"] = leave.EndDate.ToString("MMM dd, yyyy"),
                ["approverName"] = currentUserService.Email ?? "Manager",
                ["daysCount"] = leave.TotalDays
            },
            tenantId: currentUserService.TenantId,
            cancellationToken: ct);

        return Result.Success();
    }
}
```

### Send to a Specific Channel

```csharp
// Force SMS delivery for urgent notifications
await messageDispatcher.SendToChannelAsync(
    templateName: "shift.emergency-change",
    recipientUserId: employeeId,
    channel: NotificationChannelType.Sms,
    variables: new Dictionary<string, object>
    {
        ["shiftDate"] = "Tomorrow",
        ["newTime"] = "6:00 AM",
        ["location"] = "Building A"
    },
    cancellationToken: ct);
```

### When Communication Module is Not Installed

Nothing happens. The `NullMessageDispatcher` logs a debug message and returns `Guid.Empty`. Your code runs without errors.

---

## Event-Driven Messaging via Trigger Rules

Use `ICommunicationEventNotifier` when you want **tenants to control** which events trigger which messages. This is the recommended approach for business-process notifications.

### How It Works

1. Your module raises a domain event (e.g., `LeaveApprovedEvent`)
2. Your `INotificationHandler<T>` calls `ICommunicationEventNotifier.NotifyAsync()`
3. The Communication module queries matching trigger rules for the tenant
4. For each match: resolves the template, renders it, dispatches via configured channels
5. The tenant configured all this in the Trigger Rules UI — no code changes needed

### Step 1: Define Your Domain Event

```csharp
// In your module's Domain/Events/
public sealed record LeaveApprovedEvent(
    Guid LeaveRequestId,
    Guid EmployeeUserId,
    Guid? TenantId,         // Always include TenantId
    string EmployeeName,
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    int TotalDays,
    string ApproverName) : DomainEventBase;
```

**Best practice:** Include `TenantId` in the event. Domain events fire during `SaveChangesAsync` (before the transaction commits), so the newly created entity may not be queryable from the DB yet.

### Step 2: Raise the Event in Your Entity

```csharp
public void Approve(string approverName)
{
    Status = LeaveStatus.Approved;
    ApprovedAt = DateTime.UtcNow;

    RaiseDomainEvent(new LeaveApprovedEvent(
        Id, EmployeeUserId, TenantId,
        EmployeeName, LeaveType.Name,
        StartDate, EndDate, TotalDays,
        approverName));
}
```

### Step 3: Create the Event Handler

This handler lives **in your module** and references only `Starter.Abstractions`:

```csharp
using MediatR;
using Starter.Abstractions.Capabilities;

internal sealed class LeaveNotificationHandler(
    ICommunicationEventNotifier communicationNotifier)    // From Abstractions
    : INotificationHandler<LeaveApprovedEvent>,
      INotificationHandler<LeaveRejectedEvent>
{
    public async Task Handle(LeaveApprovedEvent notification, CancellationToken ct)
    {
        if (notification.TenantId is null) return;

        await communicationNotifier.NotifyAsync(
            eventName: "leave.approved",
            tenantId: notification.TenantId.Value,
            actorUserId: notification.EmployeeUserId,
            eventData: new Dictionary<string, object>
            {
                ["userId"] = notification.EmployeeUserId.ToString(),
                ["employeeName"] = notification.EmployeeName,
                ["leaveType"] = notification.LeaveType,
                ["startDate"] = notification.StartDate.ToString("MMM dd, yyyy"),
                ["endDate"] = notification.EndDate.ToString("MMM dd, yyyy"),
                ["daysCount"] = notification.TotalDays,
                ["approverName"] = notification.ApproverName
            },
            ct: ct);
    }

    public async Task Handle(LeaveRejectedEvent notification, CancellationToken ct)
    {
        if (notification.TenantId is null) return;

        await communicationNotifier.NotifyAsync(
            eventName: "leave.rejected",
            tenantId: notification.TenantId.Value,
            actorUserId: notification.EmployeeUserId,
            eventData: new Dictionary<string, object>
            {
                ["userId"] = notification.EmployeeUserId.ToString(),
                ["employeeName"] = notification.EmployeeName,
                ["reason"] = notification.RejectionReason ?? ""
            },
            ct: ct);
    }
}
```

**Note:** Your module's `.csproj` only references `Starter.Abstractions.Web` (which transitively includes `Starter.Abstractions`). No reference to `Starter.Module.Communication` is needed.

### IMessageDispatcher vs ICommunicationEventNotifier

| Aspect | `IMessageDispatcher` | `ICommunicationEventNotifier` |
|--------|---------------------|-------------------------------|
| **Sends** | Always — message is always dispatched | Only if a matching trigger rule exists |
| **Template** | You specify the template name | Trigger rule specifies the template |
| **Channel** | Template's default or your explicit choice | Trigger rule's channel sequence |
| **Tenant control** | None — always sends | Full — tenants create/disable rules |
| **Use for** | Critical system messages | Business-process notifications |

---

## Registering Templates and Events

Use `ITemplateRegistrar` in your module's `SeedDataAsync` to register message templates and event definitions. This replaces direct `CommunicationDbContext` access.

### Registering Events

Events appear in the Trigger Rules UI dropdown, letting tenants create rules for your module's events:

```csharp
public async Task SeedDataAsync(IServiceProvider services, CancellationToken ct = default)
{
    using var scope = services.CreateScope();
    var registrar = scope.ServiceProvider.GetRequiredService<ITemplateRegistrar>();

    // Register events (idempotent — skips if already exists)
    await registrar.RegisterEventAsync(
        "leave.requested", "Leave", "Leave Requested",
        "Triggered when an employee submits a new leave request", ct);

    await registrar.RegisterEventAsync(
        "leave.approved", "Leave", "Leave Approved",
        "Triggered when a manager approves a leave request", ct);

    await registrar.RegisterEventAsync(
        "leave.rejected", "Leave", "Leave Rejected",
        "Triggered when a manager rejects a leave request", ct);
}
```

### Registering Templates

Templates define the message content. Register them with variable schemas so the template editor shows available variables:

```csharp
await registrar.RegisterTemplateAsync(
    name: "leave.approved",
    moduleSource: "Leave",
    category: "Leave Management",
    description: "Notification sent when a leave request is approved",
    subjectTemplate: "Your {{leaveType}} leave has been approved",
    bodyTemplate: """
        Hi {{employeeName}},

        Your {{leaveType}} leave request has been approved by {{approverName}}.

        Details:
        - From: {{startDate}}
        - To: {{endDate}}
        - Days: {{daysCount}}

        Enjoy your time off!
        """,
    defaultChannel: NotificationChannelType.Email,
    availableChannels: ["Email", "Push", "InApp"],
    variableSchema: new Dictionary<string, string>
    {
        ["employeeName"] = "Employee's full name",
        ["leaveType"] = "Type of leave (Annual, Sick, etc.)",
        ["startDate"] = "Leave start date",
        ["endDate"] = "Leave end date",
        ["daysCount"] = "Total number of days",
        ["approverName"] = "Name of the approving manager"
    },
    sampleVariables: new Dictionary<string, object>
    {
        ["employeeName"] = "Jane Smith",
        ["leaveType"] = "Annual",
        ["startDate"] = "Jan 15, 2026",
        ["endDate"] = "Jan 20, 2026",
        ["daysCount"] = 5,
        ["approverName"] = "John Manager"
    },
    ct: ct);
```

### When Communication Module is Not Installed

`NullTemplateRegistrar` silently returns — no templates are registered, no errors thrown. Your module's `SeedDataAsync` runs cleanly regardless.

---

## Complete Integration Walkthrough

Here's the full end-to-end for adding Communication support to a new module:

### 1. In your module's `.csproj` — NO changes needed

Your module already references `Starter.Abstractions.Web`. The capability interfaces are available.

### 2. In your domain — Raise events with TenantId

```csharp
public void Approve(string approverName)
{
    Status = LeaveStatus.Approved;
    RaiseDomainEvent(new LeaveApprovedEvent(Id, EmployeeUserId, TenantId, ...));
}
```

### 3. In your event handlers — Call the notifier

```csharp
internal sealed class LeaveNotificationHandler(
    ICommunicationEventNotifier communicationNotifier)
    : INotificationHandler<LeaveApprovedEvent>
{
    public async Task Handle(LeaveApprovedEvent e, CancellationToken ct)
    {
        if (e.TenantId is null) return;
        await communicationNotifier.NotifyAsync("leave.approved", e.TenantId.Value, e.EmployeeUserId,
            new() { ["employeeName"] = e.EmployeeName, ... }, ct);
    }
}
```

### 4. In your module class — Seed templates and events

```csharp
public async Task SeedDataAsync(IServiceProvider services, CancellationToken ct)
{
    using var scope = services.CreateScope();
    var registrar = scope.ServiceProvider.GetRequiredService<ITemplateRegistrar>();

    await registrar.RegisterEventAsync("leave.approved", "Leave", "Leave Approved", "...", ct);
    await registrar.RegisterTemplateAsync("leave.approved", "Leave", "Leave Management", "...",
        "Subject", "Body with {{variables}}", NotificationChannelType.Email,
        ["Email", "Push", "InApp"], variableSchema: ..., sampleVariables: ..., ct: ct);
}
```

### 5. That's it

- No project references to Communication module
- No access to CommunicationDbContext
- No conditional `GetService<T>()` null checks
- Works perfectly when Communication module is not installed

---

## Real-World Integration Examples

### HR Leave Module

```csharp
// Event handler — one handler for multiple events
internal sealed class LeaveNotificationHandler(
    ICommunicationEventNotifier notifier)
    : INotificationHandler<LeaveApprovedEvent>,
      INotificationHandler<LeaveRejectedEvent>,
      INotificationHandler<LeaveRequestedEvent>
{
    public async Task Handle(LeaveRequestedEvent e, CancellationToken ct)
    {
        if (e.TenantId is null) return;
        // Notify the MANAGER, not the employee
        await notifier.NotifyAsync("leave.requested", e.TenantId.Value, e.ManagerUserId,
            new() { ["employeeName"] = e.EmployeeName, ["leaveType"] = e.LeaveType, ... }, ct);
    }
    // ... other handlers
}
```

### E-Commerce Orders Module

```csharp
internal sealed class OrderNotificationHandler(
    ICommunicationEventNotifier notifier,
    IMessageDispatcher dispatcher)  // Use BOTH — notifier for triggers, dispatcher for critical
    : INotificationHandler<OrderShippedEvent>
{
    public async Task Handle(OrderShippedEvent e, CancellationToken ct)
    {
        if (e.TenantId is null) return;

        // Critical: always send shipping confirmation via direct dispatch
        await dispatcher.SendAsync("order.shipped", e.CustomerId,
            new() { ["orderNumber"] = e.OrderNumber, ["trackingUrl"] = e.TrackingUrl, ... },
            e.TenantId, ct);

        // Also trigger tenant-configured rules (e.g., post to Slack #orders)
        await notifier.NotifyAsync("order.shipped", e.TenantId.Value, e.CustomerId,
            new() { ["orderNumber"] = e.OrderNumber, ... }, ct);
    }
}
```

### Workflow & Approvals Module

```csharp
internal sealed class WorkflowNotificationHandler(
    ICommunicationEventNotifier notifier)
    : INotificationHandler<ApprovalNeededEvent>,
      INotificationHandler<ApprovalSlaWarningEvent>
{
    public async Task Handle(ApprovalNeededEvent e, CancellationToken ct)
    {
        if (e.TenantId is null) return;
        await notifier.NotifyAsync("approval.needed", e.TenantId.Value, e.ApproverUserId,
            new()
            {
                ["userId"] = e.ApproverUserId.ToString(),
                ["entityType"] = e.EntityType,
                ["entityName"] = e.EntityName,
                ["requesterName"] = e.RequesterName,
                ["dueDate"] = e.DueDate.ToString("MMM dd, yyyy HH:mm"),
                ["approvalUrl"] = e.ApprovalUrl
            }, ct);
    }

    public async Task Handle(ApprovalSlaWarningEvent e, CancellationToken ct)
    {
        if (e.TenantId is null) return;
        // Use direct dispatcher for SLA warnings — these should ALWAYS send
        await notifier.NotifyAsync("approval.sla_warning", e.TenantId.Value, e.ApproverUserId,
            new()
            {
                ["userId"] = e.ApproverUserId.ToString(),
                ["entityName"] = e.EntityName,
                ["hoursRemaining"] = e.HoursRemaining
            }, ct);
    }
}
```

### Billing Module

```csharp
// In SeedDataAsync — register billing templates
await registrar.RegisterEventAsync("payment.failed", "Billing", "Payment Failed",
    "Triggered when a recurring payment fails");

await registrar.RegisterTemplateAsync("payment.failed", "Billing", "Billing",
    "Notification when a payment attempt fails",
    "Payment failed for your {{planName}} subscription",
    """
    Hi {{userName}},

    We were unable to process your payment of {{amount}} for the {{planName}} plan.

    {{#retryDate}}We will retry on {{retryDate}}.{{/retryDate}}

    Please update your payment method to avoid service interruption.
    """,
    NotificationChannelType.Email, ["Email", "Push", "InApp"],
    variableSchema: new()
    {
        ["userName"] = "Account holder name",
        ["amount"] = "Payment amount with currency",
        ["planName"] = "Subscription plan name",
        ["retryDate"] = "Next retry date (optional)"
    }, ct: ct);
```

---

## Template Syntax Reference

Templates use **Mustache** syntax (via Stubble.Core):

| Syntax | Description | Example |
|--------|-------------|---------|
| `{{variable}}` | Insert a value | `Hi {{userName}}` |
| `{{#section}}...{{/section}}` | Conditional block (renders if truthy/non-empty) | `{{#trackingUrl}}Track: {{trackingUrl}}{{/trackingUrl}}` |
| `{{^section}}...{{/section}}` | Inverted block (renders if falsy/empty) | `{{^trackingUrl}}Tracking not available{{/trackingUrl}}` |
| `{{#list}}...{{/list}}` | Loop over array items | `{{#items}}- {{name}}: {{price}}{{/items}}` |

**Note:** This is Mustache, not Handlebars. `{{#if}}` and `{{#each}}` are NOT supported.

---

## Integration Checklist

- [ ] **No Communication module reference** — your `.csproj` only references `Starter.Abstractions.Web`
- [ ] **TenantId in events** — all domain events carry `Guid? TenantId` for pre-save resolution
- [ ] **Event handler created** — `INotificationHandler<T>` that calls `ICommunicationEventNotifier.NotifyAsync()`
- [ ] **Events registered** — `ITemplateRegistrar.RegisterEventAsync()` in `SeedDataAsync`
- [ ] **Templates registered** — `ITemplateRegistrar.RegisterTemplateAsync()` with variable schemas + sample data
- [ ] **Variable naming** — camelCase, descriptive, documented in the schema
- [ ] **Channel selection** — templates specify appropriate channels (don't offer SMS for lengthy content)
- [ ] **Build test** — `dotnet build` passes with and without the Communication module
- [ ] **Graceful degradation** — module works when Communication is not installed (null objects handle it)

---

## API Reference

### ICommunicationEventNotifier

```csharp
public interface ICommunicationEventNotifier : ICapability
{
    Task NotifyAsync(
        string eventName,           // Must match EventRegistration.EventName
        Guid tenantId,              // Tenant context for rule evaluation
        Guid? actorUserId,          // User who triggered the event (for recipient resolution)
        Dictionary<string, object> eventData,  // Variables for template rendering
        CancellationToken ct = default);
}
```

### IMessageDispatcher

```csharp
public interface IMessageDispatcher : ICapability
{
    Task<Guid> SendAsync(
        string templateName,         // Must match MessageTemplate.Name
        Guid recipientUserId,        // Who receives the message
        Dictionary<string, object> variables,
        Guid? tenantId = null,       // Defaults to current user's tenant
        CancellationToken ct = default);

    Task<Guid> SendToChannelAsync(
        string templateName,
        Guid recipientUserId,
        NotificationChannelType channel,  // Force specific channel
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken ct = default);
}
```

**Returns:** `Guid` — DeliveryLog ID. Returns `Guid.Empty` if module not installed.

### ITemplateRegistrar

```csharp
public interface ITemplateRegistrar : ICapability
{
    Task RegisterTemplateAsync(
        string name, string moduleSource, string category,
        string? description, string? subjectTemplate, string bodyTemplate,
        NotificationChannelType defaultChannel, string[] availableChannels,
        Dictionary<string, string>? variableSchema = null,
        Dictionary<string, object>? sampleVariables = null,
        CancellationToken ct = default);

    Task RegisterEventAsync(
        string eventName, string moduleSource, string displayName,
        string? description = null, CancellationToken ct = default);
}
```

Both methods are **idempotent** — they skip if the template/event already exists.

### NotificationChannelType

```csharp
public enum NotificationChannelType
{
    Email = 0,
    Sms = 1,
    Push = 2,
    WhatsApp = 3,
    InApp = 4
}
```
