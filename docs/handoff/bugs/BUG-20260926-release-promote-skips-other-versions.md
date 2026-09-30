# BUG-20260926-release-promote-skips-other-versions: the release promote job is skipped for every version outside 1.x and 2.0.0

Status: fixed (merged into `1.1.x` by integration B #477, merge `912db1a5c`; released in v1.1.13, `1268f72d2`, 2026-09-28)
Severity: P3
Found: 2026-09-26, Claude Code (Opus 5.5), while designing the release workflow cleanup (WS-GOV), at `feature/1.1.13/ws-gov`@`8682dd269`
Where: `.github/workflows/release.yml:602-607` (`promote` `needs` and `if`); `published-smoke` (`:1019-1024`) depends on it.
Observed: `promote` runs only when the version is `2.0.0` and `v0916-parity-finalize` succeeded, or when the version starts with `1.` and that job was skipped. For every other version (`2.0.1` and later, `3.x`, or one of the `0.9.17`-`0.9.19` maintenance pairs the workflow still offers) `promote` is skipped, `published-smoke` is skipped with it, and the run ends without a failure although nothing is tagged or published. The condition came with `ae932e245` (2026-08-28, "defer terminal parity gate to 2.0.0"), after the last maintenance release `v0.9.19` (2026-08-08).
Expected: an admitted candidate that the release owner approves is promoted ([ADR 0033](../../adr/0033-ci-owned-stable-release-promotion.md)); [ADR 0057](../../adr/0057-v0916-black-box-parity-certification.md) defers its parity gate to 2.0.0 but does not exempt later versions from promotion. ADR 0033, `.github/AGENTS.md` and `.github/workflows/README.md` still state that the maintenance pairs may publish.
Evidence: the `if:` expression at `release.yml:607`; `git log -S"startsWith(needs.candidate.outputs.version, '1.')" -- .github/workflows/release.yml` returns `ae932e245`. No release is affected before 2.0.1 or a maintenance release.
Owner: unassigned; proposed fix in batch R-1 of the [release workflow cleanup design](../1.1.13/DESIGN-release-workflow-cleanup.md) (item 2).
Resolution: R-1 replaces the promote condition with eligibility success and checks the version floor at the candidate and at pre-tag (`ee6c97e09`); closes when that pull request merges.

Closed (2026-09-29): both `4d2d642f7` and `ee6c97e09` are ancestors of integration B's merge `912db1a5c` (#477) and of the v1.1.13 release merge `1268f72d2` (#479, published 2026-09-28).
