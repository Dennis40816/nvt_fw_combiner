# 1.1.13 TODO inventory

Commander, 2026-09-27, after #459; statuses refreshed after #462 the same day and after #468 on 2026-09-28. The owner asked that progress be counted
over every item scheduled for 1.1.13, not only the scope table in
[`../1.1.13.md`](../1.1.13.md). This list breaks that table into separately
deliverable items and adds the records made outside it, the bugs whose owner
is 1.1.13 and the release steps. States: done (merged into `1.1.x` or closed
by evidence), active (someone is working on it), waiting (blocked on the owner,
another item or a quiet machine), open (not started). The owner may add or
remove items; the board decisions stay the authority for scope.

## Done (45)

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
| 27 | Navigation focus underline (decision 32) | #461 |
| 28 | Wave 5 F03/F06 process cleanup and ADR 0081 | #461 |
| 29 | Test local-state isolation and the ADR 0079 amendment | #461 |
| 50 | UiSmoke headless session stall: fail-fast guard (U1) | #464 |
| 60 | `MemoryCoverageBar` one rebuild per load (decision 112) | #464 |
| 61 | Local verify speed: UiSmoke partition and lane overlap (decisions 107, 128-130, 132, 134): about 24 to 14 min | #464 |
| 45 | Version-manager state test isolation (W6-C) | #468 |
| 46 | Process start failure becomes a typed result (W6-B) | #468 |
| 47 | `verify.py --all` help text | #468 |
| 51 | Deploy post-hash attack test made deterministic (W6-D) | #468 |
| 55 | Document at the line ceiling: dated handoff history archived | #468 |
| 57 | Canonical roadmap synchronized with decisions 103-104 (W6-E) | #468 |
| 40 | ADR 0077 Step 0 measurement: gate admits B2 (avoidable wall time 916.8 and 929.6 ms in two passes at `47e01ebab`; acceptance cost 66.7 ms median) | evidence `<test-area>/evidence/adr0077-step0/` |
| 31 | WS-GOV governance reset (ADR 0080): G1-A (decision 145) and G1-B cutover (decision 146) | #463, #469 (`8f5223860`) |
| 30 | G0: GitHub App, rulesets and settings, including A-5; D6, D7 and A8 done on 2026-09-28 | G0 checklist, WS-GOV log |
| 62 | Trust probe catches the typed process start failure | #470 (`93da007af`) |
| 63 | Win32 start-failure handoff test reaches the start | #470 (`93da007af`) |
| 64 | Launcher cleanup lock-thread flake | #470 (`93da007af`) |
| 65 | Archived handoff history uses test-area-relative evidence paths | #470 (`93da007af`) |
| 69 | Replace selection renders its rows on first entry | #470 (`93da007af`) |

## Active (2)

| # | Item | Next |
| --- | --- | --- |
| 26 | C-7 TP SVN display (R3) | paused behind the verify-speed work: admission on the current checkpoint, ADR 0075 memory gate, firmware and release attestations |
| 44 | Wave 5 window lifetime F01/F02/F25 (W6-A, decisions 87, 150) | three reviewable commits (UiSmoke 145/145); rebase onto the cutover trunk, then the non-UI remainder; Settings references (decision 150) wait for the owner |

## Waiting or open (19)

| # | Item | Blocked on |
| --- | --- | --- |
| 32 | Header backup CRC (R3), with the stale B-normalization Header CRC bug | owner's six firmware facts |
| 33 | F20/F21 residuals (stale picker and I/O results) | open |
| 34 | Release workflow cleanup R-1 | G0 |
| 35 | Release workflow cleanup R-2 | R-1 |
| 36 | WS-AI dual-runtime agent documents, with the two agent-skill bugs | open |
| 38 | WS-FLOW F1 second half, F5 and F6 re-evaluation | open |
| 41 | ADR 0077 B2a (R2) | Step 0 admitted it; starts after G1-B merges |
| 42 | ADR 0077 B2b trust policy (R3) | B2a |
| 43 | First window within the EXE size ceiling | ADR 0077 |
| 48 | Version-branch CI gap | WS-GOV |
| 52 | Legacy Combiner long path | open |
| 53 | CI core-shard H2 and H3 follow-ups | open |
| 54 | CI failure evidence: controlled cross-attempt re-run | a real CI failure to re-run |
| 56 | Private-string check rejects user-profile paths in new changes (rest of the public-paths bug) | open |
| 58 | 1.1.13 release notes, including the 1.1.12 changelog correction and decision 84 compatibility | release |
| 66 | main-package 1.x launcher contract (`BUG-20260927-main-package-1x-launcher-contract`, R3) | release workflow cleanup R-1/R-2 |
| 67 | G0 `gh` wrapper repository option conflict (`BUG-20260927-g0-gh-wrapper-repo-option-conflict`) | WS-GOV |
| 70 | Local verify L3: Infrastructure.Tests shares the lane pool (about 2 min; decision 153) | a quiet machine for the measurement |
| 59 | Release: candidate, all Golden cases, owner approvals, promotion and publication | all items above |

## Moved out (4)

| # | Item | Now in |
| --- | --- | --- |
| 37 | WS-TEST: U0 measured, U2a closed (decisions 113-114), local verify speed merged (#464) | 1.2.x (decision 154) |
| 39 | Parity P-2 comparator and the v0.9.16 baseline executor (decision 79), with the baseline-restore and cascade-plan bugs | 1.1.14 (decision 154) |
| 49 | Test hygiene findings (WS-TEST T2-T4, G2) | 1.2.x (decision 154) |
| 68 | Predecessor validator false inconsistency (`BUG-20260927-predecessor-validator-false-inconsistent`) | 1.1.14 (decision 154) |

Not 1.1.13: the display-convention inventory (1.1.14, decision 39); the 1.2.1
architecture and UI reviews and Launcher; CtrlRAM cold first-open and F14/F15
(1.2.8); terminal v0.9.16 certification (2.0.0).
