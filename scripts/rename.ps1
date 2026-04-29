<#
.SYNOPSIS
    Creates a new project from the Starter boilerplate with a custom name.

.DESCRIPTION
    Copies boilerplateBE/, boilerplateFE/, and boilerplateMobile/ into a new
    directory, then renames all files, directories, namespaces, package IDs,
    and configuration values from "Starter" to the specified project name.

.PARAMETER Name
    The new project name (must be a valid C# identifier, e.g., "MyApp", "EduPay").

.PARAMETER OutputDir
    The output directory where the new project folder will be created.
    Defaults to the parent directory of this script's repository root.

.PARAMETER IncludeMobile
    Include the Flutter mobile boilerplate. Default: true.

.PARAMETER MobileMultiTenancy
    Enable multi-tenancy in the mobile app. Default: true.
    When false, sets multiTenancyEnabled to false in both flavor entry points.

.EXAMPLE
    .\scripts\rename.ps1 -Name "MyApp"
    .\scripts\rename.ps1 -Name "EduPay" -OutputDir "C:\Projects" -Modules "billing"
    .\scripts\rename.ps1 -Name "SchoolApp" -IncludeMobile -MobileMultiTenancy:$false -Modules "None"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [string]$Modules,  # Comma-separated list of OPTIONAL modules to include, "All" (default), or "None"

    [Parameter(Mandatory = $false)]
    [bool]$IncludeMobile = $true,  # Include the Flutter mobile boilerplate (default: true)

    [Parameter(Mandatory = $false)]
    [bool]$MobileMultiTenancy = $true  # Enable multi-tenancy in mobile flavors (default: true)
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
$TargetMobile = Join-Path $TargetRoot "$Name-Mobile"

$SourceBE = Join-Path $RepoRoot "boilerplateBE"
$SourceFE = Join-Path $RepoRoot "boilerplateFE"
$SourceMobile = Join-Path $RepoRoot "boilerplateMobile"

if (Test-Path $TargetRoot) {
    Write-Error "Target directory '$TargetRoot' already exists. Remove it first or choose a different name."
    exit 1
}

if (-not (Test-Path $SourceBE)) {
    Write-Error "Backend boilerplate not found at '$SourceBE'."
    exit 1
}

function Get-CatalogModuleProperties {
    param([object]$Catalog)

    return @($Catalog.PSObject.Properties | Where-Object { -not $_.Name.StartsWith("_") })
}

function ConvertTo-SnakeCase {
    param([Parameter(Mandatory = $true)][string]$Value)

    return (($Value -creplace '([A-Z])', '_$1').TrimStart('_').ToLower())
}

function Get-WebModuleSymbol {
    param([Parameter(Mandatory = $true)][string]$ConfigKey)

    return "$($ConfigKey)Module"
}

function Resolve-ModuleSelection {
    param(
        [string]$RequestedModules,
        [string[]]$AllOptional
    )

    if (-not $RequestedModules -or $RequestedModules -eq "All") {
        return @{
            Included = @($AllOptional)
            Excluded = @()
        }
    }

    if ($RequestedModules -eq "None") {
        return @{
            Included = @()
            Excluded = @($AllOptional)
        }
    }

    $requested = @($RequestedModules -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $validLookup = @{}
    foreach ($moduleId in $AllOptional) {
        $validLookup[$moduleId.ToLowerInvariant()] = $moduleId
    }

    $unknown = @()
    $included = @()
    foreach ($moduleId in $requested) {
        $key = $moduleId.ToLowerInvariant()
        if (-not $validLookup.ContainsKey($key)) {
            $unknown += $moduleId
        } else {
            $included += $validLookup[$key]
        }
    }

    if ($unknown.Count -gt 0) {
        $lines = @()
        $lines += ""
        $lines += "ERROR: Unknown module id(s): $($unknown -join ', ')"
        $lines += "Valid optional module ids: $($AllOptional -join ', ')"
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }

    # Dedupe but preserve catalog enumeration order so generated `enabledModules`
    # arrays are stable regardless of the order ids were typed on the CLI.
    $seen = @{}
    $orderedIncluded = @()
    foreach ($id in $AllOptional) {
        if (($included -contains $id) -and -not $seen.ContainsKey($id)) {
            $orderedIncluded += $id
            $seen[$id] = $true
        }
    }
    $included = $orderedIncluded
    $excluded = @($AllOptional | Where-Object { $_ -notin $included })

    return @{
        Included = $included
        Excluded = $excluded
    }
}

function Assert-ModuleDependencies {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional
    )

    $selectedSet = @{}
    foreach ($moduleId in @($IncludedOptional)) { $selectedSet[$moduleId] = $true }

    $missing = @{}
    foreach ($moduleId in @($IncludedOptional)) {
        $entry = $Catalog.$moduleId
        if ($null -eq $entry.dependencies) { continue }
        foreach ($dep in @($entry.dependencies)) {
            if (-not $selectedSet.ContainsKey($dep)) {
                if (-not $missing.ContainsKey($moduleId)) {
                    $missing[$moduleId] = New-Object System.Collections.ArrayList
                }
                [void]$missing[$moduleId].Add($dep)
            }
        }
    }

    if ($missing.Count -gt 0) {
        $allMissing = @{}
        foreach ($mod in $missing.Keys) {
            foreach ($dep in $missing[$mod]) { $allMissing[$dep] = $true }
        }
        $resolvedSelection = @(@($IncludedOptional) + @($allMissing.Keys)) | Sort-Object -Unique
        $lines = @()
        $lines += ""
        $lines += "ERROR: One or more selected modules are missing required dependencies."
        $lines += ""
        foreach ($mod in $missing.Keys) {
            $depList = ($missing[$mod] | Sort-Object) -join ", "
            $lines += "  - '$mod' requires: $depList"
        }
        $lines += ""
        $lines += "Re-run with the full set:"
        $lines += "  -Modules `"$($resolvedSelection -join ',')`""
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }
}

function Assert-SelectedModuleArtifacts {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional,
        [string]$SourceBE,
        [string]$SourceFE,
        [string]$SourceMobile,
        [bool]$IncludeMobile
    )

    $problems = @()

    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId

        if ($module.backendModule) {
            $backendPath = Join-Path (Join-Path (Join-Path $SourceBE "src") "modules") $module.backendModule
            if (-not (Test-Path $backendPath)) {
                $problems += "[$moduleId/backend] Missing template project folder: $backendPath"
            }
        }

        if ($module.frontendFeature) {
            $featurePath = Join-Path (Join-Path (Join-Path $SourceFE "src") "features") $module.frontendFeature
            $indexTs = Join-Path $featurePath "index.ts"
            $indexTsx = Join-Path $featurePath "index.tsx"
            if (-not (Test-Path $featurePath)) {
                $problems += "[$moduleId/web] Missing template feature folder: $featurePath"
            } elseif (-not ((Test-Path $indexTs) -or (Test-Path $indexTsx))) {
                $problems += "[$moduleId/web] Missing feature entrypoint: $indexTs or $indexTsx"
            }
        }

        if ($IncludeMobile -and $module.mobileFolder -and $module.mobileModule) {
            $moduleFile = "$(ConvertTo-SnakeCase -Value $module.mobileModule).dart"
            $mobilePath = Join-Path (Join-Path (Join-Path (Join-Path $SourceMobile "lib") "modules") $module.mobileFolder) $moduleFile
            if (-not (Test-Path $mobilePath)) {
                $problems += "[$moduleId/mobile] Missing mobile entrypoint: $mobilePath"
            }
        }
    }

    if ($problems.Count -gt 0) {
        $lines = @()
        $lines += ""
        $lines += "ERROR: Selected module artifacts are missing from the template."
        $lines += $problems
        $lines += ""
        Write-Error ($lines -join [Environment]::NewLine)
        exit 1
    }
}

function Write-WebModulesConfig {
    param(
        [object]$Catalog,
        [string[]]$AllOptional,
        [string[]]$IncludedOptional,
        [string]$TargetFE
    )

    if (-not (Test-Path $TargetFE)) { return }

    $imports = @()
    $enabled = @()
    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId
        if (-not $module.frontendFeature) { continue }
        $symbol = Get-WebModuleSymbol -ConfigKey $module.configKey
        $imports += "import { $symbol } from '@/features/$($module.frontendFeature)';"
        $enabled += "  $symbol,"
    }

    $moduleNameUnion = @()
    $allIds = @($AllOptional | ForEach-Object { $Catalog.$_.configKey })
    for ($i = 0; $i -lt $allIds.Count; $i++) {
        $moduleNameUnion += "  | '$($allIds[$i])'"
    }

    $derivedFlags = @()
    foreach ($id in $allIds) {
        $derivedFlags += "  $($id): isModuleActive('$id'),"
    }

    $contentLines = @()
    $contentLines += $imports
    if ($imports.Count -gt 0) { $contentLines += "" }
    $contentLines += "import { registerWebModules, type WebModule } from '@/lib/modules';"
    $contentLines += ""
    $contentLines += "/**"
    $contentLines += " * Optional module registry generated by rename.ps1 from modules.catalog.json."
    $contentLines += " * Do not hand-edit generated apps; change the catalog or generator instead."
    $contentLines += " *"
    $contentLines += " * enabledModules is the single source of truth. isModuleActive() and the"
    $contentLines += " * activeModules literal are both derived from it, so the two cannot drift."
    $contentLines += " */"
    $contentLines += "export type ModuleName ="
    $contentLines += $moduleNameUnion
    $contentLines += "  ;"
    $contentLines += ""
    $contentLines += "export const enabledModules: WebModule[] = ["
    $contentLines += $enabled
    $contentLines += "];"
    $contentLines += ""
    $contentLines += "const enabledIds = new Set<string>(enabledModules.map((m) => m.id));"
    $contentLines += ""
    $contentLines += "export function isModuleActive(module: ModuleName): boolean {"
    $contentLines += "  return enabledIds.has(module);"
    $contentLines += "}"
    $contentLines += ""
    $contentLines += "export const activeModules: Readonly<Record<ModuleName, boolean>> = Object.freeze({"
    $contentLines += $derivedFlags
    $contentLines += "});"
    $contentLines += ""
    $contentLines += "export function registerAllModules(): void {"
    $contentLines += "  registerWebModules(enabledModules);"
    $contentLines += "}"

    $modulesConfigTsPath = Join-Path (Join-Path (Join-Path $TargetFE "src") "config") "modules.config.ts"
    Set-Content -Path $modulesConfigTsPath -Value ($contentLines -join [Environment]::NewLine) -NoNewline
}

function Write-MobileModulesConfig {
    param(
        [object]$Catalog,
        [string[]]$IncludedOptional,
        [string]$TargetMobile,
        [string]$PackageName,
        [bool]$IncludeMobile
    )

    if (-not $IncludeMobile -or -not (Test-Path $TargetMobile)) { return }

    $imports = @()
    $instances = @()
    foreach ($moduleId in @($IncludedOptional)) {
        $module = $Catalog.$moduleId
        if (-not ($module.mobileFolder -and $module.mobileModule)) { continue }

        $moduleFile = "$(ConvertTo-SnakeCase -Value $module.mobileModule).dart"
        $imports += "import 'package:$PackageName/modules/$($module.mobileFolder)/$moduleFile';"
        $instances += "      $($module.mobileModule)(),"
    }

    $contentLines = @()
    $contentLines += "import 'package:$PackageName/core/modularity/app_module.dart';"
    if ($imports.Count -gt 0) {
        $contentLines += ""
        $contentLines += $imports
    }
    $contentLines += ""
    $contentLines += "/// Optional modules generated by rename.ps1 from modules.catalog.json."
    $contentLines += "/// Do not hand-edit generated apps; change the catalog or generator instead."
    $contentLines += "List<AppModule> activeModules() => <AppModule>["
    $contentLines += $instances
    $contentLines += "    ];"

    $mobileModulesConfigPath = Join-Path (Join-Path (Join-Path $TargetMobile "lib") "app") "modules.config.dart"
    Set-Content -Path $mobileModulesConfigPath -Value ($contentLines -join [Environment]::NewLine) -NoNewline
}

# ── Module selection preflight ──────────────────────────────────────────────

$modulesJsonPath = Join-Path $RepoRoot "modules.catalog.json"
$excludedModules = @()
$includedOptional = @()
$allRequired = @()
$modulesConfig = $null

if (Test-Path $modulesJsonPath) {
    $modulesConfig = Get-Content $modulesJsonPath -Raw | ConvertFrom-Json
    $allOptional = @()

    foreach ($prop in Get-CatalogModuleProperties -Catalog $modulesConfig) {
        if ($prop.Value.required) {
            $allRequired += $prop.Name
        } else {
            $allOptional += $prop.Name
        }
    }

    $selection = Resolve-ModuleSelection -RequestedModules $Modules -AllOptional $allOptional
    $includedOptional = @($selection.Included)
    $excludedModules = @($selection.Excluded)

    Assert-ModuleDependencies -Catalog $modulesConfig -IncludedOptional $includedOptional
    Assert-SelectedModuleArtifacts `
        -Catalog $modulesConfig `
        -IncludedOptional $includedOptional `
        -SourceBE $SourceBE `
        -SourceFE $SourceFE `
        -SourceMobile $SourceMobile `
        -IncludeMobile $IncludeMobile
}

# ── Lowercase variant ──────────────────────────────────────────────────────

$NameLower = $Name.ToLower()

# Snake-case variant for Dart package name (e.g., "MyApp" → "my_app")
$NameSnake = ($Name -creplace '([A-Z])', '_$1').TrimStart('_').ToLower()
# Bundle ID for mobile (e.g., "com.myapp.app")
$MobileBundleId = "com.$NameLower.app"

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

# Copy mobile if it exists and -IncludeMobile is set
if ($IncludeMobile -and (Test-Path $SourceMobile)) {
    Write-Host "  Copying mobile boilerplate..." -ForegroundColor Gray
    Copy-Item -Path $SourceMobile -Destination $TargetMobile -Recurse -Force
}

# Copy GitHub Actions workflows if they exist
$SourceGitHub = Join-Path $RepoRoot ".github"
if (Test-Path $SourceGitHub) {
    Write-Host "  Copying GitHub Actions workflows..." -ForegroundColor Gray
    Copy-Item -Path $SourceGitHub -Destination (Join-Path $TargetRoot ".github") -Recurse -Force
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

$fileExtensions = @("*.cs", "*.csproj", "*.sln", "*.json", "*.md", "*.yml", "*.yaml", "*.xml", "*.props", "*.targets", "Dockerfile", "*.dockerignore", "*.sh", "*.ps1", "*.toml", "*.env*", "*.ts", "*.tsx", "*.css", "*.html", "*.dart", "*.arb", "*.gradle", "*.gradle.kts", "*.pbxproj", "*.plist", "*.xcscheme", "*.xcconfig")

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

# ── Mobile-specific renaming ────────────────────────────────────────────────

if ($IncludeMobile -and (Test-Path $TargetMobile)) {
    Write-Host "  Renaming mobile package identifiers..." -ForegroundColor Gray

    # 1. Rename Dart package in pubspec.yaml
    $pubspecPath = Join-Path $TargetMobile "pubspec.yaml"
    if (Test-Path $pubspecPath) {
        $pubContent = Get-Content $pubspecPath -Raw
        $pubContent = $pubContent -replace "name:\s*boilerplate_mobile", "name: $NameSnake"
        Set-Content -Path $pubspecPath -Value $pubContent -NoNewline
    }

    # 2. Replace all Dart package imports
    $dartFiles = Get-ChildItem -Path $TargetMobile -Recurse -File -Filter "*.dart" -ErrorAction SilentlyContinue
    foreach ($df in $dartFiles) {
        try {
            $dc = [System.IO.File]::ReadAllText($df.FullName)
            $orig = $dc
            $dc = $dc.Replace("package:boilerplate_mobile/", "package:${NameSnake}/")
            if ($dc -ne $orig) {
                [System.IO.File]::WriteAllText($df.FullName, $dc)
            }
        } catch {
            Write-Warning "  Could not process Dart file: $($df.FullName) - $_"
        }
    }

    # 3. Android: update applicationId and namespace in build.gradle.kts
    # NOTE: The general replacement already changed com.starter → com.{namelower},
    # so we match com.{anything}.boilerplate_mobile (not com.starter specifically).
    $gradlePath = Join-Path (Join-Path (Join-Path $TargetMobile "android") "app") "build.gradle.kts"
    if (Test-Path $gradlePath) {
        $gc = Get-Content $gradlePath -Raw
        $gc = $gc -replace 'com\.\w+\.boilerplate_mobile', $MobileBundleId
        # Update app_name resValues (already renamed by general pass, match current name)
        $nameEscaped = [regex]::Escape($Name)
        $gc = $gc -replace "resValue\(`"string`",\s*`"app_name`",\s*`"$nameEscaped Staging`"\)", "resValue(`"string`", `"app_name`", `"$Name Staging`")"
        $gc = $gc -replace "resValue\(`"string`",\s*`"app_name`",\s*`"$nameEscaped`"\)", "resValue(`"string`", `"app_name`", `"$Name`")"
        Set-Content -Path $gradlePath -Value $gc -NoNewline
    }

    # 4. Android: update AndroidManifest.xml package (if present as attribute)
    $manifestPath = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $TargetMobile "android") "app") "src") "main") "AndroidManifest.xml"
    if (Test-Path $manifestPath) {
        $mc = Get-Content $manifestPath -Raw
        $mc = $mc -replace 'com\.\w+\.boilerplate_mobile', $MobileBundleId
        Set-Content -Path $manifestPath -Value $mc -NoNewline
    }

    # 5. Android: recreate kotlin directory structure with correct bundle ID
    # The general rename pass may have mangled the old com/starter/ directory,
    # so we delete everything under kotlin/ and create the correct structure.
    $kotlinRoot = Join-Path $TargetMobile "android/app/src/main/kotlin"
    if (Test-Path $kotlinRoot) {
        Get-ChildItem -Path $kotlinRoot -Directory -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

        # Create new path: com/{name}/app/
        $bundleParts = $MobileBundleId -split '\.'
        $newKotlinPath = $kotlinRoot
        foreach ($part in $bundleParts) {
            $newKotlinPath = Join-Path $newKotlinPath $part
        }
        New-Item -ItemType Directory -Path $newKotlinPath -Force | Out-Null

        # Write MainActivity.kt with the correct package
        $mainActivityContent = @"
package $MobileBundleId

import io.flutter.embedding.android.FlutterActivity

class MainActivity: FlutterActivity()
"@
        Set-Content -Path (Join-Path $newKotlinPath "MainActivity.kt") -Value $mainActivityContent -NoNewline
    }

    # 6. iOS: update PRODUCT_BUNDLE_IDENTIFIER in project.pbxproj
    $pbxprojPath = Join-Path (Join-Path (Join-Path $TargetMobile "ios") "Runner.xcodeproj") "project.pbxproj"
    if (Test-Path $pbxprojPath) {
        $pc = Get-Content $pbxprojPath -Raw
        $pc = $pc -replace 'com\.\w+\.boilerplateMobile', $MobileBundleId
        Set-Content -Path $pbxprojPath -Value $pc -NoNewline
    }

    # 7. Rewrite flavor configs (API base URLs and app names in Dart entry points)
    $mainStagingPath = Join-Path (Join-Path $TargetMobile "lib") "main_staging.dart"
    $mainProdPath = Join-Path (Join-Path $TargetMobile "lib") "main_prod.dart"
    foreach ($entryPoint in @($mainStagingPath, $mainProdPath)) {
        if (Test-Path $entryPoint) {
            $epContent = Get-Content $entryPoint -Raw
            $epContent = $epContent.Replace("package:boilerplate_mobile/", "package:${NameSnake}/")
            $epContent = $epContent -replace "appName:\s*'Starter Staging'", "appName: '$Name Staging'"
            $epContent = $epContent -replace "appName:\s*'Starter'", "appName: '$Name'"
            Set-Content -Path $entryPoint -Value $epContent -NoNewline
        }
    }

    # 8. Toggle multi-tenancy flag if -MobileMultiTenancy is $false
    if (-not $MobileMultiTenancy) {
        foreach ($entryPoint in @($mainStagingPath, $mainProdPath)) {
            if (Test-Path $entryPoint) {
                $epContent = Get-Content $entryPoint -Raw
                $epContent = $epContent -replace "multiTenancyEnabled:\s*true", "multiTenancyEnabled: false"
                Set-Content -Path $entryPoint -Value $epContent -NoNewline
            }
        }
        Write-Host "    Multi-tenancy disabled in mobile flavors." -ForegroundColor Yellow
    }

    Write-Host "  Mobile package renamed to '$NameSnake' (bundle: $MobileBundleId)" -ForegroundColor Gray
}

# ── Module selection (remove excluded modules) ──────────────────────────────

if ($null -ne $modulesConfig) {
    Write-Host "  Processing module selection..." -ForegroundColor Gray

    # Remove each excluded module
    foreach ($moduleKey in $excludedModules) {
        $module = $modulesConfig.$moduleKey
        $backendModuleName = $module.backendModule -replace "Starter", $Name
        $frontendFeature = $module.frontendFeature

        # 1. Delete backend module project folder
        $beModulePath = Join-Path (Join-Path (Join-Path $TargetBE "src") "modules") $backendModuleName
        if (Test-Path $beModulePath) {
            Remove-Item $beModulePath -Recurse -Force -ErrorAction SilentlyContinue
        }

        # 2. Remove ProjectReference from Api.csproj
        $apiCsprojPath = Join-Path (Join-Path (Join-Path $TargetBE "src") "$Name.Api") "$Name.Api.csproj"
        if (Test-Path $apiCsprojPath) {
            $csprojContent = Get-Content $apiCsprojPath -Raw
            $escaped = [regex]::Escape($backendModuleName)
            $csprojContent = $csprojContent -replace "(?m)^\s*<ProjectReference Include=`"[^`"]*$escaped[^`"]*`"\s*/>\s*\r?\n?", ""
            Set-Content -Path $apiCsprojPath -Value $csprojContent -NoNewline
        }

        # 3. Remove project entry from solution file (+ NestedProjects entry for its GUID)
        $slnFile = Get-ChildItem -Path $TargetBE -Filter "*.sln" -File | Select-Object -First 1
        if ($slnFile) {
            $slnContent = Get-Content $slnFile.FullName -Raw
            $escapedName = [regex]::Escape($backendModuleName)

            # Extract the project's GUID before removing the block
            $projectGuid = $null
            if ($slnContent -match "(?ms)Project\(`"\{[^}]+\}`"\)\s*=\s*`"$escapedName`"[^\r\n]*,\s*`"\{([0-9A-Fa-f-]+)\}`"") {
                $projectGuid = $matches[1]
            }

            # Remove the Project/EndProject block
            $pattern = "(?ms)Project\(`"\{[^}]+\}`"\)\s*=\s*`"$escapedName`".*?EndProject\s*\r?\n?"
            $slnContent = $slnContent -replace $pattern, ""

            # Remove the NestedProjects entry for this GUID (if we captured it)
            if ($projectGuid) {
                $nestedPattern = "(?m)^\s*\{$projectGuid\}\s*=\s*\{[^}]+\}\s*\r?\n?"
                $slnContent = $slnContent -replace $nestedPattern, ""
            }

            Set-Content -Path $slnFile.FullName -Value $slnContent -NoNewline
        }

        # 3b. Remove AI-only tooling when the AI module is excluded. The warmup
        # tool imports AI module types directly, so leaving it in a reduced
        # solution breaks `dotnet build`.
        if ($moduleKey -eq "ai") {
            $evalToolPath = Join-Path (Join-Path $TargetBE "tools") "EvalCacheWarmup"
            if (Test-Path $evalToolPath) {
                Remove-Item $evalToolPath -Recurse -Force -ErrorAction SilentlyContinue
            }

            if ($slnFile) {
                $slnContent = Get-Content $slnFile.FullName -Raw
                $toolGuid = $null
                if ($slnContent -match "(?ms)Project\(`"\{[^}]+\}`"\)\s*=\s*`"EvalCacheWarmup`"[^\r\n]*,\s*`"\{([0-9A-Fa-f-]+)\}`"") {
                    $toolGuid = $matches[1]
                }

                $toolPattern = "(?ms)Project\(`"\{[^}]+\}`"\)\s*=\s*`"EvalCacheWarmup`".*?EndProject\s*\r?\n?"
                $slnContent = $slnContent -replace $toolPattern, ""

                if ($toolGuid) {
                    $nestedPattern = "(?m)^\s*\{$toolGuid\}\s*=\s*\{[^}]+\}\s*\r?\n?"
                    $slnContent = $slnContent -replace $nestedPattern, ""
                }

                Set-Content -Path $slnFile.FullName -Value $slnContent -NoNewline
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($frontendFeature)) {
            # Delete frontend feature folder. Optional routes/nav/slot wiring
            # lives entirely inside the feature folder and is excluded from
            # the generated app via Write-WebModulesConfig — no surgical
            # rewrites against core source files needed.
            $feFolderPath = Join-Path (Join-Path (Join-Path $TargetFE "src") "features") $frontendFeature
            if (Test-Path $feFolderPath) {
                Remove-Item $feFolderPath -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        # 5. Delete module-owned test folder under tests/{Name}.Api.Tests/
        # Tests for each module live in tests/Starter.Api.Tests/{testsFolder}/. Leaving
        # them behind after the module is removed orphans references to deleted types
        # and breaks `dotnet build`.
        $testsFolder = $module.testsFolder
        if ($testsFolder) {
            $testsPath = Join-Path (Join-Path (Join-Path $TargetBE "tests") "$Name.Api.Tests") $testsFolder
            if (Test-Path $testsPath) {
                Remove-Item $testsPath -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        # 6. Delete mobile module folder
        if ($IncludeMobile -and (Test-Path $TargetMobile)) {
            $mobileFolder = $module.mobileFolder
            $mobileModuleName = $module.mobileModule

            if ($mobileFolder -and $mobileModuleName) {
                $mobileFolderPath = Join-Path (Join-Path (Join-Path $TargetMobile "lib") "modules") $mobileFolder
                if (Test-Path $mobileFolderPath) {
                    Remove-Item $mobileFolderPath -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
        }

        Write-Host "    - Removed: $($module.displayName)" -ForegroundColor Yellow
    }

    Write-WebModulesConfig `
        -Catalog $modulesConfig `
        -AllOptional $allOptional `
        -IncludedOptional $includedOptional `
        -TargetFE $TargetFE

    Write-MobileModulesConfig `
        -Catalog $modulesConfig `
        -IncludedOptional $includedOptional `
        -TargetMobile $TargetMobile `
        -PackageName $NameSnake `
        -IncludeMobile $IncludeMobile

    if ($excludedModules.Count -eq 0 -and $includedOptional.Count -gt 0) {
        Write-Host "    All modules included." -ForegroundColor Gray
    }
}

# ── Summary ─────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Project '$Name' created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Location: $TargetRoot" -ForegroundColor Yellow
Write-Host ""

if ($allRequired.Count -gt 0 -or $includedOptional.Count -gt 0 -or $excludedModules.Count -gt 0) {
    Write-Host "Modules:" -ForegroundColor Cyan
    if ($allRequired.Count -gt 0) {
        Write-Host "  Required (always included): $($allRequired -join ', ')" -ForegroundColor White
    }
    if ($includedOptional.Count -gt 0) {
        Write-Host "  Optional (included): $($includedOptional -join ', ')" -ForegroundColor Green
    }
    if ($excludedModules.Count -gt 0) {
        Write-Host "  Optional (excluded): $($excludedModules -join ', ')" -ForegroundColor DarkGray
    }
    Write-Host ""
}

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

if ($IncludeMobile -and (Test-Path $TargetMobile)) {
    Write-Host "  Mobile:" -ForegroundColor White
    Write-Host "    1. cd `"$TargetMobile`""
    Write-Host "    2. flutter pub get"
    Write-Host "    3. dart run build_runner build --delete-conflicting-outputs"
    Write-Host "    4. flutter run --flavor staging -t lib/main_staging.dart"
    Write-Host ""
}

# ── Verify no leftover Starter references ───────────────────────────────────

$leftover = Get-ChildItem -Path $TargetRoot -Recurse -File -Include "*.cs","*.csproj","*.sln","*.json","*.props","*.ts","*.tsx","*.css","*.html","*.dart","*.arb" |
    Select-String -Pattern "\bStarter\b" -SimpleMatch -ErrorAction SilentlyContinue

if ($leftover) {
    Write-Host "Warning: Found leftover 'Starter' references in:" -ForegroundColor Yellow
    $leftover | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber)" -ForegroundColor Yellow }
}
else {
    Write-Host "Verified: No leftover 'Starter' references found." -ForegroundColor Green
}
