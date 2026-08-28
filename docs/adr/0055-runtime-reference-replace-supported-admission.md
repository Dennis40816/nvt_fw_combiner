# ADR 0055: Runtime Reference-Replace Supported Admission

- Status: Accepted
- Date: 2026-08-25
- Owners: Repository owner + architecture owner + firmware owner
- Amends: ADR 0020 and ADR 0024
- Preserves: ADR 0038 and ADR 0046

## Context

ADR 0020 introduced `runtime-reference-replace` as a candidate-only compilation
context while runtime routing and evidence were incomplete. ADR 0024 then proved
that a TP work image and a complete FlashCode can share one immutable
reference-clone workflow: the processor receives the zero-based TP prefix and a
full-Flash-only tail is preserved byte-for-byte.

Runtime routing, typed capability binding, staging isolation, processor diff
enforcement, and owner-approved canonical evidence now exist for the declared
CtrlRAM Replace routes. The repository owner confirmed on 2026-08-25 that a TP
work BIN is a first-class base for those routes: its valid range is the same TP
prefix used by FlashCode, while FlashCode may contain an additional unrelated
tail. Keeping every runtime-reference profile below `supported` would no longer
describe the implemented contract.

The former Domain guard also mixed profile structure with publication and
evidence policy. Removing only that guard would be incomplete because the
runtime-reference compiler previously produced `V2PlanCompiled` even for a
supported profile.

## Decision

A structurally valid `runtime-reference-replace` profile may declare promotion
stage `supported`. The Domain continues to require the closed reference-clone
shape, typed immutable inputs, physical region access, and bounded processor
authority. A supported profile additionally requires an output renderer that
is admitted for runtime Replace and uses the `reject` invalid-character policy.

The Profiles compiler is the sole authority that mints execution eligibility:

- `supported` produces `V2RuntimeExecutable` through the existing shared
  `Succeed` and artifact-admission path;
- `compilable` and `executable-candidate` remain `V2PlanCompiled`; and
- unsafe output policy, invalid range, wrong topology, missing processor proof,
  or any other structural failure remains fail-closed.

This does not create a CtrlRAM executor. Preview and Build continue through the
same `CompositionPlan`, `CompositionEngine`, Application request, capability
binding, runtime proof, and external-processor host used before promotion.

For each declared CtrlRAM topology, every base resolves by exact capacity over
one TP-relative geometry. A shortened TP artifact and its larger full-Flash
container use separate exact-capacity maps. When a typed TP artifact already
spans the complete declared image capacity, it reuses that same capacity-matched
map; sharing a map never reclassifies it as FlashCode. The processor may read
and write only the profile-declared zero-based TP range. A complete FlashCode is
cloned at its complete capacity and every byte after that range remains
immutable unless a separately declared region and operation grants authority.

## Independent publication and evidence

Profile promotion does not publish product support. ADR 0038's canonical
capability policy remains the only publication authority. ADR 0038 and ADR
0046 also require evidence to bind the exact route id and capability
fingerprint.

The canonical Golden manifest therefore records each direct Golden,
owner-approved alias, synthetic oracle, or honest contract-only route
declaration explicitly. A TP expected output may be a hash-pinned prefix view
of an owner-approved full output only when execution from the original
canonical TP base has exact or owner-approved allowed-difference parity with
that view. A full-output prefix containing DP-origin bytes that are absent from
the standalone TP base is not Direct Golden evidence for the TP route. Such a
route remains independently publishable as Supported, but its evidence stays
Contract Only until an independent TP-only expected output is supplied. The
repository must declare every view and its exact route rather than infer it
from a filename, IC, version, PID, or folder.

Release promotion still requires the policy, profile bundle trust index,
canonical evidence inventory, packaged hashes, and regression tests to agree.

## Consequences

- Supported CtrlRAM profiles can execute without the historical candidate
  exception.
- General Replace candidates remain candidates until independently promoted;
  this ADR does not publish General Replace or Saved Rules.
- TP-only and full-Flash inputs use the same mappings, selector, command plan,
  report metadata, and processor write authority.
- Exact TP/full capacities, neighboring lengths, wrong topology, immutable
  inputs, full-tail preservation, processor ranges, and Golden bytes are
  permanent regression gates.
- Existing bundle directory names containing `candidate` remain trusted path
  identities and are not renamed merely for appearance.

## Non-goals

- No firmware range, CRC/header behavior, Legacy Combiner command, output
  naming rule, or expected Golden byte changes.
- No filename-, hash-, PID-, or version-derived support inference.
- No UI-only eligibility rule and no second support matrix.
- No relaxation of R3 firmware-owner, evidence, packaging, or release review.
