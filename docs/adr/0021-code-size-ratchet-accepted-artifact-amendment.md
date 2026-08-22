# ADR 0021 accepted-artifact snapshot consolidation amendment

Status: accepted normative appendix to
`0021-code-size-ratchet-and-convergence.md`.

## Decision

The 2026-08-23 fixed-workflow same-locator correction removes the duplicate
fixed/General accepted-artifact aggregation and centralizes the OS-aware
locator comparer.

With ordinary multiline formatting, full production decreases from 109,955 to
109,954 nonblank lines, runtime from 75,223 to 75,222, and Application from
33,435 to 33,434. The executable base ratchets therefore descend by one to
102,896 full production, 70,056 runtime, and 30,690 Application; existing
non-transferable allowances remain unchanged.

The consolidation retains one immutable reader snapshot only when accepted
`FileStamp` and complete bytes agree, while preserving every logical binding,
source view, operation, trace, and report identity. It changes no profile,
range, operation order, firmware byte, CRC/header behavior, processor, naming,
writer, support claim, Golden expectation, or release authority.

## Verification

- `python scripts/verify.py --structure-only`
- `python -m unittest discover -s tests/scripts -p test_code_size_policy.py`
- fixed-workflow accepted-session identity regressions for Standard Merge, AB
  Merge, and CtrlRAM Replace
- `python scripts/verify.py --all`
