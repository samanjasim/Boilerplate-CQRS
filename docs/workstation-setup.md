# Workstation Setup

**Audience:** Anyone starting work on this codebase on a fresh machine — whether that's a new laptop, a clean VM, or a teammate joining the project. Also the "I just pulled on a second laptop" checklist for continuation sessions.

**Goal:** After following this guide, you can `git clone`, open Claude Code, and start a new session that has the same tools, skills, and environment as the machine the work was originally done on.

---

## 1. Prerequisites — install these on the OS

Tested on Windows 11. The paths assume Windows; adapt for macOS/Linux as needed.

### 1.1 Language runtimes & SDKs

| Tool | Version | Why | How |
|---|---|---|---|
| **.NET SDK** | 10.0+ | Backend | https://dotnet.microsoft.com/download |
| **Node.js** | 20 LTS or later | Frontend + rename script helpers | https://nodejs.org/ |
| **npm** | ships with Node | Frontend package manager | — |
| **PowerShell 7** | 7.4+ | Runs `scripts/rename.ps1` (the `pwsh` binary, NOT Windows PowerShell 5.1) | `winget install Microsoft.PowerShell` |
| **PostgreSQL** | 16.x | Primary database, used by every test app instance | https://www.postgresql.org/download/ (install to default path `C:\Program Files\PostgreSQL\16\`, set password to `123456`) |
| **Flutter SDK** | 3.24+ | Mobile app | https://docs.flutter.dev/get-started/install |
| **Android Studio** | latest | Android emulator + SDK | https://developer.android.com/studio |
| **Xcode** | 15+ (macOS only) | iOS builds + simulator | Mac App Store |
| **Git** | 2.40+ | Version control | https://git-scm.com/ |

### 1.2 Docker Desktop

Docker is used for the supporting services (Redis, RabbitMQ, MinIO, Mailpit, Jaeger, Prometheus). The `boilerplateBE/docker-compose.yml` file spins them all up with one command.

Install Docker Desktop from https://www.docker.com/products/docker-desktop/ and make sure it's running before you try to start the backend.

### 1.3 Your editor / IDE

The project has been developed primarily in **VS Code** + **Claude Code**. You can use any editor, but this doc assumes VS Code.

- VS Code: https://code.visualstudio.com/
- Claude Code extension: install via the VS Code marketplace, or via the official installer at https://claude.com/claude-code

Recommended VS Code extensions:
- C# Dev Kit (Microsoft)
- ESLint (Microsoft)
- Tailwind CSS IntelliSense
- Prettier
- GitLens

None of these are required; the build and tests run from the command line.

---

## 2. Claude Code plugins installed globally

Claude Code supports global "plugins" that register skills, MCP servers, and slash commands for every project. These live under `~/.claude/plugins/` and are **not** part of the project repo — each machine installs them separately.

### 2.1 The plugins currently installed on the origin machine

Listed here so you can install the same set on a new laptop. All come from the `claude-plugins-official` marketplace unless noted:

| Plugin | Version | What it provides |
|---|---|---|
| **`superpowers`** | 5.0.7 | The core skills framework. Ships the `using-superpowers` introduction skill + a suite of workflow skills (see §2.2). This plugin is the one the project's CLAUDE.md most depends on. |
| **`autofix-bot`** | (varies) | Automated fix loops |
| **`claude-code-setup`** | 1.0.0 | One-time environment configuration helper |
| **`claude-md-management`** | 1.0.0 | Tools for creating and updating `CLAUDE.md` project memory files |
| **`code-review`** | unknown | Structured code review workflow + slash commands |
| **`figma`** | (varies) | Figma MCP integration for design-to-code flows |
| **`playwright`** | (varies) | Playwright MCP server for browser automation tests |
| **`ralph-loop`** | (varies) | Loop-style agent pattern |

### 2.2 Superpowers skills (global, from the `superpowers` plugin)

These get loaded automatically when the `superpowers` plugin is installed. They are **not** in the project repo — they come from the plugin cache at `~/.claude/plugins/cache/claude-plugins-official/superpowers/{version}/skills/`.

Current skill set (v5.0.7):

- `brainstorming` — structured ideation before committing to an implementation
- `dispatching-parallel-agents` — guidance for launching multiple subagents in parallel
- `executing-plans` — workflow for carrying out approved plans
- `finishing-a-development-branch` — wrap-up checklist for completing work on a branch
- `receiving-code-review` — how to respond to review feedback
- `requesting-code-review` — how to ask for a review
- `subagent-driven-development` — patterns for orchestrating work via subagents
- `systematic-debugging` — structured debugging approach
- `test-driven-development` — TDD workflow
- `using-git-worktrees` — working with multiple branches via worktrees
- `using-superpowers` — the introduction skill loaded at session start
- `verification-before-completion` — checklist before declaring work done
- `writing-plans` — how to draft a plan for a non-trivial change
- `writing-skills` — how to create a new skill

Claude Code invokes these via the `Skill` tool. You don't install them individually — they come with the `superpowers` plugin.

### 2.3 How to install the plugins on a new laptop

Open Claude Code and use the built-in plugin install flow:

```
/plugin install superpowers
/plugin install autofix-bot
/plugin install claude-code-setup
/plugin install claude-md-management
/plugin install code-review
/plugin install figma
/plugin install playwright
/plugin install ralph-loop
```

(Exact command syntax may depend on your Claude Code version. Check `/plugin` or `/help` to see the current options.)

Verify they're installed:

```bash
cat ~/.claude/plugins/installed_plugins.json
```

You should see an entry for each plugin under `.plugins.{name}@claude-plugins-official`.

### 2.4 Global Claude settings

Some permission allowances and additional directories live in `~/.claude/settings.json`. These are **per-machine** and intentionally not version-controlled. On a fresh laptop, Claude Code starts with empty settings and learns them as you approve tool calls during a session. That's fine — no manual copy needed. If you want to bootstrap the permission list from the old machine, you can copy `~/.claude/settings.json` over manually, but it's not required.

---

## 3. Project-local skills

Separate from the global plugins, this project keeps **three** skills under `.claude/skills/` in the repo. These are tracked in git and ship with the project, so a fresh clone automatically has them.

Located at `.claude/skills/` in the repo:

| Skill | Purpose |
|---|---|
| **`post-feature-testing.md`** | The workflow for standing up a test instance of the app on ports 5100/3100 via `rename.ps1`, running it against a dedicated PostgreSQL database, and exercising the feature end-to-end. Used after finishing a new feature but before requesting review. |
| **`test-cleanup.md`** | Tear-down workflow for the test app: kill the backend/frontend processes, drop the test database, remove the test directory, verify a clean workspace. Run this after `post-feature-testing` is done or when starting a new test cycle. |
| **`feature-code-review.md`** | Structured code review checklist specific to this codebase (CQRS handler conventions, result-pattern usage, multi-tenancy filters, permission attributes, etc.). |

These are loaded by Claude Code automatically on session start when the project has a `.claude/skills/` folder, independent of the global plugin set. **No extra install needed — they come with the git clone.**

### 3.1 The `.claude` gitignore policy

This project's `.gitignore` includes:

```
.claude/*
!.claude/skills/
```

Meaning: everything under `.claude/` is ignored **except** `.claude/skills/`. This keeps `settings.local.json`, `worktrees/`, and other per-machine state out of the repo while still sharing the three project skills across machines.

If you find yourself wanting to add another shared project skill, drop it in `.claude/skills/` and commit it.

---

## 4. Project services (Docker Compose)

All supporting services run in Docker. Start them with:

```bash
cd boilerplateBE
docker compose up -d
```

This spins up:

| Service | Port(s) | Purpose |
|---|---|---|
| PostgreSQL | 5432 | (NOTE: the boilerplate uses the LOCAL PostgreSQL install, not this container — see §5.1) |
| Redis | 6379 | Distributed cache |
| RabbitMQ | 5672, 15672 | Message broker + management UI |
| Mailpit | 1025 (SMTP), 8025 (web) | Dev email viewer |
| MinIO | 9000, 9001 | S3-compatible file storage + console |
| Jaeger | 16686, 4317, 4318 | Distributed tracing UI + OTLP ingest |
| Prometheus | 9090 | Metrics collection |

Verify everything is healthy:

```bash
docker ps
```

You should see all the containers running.

### 4.1 Important: PostgreSQL is local, not Docker

The boilerplate's default `ConnectionStrings:DefaultConnection` in `appsettings.Development.json` points at `localhost:5432` with username `postgres` and password `123456`. This is the **local** PostgreSQL install from §1.1, not the docker-compose PostgreSQL service.

Why: the test-app workflow in `.claude/skills/post-feature-testing.md` creates per-test databases via the local `psql.exe` binary, which is easier when PostgreSQL is natively installed. If you prefer Dockerized PostgreSQL, update the skill + the appsettings files to match.

---

## 5. First-time clone checklist (new laptop)

Once the prerequisites from §1 and §2 are in place:

### 5.1 Clone the repo

```bash
git clone https://github.com/samanjasim/Boilerplate-CQRS.git
cd Boilerplate-CQRS
git checkout feature/module-architecture   # or whatever branch is current
```

### 5.2 Verify PostgreSQL is reachable

```bash
PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -h localhost -c "SELECT version();"
```

Should print the PostgreSQL version. If not, fix the install before continuing.

### 5.3 Start Docker services

```bash
cd boilerplateBE
docker compose up -d
cd ..
```

### 5.4 Build the backend

```bash
cd boilerplateBE
dotnet restore
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

### 5.5 Run the architecture tests

```bash
dotnet test tests/Starter.Api.Tests --filter "FullyQualifiedName~AbstractionsPurityTests"
```

Expected: `Passed! Failed: 0, Passed: 2`. This verifies the dependency-graph invariants are intact — if it fails immediately after a fresh clone, something is wrong with the environment.

### 5.6 Create an initial migration + seed the database

The boilerplate ships with NO migrations (they are generated per-app, not shipped). For a dev environment:

```bash
cd boilerplateBE
dotnet ef migrations add InitialCreate --project src/Starter.Infrastructure --startup-project src/Starter.Api
```

Then run the API — it will apply the migration and seed default data on startup:

```bash
cd src/Starter.Api
dotnet run --launch-profile http
```

Default port is 5000. Default superadmin credentials are `superadmin@starter.com` / `Admin@123456`.

### 5.7 Build and run the mobile app

In a third terminal (requires Android emulator running or iOS simulator):

```bash
cd boilerplateMobile
flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter run --flavor staging -t lib/main_staging.dart
```

The staging flavor points at `http://10.0.2.2:5000/api/v1` (Android emulator) or `http://localhost:5000/api/v1` (iOS simulator). Make sure the backend is running on port 5000.

### 5.8 Build and run the frontend

In a second terminal:

```bash
cd boilerplateFE
npm install
npm run dev
```

Default port is 3000. Open http://localhost:3000 and log in with the credentials above.

### 5.9 Verify Claude Code sees the project skills

Start a new Claude Code session in the repo root. The `.claude/skills/` folder should be auto-discovered. In a session, you can ask Claude something like "list the available project skills" — it should mention `post-feature-testing`, `test-cleanup`, and `feature-code-review`.

If you have the `superpowers` plugin installed globally (§2.3), Claude will also load the superpowers skills on session start. The `using-superpowers` intro skill is the first one loaded and it includes a red-flag checklist that's relevant to almost every task.

---

## 6. Onboarding reading order

Once the environment is up, read these in this order to get productive:

1. **[README.md](../README.md)** (repo root) — one-page project overview
2. **[CLAUDE.md](../CLAUDE.md)** (repo root) — project instructions, build commands, feature inventory, patterns. Claude Code automatically loads this at session start.
3. **[docs/architecture/system-design.md](./architecture/system-design.md)** — the map of the codebase. Project graph, folder layout, key patterns, request lifecycle, event lifecycle.
4. **[docs/architecture/module-development-guide.md](./architecture/module-development-guide.md)** — how to add or extend a feature. Has a decision framework for "is this core or a module", step-by-step guides for each scenario, and the cookbook section G.
5. **[docs/architecture/cross-module-communication.md](./architecture/cross-module-communication.md)** — the three patterns (capability calls, integration events, reader services) with a decision tree, real examples, and anti-patterns. Read this before adding anything that crosses a module boundary.
6. **[docs/superpowers/specs/2026-04-07-true-modularity-refactor.md](./superpowers/specs/2026-04-07-true-modularity-refactor.md)** — the historical spec for the true-modularity refactor. Useful background context but not required for day-to-day work.
7. **[docs/D2-domain-module-example.md](./D2-domain-module-example.md)** — the planned next step (minimal domain module exercise).
8. **[.claude/skills/post-feature-testing.md](../.claude/skills/post-feature-testing.md)** and **[.claude/skills/test-cleanup.md](../.claude/skills/test-cleanup.md)** — the testing workflow used after every feature.

20–30 minutes of reading total.

---

## 7. Ports you'll see

| Instance | Backend | Frontend |
|---|---|---|
| Dev | 5000 | 3000 |
| Test (via `rename.ps1`) | 5100 | 3100 |

If you spin up multiple test apps simultaneously, either tear down the previous one first (via `.claude/skills/test-cleanup.md`) or manually reconfigure ports per test instance.

---

## 8. Continuation checklist for a fresh chat session on a new laptop

If you've already done §5 and you're starting a new Claude Code session after pulling the latest code:

1. `git pull` on `feature/module-architecture` (should include commits up to `43f3052` or later)
2. `cd boilerplateBE && dotnet build` — must succeed before you start any work
3. `cd boilerplateBE && dotnet test --filter AbstractionsPurityTests` — must pass 2/2
4. Skim [docs/D2-domain-module-example.md](./D2-domain-module-example.md) to remember where the roadmap is
5. Tell Claude: *"I'm continuing the module architecture work on a new laptop. Read `docs/workstation-setup.md`, then `docs/D2-domain-module-example.md`, then start D2 following §5 execution order."*

Claude should pick up where the previous session left off without needing the prior conversation history — every architectural decision is documented in one of the files listed in §6.

---

## 9. Troubleshooting

### Build fails with "Cannot find project reference `Starter.Abstractions.Web`"

You may be on an old branch. Check `git log --oneline | head -20` — if you don't see commits like `refactor(billing): relocate module types` or `feat: Phase 1 — capability contracts`, pull the latest `feature/module-architecture` branch.

### `dotnet ef migrations add` fails with "Unable to create a DbContext of type"

You probably have the Docker PostgreSQL service running instead of the local one. The connection string points at `localhost:5432`; make sure that's the local install, not the docker-compose one. Stop the docker postgres container or change the connection string in `appsettings.Development.json`.

### `rename.ps1` fails with "Cannot find path"

You're running from the wrong directory. The script expects to be invoked from the repo root:
```bash
pwsh ./scripts/rename.ps1 -Name "MyApp" -OutputDir "c:/tmp"
```

### Claude Code doesn't see the superpowers skills

The plugin probably isn't installed on this machine. See §2.3. Alternatively, you can run without the superpowers plugin — just the project-local skills from `.claude/skills/` — but the session-start guidance from `using-superpowers` won't be loaded.

### "Device or resource busy" when deleting a test directory

Per `.claude/skills/test-cleanup.md`: this is usually a locked node_modules file from a prior `npm install`. Safe to ignore; the OS releases it on the next reboot. Don't force-delete.

---

## 10. Keeping this doc accurate

When you install a new global plugin, add a new project skill, change the supported versions, or change the port convention — update this doc in the same PR as the change. Stale workstation docs are worse than no workstation docs because the "new laptop" path silently breaks.
