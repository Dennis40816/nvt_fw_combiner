# ADR 0052: Bind CtrlRAM report metadata to an exact Standard map

- Status: Accepted
- Date: 2026-08-22
- Accepted: 2026-08-22 by the repository owner
- Owners: Product owner + architecture owner
- Risk: R2 architecture and package-policy contract; no firmware-byte change
- Amends: ADR 0046

## Context

CtrlRAM Replace reports reuse metadata structures and report classifications
owned by the same IC's Standard profile. The previous adapter searched Standard
registrations at runtime and selected a candidate from reference capacity or TP
input length. That heuristic duplicated map-selection policy outside the
profile compiler and could silently choose another map with the same capacity.
It also made a missing counterpart appear as a late warning instead of a
package-admission defect.

The package already has one hash-pinned runtime-registration authority, and the
Standard profile/compiler already has one exact map-materialization authority.
The missing contract is only the explicit cross-workflow reference between
them.

## Decision

Package trust-index schema `1.1` adds optional `reportMetadataMapId`, permitted
only on `ctrlram-replace` runtime registrations.

- If the exact same-IC Standard profile contains report-classification
  metadata, every corresponding CtrlRAM registration must declare one exact
  map id.
- If that Standard profile contains no report-classification metadata, the
  CtrlRAM registration must omit the field.
- The field is a reference only. Map geometry, capacity, selection groups,
  metadata structures, purposes, and formatters remain owned by the Standard
  profile/family and canonical profile compiler.
- For a Standard profile with a selection group, admission compiles only its
  existing bounded selection states (none and the declared group) and accepts
  the unique candidate whose compiled map id equals the declared reference.
  Capacity and TP input length never identify the counterpart.
- Infrastructure validates the complete trust-index candidate before route
  publication. It resolves the exact same-IC Standard registration, compiles
  the declared map through the existing Standard registration/compiler path,
  keeps only report-classification entries, and rebases their input slot to the
  immutable CtrlRAM reference base.
- Missing Standard registration, missing or extraneous field, unknown map,
  cross-IC map, same-capacity substitute, or materialized-map mismatch rejects
  the complete candidate. No partial CtrlRAM registry is published.
- The admitted immutable plan is stored on the CtrlRAM route. The adapter
  returns it directly; it does not search, rank, deduplicate, or fall back.
- ADR 0046 capability semantics bind the declared report map id. Application
  independently derives the actual map id from the materialized metadata plan
  and rejects a mismatch during compilation binding.

The reviewed built-in inventory has 25 CtrlRAM runtime registrations: 19
reportful and 6 reportless. Those registrations project 33 canonical routes:
23 reportful and 10 reportless. Capability-policy catalog `1.8.0` supersedes
only the 23 affected route fingerprints and their three pinned decisions.

## Consequences

### Read-only memory overview extension (v1.1.4)

The owner-approved CtrlRAM overview may reuse the exact companion retained by
the immutable metadata plan, including its trusted source identity, to locate
declared TP/DP sections. Application publishes this separate read-only context
in the same memory-layout projection. It does not authorize selection, writes,
mapping/access, or capacity decisions, and does not replace the primary CtrlRAM
map, coverage, metadata values, or Report authority. Actual output capacity only
limits visibility of already-selected whole sections; it never selects a map.
Missing or ambiguous companion context displays neutrally, without invented
TP/DP labels. Existing package-admission failures remain failures.
Outermost TP/DP Code regions remain whole: their declared descendants, including
Header, CRC and Unmapped children, do not become overview cuts. Only outer gaps
outside those Code trees are displayed separately; ancestry, not names, proves
containment. Unrelated overlapping classifications fail closed to Context.

#### Reportless context extension (Unit58 candidate; final owner attestation pending)

Schema `1.3` permits an independent optional `memoryLayoutContextMapId`
reference. Admission reuses the exact same-IC Standard registration's
`GetMapVariants` materialization; it never creates a Report metadata plan or a
second map. The immutable context and exact source identity are carried through
the dynamic route, definition, publication and compiled capability, checked
against its fingerprint bindings. Existing reportful routes retain the overview
fallback above. Explicit display and Report counterparts, if both present, must
agree. Unknown/cross-IC/unmaterialized maps or address-space disagreement reject
the candidate before publication.

DP Image containers are distinct from DP Code. A declared descendant TP Code
region takes its own interval; the remaining container intervals retain the
display label `DP`, not an invented DP Code complement. Technical details retain
the DP Image container distinction. This follows the owner's subsequent label
clarification and supersedes only the original `DP image` display wording;
the historical admission remains unchanged. Fine-grained descendants
do not add overview cuts. A container not wholly present in the actual output
is excluded; this does not select another map. Thus a TP-work image cannot gain
a complete DP image claim. Unrelated overlaps remain neutral. This extension
changes location context and its explicit package binding, not firmware facts,
execution, Report authority, evidence rank or publication decisions.

