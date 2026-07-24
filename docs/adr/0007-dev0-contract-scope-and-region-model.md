# ADR 0007: Dev0 Contract Scope, Region Model Refinements, and Saved Rule Promotion

- Status: Accepted; Dev0/Dev1 sequencing is historical
- Date: 2026-06-26
- Amended by: ADR 0015

## Context

The owner clarified three important product constraints:

1. Header regions must not be modeled as one generic `header` region. DP and TP can have distinct header semantics, write ranges, integrity behavior, and external combiner processing. The region model must distinguish DP header and TP header.
2. General Merge/Replace is not only a one-off advanced screen. A user-defined general mapping may become a saved rule and later appear as a selectable normal workflow rule. This is a core feature.
3. AB Merge often starts by copying a DP_AB artifact because the DP_AB source already contains DP plus TP layout portions. Normal Merge usually treats DP and TP as separate source inputs. The difference must be modeled by an explicit operation-order and overlay rule, not by hard-coded workflow branches.

The owner also prefers dev0 to focus on specification and contract validation, with most non-UI core implementation starting in dev1. `0.1.0` should validate and stabilize the foundation/contract. `0.1.1` should begin UI design documents and early demo interface thinking.

## Decision

### Dev0 scope

`0.1.0-dev.0` and the `0.1.0` branch are contract-first milestones. They may contain small C# primitives that validate the contract, but must not attempt broad CompositionEngine implementation.

Dev0 allowed implementation:

- half-open `ByteRange` semantics;
- deterministic byte diff;
- allowed write-range verdict;
- external combiner tool manifest DTO, validator, and registry;
- schema/policy validation scripts;
- fake/synthetic tests proving risky invariants.

Dev0 deferred implementation:

- full `CompositionEngine`;
- real profile compiler;
- real firmware copy/replace parity;
- real legacy `combiner.exe` execution;
- real IC support claims;
- UI flows beyond bootstrap shell.

### Dev1 scope

Dev1 begins after dev0 contract review. It starts most non-UI core implementation:

- typed profile loading/compilation;
- `CompositionPlan` construction;
- operation execution for copy/fill/replace/patch;
- preview/report model;
- staging workspace abstraction;
- fake external processor runner;
- synthetic merge/replace regression.

### Region model refinement

The region taxonomy must distinguish container regions from functional sub-regions. `dp` and `tp` are high-level firmware ownership regions. `dp-header` and `tp-header` are separate sub-regions, not one shared `header` bucket.

Recommended classification tags:

```text
dp
tp
dp-header
tp-header
tp-ctrlram
tp-fw
bank-a
bank-b
protected
replaceable
integrity-read
integrity-write
```

A region may have multiple tags. For example, a TP header write range can be tagged `tp`, `tp-header`, `integrity-write`, `protected`.

### Saved rule promotion

General Merge/Replace authoring produces a typed mapping overlay. If a mapping is validated and approved, it may be promoted to a saved rule.

A saved rule is not a script. It is a versioned profile fragment with:

- stable rule id;
- parent profile id and compatibility constraints;
- input slot template;
- mapping rows compiled to standard operations;
- allowed region/range envelope;
- processor dependencies;
- validation rules;
- owner/reviewer metadata;
- golden evidence when required.

A saved rule can later appear in the normal workflow catalog if the profile compiler can prove it is compatible with the selected IC/mode and safety envelope.

### Copy order rule

Operation order is a profile property, not workflow code. AB and Normal Merge must both compile to ordered operations.

- AB Merge can copy DP_AB first when the source artifact is a container that intentionally covers DP and TP layout portions.
- Standard Normal Merge should normally copy DP and TP from separate input slots into non-overlapping target regions.
- Overlap defaults to reject. Any overlapping copy must declare `overlapPolicy = allow-declared` or `replace-existing` with an explanation and validation rules.
- Copy order must be visible in Preview and mutation trace.

## Consequences

- The main engine remains generic. It executes ordered operations and validates ranges; it does not know special AB branches.
- DP header and TP header can evolve independently.
- General authoring becomes a way to create reusable profile fragments rather than a disposable manual mode.
- Dev0 does not expand into a large hidden implementation sprint.
- Dev1 can implement core engine work with a stable contract.

## Required follow-up

- Add `docs/architecture/region-model.md`.
- Add `docs/architecture/saved-rule-promotion.md`.
- Add `docs/architecture/operation-order-and-overlap-policy.md`.
- Update development tag plan with `0.1.0`, `0.1.1`, and `0.2.0` scope boundaries.
- Ensure UI design documents are scheduled for `0.1.1`.
