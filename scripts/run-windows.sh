#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
executable="$repository_root/artifacts/windows/win-x64/Osynt.Desktop.exe"

if ! command -v wslpath >/dev/null 2>&1; then
  printf 'Run this script inside WSL with Windows interop enabled.\n' >&2
  exit 1
fi

if [[ ! -f "$executable" ]]; then
  printf 'Build Windows first: bash scripts/publish-windows.sh\n' >&2
  exit 1
fi

# WSL interop launches this PE executable on Windows, rather than through WSLg.
exec "$executable"
