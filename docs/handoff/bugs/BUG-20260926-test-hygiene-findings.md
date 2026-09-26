# BUG-20260926-test-hygiene-findings: test naming and coverage gaps found by the WS-TEST design

Status: open
Severity: P3
Found: 2026-09-26, WS-TEST design on `feature/1.1.13/test-architecture`
Observed: (1) 147 test files carry a file name that differs from the class they contain, which made the 1.1.12
largest-class measurement wrong (`ShellViewModelTests` was split in 2026-08 at `acc6ea039`; counting by file
prefix merged the parts); (2) no test project references the Desktop and Launcher projects; (3) ADR 0027 still
requires every pull request to run all eight test projects and stops sharding above 300 s, which no longer
matches the 577 s CI median; (4) the WS-GOV "prose" class assumes no test reads the file, but the 2,500-line
ceiling test scans `docs/handoff`.
Expected: the WS-TEST ADR amends ADR 0027 and defines naming and coverage rules; WS-GOV G2 moves the document
checks into the structure lane before prose-only pull requests skip product tests.
Owner: WS-TEST and WS-GOV (1.1.13 design; implementation batches T2-T4 and G2).
Resolution: not fixed.
