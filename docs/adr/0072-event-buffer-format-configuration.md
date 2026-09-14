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

## Family-owned format admission

The sixth unit adds the optional `abFormatPolicy` through the frozen
`firmware-family-v1.2-ab-format.schema.json`. Domain/Profiles own immutable
format identities, default recognition bytes, exact primary structure/field/
relation references and member/format/map variants. The opaque configuration
scope is independent of the `ab-merge` workflow ID. Common is reserved and
absent from configurable identities, but has explicit map variants. Policy
references existing map topology requirements; it declares no second set of
addresses, capacities, processor commands or IC counts.

Application `AbMergeFormatAdmission` is the sole terminal selection owner.
It reuses configuration admission against the current family catalog and
requires exactly one primary observation per declared binding, from the same
family identity/version/hash and selected member, with immutable artifact
provenance. Atomic decode and the declared firmware-version complement
relation must succeed. That relation validates the firmware-version pair,
not the separate Event Buffer field. Invalid primary, stale/missing config
or mismatched effective A/B formats produce blocking issues, never Common.
Valid nonmatches select the declared Common identity; different raw IDs can
select the same effective format. Aliases affect only the display label.

For topology-bearing members, existing `AbMergeTopologyAdmission` must first
succeed over this call's immutable `FirmwareBinInspectionArtifact` inputs and
current selector. Their slot IDs, full SHA-256 and lengths must match the primary
inspection. An unbound topology success from another file pair or selector is
never accepted. An exact-count override is selected only when both actual TP counts
match that map's declared exact requirement, after a compatible baseline is
available. The Cascade selector's minimum is not an actual-count observation;
2/3 remains on the generic cascade baseline. Selector-free NT51951 adds no
Backup requirement. A decision captures raw IDs, config generation/source hash,
family authority and the primary inspection snapshot for subsequent binding.

This pure decision and its compiled-metadata tests do not publish a new runtime
route or implement the Config UI. Existing runtime registrations retain their
map allowlists and support/evidence decisions; source identities are repinned
only. Runtime authoring, accepted-run proof, output warnings and UI integration
remain subsequent units under the same owner goal.

## Settings configuration service and editor

The seventh unit exposes the existing Application session through
`IEventBufferFormatConfigurationSession`; its implementation and constructors
remain internal. Existing immutable draft/configuration/state/issue models are
shared with Presentation, not copied into a second transport or validation
framework. The catalog is a read-only projection of the trusted registered
profile's family policy. Each configurable format retains all member/map effects
from that map's canonical `b-tp-code` region in its named address space; this
disclosure is not a currently selected runtime map or Build authority.

Bootstrap owns one lazy session per host graph. Constructing the host or visiting
ordinary Settings does not load the configuration family. The first Config
request loads the exact registered profile's trusted family without compiling
a pretend output. Concurrent consumers share the initialization task; a caller's
cancellation stops only its wait. Failed host acquisition tasks are not permanently
cached. This does not override the existing trusted bundle/registry initialization
policy: a cached catalog initialization exception may require repairing installed
files and restarting. Config must report that limitation rather than promise
same-process recovery or create a second catalog. The default file is
`%LOCALAPPDATA%/NvtFwCombiner/event-buffer-format.v1.json`, using the existing
bounded/atomic storage adapter. Tests supply an isolated path. Missing/Invalid
stays explicit, with no automatic first-install write or effective defaults.

The approved editor lives in the existing Settings modal. Identity selection
and byte membership use the Application owner; aliases remain display-only.
Defaults and last-saved entries are draft sources. Sections retain unsaved
drafts; dirty Close and Escape share discard confirmation. Persistence failures
preserve the draft and prior effective state. No successful-save wording may
claim AB input re-evaluation until the runtime consumer is actually connected.
This unit introduces no speculative global Changed event; subsequent accepted
run/inspection invalidation must use this same session, not a per-page copy.

Verification covers public-interface draft isolation, trusted effect projection,
lazy/concurrent/cancel/retry lifetime, real Save/restart reads, editor commands,
dirty-close behavior and full-reference geometry in both themes and languages.
Runtime format compilation, selected-input re-evaluation, DP-size warnings and
new-variant Golden/processor evidence remain subsequent integration work, not
implied by the existence of this editor.

## Exact dynamic route compilation

The eighth unit retains the full published `CapabilityRouteIdentity` through
the existing dynamic compilation adapter. Application resolves that identity
against the current catalog; Infrastructure resolves the trusted AB registration
by IC plus its declared map-set. An unknown map-set never falls back to another
registration for the same IC. The IC/topology convenience path returns an
explicit ambiguity issue when multiple formats match. Strict `BindCompilation`
profile/version/hash/map validation is unchanged.

The existing DP Replace prepublication contract probe remains a separately named
adapter operation, restricted to that workflow and sharing the same private
profile compilation core. It does not fabricate a published route or acquire AB
execution authority. Static route inventory still treats its map axis as an
individual map ID, not a map-set; its legacy unique-IC registration lookup fails
closed on ambiguity. AB disclosure enumerates registrations, while legacy
unique-IC compatibility readers are evaluated on demand so they cannot poison
registry initialization when variants are later registered.

This prerequisite changes no trust index, schema, registered route, profile
operation or evidence status. Runtime format discovery, new route publication,
configuration freshness and complete output evidence remain required; exact
identity transport alone does not enable Desay execution.

## Primary discovery before executable map selection

The ninth unit removes the circular requirement to compile an output map before
observing the primary format needed to choose that map. The owning family
resolves its declared A/B primary pair through the existing locator/decoder.
It selects a deterministic observation context from the member's variants,
not a caller-provided output map. Family construction requires the same
canonical primary structures, locator-dependent regions, address space,
prerequisite contexts and read envelope across those variants. Unrelated output
geometry is not compared. The policy stores no mutable family/anchor cache.

The returned context map IDs do not authorize output compilation. Missing or
rejected structures retain their canonical outcomes; a decoded bad complement
retains its false relation fact. The existing Application format admission
blocks that condition. It obtains both primary resolutions from the captured
input artifacts and evaluates topology against those same immutable bytes,
then captures A/B resolutions together with format/configuration/family proof.
NT51951 primary inspection does not require a Backup structure.

For migration, the previous compiled-snapshot overload first enforces all of
its family/member/structure/artifact provenance checks, then delegates to the
same new admission entry. It does not retain a second classifier. Delete that
overload and its snapshot-only selection property once runtime callers use
the discovery entry and equivalent security tests have migrated. Existing
snapshot-provenance negative cases remain effective until then.

This unit does not publish the new executable routes or claim that the four
real-host format integration cases pass. Runtime authoring, configuration
reapplication/freshness and complete output evidence remain required.
