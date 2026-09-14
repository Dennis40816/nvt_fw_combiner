# ADR 0072: Admit Event Buffer configuration separately from firmware effects

- Status: Accepted (bounded configuration-admission design; runtime integration pending)
- Date: 2026-09-14
- Authority: owner-approved Config behavior and implementation request; independent design review by `/root/v116_config_architecture`
- Risk: R2 configuration boundary; firmware integration retains R3 evidence gates

## Context

The owner-approved [Config reference and decisions](../ui/v1.1.x-custom-options-layout-handoff.md#event-buffer-format-identity-and-alias--2026-09-14)
allow selecting a predefined Unique ID, changing its alias, and adding/removing
recognition bytes. Those are not permission to create firmware profiles or edit
their effects. Existing ShellPreferenceFileStore silently uses defaults after
invalid input and swallows persistence failures; that behavior is intentionally
unsuitable for firmware-affecting configuration.

## Decision

Application owns configuration admission. A pure admission call accepts one
opaque canonical scope ID, that scope's supported identity/display-label catalog,
and draft entries. It returns either immutable normalized configuration or typed
issues. Identity keys are exact; aliases affect display only. Each identity and
recognition byte occurs at most once in that scope. Missing/invalid entries and
out-of-byte-range values fail admission. An empty recognition set is valid and
matches nothing. Empty aliases use the canonical display label. A valid nonmatch
is returned as no match, not interpreted here as a Common firmware decision.

The catalog is supplied by canonical declarations, not a second hardcoded
Application registry. This first unit introduces no Desay address, built-in
identity catalog, automatic support promotion, scope hierarchy, or overlapping
scope resolver. Its successful result means **configuration-valid for the supplied
scope**, not execution-supported or Build-authorized.

Subsequent units reuse ILocalFileStore bounded stable reads and atomic writes,
with a finite versioned JSON codec and source-generated serialization. UI and
CLI consume the same Application owner. Save validates and persists before
publishing a new immutable snapshot; invalid drafts or save failures preserve
the old effective state. Invalid external configuration yields an explicit
affected-scope blocker, not Common/default/stale-snapshot fallback. Restore
defaults affects only the draft. A running operation retains its captured state.

Profiles/Domain remain the sole owners of geometry, applicability, metadata
locations and output effects. AB integration must consume an accepted primary
observation before selecting a declared variant; it must not merely repaint
Info while retaining an old compilation. Preserve the distinction in
[ADR 0046](0046-capability-and-compilation-fingerprint-boundary.md): catalog
semantics belong to reviewed capability authority, and the effective choice and
configuration provenance must invalidate stale run/inspection acceptance.
Do not put alias text into firmware plan semantics. Exact JSON/report and
runtime selection contracts need their own concrete admission before wiring.

## Rejected options and consequences

- Reusing preference fallback would silently change Build behavior after a bad
  file. Reuse low-level IO, not its policy.
- An arbitrary rules engine, per-page ID registry or UI address branch would
  duplicate firmware authority. The editor only configures admitted identities.
- Global overlap machinery is unnecessary in the first single-scope unit.
  Later catalog integration must validate any actual overlapping scope conflict
  before claiming wider applicability.

## Implementation and verification boundary

First unit: Application's EventBufferFormatConfiguration and its admission
function, with behavioral tests for membership, aliases, exact identity keys,
byte boundaries, duplicate/conflicting values, empty sets, null input, and
caller-mutation isolation. There is no filesystem, runtime selection or UI
implementation in this unit. Later persistence, profile selection, UI and report
units retain their own tests and independent review. This ADR does not certify
Golden output, an integration candidate, or a release.

Second unit: the [versioned JSON contract](../contracts/event-buffer-format-configuration-v1.md),
bounded Infrastructure codec over existing local IO, and serialized Application
session. Missing always means no effective configuration, including after restart;
defaults and retained LastSaved are draft sources only. No first-install heuristic
or silent fallback is introduced. This is persistence/publication, not firmware
runtime integration or a claim that the approved Settings screen is implemented.

Third unit extracts the existing AB Backup topology check into one Application
`AbMergeTopologyAdmission` owner, reused by RunService. Results retain nullable
actual TPA/TPB counts (unavailable differs from decoded zero). The existing
single/cascade policy, diagnostic precedence and selector-free NT51951 guards
are unchanged; 2 and 3 remain separately observed counts, not an equality claim.
The future exact-2 Common selector must consume successful observed counts,
not the Cascade picker's minimum. This extraction adds no primary Event Buffer
locator, profile variant, geometry or Settings integration.

## Primary AB observation

The owner-approved NT51950/NT51951 partial-family AB Event Buffer decision is
a narrow exception to ADR 0012's prohibition on primary runtime metadata.
Both native TP inputs use primary FWConfig at `flash [0x22200,0x22229)` and
the canonical Event Buffer field at relative `0x0C`. Each located binding
references the same exact NT51927 provider definition; no decoder, field table
or relation is copied. The `a-tp-code` allowed-result region bounds the native
TP address range for both inputs, not the relocated output B range.

The fourth unit adds only `inspection` bindings to the real AB compiled
metadata plan. Primary and Backup may disagree: existing Backup-derived
version/count/naming remain unchanged. A successful decode can retain a false
firmware-version complement relation; `Value` is not sufficient valid-format
evidence. Missing/truncated primary cannot borrow Backup or another slot.
The format-selection owner must later consume those typed observations and
relations before admitting a declared variant. Merely adding these metadata
bindings does not make the current authoring slot status block Build.

This unit preserves every operation, output range, processor permission and
Golden expected byte. Updated profile/bundle/capability identities describe the
new metadata authority, not a support/evidence promotion. Final R3 byte/write-range
and owner evidence remain integration gates. Closed profile variants will use
their own exact admitted routes and the existing strict profile/hash/map
`BindCompilation`, not a post-compilation placement patch.

## Closed AB execution variants

The fifth unit adds closed profiles, not a generic profile inheritance or
per-request address override. Existing family region sets remain the physical
owner; each profile couples its map with the matching private bank transport,
DIFF relocation and processor invocation. The shared TP template remains
`[0xA000,0x37000)`, length `0x2D000`; the workbook's larger extent is an
erratum, not additional write authority.

| Profile | Applicability | Output / bank bytes | TP B overlay |
| --- | --- | --- | --- |
| `nt51950-ab-merge-desay` | NT51950 single or cascade | `0x100000 / 0x40000` | `[0x4A000,0x77000)` |
| `nt51951-ab-merge-desay` | NT51951, selector-free | `0x100000 / 0x40000` | `[0x4A000,0x77000)` |
| `nt51950-ab-merge-common-2ic` | NT51950, exact count 2 | `0x100000 / 0x80000` | `[0x8A000,0xB7000)` |

Only the three four-byte B header fields at TP B offsets `0x100`, `0x110`
and `0x130` can be imported after postbuild. Desay's upper half remains
DP-seeded, or `0xFF` under Dummy DP, rather than entering the smaller private
Combiner transport. Desay DP uses the existing source-view-coverage contract:
extra bytes beyond the declared 1 MiB are not copied; a source shorter than the
required view still fails. The separate expected-size warning remains pending
runtime integration and must not weaken that coverage check.

These new profiles are executable candidates for direct trusted compilation and
byte testing, **not newly published runtime routes**. Existing route identities
are repinned without widening their allowed maps or promoting evidence status.
The later format owner must consume accepted primary observations and actual
TP counts before selecting a unique admitted variant. An exact-2 map declaration
alone does not prove that the current Cascade picker observed two ICs.

Synthetic execution tests compare complete outputs against independently coded
owner ranges, including immutable inputs, Dummy/seed preservation, DIFF and
header imports. The external host's existing `ByteDiff`/`ChangedRangePolicy`
owns processor write-range rejection; the bare engine callback accepts an
already host-validated result and is not a second security validator. These
tests do not replace real Combiner/Golden certification or the final owner gate.
