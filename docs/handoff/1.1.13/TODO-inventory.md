# 1.1.13 TODO inventory

Commander, 2026-09-27, after #459. The owner asked that progress be counted
over every item scheduled for 1.1.13, not only the scope table in
[`../1.1.13.md`](../1.1.13.md). This list breaks that table into separately
deliverable items and adds the records made outside it, the bugs whose owner
is 1.1.13 and the release steps. States: done (merged into `1.1.x` or closed
by evidence), active (someone is working on it), waiting (blocked on the owner,
another item or a quiet machine), open (not started). The owner may add or
remove items; the board decisions stay the authority for scope.

## Done (25)

| # | Item | Where |
| --- | --- | --- |
| 1 | CLI report receipt after a failed `--report` write | #457 |
| 2 | F07 residuals and F-10 comment | #457 |
| 3 | CLI report bundle-artifact guard | #457 |
| 4 | Envelope root comment | #457 |
| 5 | CI core-shard diagnosis | board (R0) |
| 6 | Release re-run conflict: interim option A (decision 23) | board (R0) |
| 7 | CI H1 start/readiness deadline split | #458 |
| 8 | CI failure evidence (R3) | #458 |
| 9 | NVT end-flag rule and its release pin (R3) | #458 |
| 10 | F08 retryable save notice | #458 |
| 11 | Report publication after every fallible step | #458 |
| 12 | CLI `--report` atomic write | #458 |
| 13 | VersionManagement local schema references | #458 |
| 14 | One JSON metadata context per bundle root | #458 |
| 15 | Roadmap history archive | #458 |
| 16 | Agent model routing tiers (decision 56) | #458 |
| 17 | ADR 0077 pre-built catalog accepted | #458 |
| 18 | Local user paths removed from public documents | #459 |
| 19 | CLI refusal of an in-flight reload (decision 68) | #459 |
| 20 | Predecessor comparison contracts and ADR 0078 (parity P-1) | #459 |
| 21 | v0.9.16 1.x amendment (R3) | #459 |
| 22 | ADR 0079 test architecture | #459 |
| 23 | NVT reader XML documentation pointers | #459 |
| 24 | Display OSD marker bug closed: fixed by the NVT end-flag rule | #458 (tests inverted) |
| 25 | Bug ledger statuses brought up to date for #457-#459 | board |

## Active (6)

| # | Item | Next |
| --- | --- | --- |
| 26 | C-7 TP SVN display (R3) | decisions 93-94, delta re-review, admission, ADR 0075 memory gate, firmware and release attestations |
| 27 | Navigation focus underline (decision 32) | render the focus ring (review F-3), re-review |
| 28 | Wave 5 F03/F06 process cleanup and ADR 0081 | admission and implementation; owner acceptance of ADR 0081 |
| 29 | Test local-state isolation and the ADR 0079 amendment | admission and implementation |
| 30 | G0: GitHub App, rulesets and settings, including A-5 | owner runs the scripts |
| 31 | WS-GOV governance reset (ADR 0080) | on `feature/1.1.13/ws-gov`; integration after G0 |

## Waiting or open (27)

| # | Item | Blocked on |
| --- | --- | --- |
| 32 | Header backup CRC (R3), with the stale B-normalization Header CRC bug | owner's six firmware facts |
| 33 | F20/F21 residuals (stale picker and I/O results) | open |
| 34 | Release workflow cleanup R-1 | G0 |
| 35 | Release workflow cleanup R-2 | R-1 |
| 36 | WS-AI dual-runtime agent documents, with the two agent-skill bugs | open |
| 37 | WS-TEST pilot split (T1, T2a) and UiSmoke measurement U0 | open; U0 needs a quiet machine |
| 38 | WS-FLOW F1 second half, F5 and F6 re-evaluation | open |
| 39 | Parity P-2 comparator and the v0.9.16 baseline executor (decision 79), with the baseline-restore and cascade-plan bugs | open (P-1 merged) |
| 40 | ADR 0077 Step 0 measurement | quiet machine |
| 41 | ADR 0077 B2a (R2) | Step 0 |
| 42 | ADR 0077 B2b trust policy (R3) | B2a |
| 43 | First window within the EXE size ceiling | ADR 0077 |
| 44 | Wave 5 window lifetime F01/F02/F25 (decisions 87, 88) | wave 5 F03/F06 |
| 45 | Version-manager state test isolation (1.1.13 or later) | open |
| 46 | Process start failure escapes the typed result | open |
| 47 | `verify.py --all` help text | WS-GOV |
| 48 | Version-branch CI gap | WS-GOV |
| 49 | Test hygiene findings (WS-TEST T2-T4, G2) | WS-TEST |
| 50 | UiSmoke headless session stall | root cause found (Avalonia.Headless setup race); fix: fail-fast diagnostics and off-session Avalonia use |
| 51 | Deploy post-hash attack test race | open |
| 52 | Legacy Combiner long path | open |
| 53 | CI core-shard H2 and H3 follow-ups | open |
| 54 | CI failure evidence: controlled cross-attempt re-run | a real CI failure to re-run |
| 55 | Document at the line ceiling | open |
| 56 | Private-string check rejects user-profile paths in new changes (rest of the public-paths bug) | open |
| 57 | 1.1.13 release notes, including the 1.1.12 changelog correction and decision 84 compatibility | release |
| 58 | Release: candidate, all Golden cases, owner approvals, promotion and publication | all items above |

Not 1.1.13: the display-convention inventory (1.1.14, decision 39); the 1.2.1
architecture and UI reviews and Launcher; CtrlRAM cold first-open and F14/F15
(1.2.8); terminal v0.9.16 certification (2.0.0).
