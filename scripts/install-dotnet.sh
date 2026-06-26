#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
global_json="$repo_root/global.json"
scope="repository"
install_dir=""
architecture="auto"
force=0
persist=0
installer_commit="cbd31355adcf0c63eaeff601fb2eaa5fd0778f2b"
installer_url="https://raw.githubusercontent.com/dotnet/install-scripts/$installer_commit/src/dotnet-install.sh"

usage() {
  cat <<'EOF'
Usage: scripts/install-dotnet.sh [--scope repository|user] [--install-dir PATH]
                                 [--architecture auto|x64|arm64] [--force] [--persist]
EOF
}

while (($#)); do
  case "$1" in
    --scope) scope="$2"; shift 2 ;;
    --install-dir) install_dir="$2"; shift 2 ;;
    --architecture) architecture="$2"; shift 2 ;;
    --force) force=1; shift ;;
    --persist) persist=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 64 ;;
  esac
done

[[ -f "$global_json" ]] || { echo "global.json not found: $global_json" >&2; exit 1; }
sdk_version="$(sed -nE 's/^[[:space:]]*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' "$global_json" | head -n1)"
[[ "$sdk_version" =~ ^10\.0\.[0-9]+$ ]] || { echo "global.json must pin a stable .NET 10 SDK; found '$sdk_version'." >&2; exit 1; }

case "$scope" in
  repository) : "${install_dir:=$repo_root/.dotnet}" ;;
  user) : "${install_dir:=$HOME/.dotnet}" ;;
  *) echo "--scope must be repository or user" >&2; exit 64 ;;
esac

case "$architecture" in auto|x64|arm64) ;; *) echo "Unsupported architecture: $architecture" >&2; exit 64 ;; esac
mkdir -p "$install_dir"
dotnet_bin="$install_dir/dotnet"
installer_architecture="$architecture"
[[ "$architecture" == "auto" ]] && installer_architecture="<auto>"

has_sdk() {
  [[ -x "$dotnet_bin" ]] && "$dotnet_bin" --list-sdks 2>/dev/null | grep -Eq "^${sdk_version//./\.}[[:space:]]"
}

if ((force == 0)) && has_sdk; then
  printf '.NET SDK %s is already installed at %s\n' "$sdk_version" "$install_dir"
else
  command -v curl >/dev/null 2>&1 || { echo "curl is required." >&2; exit 1; }
  tmp_dir="$(mktemp -d)"
  trap 'rm -rf "$tmp_dir"' EXIT
  curl --fail --location --proto '=https' --tlsv1.2 --silent --show-error \
    "$installer_url" -o "$tmp_dir/dotnet-install.sh"
  chmod 0700 "$tmp_dir/dotnet-install.sh"
  "$tmp_dir/dotnet-install.sh" --version "$sdk_version" --install-dir "$install_dir" \
    --architecture "$installer_architecture" --no-path
fi

has_sdk || { echo ".NET SDK $sdk_version was not found after installation." >&2; exit 1; }
export DOTNET_ROOT="$install_dir"
export PATH="$install_dir:$install_dir/tools:$PATH"

if ((persist == 1)); then
  profile_file="${SHELL##*/}"
  case "$profile_file" in
    zsh) profile_file="$HOME/.zshrc" ;;
    *) profile_file="$HOME/.bashrc" ;;
  esac
  marker_begin='# >>> nvt_fw_combiner dotnet >>>'
  marker_end='# <<< nvt_fw_combiner dotnet <<<'
  if ! grep -Fq "$marker_begin" "$profile_file" 2>/dev/null; then
    {
      printf '\n%s\n' "$marker_begin"
      printf 'export DOTNET_ROOT=%q\n' "$install_dir"
      printf 'export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"\n'
      printf '%s\n' "$marker_end"
    } >> "$profile_file"
  fi
fi

printf 'Installed .NET SDK: %s\n' "$("$dotnet_bin" --version)"
printf 'DOTNET_ROOT: %s\n' "$install_dir"
printf 'Run: export DOTNET_ROOT=%q; export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"\n' "$install_dir"
