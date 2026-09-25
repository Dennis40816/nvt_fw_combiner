# v0.9.16 local comparison for 1.1.12 (non-certifying)

Status: partial. The candidate side ran for all 37 routes with canonical
inputs; the v0.9.16 predecessor side is blocked
([BUG-20260925-v0916-baseline-restore-nu1004](../../bugs/BUG-20260925-v0916-baseline-restore-nu1004.md)),
so no route is compared yet. Commander decision pending (WS-PARITY log).

Authority: 1.1.12 board owner decision 10, option A. This is a local
observation, not ADR 0057 certification: no receipts, package admission,
owner attestation or verdict. `scripts/`, workflows, contracts and ADR 0057
are unchanged; the formal 1.x comparator moves to 1.1.13.

## Identities

- Plan: `docs/contracts/v0916-parity-certification-v1.json`, SHA-256
  `8e6401cc2b7f54f81370b6fa93d5df19d2f44ab68c848cb93e1eda6decc09afb`; 64
  routes, of which 37 have canonical inputs and 27 are listed in
  `canonicalInputAuthority.currentlyMissingRouteIds`.
- Inputs: the plan's pinned canonical Golden snapshot (commit
  `1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220`), read from Git objects,
  materialized in the test area and checked by
  `scripts/canonical_golden_validation.py` through the existing parity loader.
  CtrlRAM full-flash routes rebuild their base with each executor's own
  Standard Merge from the case DP and TP inputs, as the plan declares.
- Candidate: product source `1c37bd718d8c4fbc5cc247952229e8b2f9f54ce8`
  (`v1.1.11`, tree `f7dcfbd03d2f182e609a57053add09ead3f9a1f4`), exported with
  `git archive` into the test area and built with the command shape of
  `docs/contracts/v100-candidate-source-executor-v1.json` (locked `win-x64`
  restore, Release self-contained build, `ContinuousIntegrationBuild`,
  `PathMap`). `NvtFwCombiner.Cli.exe` 162304 bytes, SHA-256
  `2709ccda1d77a4ab2a72e789da07eda9cabe558f0dc51813e225da31529567d6`; runtime
  closure 366 files, 92,947,508 bytes, closure SHA-256
  `66e41c6a3d6eebcdcdd86041be23ed6ce6d18441535ff351014743cc16c84689`. The
  build left every lock file unchanged.
- Predecessor: `v0.9.16` (tag object `578b2614632d6c2affdf2000324b134b5d1a16c1`,
  peeled `462590e8b993b8e42d088bc07377571a4bb9f25d`, tree
  `dc46c9aa9ecf00cb898ba3bc287e1b15acdab735`). The export matches all 15
  files the baseline executor contract declares, but the contract's own
  locked restore fails `NU1004`; see the bug.

## Method

Each executor runs `preview` then `build` for every route, exactly with the
argument form of `scripts/v0916_parity_certification.py` (`_cli_arguments`),
on inputs the plan's resolver writes afresh for each route and side; input
hashes are rechecked after every process. Exact routes compare complete outputs byte for byte. The four
runnable TP-work routes use the plan's transitive proof: TP output length,
TP output equal to the candidate full-route prefix and to the v0.9.16
full-route prefix, and the candidate full-route tail equal to its base.
Differences are listed as half-open `[start, endExclusive)` ranges in the
output file offset space, up to 32 ranges per file.

Harness: [`v0916_local_compare.py`](v0916_local_compare.py); renderer:
[`render_table.py`](render_table.py); explanations:
[`explanations.json`](explanations.json); path-free results with full
hashes, sizes, exit codes and durations:
[`v0916-local-comparison.json`](v0916-local-comparison.json). The table shows
the first 16 hex digits of each SHA-256.

## Coverage, 2026-09-25 run

| Result | Routes | Meaning |
| --- | --- | --- |
| equal | 0 | - |
| different | 0 | - |
| error | 1 | NT51950 FW1.x cascade CtrlRAM full-flash, candidate side ([bug](../../bugs/BUG-20260925-nt51950-cascade-ctrlram-plan-base.md)) |
| candidate-only | 36 | candidate ran; predecessor blocked |
| not covered | 27 | no canonical input |

