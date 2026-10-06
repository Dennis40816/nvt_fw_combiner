#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$repo_root/scripts/install-dotnet.sh" --scope repository
export DOTNET_ROOT="$repo_root/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
python_bin="$(command -v python3 || command -v python)"
"$python_bin" -B "$repo_root/scripts/fetch_core_packages.py" --manifest "$repo_root/core-packages.json"
dotnet restore "$repo_root/NvtFwCombiner.slnx"
dotnet build "$repo_root/NvtFwCombiner.slnx" -c Debug --no-restore
