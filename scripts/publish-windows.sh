#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
target_runtime="${1:-win-x64}"

case "$target_runtime" in
  win-x64|win-arm64) ;;
  *) printf 'Choose win-x64 or win-arm64.\n' >&2; exit 1 ;;
esac

output_directory="$repository_root/artifacts/windows/$target_runtime"

dotnet publish "$repository_root/src/Osynt.Desktop/Osynt.Desktop.csproj" \
  --configuration Release \
  --runtime "$target_runtime" \
  --self-contained true \
  --output "$output_directory" \
  -p:PublishProfile=WindowsPortable \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -m:1

printf '\nWindows build ready: %s/Osynt.Desktop.exe\n' "$output_directory"

if command -v wslpath >/dev/null 2>&1; then
  printf 'Windows path: %s\n' "$(wslpath -w "$output_directory/Osynt.Desktop.exe")"
fi
