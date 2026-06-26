# Dev0 Contract Scope Addendum

This addendum refines `SPEC.md` for the `0.1.0` branch. It is the working source for dev0 until the main spec is reorganized.

## Dev0 vs Dev1 decision

The owner preference is accepted:

- **Dev0 / `0.1.0`**: contract definition, risk model, small proof primitives, and validation of whether contracts need revision.
- **Dev1 / `0.2.0`**: most non-UI core implementation begins after dev0 review.
- **`0.1.1`**: UI design documents, early demo interface planning, terminal/log/report UX.

Dev0 may keep the current range/diff/manifest proof code because those classes validate the contract. Dev0 must not expand into full `CompositionEngine` implementation.

## Region refinement

Header is not one region. DP and TP headers must be independently modeled.

Minimum required header tags:

```text
dp-header
tp-header
integrity-read
integrity-write
protected
version-token
```

A TP header CRC write range should be tagged at least:

```text
tp, tp-header, integrity-write, protected
```

A DP version/header range should be tagged at least:

```text
dp, dp-header, version-token, protected
```

See `docs/architecture/region-model.md`.

## Saved General rule is core

General Merge/Replace authoring must be able to produce reusable saved rules. This is a core product feature, not a later convenience.

A saved rule is a profile fragment that compiles to normal operations. It can later become selectable in normal flows only after validation, compatibility checking, and review.

See `docs/architecture/saved-rule-promotion.md`.

## AB vs Normal copy order

AB Merge may copy a DP_AB container first because the source artifact can intentionally include a broader layout. Normal Merge should normally use separate DP and TP input slots. The difference is not an executor branch. It is modeled by:

- logical views;
- ordered operations;
- declared overlap policy;
- validation rules;
- preview/mutation trace.

See `docs/architecture/operation-order-and-overlap-policy.md`.

## Terminal/log/report planning

`0.1.1` must design terminal/log/report surfaces before UI grows. Terminal is a read-only diagnostics surface, not an arbitrary shell. Structured reports are authoritative for test and support.

See `docs/architecture/terminal-log-and-diagnostics.md` and `docs/ui/0.1.1-demo-interface-plan.md`.

## Dev0 exit checklist

- Existing contracts reviewed for DP/TP header split.
- Existing schema reviewed for whether region fields need first-class `owner` and `description`.
- Saved rule model accepted or revised.
- Operation order and overlap policy accepted or revised.
- External combiner tool runner ADR accepted or revised.
- C# proof primitives remain small and contract-focused.
- No production firmware support claim is made.
- Dev1 backlog contains non-UI core tasks.
