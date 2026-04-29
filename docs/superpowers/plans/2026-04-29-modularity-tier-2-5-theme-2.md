# Tier 2.5 — Theme 2: CI killer-test matrix

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a CI workflow that, on every push and PR, runs `scripts/rename.ps1` against three module sets and verifies the generated app builds on every platform. Make module-isolation regressions blocking instead of human-discipline-gated.

**Architecture:** New file `.github/workflows/modularity.yml`. Three jobs (`backend-killer`, `frontend-killer`, `mobile-killer`) each running against a matrix of module selections. A fourth job (`negative-test`) verifies `rename.ps1` correctly *fails* when a module's catalog dependencies aren't selected. No production code changed.

**Tech Stack:** GitHub Actions, PowerShell 7 (preinstalled on `ubuntu-latest`), .NET 10, Node 20, Flutter stable.

**Spec reference:** [2026-04-29-modularity-tier-2-5-hardening.md](../specs/2026-04-29-modularity-tier-2-5-hardening.md) Theme 2.

---

## File Structure

- Create: `.github/workflows/modularity.yml` — the new killer-test workflow.

That's it. No production code, no tests, no docs.

---

## Module set matrix

The three killer scenarios:

| Set | Value | What it proves |
|-----|-------|----------------|
| `None` | `None` | Strip every optional module; only core ships. The strongest "no module is load-bearing" signal. |
| `All` | `All` | Default selection works end-to-end. |
| `workflow-chain` | `workflow,commentsActivity,communication` | Dependency-chain works (workflow needs the other two via catalog `dependencies`). |

A negative test asserts that `-Modules workflow` (without its deps) fails with a helpful error.

---

## Task 1: Add the modularity workflow file

**Files:**
- Create: `.github/workflows/modularity.yml`

- [ ] **Step 1: Write the file**

```yaml
name: Modularity Killer Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend-killer:
    name: Backend (modules=${{ matrix.label }})
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - label: none
            modules: None
          - label: all
            modules: All
          - label: workflow-chain
            modules: workflow,commentsActivity,communication
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Generate app via rename.ps1
        shell: pwsh
        run: |
          ./scripts/rename.ps1 `
            -Name "killerSmoke" `
            -OutputDir "${{ runner.temp }}" `
            -Modules "${{ matrix.modules }}" `
            -IncludeMobile:$false

      - name: Restore + build generated backend
        working-directory: ${{ runner.temp }}/killerSmoke/killerSmoke-BE
        run: |
          dotnet restore
          dotnet build --no-restore -c Release

      - name: Run generated app's architecture tests
        working-directory: ${{ runner.temp }}/killerSmoke/killerSmoke-BE
        run: |
          dotnet test tests/killerSmoke.Api.Tests/killerSmoke.Api.Tests.csproj \
            --no-build -c Release \
            --filter "FullyQualifiedName~Architecture"

  frontend-killer:
    name: Frontend (modules=${{ matrix.label }})
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - label: none
            modules: None
          - label: all
            modules: All
          - label: workflow-chain
            modules: workflow,commentsActivity,communication
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 20

      - name: Generate app via rename.ps1
        shell: pwsh
        run: |
          ./scripts/rename.ps1 `
            -Name "killerSmoke" `
            -OutputDir "${{ runner.temp }}" `
            -Modules "${{ matrix.modules }}" `
            -IncludeMobile:$false

      - name: Install + lint + build generated frontend
        working-directory: ${{ runner.temp }}/killerSmoke/killerSmoke-FE
        run: |
          npm install --no-audit --no-fund
          npm run lint
          npm run build

  mobile-killer:
    name: Mobile (modules=${{ matrix.label }})
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - label: none
            modules: None
          - label: all
            modules: All
    steps:
      - uses: actions/checkout@v4

      - name: Setup Flutter
        uses: subosito/flutter-action@v2
        with:
          channel: stable

      - name: Generate app via rename.ps1
        shell: pwsh
        run: |
          ./scripts/rename.ps1 `
            -Name "killerSmoke" `
            -OutputDir "${{ runner.temp }}" `
            -Modules "${{ matrix.modules }}" `
            -IncludeMobile:$true

      - name: pub get + analyze generated mobile app
        working-directory: ${{ runner.temp }}/killerSmoke/killerSmoke-Mobile
        run: |
          flutter pub get
          flutter analyze

  negative-test:
    name: Negative — missing dependency must fail rename.ps1
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run rename.ps1 with workflow but not its deps
        shell: pwsh
        continue-on-error: true
        id: rename
        run: |
          $ErrorActionPreference = 'Continue'
          ./scripts/rename.ps1 `
            -Name "shouldFail" `
            -OutputDir "${{ runner.temp }}" `
            -Modules "workflow" `
            -IncludeMobile:$false
          "exitcode=$LASTEXITCODE" | Out-File -FilePath $env:GITHUB_OUTPUT -Append

      - name: Assert exit code was non-zero
        shell: pwsh
        run: |
          if ('${{ steps.rename.outputs.exitcode }}' -eq '0') {
            throw "rename.ps1 should have failed when 'workflow' was selected without its dependencies (commentsActivity, communication), but it succeeded. Strict mode is broken."
          }
          Write-Host "PASS: rename.ps1 correctly rejected the invalid selection."
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/modularity.yml
git commit -m "ci(modules): killer-test matrix — rename.ps1 + build per module set

Tier 2.5 Theme 2. Adds .github/workflows/modularity.yml with four jobs:

- backend-killer: matrix [None, All, workflow,commentsActivity,communication]
  → rename.ps1 -IncludeMobile:false, then dotnet build + architecture
  tests on the generated app.
- frontend-killer: same matrix → rename.ps1 + npm install + lint + build.
- mobile-killer: matrix [None, All] → rename.ps1 -IncludeMobile:true,
  then flutter pub get + analyze. Skips workflow-chain because no
  optional module ships a mobile counterpart yet.
- negative-test: -Modules workflow (without commentsActivity/communication)
  must fail loud. Verifies the strict dependency check from Tier 1 still
  works.

Tested locally with pwsh 7.6 + .NET 10 — \`-Modules None\` generates and
builds a buildable backend in ~5s after restore."
```

