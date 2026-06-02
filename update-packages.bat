#!/usr/bin/env bash
set -euo pipefail

if [ -z "${GITEA_TOKEN:-}" ]; then
    echo "ERROR: GITEA_TOKEN environment variable is not set."
    exit 1
fi

TOKEN="${GITEA_TOKEN}"
GITEA_FEED="http://localhost:300/api/packages/Chronos/nuget/index.json"
VERSION_FILE="VERSION.txt"
NUPKG_DIR="nupkgs"

ABSTR_PROJ="src/Chronos.Core.Abstractions/Chronos.Core.Abstractions.csproj"
KERNEL_PROJ="src/Chronos.Core.Kernel/Chronos.Core.Kernel.csproj"

if [ ! -f "$VERSION_FILE" ]; then
    echo "ERROR: $VERSION_FILE not found."
    exit 1
fi

CURRENT_VERSION=$(cat "$VERSION_FILE" | tr -d '[:space:]')
echo "Current base version: $CURRENT_VERSION"

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"
NEXT_PATCH=$((PATCH + 1))
DEFAULT_VERSION="${MAJOR}.${MINOR}.${NEXT_PATCH}"

read -p "Enter new version (default: $DEFAULT_VERSION): " NEW_VERSION
NEW_VERSION=${NEW_VERSION:-$DEFAULT_VERSION}
echo "Using version $NEW_VERSION"

echo "$NEW_VERSION" > "$VERSION_FILE"
echo "Updated $VERSION_FILE to $NEW_VERSION"

rm -rf "$NUPKG_DIR"
mkdir -p "$NUPKG_DIR"

echo -e "\n[1/5] Building Chronos.Core.Abstractions..."
dotnet build "$ABSTR_PROJ" --configuration Release

echo "[2/5] Packing Chronos.Core.Abstractions..."
dotnet build-server shutdown >/dev/null 2>&1
MSBUILDNOINPROCNODE=1 dotnet pack "$ABSTR_PROJ" --no-build --configuration Release \
    -p:PackageVersion="$NEW_VERSION" --output "$NUPKG_DIR" -nodeReuse:false

echo "[3/5] Pushing Chronos.Core.Abstractions to Gitea..."
dotnet nuget push "$NUPKG_DIR/Chronos.Core.Abstractions.$NEW_VERSION.nupkg" \
    --source "$GITEA_FEED" \
    --api-key "$TOKEN" \
    --skip-duplicate

echo -e "\n[4/5] Updating Chronos.Core.Kernel to use Chronos.Core.Abstractions $NEW_VERSION..."
sed -i "s|<PackageReference Include=\"Chronos.Core.Abstractions\" Version=\".*\"|<PackageReference Include=\"Chronos.Core.Abstractions\" Version=\"$NEW_VERSION\"|g" "$KERNEL_PROJ"

export NUGET_USERNAME=sinavali
export NUGET_PASSWORD=$TOKEN
dotnet restore "$KERNEL_PROJ" --source "$GITEA_FEED"

echo -e "\n[5/5] Building and packing Chronos.Core.Kernel..."
dotnet build "$KERNEL_PROJ" --configuration Release

dotnet build-server shutdown >/dev/null 2>&1
MSBUILDNOINPROCNODE=1 dotnet pack "$KERNEL_PROJ" --no-build --configuration Release \
    -p:PackageVersion="$NEW_VERSION" -p:UseLocalAbstractions=false --output "$NUPKG_DIR" -nodeReuse:false

echo "Pushing Chronos.Core.Kernel to Gitea..."
dotnet nuget push "$NUPKG_DIR/Chronos.Core.Kernel.$NEW_VERSION.nupkg" \
    --source "$GITEA_FEED" \
    --api-key "$TOKEN" \
    --skip-duplicate

echo "============================================================"
echo " Chronos.Core.Abstractions $NEW_VERSION and"
echo " Chronos.Core.Kernel $NEW_VERSION published successfully!"
echo "============================================================"
