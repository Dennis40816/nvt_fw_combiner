# Experience and Operation Rules

This document expands the product rules summarized in `SPEC.md` section 7.5. These rules constrain supported firmware composition; they do not constrain the standalone raw-BIN Hex Editor defined by ADR 0014.

## Replace and Merge Authoring

- **DP Replace**: DP may be whole or profile-declared partitions. LD replacement belongs to DP Replace and may use a separate LD slot. TP-specific replace personas are not exposed.
- **CtrlRAM Replace**: only named regions or groups tagged `tp-ctrlram` are replaceable.
- **General Replace**: explicit mappings are available only in profile `explicit-range` access. Protected regions remain blocked. A TP-classified mapping must select an approved legacy Combiner CRC/header refresh after mutation or fail closed.
- **General Merge**: input cardinality is extensible and every mapping compiles to standard operations over a blank image.
- **Hex Editor**: follows ADR 0014. It is a raw in-memory BIN utility with no firmware support claim.

## Operation Algebra

Only these composition primitives are allowed:

```text
initialize-image
create-work-buffer
copy-range
fill-range
patch-scalar
replace-range
run-external-processor
assert-range
validate-checksum
extract-metadata
finalize-output
```

Each operation declares an id, sequence, source/target spaces and ranges, overlap policy, pre/postconditions, and reason. UI authoring interactions do not mutate bytes directly.

## Integrity Authority

Do not model integrity as `needsCrc: bool`.

```text
IntegrityDisposition: none | verify-existing | recalculate-and-write
ProcessorAuthority: calculate | transform
```

Inventory data may be `unknown`, but a supported profile may not. A transform may mutate only a host-created staging copy and only declared write ranges. The host independently validates the resulting diff.

## Range and Mutation Invariants

- Internal ranges are half-open `[start, endExclusive)`.
- JSON uses `start` plus `length`; UI may additionally display an inclusive end.
- Arithmetic is checked; overflow and out-of-bounds fail before execution.
- Overlap rejects by default and must be explicitly declared per operation.
- Every mutation records operation id, target space/range, before/after digest, changed ranges, and reason.