Facts from the candidate side alone:

- All 36 other runnable routes built with exit 0 for `preview` and `build`,
  and no process changed an input.
- Every Standard and AB output (13 routes: 9 Standard, 4 AB) equals its canonical Golden
  expected output. These cases are direct or alias Golden with complete-output
  dispositions. CtrlRAM cases carry `allowed-byte-difference` or fact-scoped
  dispositions, so their Golden comparison in the JSON is informational only;
  ADR 0057 forbids expected output as the parity reference.
- NT51951 FW1.x cascade CtrlRAM output equals the plan's approved-correction
  candidate hash, and its rebuilt base equals the full-base hash in
  `docs/contracts/v0916-nt51951-c2-diagnostic-v1.json`.
- The four runnable TP-work routes pass the candidate half of the transitive
  proof (length, candidate full-route prefix, unchanged full-route tail). The
  v0.9.16 prefix check needs the predecessor.

Duration: 726 s wall clock for the whole plan including pauses for another
lane's timing runs; 462 s of route execution (about 9 s per two-invocation
route and 17 to 22 s per CtrlRAM full-flash route with its base precursor)
plus 23 s to materialize and validate the canonical snapshot. A predecessor
side adds roughly 430 s if v0.9.16 runs at the same speed (33 exact routes,
no TP rows).

<!-- table -->

