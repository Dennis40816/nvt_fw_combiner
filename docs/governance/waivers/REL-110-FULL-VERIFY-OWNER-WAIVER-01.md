# REL-110 full-verifier owner waiver

- Rule/tool: Polytail canonical `python scripts/verify.py --all` gate, the `python scripts/verify.py --structure-only` completion gate, and the managed `.github/workflows/release.yml` promotion workflow.
- Scope: only the exact reviewed v1.1.0 Windows x64 `manual-only` Application package and its ZIP, SPDX SBOM, and provenance assets.
- Reason: the frozen v1.0.8 release run `33454068996` is blocked by deterministic shadow-output repository-root discovery failures rather than an Application runtime failure. On 2026-09-01 the v1.1.0 structure-only run emitted the normal code-size warnings and no validation error, then its capability-history scan consumed the full 600-second lane limit. The owner excluded Launcher, Setup, Catalog, Registry, automatic update, and Version deployment from v1.1.0 and directed that this direct package not rerun the managed release flow. The verification architecture is the sole v1.1.1 change.
- Risk: R3. The residual risk is that neither broad source verification nor completion of the governance-history scan is available for this release. It is bounded by exact-head independent review, protected-main CI, direct JSON/PowerShell parsing, narrow contract tests, construction by the canonical packager, closed package smoke, release-owner digest approval, and a fresh-download visible-startup smoke in the fixed test area.
- Owner: Dennis Liu, release owner.
- Issue: v1.1.1 test, verification, and release architecture review; known failing release run `33454068996`.
- Approver: Dennis Liu, by the explicit 2026-09-01 directions to leave v1.0.8 unchanged, ignore Version deployment, avoid rerunning the release process for v1.1.0, and publish directly when direct-Application risk is low.
- Created: 2026-09-01 (Asia/Taipei).
- Expires: when v1.1.0 is published or at 2026-09-02 00:00 Asia/Taipei, whichever comes first.
- Removal condition: this waiver is single-use and must not be reused by any later release. v1.1.1 must either restore a passing canonical full gate or replace it through an owner-approved verification architecture.

This waiver does not weaken firmware range safety, processor write ranges,
integrity order, secrets or signing controls, release allowlists, independent
Golden expectations, exact source identity, artifact hashes, or the prohibition
on moving or replacing a stable tag. It does not authorize a managed install,
Catalog/Registry mutation, Launcher/Setup asset, automatic update, Version
deployment, reference payload, or any claim that the waived gates are green.

## Structure-only timeout evidence

- Command: `python scripts/verify.py --structure-only`
- Started: 2026-09-01 09:21:50 Asia/Taipei.
- Ended: 2026-09-01 09:31:50 Asia/Taipei at the configured 600-second lane limit.
- Result: **TIMED OUT / WAIVED**, not passed.
- Scope identity: only the exact `reviewedHead`, governed-path digest, and merge-
  equivalent source tree recorded by
  `REL-110-MANUAL-ONLY-PACKAGE-01`; any source-tree change invalidates this
  waiver and requires the gates to be re-evaluated.
- Last completed phase: the validators preceding `validate_agent_files` in
  `validate_repository.validate()` completed. A read-only instrumented replay
  of `validate_capability_reuse_governance` printed
  `ENTER _historical_final_records` and did not exit within the observation
  window, confirming the bounded run exhausted time in capability-history
  enumeration rather than another validator.
- Validation findings before timeout: zero `ERROR:` findings. The only emitted
  findings were the pre-existing metrics warnings reproduced below.

Full captured lane log:

```text
VERIFICATION FAILED: verification lanes failed: structure
Verification policy: jobs=3, lane-timeout=600s, cleanup-ceiling=30s

=== structure lane (600.0s) ===

> C:\Users\liusx\miniconda3\python.exe C:\Users\liusx\.codex\worktrees\1100\nvt_fw_combiner\scripts\verify.py --internal-lane structure
> C:\Users\liusx\miniconda3\python.exe scripts/validate_repository.py
WARNING: runtime production metric: 604 files / 98551 nonblank lines (baseline 45214, delta +53337)
WARNING: Domain + Profiles metric: 157 files / 20632 nonblank lines (ratchet 20627 + approved allowance 5 = 20632)
WARNING: Application metric: 240 files / 42197 nonblank lines (ratchet 30690 + approved allowance 11507 = 42197)
WARNING: Bootstrap + CLI + Desktop host metric: 43 files / 5039 nonblank lines (ratchet 3378 + approved allowance 1661 = 5039)
WARNING: Infrastructure + Contracts + CRC worker metric: 164 files / 30683 nonblank lines (ratchet 15356 + approved allowance 15327 = 30683)
Command timing: 600.0s
[lane failed] TimeoutExpired: Command '['C:\\Users\\liusx\\miniconda3\\python.exe', 'C:\\Users\\liusx\\.codex\\worktrees\\1100\\nvt_fw_combiner\\scripts\\verify.py', '--internal-lane', 'structure']' timed out after 599.9936739999976 seconds

Verification lane summary: structure=FAIL (600.0s)
```

This timeout waiver is invalid if `scripts/verify.py`,
`scripts/validate_repository.py`, capability-history behavior, or the exact
source tree changes; if a later run emits a validation error; or if a timeout
occurs in any phase other than `_historical_final_records`.
