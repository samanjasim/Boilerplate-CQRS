<#
.SYNOPSIS
    Creates a new project from the Starter boilerplate with a custom name.

.DESCRIPTION
    Copies boilerplateBE/ (and boilerplateFE/ if present) into a new directory,
    then renames all files, directories, namespaces, and configuration values
    from "Starter" to the specified project name.

.PARAMETER Name
    The new project name (must be a valid C# identifier, e.g., "MyApp", "EduPay").

.PARAMETER OutputDir
    The output directory where the new project folder will be created.
    Defaults to the parent directory of this script's repository root.

.EXAMPLE
    .\scripts\rename.ps1 -Name "MyApp"
    .\scripts\rename.ps1 -Name "EduPay" -OutputDir "C:\Projects"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

# ── Validation ──────────────────────────────────────────────────────────────

if ($Name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
    Write-Error "Invalid project name '$Name'. Must be a valid C# identifier (letters, digits, underscores; cannot start with a digit)."
    exit 1
}

if ($Name -eq "Starter") {
    Write-Error "Project name cannot be 'Starter' - that is the placeholder name."
    exit 1
}

# ── Paths ───────────────────────────────────────────────────────────────────

$ScriptDir = Split-Path -Parent $PSScriptRoot
if (-not $ScriptDir) { $ScriptDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }

# Repo root is the parent of the scripts/ folder
$RepoRoot = Split-Path -Parent $PSScriptRoot

if (-not $OutputDir) {
    $OutputDir = Split-Path -Parent $RepoRoot
}

$TargetRoot = Join-Path $OutputDir $Name
$TargetBE = Join-Path $TargetRoot "$Name-BE"
$TargetFE = Join-Path $TargetRoot "$Name-FE"

$SourceBE = Join-Path $RepoRoot "boilerplateBE"
$SourceFE = Join-Path $RepoRoot "boilerplateFE"

if (Test-Path $TargetRoot) {
    Write-Error "Target directory '$TargetRoot' already exists. Remove it first or choose a different name."
    exit 1
}

if (-not (Test-Path $SourceBE)) {
    Write-Error "Backend boilerplate not found at '$SourceBE'."
    exit 1
}

# ── Lowercase variant ──────────────────────────────────────────────────────

$NameLower = $Name.ToLower()

# ── Copy ────────────────────────────────────────────────────────────────────

Write-Host "Creating project '$Name' at $TargetRoot ..." -ForegroundColor Cyan

New-Item -ItemType Directory -Path $TargetRoot -Force | Out-Null

# Copy backend
Write-Host "  Copying backend boilerplate..." -ForegroundColor Gray
Copy-Item -Path $SourceBE -Destination $TargetBE -Recurse -Force

# Copy frontend if it exists
if (Test-Path $SourceFE) {
    Write-Host "  Copying frontend boilerplate..." -ForegroundColor Gray
    Copy-Item -Path $SourceFE -Destination $TargetFE -Recurse -Force
}

# ── Clean build artifacts from the copy ─────────────────────────────────────

Write-Host "  Cleaning build artifacts..." -ForegroundColor Gray
$dirsToRemove = @("bin", "obj", ".vs", "node_modules", "logs")
foreach ($dir in $dirsToRemove) {
    Get-ChildItem -Path $TargetRoot -Directory -Recurse -Filter $dir -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# ── File content replacement ────────────────────────────────────────────────

Write-Host "  Replacing file contents..." -ForegroundColor Gray

$fileExtensions = @("*.cs", "*.csproj", "*.sln", "*.json", "*.md", "*.yml", "*.yaml", "*.xml", "*.props", "*.targets", "Dockerfile", "*.dockerignore", "*.sh", "*.ps1", "*.toml", "*.env*", "*.ts", "*.tsx", "*.css", "*.html")

$allFiles = @()
foreach ($ext in $fileExtensions) {
    $allFiles += Get-ChildItem -Path $TargetRoot -Recurse -File -Filter $ext -ErrorAction SilentlyContinue
}

# Also get files without extension that match specific names
$specificFiles = @("Dockerfile", ".dockerignore", ".gitignore", ".editorconfig")
foreach ($fileName in $specificFiles) {
    $allFiles += Get-ChildItem -Path $TargetRoot -Recurse -File -Filter $fileName -ErrorAction SilentlyContinue
}

$allFiles = $allFiles | Sort-Object FullName -Unique

# Replacement pairs — ORDER MATTERS (longer matches first)
$replacements = @(
    # Backend (.NET) specific
    @{ Old = "Starter.Infrastructure.Identity"; New = "$Name.Infrastructure.Identity" },
    @{ Old = "Starter.Infrastructure";          New = "$Name.Infrastructure" },
    @{ Old = "Starter.Application";             New = "$Name.Application" },
    @{ Old = "Starter.Domain";                  New = "$Name.Domain" },
    @{ Old = "Starter.Shared";                  New = "$Name.Shared" },
    @{ Old = "Starter.Api.Tests";               New = "$Name.Api.Tests" },
    @{ Old = "Starter.Api";                     New = "$Name.Api" },
    @{ Old = "Starter.Client";                  New = "$Name.Client" },
    @{ Old = "StarterPolicy";                   New = "${Name}Policy" },
    @{ Old = "<Company>Starter</Company>";      New = "<Company>$Name</Company>" },
    @{ Old = "<Product>Starter</Product>";      New = "<Product>$Name</Product>" },
    # Frontend specific (localStorage keys, package name)
    @{ Old = "starter-fe";                      New = "$NameLower-fe" },
    @{ Old = "starter-ui";                      New = "$NameLower-ui" },
    @{ Old = "starter-auth";                    New = "$NameLower-auth" },
    @{ Old = "starter_access_token";            New = "${NameLower}_access_token" },
    @{ Old = "starter_refresh_token";           New = "${NameLower}_refresh_token" },
    # Shared (both BE and FE)
    @{ Old = "starterdb";                       New = "${NameLower}db" },
    @{ Old = "starter-.log";                    New = "$NameLower-.log" },
    @{ Old = "starter.com";                     New = "$NameLower.com" },
    @{ Old = '"Starter:"';                      New = """${Name}:""" },
    @{ Old = "Starter API";                     New = "$Name API" },
    @{ Old = "Starter Team";                    New = "$Name Team" },
    @{ Old = "Copyright (c) Starter";           New = "Copyright (c) $Name" },
    # Catch-all (must be last)
    @{ Old = "Starter";                         New = $Name },
    @{ Old = "starter";                         New = $NameLower }
)

$modifiedCount = 0
foreach ($file in $allFiles) {
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        $original = $content

        foreach ($r in $replacements) {
            $content = $content.Replace($r.Old, $r.New)
        }

        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($file.FullName, $content)
            $modifiedCount++
        }
    }
    catch {
        Write-Warning "  Could not process file: $($file.FullName) - $_"
    }
}

Write-Host "  Modified $modifiedCount files." -ForegroundColor Gray

# ── Rename files ────────────────────────────────────────────────────────────

Write-Host "  Renaming files..." -ForegroundColor Gray

$filesToRename = Get-ChildItem -Path $TargetRoot -Recurse -File |
    Where-Object { $_.Name -match "Starter" } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($file in $filesToRename) {
    $newName = $file.Name -replace "Starter", $Name
    if ($newName -ne $file.Name) {
        Rename-Item -Path $file.FullName -NewName $newName
    }
}

# ── Rename directories (deepest first) ──────────────────────────────────────

Write-Host "  Renaming directories..." -ForegroundColor Gray

$dirsToRename = Get-ChildItem -Path $TargetRoot -Recurse -Directory |
    Where-Object { $_.Name -match "Starter" } |
    Sort-Object { $_.FullName.Length } -Descending

foreach ($dir in $dirsToRename) {
    $newName = $dir.Name -replace "Starter", $Name
    if ($newName -ne $dir.Name) {
        Rename-Item -Path $dir.FullName -NewName $newName
    }
}

# ── Summary ─────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Project '$Name' created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Location: $TargetRoot" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  Backend:" -ForegroundColor White
Write-Host "    1. cd `"$TargetBE`""
Write-Host "    2. dotnet build"
Write-Host "    3. Update appsettings.Development.json with your database connection string"
Write-Host "    4. dotnet ef migrations add InitialCreate --project src/$Name.Infrastructure --startup-project src/$Name.Api"
Write-Host "    5. dotnet run --project src/$Name.Api"
Write-Host ""
Write-Host "  Frontend:" -ForegroundColor White
Write-Host "    1. cd `"$TargetFE`""
Write-Host "    2. npm install"
Write-Host "    3. Update .env with your API URL"
Write-Host "    4. npm run dev"
Write-Host ""

# ── Verify no leftover Starter references ───────────────────────────────────

$leftover = Get-ChildItem -Path $TargetRoot -Recurse -File -Include "*.cs","*.csproj","*.sln","*.json","*.props","*.ts","*.tsx","*.css","*.html" |
    Select-String -Pattern "\bStarter\b" -SimpleMatch -ErrorAction SilentlyContinue

if ($leftover) {
    Write-Host "Warning: Found leftover 'Starter' references in:" -ForegroundColor Yellow
    $leftover | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber)" -ForegroundColor Yellow }
}
else {
    Write-Host "Verified: No leftover 'Starter' references found." -ForegroundColor Green
}