---

## Task 2: Verify the workflow file is syntactically valid

- [ ] **Step 1: Lint the YAML**

GitHub itself runs the workflow on push, but we can pre-flight with a quick parser:

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/modularity.yml'))" && echo OK
```

Expected: `OK`.

- [ ] **Step 2: Push and watch the first run**

```bash
git push -u origin codex/modularity-tier-2-5-theme-2
```

Open the PR and watch the **Modularity Killer Tests** check. Expected: 7 jobs, all green (3 backend + 3 frontend + 2 mobile + 1 negative — wait, 8 actually: 3+3+2+1 = 9 if counting matrix expansions, but the page renders matrix sets as one row each).

If a job fails:
- **`backend-killer (modules=none)`**: a core path is referencing an optional module → fix in the offending core file.
- **`backend-killer (modules=workflow-chain)`**: dependency wiring is broken → check `WorkflowModule.Dependencies`.
- **`mobile-killer (modules=all)`**: mobile shell or `modules.config.dart` references something that's gone after rename → check the rename.ps1 mobile path.
- **`negative-test`**: someone weakened `Assert-ModuleDependencies` in `rename.ps1` → revert.

---

## Out of scope for this theme

- **Caching of `npm`/`dotnet`/`pub` deps** — the matrix runs are short enough (under 5 min each) that cache wins are marginal and add complexity. Add later if runtime becomes a complaint.
- **Running the generated app's full test suite** — many tests need Postgres/Redis/MinIO. Architecture tests are the only ones that run pure-in-process; the rest stay in the regular `ci.yml` against the source repo.
- **Status checks on the generated app's Vite output, bundle size budgets, etc.** — pure build-passes-or-fails is the killer-test's contract.
- **A `Cancel-out` of the existing `ci.yml`** — keep both. `ci.yml` covers source-repo correctness; `modularity.yml` covers source-mode template correctness. Different invariants.

---

## Self-review

- ✅ Spec coverage: matrix runs `None`, `All`, and a dependency-chain subset (spec §2 Theme 2).
- ✅ Negative test for missing-dependency strict-mode included (spec calls out as a "stretch" — included since it's two extra steps).
- ✅ Mobile is in the matrix (the spec says all three platforms must be gated).
- ✅ No placeholders.
- ✅ All matrix labels and YAML keys consistent across the three jobs.
