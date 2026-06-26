# Profile Instructions

These rules apply to `profiles/`.

- JSON is canonical and must validate against the committed JSON Schema.
- All ranges use half-open `[start, end)` semantics and hexadecimal display in documentation.
- Every id is stable, unique, lower-kebab-case, and meaningful.
- Overwrite is deny-by-default and must be explicit per operation.
- Hooks must declare ordered execution, typed parameters, and exact allowed write ranges.
- Do not encode executable code, filesystem paths, UI behavior, or shell commands in profiles.
- A profile behavior change requires a profile version decision and golden regression review.
- New IC/mode support is incomplete without a support-matrix entry, sample manifest, owner, and expected output hash.
