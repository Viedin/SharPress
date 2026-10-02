#!/usr/bin/env bash
# Packs SharPress into ./local-packages and runs the sample against that package instead of the source,
# so you see exactly what a consumer of the NuGet package gets. Extra arguments are passed to `dotnet run`.
set -euo pipefail
cd "$(dirname "$0")"

dotnet pack SharPress/SharPress.csproj -c Release -o local-packages

# NuGet caches a package by version and never re-reads it, so repacking the same version would be
# ignored. Remove the cached copy of the version just packed.
version=$(dotnet msbuild SharPress/SharPress.csproj -getProperty:Version)
rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/sharpress/$version"

dotnet run --project SharPress.Sample -p:UseLocalPackage=true "$@"
