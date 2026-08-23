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

## Fixed-workflow bounded content-read admission

The owner approved decimal 100 MB (`100,000,000` bytes) as the inclusive hard
resource ceiling for fixed-workflow selected-file inspection, further narrowed
only by the exact compiled slot's declared maximum. The existing Application
compiled-input inspector owns the policy. Infrastructure groups immutable typed
bindings for one selected path, passes their minimum resolved ceiling to the
existing complete-file snapshot adapter, and rejects an oversized stream length
before allocating its retained byte array. General Merge/Replace retains its
separate existing resource owner.

Relative to the preceding extension-admission checkpoint, ordinary multiline
production code grows by exactly 80 nonblank lines. Full production changes
from 109,938 to 110,018 and runtime from 75,203 to 75,283. Application changes
from 33,415 to 33,445 (+30); Infrastructure plus Contracts plus CRC worker
changes from 17,653 to 17,703 (+50). Domain plus Profiles and Bootstrap plus CLI
plus Desktop host are unchanged.

The executable allowances therefore become exactly 7,122 full production,
5,227 runtime, 2,755 Application, and 2,347
Infrastructure/Contracts/worker above the frozen pre-v0.10.6 base ratchets.
They are non-transferable exact descending ceilings. The bounded read keeps
complete immutable bytes, SHA-256, `FileStamp`, source-view trailing diagnostics,
and evidenced CtrlRAM truncation behavior unchanged. It changes no profile,
firmware range, output byte, operation order, CRC/header behavior, processor,
output naming, support, Golden, UI, General authoring, or release authority, and
does not close or fund the separate repository-wide unused-module/code-size
investigation.

Additional verification:

- Application boundaries `99,999,999`, `100,000,000`, and `100,000,001`;
- Infrastructure sparse-file rejection before materialization;
- fixed-workflow and same-path Bootstrap wiring;
- exact-container, source-view, and CtrlRAM truncation regressions; and
- independent R2 architecture/contract and scoped Polytail review.
