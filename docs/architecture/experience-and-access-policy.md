# Experience and Region Access Policy

Execution behavior and user role are orthogonal. The engine executes initialization plus ordered operations; the experience policy controls which operations the UI/profile compiler may author.

| Experience ID | Composition | Audience | Layout | Region access |
| --- | --- | --- | --- | --- |
| `standard-merge` | Merge | System | Fixed | Profile mappings only |
| `ab-merge` | Merge | System | Fixed | Profile mappings and declared relocation/integrity stages |
| `general-merge` | Merge | Advanced | User-defined | One or more inputs and explicit source-to-target mappings |
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
- The dedicated DP Replace experience is retired in 1.1.10; canonical DP and LDC regions remain available only under the surviving experiences' declared policies.
- CtrlRAM Replace exposes only physical regions with `owner = tp` and `kind = ctrlram`, or approved
  groups composed only of those regions.
- There is no separate TP firmware Replace category in the product taxonomy.
- IC num input mode is profile-declared as `single`, `cascade`, or `numeric`; two-option profiles use text choices such as `single`/`cascade`, while three-or-more concrete count profiles use numeric selection with future room for Other/custom exceptions.

### Retired DP execution and preserved facts

The owner moved DP Replace retirement into 1.1.10. Active policy and package
admission reject `dp-replace`; the old CLI command fails without falling through
to General or producing output/report files. The existing Profiles compiler also
rejects old trusted DP declarations before returning an executable or plan-only
artifact. Earlier validation failures keep their existing typed results.

Retirement leaves the shared Replace operation model, Standard DP inputs, DP/LDC
regions, DPCMI and family relationships intact. Generic full-image inspection and
CtrlRAM Reference metadata use explicit canonical family views through the shared
catalog and inspector. They do not require a DP execution profile. Historical
DP Report/History identities remain readable. See the retirement amendment in
[ADR 0005](../adr/0005-replace-personas-and-general-mapping.md).

AB FlashCode replacement still requires explicit format/topology, bank,
header/CRC/backup preservation and processor authority. Shared metadata or
Standard/AB Merge support does not supply those missing firmware contracts.

Golden readiness is display/audit metadata, orthogonal to access. `Evidence open` does not disable a workflow whose executable/safety contract exists. `Not available` is used only when that contract is absent, and the UI must show the reason and opening condition.

## General mode

General mode is not scripting. It supports an extensible list of input BIN bindings and mapping rows. Every row has an explicit source range, target range, sequence, overlap policy, reason, and validation result. Mappings compile to the same `copy-range`/`replace-range` operations used by fixed profiles.
