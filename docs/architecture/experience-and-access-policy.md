# Experience and Region Access Policy

Execution behavior and user role are orthogonal. The engine executes initialization plus ordered operations; the experience policy controls which operations the UI/profile compiler may author.

| Experience ID | Composition | Audience | Layout | Region access |
| --- | --- | --- | --- | --- |
| `standard-merge` | Merge | System | Fixed | Profile mappings only |
| `ab-merge` | Merge | System | Fixed | Profile mappings and declared relocation/integrity stages |
| `general-merge` | Merge | Advanced | User-defined | One or more inputs and explicit source-to-target mappings |
| `dp-replace` | Replace | DP | Constrained | DP whole/declared partitions |
| `ctrlram-replace` | Replace | CtrlRAM | Constrained | CtrlRAM named regions/groups tagged `tp-ctrlram` |
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
- CtrlRAM Replace exposes only regions tagged `tp-ctrlram` or approved groups that contain them.
- There is no separate TP firmware Replace category in the product taxonomy.
- IC num input mode is profile-declared as `single`, `cascade`, or `numeric`; two-option profiles use text choices such as `single`/`cascade`, while three-or-more concrete count profiles use numeric selection with future room for Other/custom exceptions.

## General mode

General mode is not scripting. It supports an extensible list of input BIN bindings and mapping rows. Every row has an explicit source range, target range, sequence, overlap policy, reason, and validation result. Mappings compile to the same `copy-range`/`replace-range` operations used by fixed profiles.
