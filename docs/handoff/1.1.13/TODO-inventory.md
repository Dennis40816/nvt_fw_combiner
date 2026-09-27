# 1.1.13 TODO inventory

Commander, 2026-09-27, after #459. The owner asked that progress be counted
over every item scheduled for 1.1.13, not only the scope table in
[`../1.1.13.md`](../1.1.13.md). This list breaks that table into separately
deliverable items and adds the records made outside it, the bugs whose owner
is 1.1.13 and the release steps. States: done (merged into `1.1.x` or closed
by evidence), active (someone is working on it), waiting (blocked on the owner,
another item or a quiet machine), open (not started). The owner may add or
remove items; the board decisions stay the authority for scope.

## Done (23)

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

## Active (6)

| # | Item | Next |
| --- | --- | --- |
| 24 | C-7 TP SVN display (R3) | decisions 93-94, delta re-review, admission, ADR 0075 memory gate, firmware and release attestations |
| 25 | Navigation focus underline (decision 32) | render the focus ring (review F-3), re-review |
| 26 | Wave 5 F03/F06 process cleanup and ADR 0081 | admission and implementation; owner acceptance of ADR 0081 |
| 27 | Test local-state isolation and the ADR 0079 amendment | admission and implementation |
| 28 | G0: GitHub App, rulesets and settings, including A-5 | owner runs the scripts |
| 29 | WS-GOV governance reset (ADR 0080) | on `feature/1.1.13/ws-gov`; integration after G0 |

## Waiting or open (28)

| # | Item | Blocked on |
| --- | --- | --- |
| 30 | Header backup CRC (R3), with the stale B-normalization Header CRC bug | owner's six firmware facts |
| 31 | F20/F21 residuals (stale picker and I/O results) | open |
| 32 | Release workflow cleanup R-1 | G0 |
| 33 | Release workflow cleanup R-2 | R-1 |
| 34 | WS-AI dual-runtime agent documents, with the two agent-skill bugs | open |
| 35 | WS-TEST pilot split (T1, T2a) and UiSmoke measurement U0 | open; U0 needs a quiet machine |
| 36 | WS-FLOW F1 second half, F5 and F6 re-evaluation | open |
| 37 | Parity P-2 comparator and the v0.9.16 baseline executor (decision 79), with the baseline-restore and cascade-plan bugs | open (P-1 merged) |
| 38 | ADR 0077 Step 0 measurement | quiet machine |
| 39 | ADR 0077 B2a (R2) | Step 0 |
| 40 | ADR 0077 B2b trust policy (R3) | B2a |
| 41 | First window within the EXE size ceiling | ADR 0077 |
| 42 | Wave 5 window lifetime F01/F02/F25 (decisions 87, 88) | wave 5 F03/F06 |
| 43 | Version-manager state test isolation (1.1.13 or later) | open |
| 44 | Process start failure escapes the typed result | open |
| 45 | `verify.py --all` help text | WS-GOV |
| 46 | Version-branch CI gap | WS-GOV |
| 47 | Test hygiene findings (WS-TEST T2-T4, G2) | WS-TEST |
| 48 | UiSmoke headless session stall | owner permission to download a dump analyzer |
| 49 | Deploy post-hash attack test race | open |
| 50 | Legacy Combiner long path | open |
| 51 | CI core-shard H2 and H3 follow-ups | open |
| 52 | CI failure evidence: controlled cross-attempt re-run | a real CI failure to re-run |
| 53 | Document at the line ceiling | open |
| 54 | Display OSD marker bug: confirm the #458 fix and close it | open |
| 55 | Bug ledger statuses brought up to date | open |
| 56 | 1.1.13 release notes, including the 1.1.12 changelog correction and decision 84 compatibility | release |
| 57 | Release: candidate, all Golden cases, owner approvals, promotion and publication | all items above |

Not 1.1.13: the display-convention inventory (1.1.14, decision 39); the 1.2.1
architecture and UI reviews and Launcher; CtrlRAM cold first-open and F14/F15
(1.2.8); terminal v0.9.16 certification (2.0.0).
