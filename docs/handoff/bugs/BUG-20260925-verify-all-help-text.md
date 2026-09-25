# BUG-20260925-verify-all-help-text: `--all` help claims it is the CI completion command

Status: open
Severity: P3
Found: 2026-09-25, Claude Code (Opus 5.5), while surveying verification, at `1.1.12`@`d69b6e54a`
Where: `scripts/verify.py:4840`
Observed: the help text reads "This is the CI/Codex completion command".
Expected: accurate, runtime-neutral text. `.github/workflows/ci.yml` never runs `--all` (it runs `--structure-only` and `--ci-*` shards); only `main-package.yml:41` (manual preview) and the legacy branch at `release.yml:417` do.
Evidence: `grep -n verify.py .github/workflows/*.yml`.
Owner: Claude Code, WS-GOV, `feature/1.1.12/governance-reset`
Resolution:
