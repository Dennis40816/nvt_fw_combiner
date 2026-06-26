#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
owner="${NFC_GITHUB_OWNER:-Dennis40816}"
repository="${NFC_GITHUB_REPOSITORY:-nvt_fw_combiner}"
full_name="$owner/$repository"
version="$(tr -d '[:space:]' < "$repo_root/VERSION")"
tag="v$version"

command -v git >/dev/null 2>&1 || { echo 'git is required.' >&2; exit 1; }
command -v gh >/dev/null 2>&1 || { echo 'gh is required.' >&2; exit 1; }
cd "$repo_root"

git rev-parse --is-inside-work-tree >/dev/null
[[ -z "$(git status --porcelain)" ]] || { echo 'Refusing to publish a dirty worktree.' >&2; exit 1; }
gh auth status >/dev/null

head_commit="$(git rev-parse HEAD)"
tag_commit="$(git rev-list -n 1 "$tag" 2>/dev/null || true)"
[[ "$tag_commit" == "$head_commit" ]] || { echo "Annotated tag $tag must exist at HEAD." >&2; exit 1; }

if ! gh repo view "$full_name" --json nameWithOwner >/dev/null 2>&1; then
  gh repo create "$full_name" --private --source . --remote origin --push \
    --description 'Profile-driven firmware image composition desktop utility.'
else
  remote_url="https://github.com/$full_name.git"
  if git remote get-url origin >/dev/null 2>&1; then git remote set-url origin "$remote_url"; else git remote add origin "$remote_url"; fi
  git push --set-upstream origin main
fi

git push origin "$tag"
