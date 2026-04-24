# Feature Code Review

Use this skill after completing a feature development to review the implementation against quality standards. Run through each section systematically.

## When to Use

After a feature is fully implemented (backend + frontend builds pass), invoke this skill to verify code quality, security, UX completeness, and integration with the boilerplate's cross-cutting concerns before requesting user review or merging.

## Checklist

### 1. Clean Code & Architecture
- [ ] No code duplication (DRY) — extract shared utilities, constants, components
- [ ] SOLID principles followed (SRP in handlers, ISP in interfaces, DIP via constructor injection)
- [ ] Feature folder structure respected: `api/`, `pages/`, `components/`, `utils/`
- [ ] Consistent naming with other features (commands, queries, DTOs, hooks)
- [ ] No dead code, unused imports, or commented-out blocks
- [ ] No hardcoded secrets, passwords, or magic numbers

### 2. API Design Consistency
- [ ] Routes follow `api/v{version}/[controller]` pattern
- [ ] HTTP methods correct (GET reads, POST creates, PUT updates, DELETE deletes)
- [ ] All endpoints have `[ProducesResponseType]` attributes
- [ ] All endpoints have `[Authorize]` with proper permission policy
- [ ] Request/response DTOs consistent with other features
- [ ] `HandleResult()` / `HandlePagedResult()` used consistently
- [ ] XML documentation on controller class and public methods
- [ ] POST-create endpoints return appropriate response (200 or 201)

### 3. Security
- [ ] No hardcoded secrets or passwords in source code
- [ ] Input validation (FluentValidation) on all commands
- [ ] CSV/file injection prevention in exports (sanitize `=@+-` leading chars)
- [ ] Multi-tenancy enforced (EF query filters configured in `ApplicationDbContext`)
- [ ] HTTPS enforced for external URLs (webhook URLs, etc.)
- [ ] File upload size limits enforced
- [ ] No sensitive data exposed in DTOs (secrets, password hashes, internal IDs)

### 4. Cross-cutting Integration
- [ ] Permissions added to `Permissions.cs` + registered in `GetAllWithMetadata()`
- [ ] Permissions mirrored in `boilerplateFE/src/constants/permissions.ts`
- [ ] Role mappings updated in `Roles.cs` (Admin gets CRUD, User gets View)
- [ ] Feature flags seeded in `DataSeeder` if feature is plan-gated (`IsSystem=true`)
- [ ] Feature flags checked in command handlers (`IFeatureFlagService.IsEnabledAsync`)
- [ ] Feature flag quotas enforced (max count via `IFeatureFlagService.GetValueAsync<int>`)
- [ ] Plan features include the new flags with localized labels (all 4 plans)
- [ ] Usage tracking integrated (`IUsageTracker` increment/decrement) if applicable
- [ ] Notifications sent on async operation completion (if applicable)
- [ ] DbSet added to `IApplicationDbContext` + `ApplicationDbContext`
- [ ] EF configuration in `Persistence/Configurations/` with proper indexes
- [ ] Global query filter for tenant isolation in `ApplicationDbContext.OnModelCreating`
- [ ] DI registration in `Infrastructure/DependencyInjection.cs`

### 4b. Messaging & Integration Events
**Only applies if the feature publishes or consumes cross-module events. Skip otherwise.**

**Publishers (command handlers):**
- [ ] Handler injects `IIntegrationEventCollector` — **never** `IPublishEndpoint` or `IBus`
- [ ] Event is scheduled *before* `SaveChangesAsync()` in the handler
- [ ] Event type lives in `Starter.Application/Common/Events/` and implements `IDomainEvent`
- [ ] Event is a `record` with value-type fields (Guids, primitives, enums) — no entities, no navigation properties
- [ ] `MessagingArchitectureTests` still passes (CI enforces: no MassTransit types in `Starter.Application`)
- [ ] If adding to an existing event: changes are **additive only** (new property with default). Breaking changes → create `{Name}EventV2`

**Consumers:**
- [ ] Implements `IConsumer<TEvent>` — auto-discovered via `AddConsumers()`, no manual registration
- [ ] **Idempotency check at the top** using a domain-uniqueness key (`AnyAsync(e => e.TenantId == evt.TenantId)` or equivalent) → returns silently if already processed
- [ ] Uses the module's own DbContext, not `ApplicationDbContext`
- [ ] **Throws on transient failures** (DB unreachable, 5xx dependency) so MT's retry policy fires — does NOT swallow exceptions with try/catch-log
- [ ] **Returns quietly** on non-retryable business conditions (unknown tenant, feature off, idempotency hit)
- [ ] Custom `ConsumerDefinition` only if the default retry (3×, 1s/5s/15s + circuit breaker) is wrong for this consumer

**If the event goes to a dead-letter queue a lot:**
- [ ] Dead-letter reason is understood (check the `_error` queue on RabbitMQ management UI at `localhost:15672`)
- [ ] Consumer is genuinely idempotent — re-delivery from DLQ won't double-write

### 5. User Flow Completeness
- [ ] No dead ends — every action has feedback (success toast or error toast)
- [ ] Loading states shown during async operations (Spinner component)
- [ ] Error states shown with retry options or descriptive messages
- [ ] Empty states with icon, title, and call-to-action (EmptyState component)
- [ ] Confirmation dialogs for destructive actions (ConfirmDialog component)
- [ ] Pagination on all list views (Pagination component with persisted page size)
- [ ] Back navigation on detail pages (useBackNavigation hook)
- [ ] Search/filter on list pages where applicable

### 6. UI/UX Quality
- [ ] Toast notifications for success/error on all mutations
- [ ] Progress indicators for long-running operations
- [ ] Proper form validation feedback (field-level errors)
- [ ] Consistent badge/status styling (STATUS_BADGE_VARIANT from constants)
- [ ] All user-facing strings use i18n (`t('key')`) — no raw English
- [ ] Translations present in all supported locales (EN, AR, KU)
- [ ] Permission-gated UI elements (buttons, nav items hidden without permission)
- [ ] Feature-flag-gated sidebar items (useFeatureFlag hook)
- [ ] Responsive layout (works on mobile/tablet)

### 7. Frontend Code Quality
- [ ] TypeScript types defined for all API responses in `types/`
- [ ] No `any` types or unsafe `as` casts
- [ ] TanStack Query hooks with proper query keys from `lib/query/keys.ts`
- [ ] Mutation hooks with `onSuccess` query invalidation
- [ ] Routes registered in `config/routes.config.ts` + `routes/routes.tsx`
- [ ] Routes protected with `PermissionGuard`
- [ ] API endpoints configured in `config/api.config.ts`
- [ ] Shared components used: `PageHeader`, `EmptyState`, `Pagination`, `ConfirmDialog`

### 8. Documentation
- [ ] `CLAUDE.md` feature inventory updated (backend + frontend tables)
- [ ] `docs/future-roadmap.md` updated if feature was on roadmap
- [ ] Design spec matches implementation (endpoints, DTOs, events)
- [ ] API endpoints documented in controller XML comments

## How to Run

1. Read through each section
2. For each checkbox, verify the implementation
3. Flag any issues found with file path and line number
4. Categorize as CRITICAL / HIGH / MEDIUM / LOW
5. Fix critical and high issues before merge
6. Document medium/low as tech debt if not fixing immediately