| # | IC | Workflow | IC count | Map variant | Proof | Runnable | Result | Differing ranges (file offsets, half-open) | v0.9.16 sha256 | Candidate sha256 | Proposed explanation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | NT51917 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-full-flash` | exact | yes | candidate-only | - | - | `fdb8fef05bdb375e` | Not compared: predecessor not run (see blocker). |
| 2 | NT51917 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-tp-work-212k` | TP prefix | yes | candidate-only | - | - | `6a1bbb51faaad6fe` | Not compared: predecessor not run (see blocker). |
| 3 | NT51917 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 4 | NT51917 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 5 | NT51917 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 6 | NT51917 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 7 | NT51917 | Standard | selector-free | `nt51927-standard-merge-256k` | exact | yes | candidate-only | - | - | `8e0d362b74ba65df` | Not compared: predecessor not run (see blocker). |
| 8 | NT51919 | AB | selector-free | `nt51919-ab-merge-512k` | exact | yes | candidate-only | - | - | `c7e1e263ac8ca70f` | Not compared: predecessor not run (see blocker). |
| 9 | NT51919 | CtrlRAM | 1-ic | `nt51929-ctrlram-fw200-single-full-flash` | exact | yes | candidate-only | - | - | `d23f53a13db3c6fc` | Not compared: predecessor not run (see blocker). |
| 10 | NT51919 | CtrlRAM | 2-8-ic | `nt51929-ctrlram-fw1x-cascade-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 11 | NT51919 | Standard | selector-free | `nt51919-standard-merge-256k` | exact | yes | candidate-only | - | - | `fdba72984d662c4d` | Not compared: predecessor not run (see blocker). |
| 12 | NT51923 | CtrlRAM | 1-ic | `nt51923-ctrlram-fw141-single-full-flash` | exact | yes | candidate-only | - | - | `4759a8e87ad7ff8a` | Not compared: predecessor not run (see blocker). |
| 13 | NT51923 | CtrlRAM | 1-ic | `nt51923-ctrlram-fw141-single-tp-work-240k` | TP prefix | yes | candidate-only | - | - | `6d14818a4b11ed9c` | Not compared: predecessor not run (see blocker). |
| 14 | NT51923 | CtrlRAM | 2-plus-ic | `nt51923-ctrlram-fw141-cascade3-full-flash` | exact | yes | candidate-only | - | - | `017a157ba2419ff2` | Not compared: predecessor not run (see blocker). |
| 15 | NT51923 | CtrlRAM | 2-plus-ic | `nt51923-ctrlram-fw141-cascade3-tp-work-240k` | TP prefix | yes | candidate-only | - | - | `7b7cb25f6c267d2a` | Not compared: predecessor not run (see blocker). |
| 16 | NT51923 | Standard | selector-free | `nt51923-standard-merge-256k` | exact | yes | candidate-only | - | - | `b611bd837991be1c` | Not compared: predecessor not run (see blocker). |
| 17 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw141-full-flash-256k` | exact | yes | candidate-only | - | - | `30de4735472950e3` | Not compared: predecessor not run (see blocker). |
| 18 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw141-tp-work-240k` | exact | yes | candidate-only | - | - | `22888325a4c15a14` | Not compared: predecessor not run (see blocker). |
| 19 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw200-full-flash-256k` | exact | yes | candidate-only | - | - | `5f8913e48784bf0c` | Not compared: predecessor not run (see blocker). |
| 20 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw200-tp-work-240k` | exact | yes | candidate-only | - | - | `f1c4a61299741af4` | Not compared: predecessor not run (see blocker). |
| 21 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw141-full-flash-256k` | exact | yes | candidate-only | - | - | `a2169f9fc908207d` | Not compared: predecessor not run (see blocker). |
| 22 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw141-tp-work-240k` | exact | yes | candidate-only | - | - | `ad6b5cd0b8a3babe` | Not compared: predecessor not run (see blocker). |
| 23 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw200-full-flash-256k` | exact | yes | candidate-only | - | - | `b4336e3d935466fe` | Not compared: predecessor not run (see blocker). |
| 24 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw200-tp-work-240k` | exact | yes | candidate-only | - | - | `2e79b7634219c9e6` | Not compared: predecessor not run (see blocker). |
| 25 | NT51926 | Standard | selector-free | `nt51926-standard-merge-256k` | exact | yes | candidate-only | - | - | `910c302495ef037b` | Not compared: predecessor not run (see blocker). |
| 26 | NT51927 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-full-flash` | exact | yes | candidate-only | - | - | `fdb8fef05bdb375e` | Not compared: predecessor not run (see blocker). |
| 27 | NT51927 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-tp-work-212k` | TP prefix | yes | candidate-only | - | - | `6a1bbb51faaad6fe` | Not compared: predecessor not run (see blocker). |
| 28 | NT51927 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 29 | NT51927 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 30 | NT51927 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 31 | NT51927 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 32 | NT51927 | Standard | selector-free | `nt51927-standard-merge-256k` | exact | yes | candidate-only | - | - | `8e0d362b74ba65df` | Not compared: predecessor not run (see blocker). |
| 33 | NT51928 | CtrlRAM | 1-ic | `nt51928-ctrlram-fw141-single-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 34 | NT51928 | CtrlRAM | 1-ic | `nt51928-ctrlram-fw141-single-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 35 | NT51928 | CtrlRAM | 2-ic | `nt51928-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 36 | NT51928 | CtrlRAM | 2-ic | `nt51928-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 37 | NT51928 | CtrlRAM | 3-ic | `nt51928-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 38 | NT51928 | CtrlRAM | 3-ic | `nt51928-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 39 | NT51928 | Standard | selector-free | `nt51928-dual-capacity-256k-512k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 40 | NT51929 | AB | selector-free | `nt51929-ab-merge-512k` | exact | yes | candidate-only | - | - | `c7e1e263ac8ca70f` | Not compared: predecessor not run (see blocker). |
| 41 | NT51929 | CtrlRAM | 1-ic | `nt51929-ctrlram-fw200-single-full-flash` | exact | yes | candidate-only | - | - | `d23f53a13db3c6fc` | Not compared: predecessor not run (see blocker). |
| 42 | NT51929 | CtrlRAM | 2-8-ic | `nt51929-ctrlram-fw1x-cascade-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 43 | NT51929 | Standard | selector-free | `nt51929-standard-merge-256k` | exact | yes | candidate-only | - | - | `fdba72984d662c4d` | Not compared: predecessor not run (see blocker). |
| 44 | NT51932 | AB | selector-free | `nt51932-ab-merge-512k` | exact | yes | candidate-only | - | - | `c7e1e263ac8ca70f` | Not compared: predecessor not run (see blocker). |
| 45 | NT51932 | CtrlRAM | 1-ic | `nt51932-ctrlram-fw1x-single-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 46 | NT51932 | CtrlRAM | 2-8-ic | `nt51932-ctrlram-fw200-cascade-full-flash` | exact | yes | candidate-only | - | - | `0e59a2fbaab16979` | Not compared: predecessor not run (see blocker). |
| 47 | NT51932 | Standard | selector-free | `nt51932-standard-merge-256k` | exact | yes | candidate-only | - | - | `6a0c90b1c3145361` | Not compared: predecessor not run (see blocker). |
| 48 | NT51950 | AB | 1-ic | `nt51950-ab-merge-512k` | exact | yes | candidate-only | - | - | `d18db8dc02ab4ff5` | Not compared: predecessor not run (see blocker). |
| 49 | NT51950 | AB | 2-plus-ic | `nt51950-ab-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 50 | NT51950 | CtrlRAM | 1-ic | `nt51950-ctrlram-fw200-single-full-flash` | exact | yes | candidate-only | - | - | `a32e6896b840d44e` | Not compared: predecessor not run (see blocker). |
| 51 | NT51950 | CtrlRAM | 1-ic | `nt51950-ctrlram-fw200-single-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 52 | NT51950 | CtrlRAM | 2-ic | `nt51950-ctrlram-fw1x-cascade-full-flash` | exact | yes | error | - | - | - | Candidate error before any comparison: the plan-bound 512 KiB precursor base is rejected as an NT51950 AB reference; the route map declares 256 KiB. Suspected plan binding or classification defect (fact for the error, hypothesis for the cause; BUG-20260925-nt51950-cascade-ctrlram-plan-base) |
| 53 | NT51950 | CtrlRAM | 2-ic | `nt51950-ctrlram-fw1x-cascade-tp-work` | exact | yes | candidate-only | - | - | `a239645dd6e2527a` | Not compared: predecessor not run (see blocker). |
| 54 | NT51950 | Standard | selector-free | `nt51950-standard-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 55 | NT51950 | Standard | selector-free | `nt51950-standard-merge-256k` | exact | yes | candidate-only | - | - | `11932f352c3268dc` | Not compared: predecessor not run (see blocker). |
| 56 | NT51950 | Standard | selector-free | `nt51950-standard-merge-512k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 57 | NT51951 | AB | selector-free | `nt51951-ab-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 58 | NT51951 | CtrlRAM | 1-ic | `nt51951-ctrlram-fw200-single-full-flash` | exact | yes | candidate-only | - | - | `64ffa21a36a3a956` | Not compared: predecessor not run (see blocker). |
| 59 | NT51951 | CtrlRAM | 1-ic | `nt51951-ctrlram-fw200-single-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 60 | NT51951 | CtrlRAM | 2-ic | `nt51951-ctrlram-fw1x-cascade-full-flash` | exact | yes | candidate-only | - | - | `1536d344af83aafd` | Predecessor not run. The candidate output equals the plan's approved-correction candidate hash `1536d344...4ebd`; the expected v0.9.16 difference is the owner-approved Diff NF preservation (2816 bytes in five declared ranges) (fact for the candidate hash, expectation for the difference; plan approvedSemanticCorrections; ADR 0057; CHANGELOG 1.0.0) |
| 61 | NT51951 | CtrlRAM | 2-ic | `nt51951-ctrlram-fw1x-cascade-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 62 | NT51951 | Standard | selector-free | `nt51951-standard-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 63 | NT51951 | Standard | selector-free | `nt51951-standard-merge-256k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 64 | NT51951 | Standard | selector-free | `nt51951-standard-merge-512k` | exact | yes | candidate-only | - | - | `3266fae2f4873a7f` | Not compared: predecessor not run (see blocker). |
