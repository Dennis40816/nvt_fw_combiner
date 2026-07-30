# Operation Order and Overlap Policy

Operation order is a profile/compiler concern. It is not hard-coded by workflow type.

## Why this matters

AB Merge and Standard Merge can have different copy semantics:

- AB Merge may receive a DP_AB artifact that intentionally covers a larger container layout, including DP and TP-adjacent portions. In that case it is normal for the profile to copy DP_AB first and then overlay/patch TPA/TPB regions.
- Standard Merge normally receives DP and TP as separate inputs. It should copy them into their own declared target regions without relying on one artifact implicitly containing the other.

The engine must not contain special branches such as `if AB then paste DP first`. The profile must declare operation order and overlap policy explicitly.

## Rule 1: sequence is authoritative

Every operation has a `sequence`. The compiled plan executes operations sorted by sequence and then by operation id for deterministic ties. Duplicate sequence is allowed only when operations are proven non-overlapping and side-effect free.

## Rule 2: overlap defaults to reject

If two write operations target overlapping bytes, the compiler rejects the plan unless the later operation explicitly declares a non-default overlap policy.

Allowed policies:

| Policy | Meaning |
| --- | --- |
| `reject` | Default. Any overlap is a compile error. |
| `allow-declared` | Overlap is expected and documented. Compiler requires a reason and validation rule. |
| `replace-existing` | Later operation intentionally overrides earlier bytes. Must be visible in Preview and mutation report. |

## Rule 3: container artifacts require declared coverage

If an input artifact contains multiple logical regions, such as DP_AB, the profile must declare logical views and copy operations for that container. The executor must not infer coverage from a file name.

Source and target views also own coordinate meaning. Initial Code, DP, TP, LDC,
and TPA normally use the same firmware coordinates. TPB reads the TP-native
source view and writes it at the resolved bank placement delta. Compact CtrlRAM
is the only current built-in payload-relative case whose source begins at byte
`0` and targets a nonzero firmware region. General authoring may explicitly
choose From File Start, but the executor never infers it.

A section source needs only cover every declared read and may be supplied by a
compatible same-IC FlashCode. A complete DP AB seed or Replace Reference is
different: its exact declared container variant is authoritative outside later
overlays and cannot be admitted by section coverage alone.

Example AB shape:

```text
operation 100: copy DP_AB container view into output-image
operation 200: copy TPA view into A bank TP target
operation 300: transform TPB relocation scalars in TPB work view
operation 400: copy TPB view into B bank TP target with declared overlay if needed
operation 900: run approved combiner/header processor stage
```

Example Standard shape:

```text
operation 100: copy DP source view into DP target
operation 200: copy TP source view into TP target
operation 300: copy LDC/extra source view into extra target if declared
operation 900: run approved combiner/header processor stage if required
```

## Rule 4: preview must explain overwritten bytes

Preview must render occupancy and overwrites in operation order. If a later operation replaces existing bytes, the preview table must show:

- earlier operation id;
- later operation id;
- overlapping byte range;
- regions/tags involved;
- overlap policy;
- reason;
- validation rule id.

## Rule 5: external processor mutations are operations too

A legacy `combiner.exe` transform is a write operation. Its observed changed ranges must be compared
against the ranges compiled from `allowedWriteViewIds` and shown in the same mutation report model as
copy/replace/patch/transform operations.

## Acceptance tests

- Standard DP/TP non-overlap plan compiles.
- Standard DP/TP accidental overlap fails by default.
- AB DP_AB container followed by declared TP overlay can compile only with explicit overlap policy.
- Operation order is deterministic.
- Preview/mutation report includes declared overwrite reason.
- External processor changing outside allowed range fails.
