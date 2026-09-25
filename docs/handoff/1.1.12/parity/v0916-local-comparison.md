# v0.9.16 local comparison for 1.1.12 (non-certifying)

Status: rerun on the 1.1.12 frozen product source `badc545b0` for the 37
routes with canonical inputs; 27 routes are not covered. The owner approved
both Diff NF preservation differences on 2026-09-26. The first run, on the
`v1.1.11` product source, had the same counts; only the error route changed
side (see below).

Authority: 1.1.12 board owner decision 10, option A, and the commander's
2026-09-25 decision to use the probe predecessor build. This is a local
observation, **not** ADR 0057 certification: no receipts, package admission,
owner attestation or verdict. `scripts/`, workflows, contracts and ADR 0057
are unchanged; the formal 1.x comparator moves to 1.1.13.

## Result

| Result | Routes |
| --- | --- |
| equal | 34 |
| different | 2 |
| error | 1 |
| not covered (no canonical input) | 27 |

Differences, both explained; neither is unexplained:

1. NT51951 CtrlRAM FW1.x cascade full-flash: 2,816 bytes in
   `[0xA11C,0xA120)`, `[0xA130,0xA134)`, `[0x2D428,0x2D42C)`,
   `[0x2D43C,0x2D440)`, `[0x33B10,0x34600)`; both outputs 524,288 bytes. Both
   hashes, the count and the ranges equal the plan's owner-approved Diff NF
   preservation correction (owner decision 2026-08-28).
