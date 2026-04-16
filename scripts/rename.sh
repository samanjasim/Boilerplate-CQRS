#!/usr/bin/env bash
#
# Creates a new project from the Starter boilerplate with a custom name.
#
# Usage:
#   ./scripts/rename.sh <ProjectName> [OutputDir]
#
# Example:
#   ./scripts/rename.sh MyApp
#   ./scripts/rename.sh EduPay /home/user/projects

set -euo pipefail

# ── Arguments ───────────────────────────────────────────────────────────────

NAME="${1:-}"
OUTPUT_DIR="${2:-}"

if [ -z "$NAME" ]; then
    echo "Usage: $0 <ProjectName> [OutputDir]"
    echo "  ProjectName must be a valid C# identifier."
    exit 1
fi

if ! echo "$NAME" | grep -qE '^[A-Za-z_][A-Za-z0-9_]*$'; then
    echo "Error: Invalid project name '$NAME'. Must be a valid C# identifier."
    exit 1
fi

if [ "$NAME" = "Starter" ]; then
    echo "Error: Project name cannot be 'Starter' — that is the placeholder name."
    exit 1
fi

# ── Paths ───────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

if [ -z "$OUTPUT_DIR" ]; then
    OUTPUT_DIR="$(dirname "$REPO_ROOT")"
fi

TARGET_ROOT="$OUTPUT_DIR/$NAME"
TARGET_BE="$TARGET_ROOT/$NAME-BE"
TARGET_FE="$TARGET_ROOT/$NAME-FE"

SOURCE_BE="$REPO_ROOT/boilerplateBE"
SOURCE_FE="$REPO_ROOT/boilerplateFE"

if [ -d "$TARGET_ROOT" ]; then
    echo "Error: Target directory '$TARGET_ROOT' already exists. Remove it first."
    exit 1
fi

if [ ! -d "$SOURCE_BE" ]; then
    echo "Error: Backend boilerplate not found at '$SOURCE_BE'."
    exit 1
fi

# ── Lowercase variant ──────────────────────────────────────────────────────

NAME_LOWER="$(echo "$NAME" | tr '[:upper:]' '[:lower:]')"

# ── Copy ────────────────────────────────────────────────────────────────────

echo "Creating project '$NAME' at $TARGET_ROOT ..."

mkdir -p "$TARGET_ROOT"

echo "  Copying backend boilerplate..."
cp -r "$SOURCE_BE" "$TARGET_BE"

if [ -d "$SOURCE_FE" ]; then
    echo "  Copying frontend boilerplate..."
    cp -r "$SOURCE_FE" "$TARGET_FE"
fi

# Copy GitHub Actions workflows if they exist
if [ -d "$REPO_ROOT/.github" ]; then
    echo "  Copying GitHub Actions workflows..."
    cp -r "$REPO_ROOT/.github" "$TARGET_ROOT/.github"
fi

# ── Clean build artifacts ──────────────────────────────────────────────────

echo "  Cleaning build artifacts..."
find "$TARGET_ROOT" -type d \( -name "bin" -o -name "obj" -o -name ".vs" -o -name "node_modules" -o -name "logs" \) -exec rm -rf {} + 2>/dev/null || true

# ── File content replacement ────────────────────────────────────────────────

echo "  Replacing file contents..."

MODIFIED=0

