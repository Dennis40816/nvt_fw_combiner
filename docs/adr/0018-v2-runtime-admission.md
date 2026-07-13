# ADR 0018: V2 Runtime Admission for the Closed Merge and DP Replace Subsets

- Status: Accepted
- Date: 2026-07-12
- Owners: Product owner + architecture owner
- Amends: ADR 0015 and composition-profile-v2

## Context

The trusted profile-bundle V2 path can validate schema and trust, normalize one profile, resolve one
canonical map, and lower a closed Merge or DP Replace subset into the existing `CompositionPlan`.
`V2PlanCompiled` proves that a declaration was representable; it does not prove that every profile
policy has an Application runtime implementation. Treating all plan artifacts as executable would
allow promotion blockers, unrendered output tokens, or unsupported declarations to bypass the
trusted compiler boundary.

## Decision

`CompiledCompositionEligibility` distinguishes `V2PlanCompiled` from `V2RuntimeExecutable`.
Profiles is the only production assembly that can mint either artifact. A V2 runtime artifact requires:

- `supported` promotion with no blockers;
- trusted bundle/profile identity and a unique resolved map;
- exactly one immutable singleton space per input slot and one engine-owned output. `Merge` uses a
  blank output; `dp-replace` uses an exact, unnormalized `reference-image` clone;
- `Merge` may lower copy/fill/patch/checked-transform operations. `dp-replace` may lower a rejected
  `replace-range` from its declared DP source, followed only by a `copy-range` with
  `replace-existing` from the cloned reference input at the identical resolved half-open range. A later `copy-range` may declare
  `replace-existing` only when one earlier write in the same output space fully contains its target
  range; partial, uncovered, cross-space, and reversed writes remain rejected;
- DP short-input padding is allowed only for `dp-replace`, only at exact resolved-map capacity, and
  only when no processor or integrity stage exists. Oversized DP and every reference image remain
  exact/fail-closed;
- no metadata bindings, validations, processor stages, or extra mutable space;
- a token-free output template using the `reject` invalid-character policy.

Application accepts exactly the paired authority/eligibility tuples
`LegacyRuntimeExecutable` plus `LegacyProfileCompilationAuthority`, or `V2RuntimeExecutable` plus
`ProfileBundleV2CompilationAuthority`. It rejects every other tuple, including all
`V2PlanCompiled` artifacts. V2 request bindings must exactly match the compiled input spaces, slot
typed slot assertion, original plain filename, and accepted extension. The original filename is
reported and participates in Preview-to-Build identity. A token-free template supplies the default
output name. When `allowOverride` is false it must equal the requested output filename; when true,
Application accepts another Windows-safe plain filename. This does not add token rendering, output
paths, dynamic naming, or `replace-underscore` rendering.

The existing engine remains the only executor. Preview tokens use the complete compilation
fingerprint, and Application run reports record it; for V2 this fingerprint binds the bundle, profile
entry, resolved map, promotion, input contract, output policy, and plan.

When one trusted profile binds multiple canonical maps, the compiler requires an exact requested
capacity before resolution. It never selects a largest/default map. Existing profiles bound to one
map retain their capacity-free call path.

## Consequences

- No built-in V2 JSON profile, IC support row, firmware range, processor, CRC, or golden claim is
  added by this ADR.
- `executable-candidate`, tokenized output names, metadata/validation/processor declarations, AB,
  CtrlRAM Replace, General Replace, and every Replace shape outside the closed DP Replace contract
  remain non-executable until their closed runtime contracts are implemented.
- Runtime input byte length remains enforced by the compiled plan and existing engine input policy.

## Verification

- Domain tests lock the two V2 eligibility states and runtime minting preconditions.
- Profile contract tests lock `supported` token-free Merge/DP Replace lowering versus blocked/tokenized rejection.
- Application tests lock authority pairing, exact V2 binding provenance, output override policy, and
  Preview-to-Build fingerprint parity.
- Synthetic trusted bundle tests exercise loader, normalizer, map resolution, compiler, and the
  unchanged Application engine without representing a production firmware profile.
