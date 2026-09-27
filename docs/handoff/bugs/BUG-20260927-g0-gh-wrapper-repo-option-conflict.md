# BUG-20260927-g0-gh-wrapper-repo-option-conflict: the G0 `gh` wrapper rejects `gh --repo`

Status: open (known wrapper limitation, observed in use)
Severity: P3
Found: 2026-09-27, Claude Code (Opus 5.5), opening the batch 2c pull request as the App
Where: `docs/handoff/1.1.13/g0-scripts/Invoke-NfcGh.ps1` (installed copy outside the repository)
Observed: `Invoke-NfcGh.ps1 ... pr create --repo <owner>/<repo> ...` stops before running `gh`: PowerShell binds the
`gh` option `--repo` to the script's own `-Repo` parameter and reports that the parameter was given more than once. Without
`--repo` the same call succeeds, because the wrapper already scopes the token to the one repository; pull request #461 was
created that way.
Expected: the wrapper forwards `gh` arguments that collide with its own parameter names, or the checklist lists the options
agents must omit. The G0 review already noted the wrapper is not a general transparent forwarder.
Evidence: the failed and the successful invocation in the batch 2c session; pull request #461 authored by the App.
Owner: WS-GOV (a reviewed script change, with the helper permission-set change for `workflows`).
Resolution:
