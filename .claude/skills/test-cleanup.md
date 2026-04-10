# Test App Cleanup

After testing is complete and the user confirms, run this cleanup to tear down test instances, kill processes, drop databases, and verify a clean workspace.

## When to Use

- After post-feature testing is done and user approves
- When the user asks to clean up, stop tests, or tear down test apps
- Before starting a new test cycle (to clear stale instances)

## Step-by-Step

### 1. Kill backend processes (dotnet)

```bash
taskkill //F //IM dotnet.exe 2>/dev/null
```

Verify port 5100 is free:

```bash
netstat -ano | grep ":5100 .*LISTEN"
```

If still occupied, kill by PID:

```bash
netstat -ano | grep ":5100 .*LISTEN" | awk '{print $5}' | while read pid; do taskkill //F //PID $pid 2>/dev/null; done
```

### 2. Kill frontend processes (node)

```bash
taskkill //F //IM node.exe 2>/dev/null
```

Verify port 3100 is free:

```bash
netstat -ano | grep ":3100 .*LISTEN"
```

**Warning:** This kills ALL node processes on the machine. If the user has other Node apps running, kill by PID instead:

```bash
netstat -ano | grep ":3100 .*LISTEN" | awk '{print $5}' | while read pid; do taskkill //F //PID $pid 2>/dev/null; done
```

### 3. Drop test databases

First disconnect any lingering sessions:

```bash
PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname LIKE '_test%' AND pid <> pg_backend_pid();"
```

Then drop. The test database name follows the pattern `_{testname}db` (lowercase):

```bash
PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -c "DROP DATABASE IF EXISTS _testfeaturenamedb;"
```

To find all test databases:

```bash
PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -t -c "SELECT datname FROM pg_database WHERE datname LIKE '_test%';"
```

### 4. Remove test app directories

Test apps are created in the repo root with `_test` prefix and are gitignored by `_test*/`.

```bash
rm -rf _testFeatureName/
```

**Common issue:** Windows file locks from VS Code indexer, node_modules, or log files. If `rm -rf` fails:

1. Wait 2-3 seconds after killing processes, then retry
2. If a specific file is "Device or resource busy", it's usually a log file or node_modules — safe to ignore, it will clear on reboot
3. As last resort, close VS Code and retry

To find all test directories:

```bash
ls -d _test*/ 2>/dev/null || echo "No test directories"
```

### 5. Verify clean workspace

```bash
git status
```

Expected: `nothing to commit, working tree clean` — test directories are gitignored so they don't affect git state.

Verify no test processes running:

```bash
netstat -ano | grep -E ":(5100|3100) .*LISTEN"
```

Expected: no output.

### 6. Verify dev instance ports are free

The dev instance uses ports 5000 (backend) and 3000 (frontend). Verify they're not accidentally occupied by test processes:

```bash
netstat -ano | grep -E ":(5000|3000) .*LISTEN"
```

## Port Convention

| Instance | Backend | Frontend |
|----------|---------|----------|
| Dev      | 5000    | 3000     |
| Test     | 5100    | 3100     |

## Database Credentials

- **PostgreSQL**: User `postgres`, password `123456`, local port 5432
- **psql path**: `C:\Program Files\PostgreSQL\16\bin\psql.exe`
- Test databases always use `PGPASSWORD` env var to avoid interactive password prompt

## Docker Services (shared, do NOT stop)

These Docker containers are shared between dev and test instances. Do NOT stop them during cleanup:

- **Mailpit** (1025/8025) — dev SMTP
- **Redis** (`Bookify.Redis` on 6379) — distributed cache
- **MinIO** (9000/9001) — S3-compatible file storage
- **RabbitMQ** (5672/15672) — message broker
- **Jaeger** (16686/4317/4318) — distributed tracing
- **Prometheus** (9090) — metrics

## Known Issues

### Stubborn `_testModuleArch` type directories
When using underscore-prefixed names, node_modules and log files sometimes hold file locks on Windows. The directories are gitignored and harmless. They'll clear on reboot or when VS Code releases the indexer lock.

### Background task notifications after cleanup
If test servers were started via `run_in_background`, you may receive "failed" task notifications after killing the processes. These are expected and can be ignored — they're just the background runner reporting that the process exited.

## Quick One-Liner (Full Cleanup)

For a known test app name (e.g., `_testModArch`):

```bash
# Kill processes
taskkill //F //IM dotnet.exe 2>/dev/null; taskkill //F //IM node.exe 2>/dev/null; sleep 3

# Drop DB (replace _testmodarchdb with actual name)
PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname LIKE '_test%' AND pid <> pg_backend_pid();" && PGPASSWORD=123456 "C:/Program Files/PostgreSQL/16/bin/psql.exe" -U postgres -c "DROP DATABASE IF EXISTS _testmodarchdb;"

# Remove directory
rm -rf _testModArch/

# Verify
git status && netstat -ano | grep -E ":(5100|3100) .*LISTEN" || echo "Clean"
```
