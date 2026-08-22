# Experience and Region Access Policy

Execution behavior and user role are orthogonal. The engine executes initialization plus ordered operations; the experience policy controls which operations the UI/profile compiler may author.

| Experience ID | Composition | Audience | Layout | Region access |
| --- | --- | --- | --- | --- |
| `standard-merge` | Merge | System | Fixed | Profile mappings only |
| `ab-merge` | Merge | System | Fixed | Profile mappings and declared relocation/integrity stages |
| `general-merge` | Merge | Advanced | User-defined | One or more inputs and explicit source-to-target mappings |
| `dp-replace` | Replace | DP | Constrained | DP whole/declared partitions |
| `ctrlram-replace` | Replace | CtrlRAM | Constrained | Physical TP CtrlRAM regions and approved all-CtrlRAM groups |
| `general-replace` | Replace | Advanced | User-defined | One or more inputs and explicit mappings subject to protected ranges |

## Access vocabulary

```text
hidden
read-only
whole
parts
explicit-range
```

Each profile compiles canonical IC regions plus experience-specific `regionAccessRules` into allowed authoring operations. The executor never branches on audience or experience ID.

## Replace policy split

- CtrlRAM membership is a canonical region attribute, not inferred from filenames or UI labels.
- DP Replace exposes only DP whole or declared DP partitions.
- CtrlRAM Replace exposes only physical regions with `owner = tp` and `kind = ctrlram`, or approved
  groups composed only of those regions.
- There is no separate TP firmware Replace category in the product taxonomy.
- IC num input mode is profile-declared as `single`, `cascade`, or `numeric`; two-option profiles use text choices such as `single`/`cascade`, while three-or-more concrete count profiles use numeric selection with future room for Other/custom exceptions.

### DP Replace eligibility and inputs

- Standard Merge and DP Replace consume the same canonical memory-map facts. DP Replace therefore requires Standard Merge exposure for the same IC, but Standard Merge alone does not automatically promote DP Replace.
- Promotion additionally requires explicit DP-owned write ranges, preserved ranges, accepted capacities, normalization, integrity/postbuild behavior, golden evidence, and firmware-owner review.
- `reference-base` is presented as **Reference FlashCode**. For the current NT51950/NT51951 DP Perspective profiles it is one complete final Standard/Normal Merge `.bin` for the selected IC, with exact capacity `0x40000`, `0x80000`, or `0x100000`.
- The current NT51950/NT51951 DP replacement slot accepts a DP/FlashCode-shaped
  `.bin` only when its length exactly equals the selected Reference FlashCode
  capacity: `0x40000`, `0x80000`, or `0x100000`. Shorter, oversized, and
  cross-capacity pairs fail closed; no padding is authorized.
- Future AB FlashCode sources require an AB-specific, profile-declared artifact shape/extractor plus explicit A/B bank, header-copy, preservation, and Legacy Combiner behavior. A UI label or generic file length must never select AB offsets.

Golden readiness is display/audit metadata, orthogonal to access. `Evidence open` does not disable a workflow whose executable/safety contract exists. `Not available` is used only when that contract is absent, and the UI must show the reason and opening condition.

## General mode

General mode is not scripting. It supports an extensible list of input BIN bindings and mapping rows. Every row has an explicit source range, target range, sequence, overlap policy, reason, and validation result. Mappings compile to the same `copy-range`/`replace-range` operations used by fixed profiles.
