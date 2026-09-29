# BUG-20260929-ci-push-trunk-1-2-x: merges into the `1.2.x` trunk get no post-merge CI run

Status: open
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while opening the first pull request into `1.2.x`
(#485), at `1.2.x`@`09d2a6ca2`
Where: `.github/workflows/ci.yml:3-7`
Observed: `ci` runs on every `pull_request`, so pull requests into `1.2.x` are checked. Its `push` trigger lists only
`main` and `1.1.x`, so a merge into the `1.2.x` trunk (cut by decision 184) starts no `ci` run on the resulting
trunk commit.
Expected: every trunk gets the same post-merge `ci` run that `1.1.x` gets, so a trunk-only integration failure
(for example between two pull requests that each passed on their own base) is found on the trunk commit itself.
Evidence: `.github/workflows/ci.yml:6-7` (`push: branches: [main, 1.1.x]`); decision 184 on the
[`1.1.x` board](../1.1.12.md) cuts `1.2.x`; the trunk ruleset pattern `*.*.x` covers `1.2.x`.
Owner: unassigned. The fix (add `1.2.x`, or match every trunk) changes a CI workflow, so it takes its own
CI-governance gate and is not bundled with a records or dependency pull request.
Resolution: not fixed.