# Find all text files to process
find "$TARGET_ROOT" -type f \( \
    -name "*.cs" -o -name "*.csproj" -o -name "*.sln" -o -name "*.json" \
    -o -name "*.md" -o -name "*.yml" -o -name "*.yaml" -o -name "*.xml" \
    -o -name "*.props" -o -name "*.targets" -o -name "Dockerfile" \
    -o -name ".dockerignore" -o -name ".gitignore" -o -name "*.sh" \
    -o -name "*.ps1" -o -name "*.toml" \
    -o -name "*.ts" -o -name "*.tsx" -o -name "*.css" -o -name "*.html" \
\) | while IFS= read -r file; do
    if grep -q "Starter\|starter" "$file" 2>/dev/null; then
        # Order matters: longer matches first
        sed -i '' \
            -e "s|Starter\.Infrastructure\.Identity|${NAME}.Infrastructure.Identity|g" \
            -e "s|Starter\.Infrastructure|${NAME}.Infrastructure|g" \
            -e "s|Starter\.Application|${NAME}.Application|g" \
            -e "s|Starter\.Domain|${NAME}.Domain|g" \
            -e "s|Starter\.Shared|${NAME}.Shared|g" \
            -e "s|Starter\.Api\.Tests|${NAME}.Api.Tests|g" \
            -e "s|Starter\.Api|${NAME}.Api|g" \
            -e "s|Starter\.Client|${NAME}.Client|g" \
            -e "s|StarterPolicy|${NAME}Policy|g" \
            -e "s|starter-fe|${NAME_LOWER}-fe|g" \
            -e "s|starter-ui|${NAME_LOWER}-ui|g" \
            -e "s|starter-auth|${NAME_LOWER}-auth|g" \
            -e "s|starter_access_token|${NAME_LOWER}_access_token|g" \
            -e "s|starter_refresh_token|${NAME_LOWER}_refresh_token|g" \
            -e "s|starterdb|${NAME_LOWER}db|g" \
            -e "s|starter-\.log|${NAME_LOWER}-.log|g" \
            -e "s|starter\.com|${NAME_LOWER}.com|g" \
            -e "s|\"Starter:\"|\"${NAME}:\"|g" \
            -e "s|Starter API|${NAME} API|g" \
            -e "s|Starter Team|${NAME} Team|g" \
            -e "s|Copyright (c) Starter|Copyright (c) ${NAME}|g" \
            -e "s|<Company>Starter</Company>|<Company>${NAME}</Company>|g" \
            -e "s|<Product>Starter</Product>|<Product>${NAME}</Product>|g" \
            -e "s|Starter|${NAME}|g" \
            -e "s|starter|${NAME_LOWER}|g" \
            "$file"
        MODIFIED=$((MODIFIED + 1))
    fi
done

echo "  Content replacement complete."

# ── Rename files (deepest first) ────────────────────────────────────────────

echo "  Renaming files..."

find "$TARGET_ROOT" -type f -name "*Starter*" | sort -r | while IFS= read -r file; do
    dir="$(dirname "$file")"
    oldname="$(basename "$file")"
    newname="${oldname//Starter/$NAME}"
    if [ "$oldname" != "$newname" ]; then
        mv "$file" "$dir/$newname"
    fi
done

# ── Rename directories (deepest first) ──────────────────────────────────────

echo "  Renaming directories..."

find "$TARGET_ROOT" -type d -name "*Starter*" | sort -r | while IFS= read -r dir; do
    parent="$(dirname "$dir")"
    oldname="$(basename "$dir")"
    newname="${oldname//Starter/$NAME}"
    if [ "$oldname" != "$newname" ]; then
        mv "$dir" "$parent/$newname"
    fi
done

# ── Summary ─────────────────────────────────────────────────────────────────

echo ""
echo "Project '$NAME' created successfully!"
echo ""
echo "Location: $TARGET_ROOT"
echo ""
echo "Next steps:"
echo "  Backend:"
echo "    1. cd \"$TARGET_BE\""
echo "    2. dotnet build"
echo "    3. Update appsettings.Development.json with your database connection string"
echo "    4. dotnet ef migrations add InitialCreate --project src/${NAME}.Infrastructure --startup-project src/${NAME}.Api"
echo "    5. dotnet run --project src/${NAME}.Api"
echo ""
echo "  Frontend:"
echo "    1. cd \"$TARGET_FE\""
echo "    2. npm install"
echo "    3. Update .env with your API URL"
echo "    4. npm run dev"
echo ""

# ── Verify ──────────────────────────────────────────────────────────────────

LEFTOVER=$(grep -rl "\bStarter\b" "$TARGET_ROOT" --include="*.cs" --include="*.csproj" --include="*.sln" --include="*.json" --include="*.props" --include="*.ts" --include="*.tsx" --include="*.css" --include="*.html" 2>/dev/null || true)

if [ -n "$LEFTOVER" ]; then
    echo "Warning: Found leftover 'Starter' references in:"
    echo "$LEFTOVER"
else
    echo "Verified: No leftover 'Starter' references found."
fi