2. NT51950 CtrlRAM FW1.x cascade TP-work: the same 2,816 bytes in the same
   five ranges; both outputs 225,280 bytes. Same cause: v0.9.16 writes the
   whole Diff CtrlRAM record `[0x33200,0x34600)`, the candidate only the active
   span `[0x33200,0x33B10)` and preserves the Diff NF tail (CHANGELOG 0.10.1,
   #188). The plan names this correction only for NT51951; the owner approved
   it for this route on 2026-09-26
   ([bug](../../bugs/BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing.md)).

The error is NT51950 CtrlRAM FW1.x cascade full-flash, now on the v0.9.16 side
only. Both executors build the same plan-bound 512 KiB base. v0.9.16 rejects
it for the route's 256 KiB map; the 1.1.12 candidate admits it as a
Display-OSD envelope over that map (CTRLRAM-OSD-ENVELOPE-1112-01) and writes
524,288 bytes equal to the registered-Combiner output of the NT51951 2-IC
route, which differs from the Golden expected output only in the four approved
CRC words. In the first run, on the `v1.1.11` product source, the candidate
also rejected it
([bug](../../bugs/BUG-20260925-nt51950-cascade-ctrlram-plan-base.md)).

Other facts from this run:

- Every exact route that ran on both sides rebuilt an identical CtrlRAM base
  (14 full-flash routes), and no process changed an input.
- The four runnable TP-work transitive routes pass all four checks: TP length,
  equality with the candidate full-route prefix, equality with the v0.9.16
  full-route prefix, and an unchanged candidate full-route tail.
- The 13 Standard and AB outputs equal their Golden expected outputs on both
  sides. CtrlRAM Golden comparisons in the JSON are informational only
  (`allowed-byte-difference` or fact-scoped dispositions); ADR 0057 forbids
  expected output as the parity reference.
- The retirements of DP Replace (1.1.10) and of NT51920/25/30/31 (0.10.1)
  affect no selected route; they only explain why those routes are outside
  the plan's 64.
- The plan's route identities come from the policy at commit `1d1d1cfc`. In
  the `v1.1.11` policy, NT51950 AB `nt51950-ab-merge-512k` is now
  `nt51950-ab-merge-maps` and NT51950 AB cascade 1024k is no longer
  `supported` (CHANGELOG 1.1.10 item 1). The CLI invocation does not name a
  route, so this run is unaffected, but a rerun against a later policy must
  map these identities.

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
- Candidate: 1.1.12 frozen product source `badc545b04cf98aaaec9c7c9eb0261bdcc5cee69`
  (tree `c5ba8d404821386c63fc758a939b163f39d4c8a8`), exported with `git archive`
  into the test area and built with the command shape of
  `docs/contracts/v100-candidate-source-executor-v1.json`:
  `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --locked-mode --disable-parallel --runtime win-x64`,
  then `dotnet build src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -m:1 -p:ContinuousIntegrationBuild=true -p:PathMap=<source>=/_/src`.
  `NvtFwCombiner.Cli.exe` 162,304 bytes, SHA-256
  `5dcfbeec54c89d81dc5c5e5df4f8d4e2b8eb6d3dac2a55618f20cb0884bfb468`; runtime
  closure 366 files, 92,976,630 bytes. No lock file changed. The first run used
  `v1.1.11` (`1c37bd718d8c4fbc5cc247952229e8b2f9f54ce8`), CLI SHA-256
  `2709ccda1d77a4ab2a72e789da07eda9cabe558f0dc51813e225da31529567d6`.
- Predecessor: `v0.9.16` (tag object `578b2614632d6c2affdf2000324b134b5d1a16c1`,
  peeled `462590e8b993b8e42d088bc07377571a4bb9f25d`, tree
  `dc46c9aa9ecf00cb898ba3bc287e1b15acdab735`), exported with `git archive`;
  the export matches all 15 files the baseline executor contract declares.
  The contract's locked restore fails `NU1004`
  ([bug](../../bugs/BUG-20260925-v0916-baseline-restore-nu1004.md)), so by
  commander decision the predecessor was restored with
  `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --force-evaluate --runtime win-x64`
  and built with the contract's exact command,
  `dotnet build src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -p:ContinuousIntegrationBuild=true -p:PathMap=<source>=/_/src`.
  `NvtFwCombiner.Cli.exe` SHA-256
  `093212528d1048acdb43563ba795353e1ac872f8b6a3aa99251782e958ad1a30`
  **equals** the contract's `cliAssembly`. The runtime closure has the
  contract's 431 files but 88,359,996 bytes, 7,168 fewer than the contract's
  88,367,164, and its closure hash differs (`e77fe074...f42a` against
  `18da1123...b3b0`). The hypothesis that a Git-less export cannot embed
  SourceLink or commit metadata is not verified. The restore rewrote only
  empty `net10.0/win-x64` targets and first-party project ranges
  (`0.9.2` to `0.9.16`); no package id, version or content hash changed.

## Method

Each executor runs `preview` then `build` for every route, exactly with the
argument form of `scripts/v0916_parity_certification.py` (`_cli_arguments`),
on inputs the plan's resolver writes afresh for each route and side; input
hashes are rechecked after every process. Exact routes compare complete
outputs byte for byte. The four runnable TP-work routes use the plan's
transitive proof and are not run on v0.9.16. Differences are listed as
half-open `[start, endExclusive)` ranges in the output file offset space, up
to 32 ranges per file, with both sizes.

Harness: [`v0916_local_compare.py`](v0916_local_compare.py); renderer:
[`render_table.py`](render_table.py); explanations:
[`explanations.json`](explanations.json); path-free results with full
hashes, sizes, exit codes, operation counts and durations:
[`v0916-local-comparison.json`](v0916-local-comparison.json). The table shows
the first 16 hex digits of each SHA-256.

Rerun from the worktree root with `NFC_TEST_AREA_ROOT` set and TEMP, TMP and
TMPDIR set to its `temp` child:

```text
python docs/handoff/1.1.12/parity/v0916_local_compare.py --work-dir <test-area>/parity-1112/<new-run> --candidate-cli <candidate>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --candidate-label "<label>" --baseline-cli <predecessor>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --baseline-label "<label>" --table-json <test-area>/parity-1112/<new-run>-table.json [--pause-flag <file>]
python docs/handoff/1.1.12/parity/render_table.py --table-json docs/handoff/1.1.12/parity/v0916-local-comparison.json --explanations docs/handoff/1.1.12/parity/explanations.json --output docs/handoff/1.1.12/parity/v0916-local-comparison.md
```

Duration of the 2026-09-25 run: 659 s wall clock with no timing pauses;
14 s to materialize and validate the canonical snapshot; 501 s of candidate
and 141 s of v0.9.16 execution. v0.9.16 CLI invocations average 1.5 s (95 runs), the
candidate's 4.9 s (103 runs), so the candidate side dominates.

<!-- table -->

| # | IC | Workflow | IC count | Map variant | Proof | Runnable | Result | Differing ranges (file offsets, half-open) | v0.9.16 sha256 | Candidate sha256 | Proposed explanation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | NT51917 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-full-flash` | exact | yes | equal | none | `fdb8fef05bdb375e` | `fdb8fef05bdb375e` | No difference. |
| 2 | NT51917 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-tp-work-212k` | TP prefix | yes | equal | none | - | `6a1bbb51faaad6fe` | No difference. |
| 3 | NT51917 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 4 | NT51917 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 5 | NT51917 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 6 | NT51917 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 7 | NT51917 | Standard | selector-free | `nt51927-standard-merge-256k` | exact | yes | equal | none | `8e0d362b74ba65df` | `8e0d362b74ba65df` | No difference. |
| 8 | NT51919 | AB | selector-free | `nt51919-ab-merge-512k` | exact | yes | equal | none | `c7e1e263ac8ca70f` | `c7e1e263ac8ca70f` | No difference. |
| 9 | NT51919 | CtrlRAM | 1-ic | `nt51929-ctrlram-fw200-single-full-flash` | exact | yes | equal | none | `d23f53a13db3c6fc` | `d23f53a13db3c6fc` | No difference. |
| 10 | NT51919 | CtrlRAM | 2-8-ic | `nt51929-ctrlram-fw1x-cascade-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 11 | NT51919 | Standard | selector-free | `nt51919-standard-merge-256k` | exact | yes | equal | none | `fdba72984d662c4d` | `fdba72984d662c4d` | No difference. |
| 12 | NT51923 | CtrlRAM | 1-ic | `nt51923-ctrlram-fw141-single-full-flash` | exact | yes | equal | none | `4759a8e87ad7ff8a` | `4759a8e87ad7ff8a` | No difference. |
| 13 | NT51923 | CtrlRAM | 1-ic | `nt51923-ctrlram-fw141-single-tp-work-240k` | TP prefix | yes | equal | none | - | `6d14818a4b11ed9c` | No difference. |
| 14 | NT51923 | CtrlRAM | 2-plus-ic | `nt51923-ctrlram-fw141-cascade3-full-flash` | exact | yes | equal | none | `017a157ba2419ff2` | `017a157ba2419ff2` | No difference. |
| 15 | NT51923 | CtrlRAM | 2-plus-ic | `nt51923-ctrlram-fw141-cascade3-tp-work-240k` | TP prefix | yes | equal | none | - | `7b7cb25f6c267d2a` | No difference. |
| 16 | NT51923 | Standard | selector-free | `nt51923-standard-merge-256k` | exact | yes | equal | none | `b611bd837991be1c` | `b611bd837991be1c` | No difference. |
| 17 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw141-full-flash-256k` | exact | yes | equal | none | `30de4735472950e3` | `30de4735472950e3` | No difference. |
| 18 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw141-tp-work-240k` | exact | yes | equal | none | `22888325a4c15a14` | `22888325a4c15a14` | No difference. |
| 19 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw200-full-flash-256k` | exact | yes | equal | none | `5f8913e48784bf0c` | `5f8913e48784bf0c` | No difference. |
| 20 | NT51926 | CtrlRAM | 1-ic | `nt51926-ctrlram-fw200-tp-work-240k` | exact | yes | equal | none | `f1c4a61299741af4` | `f1c4a61299741af4` | No difference. |
| 21 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw141-full-flash-256k` | exact | yes | equal | none | `a2169f9fc908207d` | `a2169f9fc908207d` | No difference. |
| 22 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw141-tp-work-240k` | exact | yes | equal | none | `ad6b5cd0b8a3babe` | `ad6b5cd0b8a3babe` | No difference. |
| 23 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw200-full-flash-256k` | exact | yes | equal | none | `b4336e3d935466fe` | `b4336e3d935466fe` | No difference. |
| 24 | NT51926 | CtrlRAM | 2-plus-ic | `nt51926-ctrlram-fw200-tp-work-240k` | exact | yes | equal | none | `2e79b7634219c9e6` | `2e79b7634219c9e6` | No difference. |
| 25 | NT51926 | Standard | selector-free | `nt51926-standard-merge-256k` | exact | yes | equal | none | `910c302495ef037b` | `910c302495ef037b` | No difference. |
| 26 | NT51927 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-full-flash` | exact | yes | equal | none | `fdb8fef05bdb375e` | `fdb8fef05bdb375e` | No difference. |
| 27 | NT51927 | CtrlRAM | 1-ic | `nt51927-ctrlram-fw141-single-tp-work-212k` | TP prefix | yes | equal | none | - | `6a1bbb51faaad6fe` | No difference. |
| 28 | NT51927 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 29 | NT51927 | CtrlRAM | 2-ic | `nt51927-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 30 | NT51927 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 31 | NT51927 | CtrlRAM | 3-ic | `nt51927-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 32 | NT51927 | Standard | selector-free | `nt51927-standard-merge-256k` | exact | yes | equal | none | `8e0d362b74ba65df` | `8e0d362b74ba65df` | No difference. |
| 33 | NT51928 | CtrlRAM | 1-ic | `nt51928-ctrlram-fw141-single-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 34 | NT51928 | CtrlRAM | 1-ic | `nt51928-ctrlram-fw141-single-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 35 | NT51928 | CtrlRAM | 2-ic | `nt51928-ctrlram-fw132-twochip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 36 | NT51928 | CtrlRAM | 2-ic | `nt51928-ctrlram-fw132-twochip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 37 | NT51928 | CtrlRAM | 3-ic | `nt51928-ctrlram-fw140-threechip-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 38 | NT51928 | CtrlRAM | 3-ic | `nt51928-ctrlram-fw140-threechip-tp-work-212k` | TP prefix | no | not-covered | - | - | - | Not covered: no canonical input. |
| 39 | NT51928 | Standard | selector-free | `nt51928-dual-capacity-256k-512k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 40 | NT51929 | AB | selector-free | `nt51929-ab-merge-512k` | exact | yes | equal | none | `c7e1e263ac8ca70f` | `c7e1e263ac8ca70f` | No difference. |
| 41 | NT51929 | CtrlRAM | 1-ic | `nt51929-ctrlram-fw200-single-full-flash` | exact | yes | equal | none | `d23f53a13db3c6fc` | `d23f53a13db3c6fc` | No difference. |
| 42 | NT51929 | CtrlRAM | 2-8-ic | `nt51929-ctrlram-fw1x-cascade-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 43 | NT51929 | Standard | selector-free | `nt51929-standard-merge-256k` | exact | yes | equal | none | `fdba72984d662c4d` | `fdba72984d662c4d` | No difference. |
| 44 | NT51932 | AB | selector-free | `nt51932-ab-merge-512k` | exact | yes | equal | none | `c7e1e263ac8ca70f` | `c7e1e263ac8ca70f` | No difference. |
| 45 | NT51932 | CtrlRAM | 1-ic | `nt51932-ctrlram-fw1x-single-full-flash` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 46 | NT51932 | CtrlRAM | 2-8-ic | `nt51932-ctrlram-fw200-cascade-full-flash` | exact | yes | equal | none | `0e59a2fbaab16979` | `0e59a2fbaab16979` | No difference. |
| 47 | NT51932 | Standard | selector-free | `nt51932-standard-merge-256k` | exact | yes | equal | none | `6a0c90b1c3145361` | `6a0c90b1c3145361` | No difference. |
| 48 | NT51950 | AB | 1-ic | `nt51950-ab-merge-512k` | exact | yes | equal | none | `d18db8dc02ab4ff5` | `d18db8dc02ab4ff5` | No difference. |
| 49 | NT51950 | AB | 2-plus-ic | `nt51950-ab-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 50 | NT51950 | CtrlRAM | 1-ic | `nt51950-ctrlram-fw200-single-full-flash` | exact | yes | equal | none | `a32e6896b840d44e` | `a32e6896b840d44e` | No difference. |
| 51 | NT51950 | CtrlRAM | 1-ic | `nt51950-ctrlram-fw200-single-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 52 | NT51950 | CtrlRAM | 2-ic | `nt51950-ctrlram-fw1x-cascade-full-flash` | exact | yes | error | - | - | `1536d344af83aafd` | v0.9.16 only. Both build the same plan-bound 512 KiB precursor base (`ff9ad012...aa32`); v0.9.16 blocks with `profile.v2.compile.map-selection-invalid` because the route map declares 256 KiB. The 1.1.12 candidate admits the base as a Display-OSD envelope over the 256 KiB template and writes `1536d344...4ebd`, the registered-Combiner output of the NT51951 2-IC route, which differs from the Golden expected output only in the four approved CRC words (fact; CTRLRAM-OSD-ENVELOPE-1112-01; BUG-20260925-nt51950-cascade-ctrlram-plan-base) |
| 53 | NT51950 | CtrlRAM | 2-ic | `nt51950-ctrlram-fw1x-cascade-tp-work` | exact | yes | different | 2816 bytes in 5: [0xA11C,0xA120), [0xA130,0xA134), [0x2D428,0x2D42C), [0x2D43C,0x2D440), [0x33B10,0x34600); sizes 225280 / 225280 bytes | `cfae15911aac4ef6` | `a239645dd6e2527a` | Intended Diff NF preservation (0.10.1, `99766df75`, #188): v0.9.16 copies the whole 5,120-byte Diff CtrlRAM record `[0x33200,0x34600)`, the candidate only the 2,320-byte active span `[0x33200,0x33B10)`; the preserved NF tail `[0x33B10,0x34600)` and the four Header and backup-Header CRC words differ. Ranges and byte count equal the plan's owner-approved NT51951 correction, and the NT51950 alias case declares the same fact scope. The plan names the correction only for the NT51951 route; the owner approved it for this route on 2026-09-26 (fact for the ranges and the operation ranges, proposed for the intent; CHANGELOG 0.10.1 'Reviewed firmware routes and exact preservation semantics'; CHANGELOG 1.0.0; ADR 0057; alias case nt51950-cascade2-geometry-nt51951-auto-prj-599-alias; BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing) |
| 54 | NT51950 | Standard | selector-free | `nt51950-standard-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 55 | NT51950 | Standard | selector-free | `nt51950-standard-merge-256k` | exact | yes | equal | none | `11932f352c3268dc` | `11932f352c3268dc` | No difference. |
| 56 | NT51950 | Standard | selector-free | `nt51950-standard-merge-512k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 57 | NT51951 | AB | selector-free | `nt51951-ab-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 58 | NT51951 | CtrlRAM | 1-ic | `nt51951-ctrlram-fw200-single-full-flash` | exact | yes | equal | none | `64ffa21a36a3a956` | `64ffa21a36a3a956` | No difference. |
| 59 | NT51951 | CtrlRAM | 1-ic | `nt51951-ctrlram-fw200-single-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 60 | NT51951 | CtrlRAM | 2-ic | `nt51951-ctrlram-fw1x-cascade-full-flash` | exact | yes | different | 2816 bytes in 5: [0xA11C,0xA120), [0xA130,0xA134), [0x2D428,0x2D42C), [0x2D43C,0x2D440), [0x33B10,0x34600); sizes 524288 / 524288 bytes | `7d657a3d0abc2cc6` | `1536d344af83aafd` | Owner-approved Diff NF preservation (owner decision 2026-08-28): both output hashes, the 2,816-byte count and all five ranges equal the plan's `approvedSemanticCorrections` row; the rebuilt bases are identical. v0.9.16 copies the whole Diff record `[0x33200,0x34600)`, the candidate only `[0x33200,0x33B10)` (fact; plan approvedSemanticCorrections; ADR 0057; CHANGELOG 1.0.0 and 0.10.1) |
| 61 | NT51951 | CtrlRAM | 2-ic | `nt51951-ctrlram-fw1x-cascade-tp-work` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 62 | NT51951 | Standard | selector-free | `nt51951-standard-merge-1024k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 63 | NT51951 | Standard | selector-free | `nt51951-standard-merge-256k` | exact | no | not-covered | - | - | - | Not covered: no canonical input. |
| 64 | NT51951 | Standard | selector-free | `nt51951-standard-merge-512k` | exact | yes | equal | none | `3266fae2f4873a7f` | `3266fae2f4873a7f` | No difference. |