- CtrlRAM report metadata has one explicit, reviewable counterpart and no
  capacity-, filename-, PID-, hash-, or input-length inference.
- The package trust index does not become a firmware map catalog.
- Startup fails closed before authoring selectors can expose an incoherent
  CtrlRAM registry.
- A defense-in-depth Application check prevents an admitted route from being
  rebound to metadata materialized from another map.
- Output bytes, ranges, operation order, processor authority, report values,
  evidence rank, publication status, naming, and UI remain unchanged.

### AB bank viewport extension — 2026-09-22

The Application memory projector exposes immutable bank locators from the
complete accepted AB map, including banks not selected for replacement.
Locators retain canonical bank identity, address space and checked output
range; selected execution obligations must agree with their locators. Standard
and logical layouts expose no bank locators. No bank is inferred from capacity,
file names or UI labels, and no Standard counterpart is reselected.

Section locators remain the complete authoritative output partition. A
Presentation bank viewport may show their intersections with its selected
bank, retaining absolute Reference addresses and canonical facts. This display
selection is page-local: it never changes drafts, revisions, leases, inspection,
Report or executable operations.

The follow-up CtrlRAM focus projection retains two distinct identities: each
segment's exact containing AB canonical region, and an immutable attribution
to its original local canonical CtrlRAM region/map plus checked bank placement.
The accepted composite definition binds selected local compilations to one
shared declaration; that geometry also describes preserved banks, without
inventing unselected execution obligations or copying bank metadata. Discovery
ranges must match that local map one-to-one before checked translation into
the complete AB address space. No translated FirmwareRegion is manufactured.
The existing coverage/operation projection remains the source of write state,
contributors and partial-kept source grouping. Presentation filters to the
typed viewport and uses its origin for bar proportions; labels and hover
retain absolute addresses. This adds no firmware or support authority.

### Automatic Reference classification and complete bank facts — 2026-09-22

Application reuses the current canonical publication and the existing trusted
NT51929 AB/local pair for read-only Reference classification. Complete bank
placements come from the exact AB map, not selected replacement obligations.
Existing metadata readers and profile-owned structural guards inspect slices of
the same captured Reference. Bank observations retain their identity and absolute
Reference range. Local metadata plans remain local; they are not relabelled as
full AB plans.

Complete Reference observations are independent of A/B/Both replacement selection
and the memory viewport. Neither classification nor observation grants execution
or support authority. Exact route readiness, candidate disclosure, immutable-input
admission and selected-bank execution validations remain mandatory. Ambiguous
declarations, stale publication and invalid recognized AB structure are terminal;
no Standard or alternate metadata-plan fallback is introduced. The effective draft
is adopted atomically with a current successful inspection batch.

The NT51929 Standard metadata plan does not select Event Buffer Format as a
target, although its canonical General Parameters structure declares the field.
For the already admitted NT51929 AB Base classifier, the owner approved a
separate read-only projection on 2026-09-23: retain the same-publication
Standard composition and family, resolve that exact map-selected structure
against each immutable bank-local slice, and accept its Event Buffer byte only
when the resolved structure starts at the existing validated Backup location.
This projection does not amend the Standard plan's target list or add an AB
metadata binding. An optional locator/field failure yields a missing fact for
that bank only; it adds no Build issue, format/map choice or write authority.
The UI shows each bank's raw hex and approved name independently, including
zero and unknown bytes, or bank-specific Not provided. Inspection must not use
a raw offset or borrow the NT51950 format-selection policy. Equal typed
PID/Common/count values may share an A/B display label; differing values remain
bank-specific.

## Verification

- Schema and loader tests accept the 19 exact declarations and 6 exact
  omissions, and reject wrong types, invalid tokens, extraneous workflow use,
  missing, unknown, cross-IC, and same-capacity substitute maps.
- Registry tests require all 25 registrations to validate before publication.
- Canonical catalog tests require 23 reportful route bindings and 10 reportless
  omissions.
- Runtime binding tests reject a metadata plan whose actual map differs from
  the reviewed route map.
- Existing CtrlRAM capacity boundaries, plans, reports, and golden byte tests
  remain unchanged and pass the repository gate.

## Rejected options

- Keep the capacity/input-length heuristic: ambiguous and duplicates compiler
  authority.
- Put map geometry in the CtrlRAM registration: creates a second firmware-fact
  owner.
- Hard-code an IC-to-map table in C#: blocks data-only onboarding and recreates
  per-IC workflow logic.
- Treat a missing counterpart as an empty report: hides package drift and can
  misclassify output differences.
