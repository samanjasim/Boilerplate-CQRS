# Post-Feature Testing Workflow

After completing a feature (both backend and frontend build passing), run this workflow to create an isolated test instance, verify the feature with Playwright, and leave it running for manual QA.

## Prerequisites

- PostgreSQL 16 installed locally (user: `postgres`, password: `123456`)
- Docker containers running: mailpit (1025/8025), redis (6379), minio (9000/9001)
- Playwright MCP available locally

## Step-by-Step

### 1. Create test app via rename script

Use the rename script with a `_test` prefixed name. The name must be a valid C# identifier.

```powershell
# From the repo root (worktree root)
powershell -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name "_testFeatureName" -OutputDir "."
```

This creates `_testFeatureName/` in the repo root (gitignored by `_test*/`), containing:
- `_testFeatureName-BE/` — renamed backend
- `_testFeatureName-FE/` — renamed frontend

### 2. Drop existing test database (if any)

```bash
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"
```

The rename script changes `starterdb` → `_testfeaturenamedb` in appsettings.

### 3. Configure test ports

Modify the test app to use different ports (avoid conflict with dev instance):

**Backend** — `_testFeatureName-BE/src/_testFeatureName.Api/Properties/launchSettings.json`:
- Change `http://localhost:5000` → `http://localhost:5100`

**Frontend** — `_testFeatureName-FE/vite.config.ts`:
- Change `port: 3000` → `port: 3100`

**Frontend env** — `_testFeatureName-FE/.env` (create or update):
- `VITE_API_BASE_URL=http://localhost:5100/api/v1`

**Backend CORS** — `_testFeatureName-BE/src/_testFeatureName.Api/appsettings.Development.json`:
- Add `http://localhost:3100` to `Cors.AllowedOrigins` array
- Update `AppSettings.FrontendUrl` to `http://localhost:3100`

### 4. Install dependencies and build

```bash
# Backend
cd _testFeatureName/_testFeatureName-BE && dotnet build

# Frontend
cd _testFeatureName/_testFeatureName-FE && npm install
```

### 5. Run the test instance

Backend and frontend run on different ports, sharing the same Docker services (mailpit, redis, minio):

```bash
# Backend (use dotnet run, migrations + seed run automatically)
cd _testFeatureName/_testFeatureName-BE/src/_testFeatureName.Api
dotnet run --launch-profile http

# Frontend (separate terminal)
cd _testFeatureName/_testFeatureName-FE
npm run dev
```

### 6. Run Playwright tests

Use the Playwright MCP to:
1. Navigate to `http://localhost:3100`
2. Login with default credentials (`superadmin@_testfeaturename.com` / `Admin@123456`)
3. **Feature test** — exercise all CRUD flows for the new feature
4. **Regression test** — verify existing features still work (navigation, user list, roles, files, settings)

### 7. Fix any findings

If tests reveal issues, fix them in the **worktree source** (not the test copy), rebuild, re-run the rename script to regenerate the test app, and re-test.

### 8. Leave running for manual QA

Leave both backend and frontend running. Report the URLs to the user:
- Frontend: `http://localhost:3100`
- Backend API: `http://localhost:5100/swagger`
- Mailpit: `http://localhost:8025`
- MinIO Console: `http://localhost:9001`

Wait for user confirmation before pushing changes.

## Cleanup

After user approves, stop the test servers and delete the test directory:

```bash
rm -rf _testFeatureName/
```

The test database can be dropped:
```bash
"C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"
```

## Port Convention

| Instance | Backend | Frontend |
|----------|---------|----------|
| Dev      | 5000    | 3000     |
| Test     | 5100    | 3100     |

## Known Issues with Underscore-Prefixed Names

When using `_test` prefixed names (e.g., `_testFeatureName`), two things break:

1. **Seed email domain** — `superadmin@_testfeaturename.com` fails Zod `.email()` validation (domains can't start with `_`). Fix: edit `appsettings.Development.json` and change the seed email to `superadmin@testfeaturename.com` (no underscore prefix) before first run.

2. **MinIO bucket name** — S3 bucket names cannot start with `_`. The rename script generates `_testfeaturename-files` which MinIO rejects. Fix: edit `appsettings.Development.json` → `StorageSettings:BucketName` and remove the leading underscore (e.g., `testfeaturename-files`). Then create the bucket manually or let the app's `StorageBucketInitializer` create it on startup.

## Notes

- The rename script replaces ALL instances of "Starter"/"starter" with the new name
- Docker services (mailpit, redis, minio) are shared — they're stateless or tenant-isolated
- PostgreSQL is local (not Docker) — test gets its own database automatically
- The `_test*/` gitignore pattern keeps test artifacts out of version control
