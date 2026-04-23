# Workflow Analytics

A per-definition analytics dashboard surfacing six operator metrics from existing workflow tables. Available as the **Analytics** tab on any non-template workflow definition detail page.

## Access

- Permission: `Workflows.ViewAnalytics`
- Granted to: SuperAdmin, Admin
- Hidden for template definitions (analytics require real instance data)

## Time Windows

| Query param | Backend enum | Range |
|---|---|---|
| `7d` (default when omitted: `30d`) | `SevenDays` | last 7 days |
| `30d` | `ThirtyDays` | last 30 days |
| `90d` | `NinetyDays` | last 90 days |
| `all` | `AllTime` | since definition `CreatedAt` |

If the `?window` query param is missing or invalid the API returns 400.

## Metrics

### Headline strip

| Metric | Source |
|---|---|
| Started | `WorkflowInstances` with `StartedAt` in window |
| Completed | status = `Completed` |
| Cancelled | status = `Cancelled` |
| Avg. cycle time | mean of `CompletedAt − StartedAt` in hours |

### Bottleneck states

States where instances spend the longest time. Only states with **≥ 3 visits** appear (low-visit states produce unreliable medians). Shows median and p95 dwell time (entry → exit of each state).

### Action rates

Per-state breakdown of which actions (approve / reject / return) were taken by users, as both counts and percentages.

### Instance count series

Bucketed by granularity:
- 7d → daily
- 30d → daily
- 90d → weekly
- All time → monthly

### Stuck instances

Top 10 active instances (oldest first) still in the `Active` status, with the current assignee's display name if a pending `ApprovalTask` exists. Row click navigates to the instance detail page.

### Approver activity

Top 10 approvers by total actions (approve + reject + return) in the window. Avg. response time is the mean of `ApprovalTask.CreatedAt → CompletedAt` for matched tasks.

## Low-data caveat

When fewer than 5 instances fall within the window, a banner warns that metrics may not be representative.

## Implementation notes

- No new database tables — all metrics are computed from `WorkflowInstances`, `WorkflowSteps`, and `ApprovalTasks`.
- Percentile calculations run in C# (not SQL) for EF InMemory / Postgres provider compatibility.
- Response-time matching uses a 60-second heuristic window between `WorkflowStep.Timestamp` and `ApprovalTask.CompletedAt`. A future improvement could store an explicit `WorkflowStepId` FK on `ApprovalTask`.
- Templates are excluded from analytics. The handler returns `404` for template definitions.
