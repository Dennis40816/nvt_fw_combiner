# BUG-20260926-changelog-1112-product-source: the 1.1.12 notes name the wrong product source

Status: open
Severity: P3
Found: 2026-09-26, rolling-parity design on `feature/1.1.13/rolling-parity`
Where: `CHANGELOG.md`, 1.1.12 section
Observed: the section calls `badc545b0` the 1.1.12 product source, but `v1.1.12` was published from `30b17e699`,
whose `src` has eight more commits, including input-classification changes; the 1.1.12 parity comparison ran
against `badc545b0`.
Expected: the notes name the released source, and the 1.1.13 comparison against v0.9.16 covers the released
source.
Owner: 1.1.13 (correction note in the 1.1.13 changelog; the rolling-parity design schedules a v0.9.16
comparison for 1.1.13).
Resolution: not fixed.
