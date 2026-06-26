# Experience and Region Access Policy

Execution behavior and user role are orthogonal. The engine executes initialization plus ordered operations; the experience policy controls which operations the UI/profile compiler may author.

| Experience ID | Composition | Audience | Layout | Region access |
| --- | --- | --- | --- | --- |
| `standard-merge` | Merge | System | Fixed | Profile mappings only |
| `ab-merge` | Merge | System | Fixed | Profile mappings and declared relocation/integrity stages |
| `general-merge` | Merge | Advanced | User-defined | One or more inputs and explicit source-to-target mappings |
| `display-replace` | Replace | Display | Constrained | DP whole/declared partitions; TP whole only |
| `tp-hw-replace` | Replace | TP HW | Constrained | TP CtrlRAM named regions/groups; DP whole only |
| `tp-fw-replace` | Replace | TP FW | Constrained | TP non-CtrlRAM declared regions; DP whole only |
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

## TP HW and TP FW separation

- CtrlRAM membership is a canonical region attribute, not inferred from filenames or UI labels.
- TP HW exposes only regions tagged `tp-ctrlram` or approved groups that contain them.
- TP FW exposes declared TP regions not tagged `tp-ctrlram`; profile-specific header/integrity dependencies remain mandatory.
- DP is `whole` in both TP-persona workflows unless a future approved profile explicitly introduces a new experience.

## General mode

General mode is not scripting. It supports an extensible list of input BIN bindings and mapping rows. Every row has an explicit source range, target range, sequence, overlap policy, reason, and validation result. Mappings compile to the same `copy-range`/`replace-range` operations used by fixed profiles.
