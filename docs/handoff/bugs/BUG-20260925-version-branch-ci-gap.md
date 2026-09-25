# BUG-20260925-version-branch-ci-gap: direct commits to version branches get no CI

Status: open
Severity: P2
Found: 2026-09-25, Claude Code (Opus 5.5), while surveying CI, at `1.1.12`@`d69b6e54a`
Where: `.github/workflows/ci.yml:3-7`; `CONTRIBUTING.md:8`
Observed: CI triggers only on pull requests and on pushes to `main`, while `CONTRIBUTING.md` tells contributors to commit tightly coupled release work directly to the version branch.
Expected: every pushed commit on an integration branch is checked, or the contribution rule stops recommending unchecked direct commits.
Evidence: the `on:` block of `ci.yml`.
Owner: Claude Code, WS-GOV, `feature/1.1.12/governance-reset`; fix decided 2026-09-25 (WS-GOV decisions 4 and 10, re-answered): integration moves to pull requests into the `1.1.x` trunk with tiered CI, and the contribution rule stops recommending unchecked direct commits
Resolution:
