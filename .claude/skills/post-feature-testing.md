# Post-Feature Testing Workflow

After completing a feature (backend build passes, and frontend if in scope), run this workflow to create an isolated test instance, verify the feature, and leave it running for manual QA.

## Prerequisites

- PostgreSQL 16 available locally (user: `postgres`, password: `123456`). On macOS/Linux: `psql` in PATH. On Windows: use `"C:\Program Files\PostgreSQL\16\bin\psql.exe"`.
- Docker services running: at least `mailpit` (1025/8025), `redis` (6379), `minio` (9000/9001), `rabbitmq` (5672), and `qdrant` (6333/6334) if the feature exercises messaging or RAG. `docker compose up -d` from `boilerplateBE/`.
- Playwright MCP if running UI tests (backend-only features can skip).
- `pwsh` installed on macOS/Linux (`brew install powershell`) — the `rename.sh` script uses GNU-sed flags that BSD-sed on macOS rejects, so **prefer `rename.ps1`** across platforms.

## Step-by-Step

### 1. Pick a free port

The dev instance occupies 5000/3000. The long-standing test default is 5100/3100, but a feature-specific plan may reserve that range too — always verify the port you want is free before starting:

```bash
# Check a single port
lsof -i :5101

# Or scan a range, printing the first free one
for p in 5100 5101 5102 5103 5104 5105; do
  lsof -i :$p >/dev/null 2>&1 || { echo "$p FREE"; break; }
done
```

If the feature's plan prescribes a port constraint (e.g. "pick a port not in [5000, 5100]"), honor it.

### 2. Create the test app via rename script

Use a `_test` prefixed name (valid C# identifier, gitignored by `_test*/`).

```bash
# macOS / Linux / Windows (requires pwsh)
pwsh -File scripts/rename.ps1 -Name "_testFeatureName" -OutputDir "."
```

The `.ps1` script is cross-platform via pwsh. Avoid `scripts/rename.sh` on macOS — its `sed -i''` syntax is GNU-specific and fails with `sed: -e: No such file or directory` on BSD sed.

This creates `_testFeatureName/` in the repo root, containing:
- `_testFeatureName-BE/` — renamed backend
- `_testFeatureName-FE/` — renamed frontend
- `_testFeatureName-Mobile/` — renamed mobile

### 3. Drop any stale test database

```bash
# macOS / Linux
PGPASSWORD=123456 psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"

# Windows
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"
```

The rename script rewrites `starterdb` → `_testfeaturenamedb` automatically in appsettings.

### 4. Configure test ports, CORS, and frontend env

**Backend** — `_testFeatureName-BE/src/_testFeatureName.Api/Properties/launchSettings.json`:
- `http://localhost:5000` → `http://localhost:<picked-port>` (both `http` and `https` profiles)
- HTTPS pair: `https://localhost:5001` → `https://localhost:<picked-port+1>`

**Frontend** — only if the feature has UI:
- `_testFeatureName-FE/vite.config.ts` → `port: <FE-port>` (e.g. 3100)
- `_testFeatureName-FE/.env` → `VITE_API_BASE_URL=http://localhost:<BE-port>/api/v1`

**Backend CORS** — `_testFeatureName-BE/src/_testFeatureName.Api/appsettings.Development.json`:
- Add `http://localhost:<FE-port>` to `Cors.AllowedOrigins` array
- Update `AppSettings.FrontendUrl` to match

### 5. Fix the underscore-prefix traps

Two things break when the name starts with `_`:

1. **Seed email** — `superadmin@_testfeaturename.com` fails Zod `.email()`. Fix in `appsettings.Development.json` → `SeedSettings.SuperAdmin.Email`: strip the leading underscore (`superadmin@testfeaturename.com`).
2. **MinIO bucket** — S3 bucket names cannot start with `_`. Fix in `StorageSettings.BucketName`: strip the leading underscore (`testfeaturename-files`). `StorageBucketInitializer` creates it on startup.

### 6. Feature-specific config (if applicable)

If the feature touches the AI module or any optional subsystem, check the test app's appsettings for feature-specific keys:

- **RabbitMQ** — defaults to `Enabled: false` in generated test apps. Flip to `true` if the feature relies on MassTransit consumers (e.g. AI ingestion, imports, webhooks).
- **AI module** — if the feature uses embeddings on macOS/CI without Tesseract language data installed, set `AI:Ocr:Enabled = false` to avoid a constructor-time crash in `TesseractOcrService`. Plain text / CSV / DOCX ingestion doesn't need OCR; PDF fallback won't work but won't break non-PDF flows.
- **API keys for AI providers** — copy from source user-secrets into the test project (see Step 7).

### 7. Copy user-secrets into the test project

If the source `boilerplateBE/src/Starter.Api` has any user-secrets (e.g. AI provider keys), the test project starts empty. Copy only the keys you need without echoing values:

```bash
cd _testFeatureName/_testFeatureName-BE/src/_testFeatureName.Api
dotnet user-secrets init   # no-op if already initialized

for K in "AI:Providers:OpenAI:ApiKey" "AI:Providers:Anthropic:ApiKey"; do
  V=$(cd ../../../../boilerplateBE/src/Starter.Api && dotnet user-secrets list 2>/dev/null | awk -v k="$K" -F' = ' '$1==k{print $2}')
  [ -n "$V" ] && dotnet user-secrets set "$K" "$V" > /dev/null && echo "set $K"
done
```

Verify (masked):

```bash
dotnet user-secrets list | awk -F' = ' '/^AI:/{printf "%s = %s...<redacted>\n", $1, substr($2,1,10)}'
```

### 8. Restore and create EF migrations

The boilerplate **has no migrations checked in** (deliberate — each app generates its own). The test app uses a module architecture with **one DbContext per module**. You must generate an `InitialCreate` per context — the old single-context command fails with "More than one DbContext was found."

```bash
cd _testFeatureName/_testFeatureName-BE
dotnet restore

# Discover all contexts
dotnet ef dbcontext list --project src/_testFeatureName.Infrastructure --startup-project src/_testFeatureName.Api
```

For each context, create the migration in the project that owns it — the main `ApplicationDbContext` lives in `.Infrastructure`; each module's context lives in that module's project:

```bash
SP=src/_testFeatureName.Api

# Core
dotnet ef migrations add InitialCreate \
  --context ApplicationDbContext \
  --project src/_testFeatureName.Infrastructure \
  --startup-project $SP

# Modules — one per DbContext listed above
for CTX in \
  "ImportExportDbContext:src/modules/_testFeatureName.Module.ImportExport" \
  "BillingDbContext:src/modules/_testFeatureName.Module.Billing" \
  "WebhooksDbContext:src/modules/_testFeatureName.Module.Webhooks" \
  "ProductsDbContext:src/modules/_testFeatureName.Module.Products" \
  "AiDbContext:src/modules/_testFeatureName.Module.AI"
do
  NAME="${CTX%%:*}"; PROJ="${CTX##*:}"
  dotnet ef migrations add InitialCreate --context "$NAME" --project "$PROJ" --startup-project $SP
done
```

Each module's migrations assembly points at itself (via `MigrationsAssembly(typeof(XDbContext).Assembly.FullName)`) and its history table is isolated (e.g. `__EFMigrationsHistory_AI`) so migrations coexist cleanly. `DatabaseSettings.ApplyMigrationsOnStartup = true` in the seed config runs them all on first boot.

If the feature is a new module (added since this doc was written), add its context to the list.

### 9. Install dependencies and build

```bash
# Backend
cd _testFeatureName/_testFeatureName-BE && dotnet build

# Frontend (skip if backend-only)
cd _testFeatureName/_testFeatureName-FE && npm install
```

### 10. Run the test instance

```bash
# Backend — migrations + seed run automatically on startup
cd _testFeatureName/_testFeatureName-BE/src/_testFeatureName.Api
dotnet run --launch-profile http

# Frontend (separate terminal, if in scope)
cd _testFeatureName/_testFeatureName-FE
npm run dev
```

Wait for the `/health` endpoint before hitting APIs:

```bash
until curl -s http://localhost:<BE-port>/health >/dev/null; do sleep 2; done
```

### 11. Verify the feature

Login with `superadmin@testfeaturename.com` / `Admin@123456` (note: no underscore in the email — see Step 5). Exercise:

1. **Feature test** — all CRUD / state transitions the feature introduces. For async features, poll for terminal state instead of sleeping.
2. **Regression test** — nav, users, roles, files, settings, plus any feature adjacent to yours.

For backend-only features, a curl script against the REST endpoints is faster than booting Playwright. For UI features, use the Playwright MCP against `http://localhost:<FE-port>`.

### 12. Fix any findings

Fix in the **worktree source** (not the test copy). Re-run the rename script to regenerate the test app, then re-test. The test app is disposable; never edit source files only to make the test pass.

### 13. Leave running for manual QA

Report URLs to the user:
- Frontend: `http://localhost:<FE-port>`
- Backend API: `http://localhost:<BE-port>/swagger`
- Mailpit: `http://localhost:8025`
- MinIO Console: `http://localhost:9001`
- Qdrant Dashboard (if RAG): `http://localhost:6333/dashboard`
- Jaeger: `http://localhost:16686`

Wait for confirmation before pushing.

## Cleanup

After user approves:

```bash
# Stop test servers (Ctrl+C the processes, or)
kill $(lsof -ti :<BE-port>) 2>/dev/null

# Drop the test DB
PGPASSWORD=123456 psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"

# Remove the test directory
rm -rf _testFeatureName/
```

The user-secrets entries for the test project are keyed by a per-project `UserSecretsId` in the csproj — those secrets are orphaned after `rm -rf` but harmless. Clean them by deleting the matching folder under `~/.microsoft/usersecrets/<id>/` if desired.

Qdrant collections named `tenant_*` remain after the DB is dropped — they're tenant-scoped and will be reused or re-seeded on the next run. Clear them via `curl -X DELETE http://localhost:6333/collections/tenant_<guid>` if you want a truly clean slate.

## Port Convention

| Instance | Backend | Frontend | Notes |
|----------|---------|----------|-------|
| Dev      | 5000    | 3000     | Always |
| Test     | 5100    | 3100     | Default; bump if busy |
| Alt test | 5101+   | 3101+    | When a plan explicitly reserves 5100 or you run multiple test apps |

## Known Traps

1. **BSD sed vs. GNU sed** — `scripts/rename.sh` uses `sed -i''` which fails on macOS. Use `rename.ps1` via `pwsh` instead.
2. **Underscore-prefix names** — break Zod email validation and S3 bucket naming. Fix manually post-rename (Step 5).
3. **Single-context migration command is outdated** — the current module architecture needs one migration per DbContext (Step 8).
4. **Tesseract OCR constructor throws without tessdata** — disable with `AI:Ocr:Enabled=false` if the feature doesn't need PDF OCR fallback, or install the Tesseract language data.
5. **MassTransit `RabbitMQ.Enabled=false` by default** — flip to `true` in the test app if the feature uses consumers.
6. **User-secrets are per-project** — the test project gets its own UUID and starts empty. Copy keys from the source project if needed (Step 7).

## Notes

- The rename script replaces ALL instances of "Starter"/"starter" with the new name.
- Docker services (postgres, rabbitmq, qdrant, redis, minio, mailpit) are shared between dev and test — tests isolate via separate DB names and tenant-scoped Qdrant collections.
- PostgreSQL is local (not Docker in the default setup) — test gets its own database automatically.
- The `_test*/` gitignore pattern keeps test artifacts out of version control.
