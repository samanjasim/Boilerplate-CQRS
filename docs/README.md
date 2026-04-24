# Documentation

## Start here

- **[Developer Getting Started](getting-started/developer.md)** — workstation setup, local environment, first build.
- **[User Getting Started](getting-started/user.md)** — creating an account, first login, tenant basics.

## Modules

Each module folder contains a README (overview), user-guide (end-user), developer-guide (integrator), roadmap (deferred items), and per-feature deep dives where applicable.

| Module | Purpose | Docs |
|---|---|---|
| **[Workflow & Approvals](modules/workflow/README.md)** | State-machine engine for approvals and multi-step processes | [User](modules/workflow/user-guide.md) · [Dev](modules/workflow/developer-guide.md) · [Roadmap](modules/workflow/roadmap.md) |
| **[AI](modules/ai/README.md)** | RAG pipeline, agent runtime, observability | [Features](modules/ai/features/) |
| **[Communication](modules/communication/README.md)** | Message dispatcher, templates, channels (email/SMS/push) | [User](modules/communication/user-guide.md) · [Dev](modules/communication/developer-guide.md) · [Roadmap](modules/communication/roadmap.md) |
| **[Comments & Activity](modules/comments-activity/README.md)** | Entity-scoped comments, activity feed, @mentions | [User](modules/comments-activity/user-guide.md) · [Dev](modules/comments-activity/developer-guide.md) · [Roadmap](modules/comments-activity/roadmap.md) |
| **[Webhooks](modules/webhooks/README.md)** | Outbound webhook publishing, delivery log, testing | [Dev](modules/webhooks/developer-guide.md) |
| **[Feature Flags](modules/feature-flags/README.md)** | Per-tenant overrides, enforcement, opt-out | [Dev](modules/feature-flags/developer-guide.md) |
| **[Observability](modules/observability/README.md)** | OpenTelemetry traces, Serilog logs, Prometheus metrics | [Dev](modules/observability/developer-guide.md) |

## Architecture

- **[System Design](architecture/system-design.md)** — overall architecture, dependency rules, layering.
- **[Module Development](architecture/module-development.md)** — adding a new module end-to-end.
- **[Cross-Module Communication](architecture/cross-module-communication.md)** — capability contracts, null objects, events.
- **[Domain Module Example](architecture/domain-module-example.md)** — walking through the domain module template.

## Guides

- **[Theming](guides/theming.md)** — theme preset system, semantic tokens, RTL.

## Product

- **[Market Assessment](product/market-assessment.md)** — strategic positioning.
- **[Product Roadmap](product/roadmap.md)** — cross-module product direction.

## Other

- **[Testing](testing/)** — regression checklist and session logs.
- **[Superpowers](superpowers/)** — AI session artifacts (design specs and implementation plans).
- **[Session Handoff](session-handoff.md)** — current open context for the next session (transient).

## Navigation conventions

- **README.md** in each folder = overview and entry point.
- **user-guide.md** = end-user facing.
- **developer-guide.md** = integrator/developer facing.
- **roadmap.md** = deferred items, maintainer-facing.
- **features/** subfolder = deep dives per feature within a module.
