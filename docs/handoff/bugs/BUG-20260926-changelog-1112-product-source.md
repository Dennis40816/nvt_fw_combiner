# BUG-20260926-changelog-1112-product-source: the 1.1.12 notes name the wrong product source

Status: fixed (corrected in the 1.1.13 changelog by commit `96621a65a`; released in v1.1.13, `1268f72d2`, 2026-09-28)
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
Resolution: not fixed at the time of finding.

Fixed (2026-09-29): the 1.1.13 changelog (`CHANGELOG.md`, 1.1.13 section) now states "the v1.1.12 release product source was `30b17e699`; the non-certifying v0.9.16 comparison cited there ran against `badc545b0`, not the released product source" (commit `96621a65a`), an ancestor of the v1.1.13 release merge `1268f72d2` (#479, published 2026-09-28). The 1.1.13 comparison against v0.9.16 (parity P-2) moved to 1.2.x (board decision 167).
