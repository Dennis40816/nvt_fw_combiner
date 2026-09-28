# BUG-20260927-main-package-1x-launcher-contract: `main-package.yml` always fails its package smoke for 1.x

Status: open
Severity: P2 (a manual workflow that can no longer succeed; the release workflow is not affected)
Found: 2026-09-27, Claude Code (Opus 5.5), reading the decision 132 gate run for pull request #464
Where: `.github/workflows/main-package.yml` (packaging step), `scripts/package.ps1:1389`, `scripts/smoke-release.ps1:689-693`
Observed: `main-package.yml` packages with `-AllowPrerelease`; `package.ps1` sets `$IncludeManagedLauncher = -not
($AllowPrerelease -or $ManualOnly)`, so the package carries no managed launcher and its manifest is schema 1.1. The smoke
step then rejects every version from 1.0.0 on: "Version 1.0.0 and newer require the managed launcher contract."
main-package run #89 (36312877403, head `c6add0071`) failed exactly there after `verify.py --all` had passed (17.5 min
on the 4-vCPU runner, job 22 of 45 min). The workflow was last changed in 0.10.x and last ran on `main` on 2026-07-22,
before 1.0, so it has been broken for every 1.x head; `release.yml` packages without `-AllowPrerelease` and is unaffected.
Expected: the manual package workflow builds and smokes the same package shape as the release path for 1.x versions, or it
is retired in favour of the release workflow's evidence.
Evidence: run 36312877403 job log (smoke step), the three source lines above.
Owner: release workflow cleanup (R3, release-owner), with R-1/R-2 or its own record; board decision 134.
Resolution:
