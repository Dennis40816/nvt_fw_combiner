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

### Observed byte names — 2026-09-21

The owner supplied `EventBufferFormat.xlsx` (SHA-256
`7992f51901c238d8f0156652db5f2fbdab6ec2e05cee71e48fc7e04af902f705`).
Its 29 explicit byte/name rows are display facts owned by
`Domain.FirmwareEventBufferFormatDisplayNames`, separate from this ADR's
configuration and map-selection authority. Imported labels replace underscores
with spaces and the leading `AUTO` with `Auto`, preserving other acronym case:
`0xA3` is `Auto STLA v1`; `0x97` and `0xA6` are respectively `Auto Desay` and
`Auto Desay Palminfo`. The workbook's reserved `0x8x` note does not assign names
to unlisted values such as `0x86..0x8F`.

`EventBufferFormatObservation.DetectedDisplayName` projects the name of each
input's already decoded raw byte. The existing `DisplayName` remains the
effective configured format label or alias. Input facts and output confirmation
checks prefer the detected name; if it is null, the existing effective label is
a compatibility fallback, not a claim of canonical byte recognition. Config
aliases are not normalized, and the output mode summary retains the effective
label. Changing recognition values may change the selected map while the same
raw byte's observed name stays constant.

This adds no primary decoder, non-AB metadata read, recognition value, format
variant or support claim. Existing primary/artifact and configuration provenance
remain captured through the same Application observation path.

### Partial-family bank consolidation — 2026-09-21

The owner disables the NT51950/NT51951 Desay specialization while retaining
its closed profiles and physical maps for future explicit readmission. Active
format variants now share Common geometry: NT51950 single has 512 KiB output,
`0x40000` banks and TPB `[0x4A000,0x77000)`; NT51950 cascade and selector-free
NT51951 have 1 MiB output, `0x80000` banks and TPB `[0x8A000,0xB7000)`.
The separate NT51950 Common exact-two override is removed; NT51927 exact-count
rules are outside this change. The existing AB count/primary admission remains.

Normal DP input must exactly match the selected Common capacity. Both shorter
and oversized inputs block; the old Desay 1 MiB expectation, warning-only size
exception and ignored DP tail no longer apply to runtime selection. Dummy DP
retains blank initialization without a DP input. Standard Merge is unchanged.
Recognition bytes, aliases, raw-byte display names, format mismatch checks and
captured configuration provenance remain; configuration cannot re-enable the
inactive geometry. Historical Desay implementation sections below describe
the retained definitions, not current runtime availability.

Family/profile declarations and existing trusted registrations remain the only
selection/execution authority. Single retains its closed transport; generic
cascade reuses the existing 1 MiB transport and `nfc-nt51951-ab-merge-combiner-v1`.
DIFF uses the existing region-instance delta, and only the three four-byte B
ILM/DLM/CRC fields may be imported after the host's write-range audit. No new
processor, page branch, firmware decoder or alternate execution path is added.
Old Desay, exact-two and generic-cascade runtime identities must fail closed.

The new cascade route is Available/Candidate/ContractOnly; the old Supported
identity is retired rather than transferred to changed bytes. Other surviving
classifications remain unchanged. Independent synthetic full-output and actual
processor tests do not establish direct product certification. Existing single
Golden output bytes and hashes remain immutable; exact candidate firmware-owner
review and release Golden execution remain required.

## Rejected options and consequences

- Reusing preference fallback would silently change Build behavior after a bad
  file. Reuse low-level IO, not its policy.
- An arbitrary rules engine, per-page ID registry or UI address branch would
  duplicate firmware authority. The editor only configures admitted identities.
- Global overlap machinery is unnecessary in the first single-scope unit.
  Later catalog integration must validate any actual overlapping scope conflict
  before claiming wider applicability.

## 2026-09-20 amendment: built-in default availability

The owner approved removing first-use Save as a prerequisite for AB Merge.
Absent custom configuration now activates the canonical admitted defaults in
the existing Application session, without persistence. An explicit typed source
flag distinguishes built-in from saved snapshots; its domain-separated digest
is not a file hash. Invalid/unreadable custom data still blocks. Built-in
publication never overwrites LastSaved; Settings edits and Discard remain drafts.
This supersedes the missing-file behavior in the historical implementation
account below; the current persistence contract defines the effective rules.

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

### 2026-09-21 amendment: equal, readable positive TP counts

Owner-approved TP validation now requires readable positive counts in each
firmware slot. AB alone additionally compares the two counts numerically,
before optional selector classification or format-map selection. Selector-free
AB no longer bypasses count validation; missing/zero cannot be inferred as
single or accepted as unknown. The same Application count owner supplies
authoring errors, action readiness and execution rejection, including families
without an Event Buffer format policy. Diagnostics distinguish unreadable,
read-as-zero and unequal AB counts. This supersedes the earlier count-admission
behavior described above; it does not change the historical evidence, primary
Event Buffer decoding, bank geometry or external processor contract.

### Primary metadata contract

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

## Input declarations before output compilation

The tenth unit queries the existing trusted profile through the canonical
compiler's current-publication boundary. Its immutable AB declaration carries
actual profile/version/bundle identity, the canonical family, unresolved input
bindings and declared selection-group membership. It carries no output geometry,
compiled plan or execution admission. The compiler rejects stale publication,
foreign identity, wrong source identity or incompatible member/group facts.

Primary input references follow the profile's structure-to-space-to-slot join;
slot IDs and address-space IDs need not be equal. Both references belong to the
same immutable binding list. Only explicit absence of a family format policy
permits absent primary references. Missing or ambiguous bindings under a present
policy are invalid, not a reason to fall back to legacy behavior. Shape checks
alone do not establish trust: the bundle verifies the actual profile's family
binding, and the compiler verifies the declaration against the current route.

The existing non-executable dynamic-route discovery snapshot can consume these
declarations before any output map is selected. This is a prerequisite for the
shared AB runtime, not its completion; configuration reapplication, exact format
route selection and complete output evidence remain required.

## Runtime integration checkpoint — 2026-09-15 (unit 11, incomplete)

The working integration now registers the three closed profiles as four exact
candidate routes, retaining contract-only evidence status. Trust-index version
is `1.1.6.0`; policy version is `1.16.0`. Registration uniqueness includes the
map-set in both the runtime loader and build materializer. The exact-2 route
retains its count authority while the existing picker projection calls it Cascade.

Async preparation, file inspection and CLI input loading now discover trusted
input declarations before choosing an output map. The AB owner copies input
bytes, captures the same host Config session's returned Reload state and uses
the existing format admission/compiler. Policy-bearing synchronous preparation
fails with `AB_FORMAT_CAPTURE_REQUIRED`; it cannot choose an assumed Common map.
Legacy families with no policy keep their original path without Config IO.

The declaration publication is retained across Config waits, target declaration
selection and compilation. A changed publication returns
`AB_FORMAT_PUBLICATION_STALE`, not a retry using old observations against new
rules. Original inspection leases remain required when adopting a different
exact format. For an equivalent exact result, retain the original capability
instance so strict partial-selection completion remains valid. Unknown or
colliding slot aliases produce typed batch issues without inventing a space.
Cancelled requests do not publish an inspection or mutate the session.

Readiness and direct execution now share a fresh accepted-format assessment.
They reuse immutable accepted BIN bytes, not selected paths. Missing/invalid
Config or a newly selected non-equivalent exact output blocks both Preview and
Build before processor acquisition and destination preparation. Execution does
not replace the accepted plan. Reload generation changes and display-only alias
changes are not by themselves firmware changes. Pre-run refusal follows the
existing exception boundary with the typed issue code and message; it does not
fabricate a Build report for an operation that never started.

Local evidence under `NFC_TEST_AREA_ROOT/evidence/v116-ab-format` includes
`ab-format-capture-boundaries-red.trx` (5 failed, 2 passed), its green successor
(26 passed), `ab-format-declaration-alias-green.trx` (12 passed),
`ab-format-ui-legacy-input-order-green.trx` (6 passed), and
`ab-format-trust-index-green.trx` (22 passed). The fresh-run regression first
failed all six cases; `ab-format-run-freshness-compatibility.trx` then passed
41 cases including equivalent Config/alias and legacy Dummy DP coverage.
`ab-format-run-zero-side-effects.trx` passed six cases, with direct counters
confirming zero processor acquisition and zero destination preparation for the
three rejected execution configurations.
These are scoped, overlapping evidence sets, not a full-suite total.

Save now awaits the existing AB owner after successful persistence. It reuses
accepted inputs from the retained AB session, including when Standard Merge is
currently visible. Reinspection begins with an expected-snapshot comparison,
retains source inspection/bytes, and drops derived action results. Completion
uses the same session lock and expected-snapshot comparison before existing
exact-batch adoption. Returned statuses, metadata and catalog belong to the
adopted snapshot. Failed Config admission retains bytes for retry; it does not
restore cached action readiness or overwrite newer selections.

The UI checks snapshot ownership before projecting either success or failure.
Persistence failure does not trigger reapplication. Reapplication failure does
not undo the saved baseline and has a separate localized status. Typed slot
issues survive language changes and clear after successful reapplication.
Confirmed page navigation still clears selections; Save does not resurrect them.
Declaration-only inputs show pending size requirements rather than throwing or
inventing a capacity. Exact input bindings retain numeric descriptions.

`ab-format-reapplication-rebound.trx` passed five retry/cancellation/concurrent
selection/publication cases, with direct assertions on returned revision,
capability, metadata and catalog ownership. The desktop Save regression first
failed at declaration-only size rendering, then failed because Save retained
the old Desay map. Its accepted-input success deletes the original temporary
BIN paths before Save, proving this case does not reread them.
`ab-format-ui-save-reapply-localization-red.trx` reproduced the stale Verified
badge (3 passed, 1 failed). `ab-format-ui-save-boundaries-green.trx` passed all
19 cases for Save, inactive Standard, explicit navigation clearing, failure and
recovery, persistence, and bilingual text. The independent scoped Save UI
Polytail review closed that badge finding with PASS.
`ab-format-ui-save-shared-projection.trx` also passed eight existing AB input
ordering/status/relocalization regressions after the shared projection change.

The desktop execution ingress now constructs its complete accepted request
before publishing progress or queueing the worker. Session, action readiness,
input paths and delivery options are captured together. Error-report profile
identity comes from that exact compilation, not an IC-only profile summary;
IC/topology are also captured rather than read from mutable selectors later.
The desktop regression first reproduced the multi-profile `Single` exception,
then reproduced missing typed action readiness when reapplication began during
progress publication. `ab-format-ui-run-capture-readiness-red-guarded.trx` is the
valid latter red evidence; the preceding unguarded test callback reentered
itself and crashed the test host, so that aborted run is not product evidence.
`ab-format-ui-run-save-green.trx` passed 23 cases, including delayed success and
failure after newer input selection, capture before progress publication, and
Save while a real execution result waits for UI publication. The latter keeps
the active-run state/result intact while slots adopt the newly selected map.

`ab-format-running-capture.trx` passed one real-processor NT51950 2-IC Dummy DP
case. It pauses destination preparation after execution's fresh format capture,
then Saves/reapplies Common 2-IC rules. The original Desay run completes with
the same complete output bytes as its baseline and retains the original exact
capability; a later execution of that old request fails `AB_FORMAT_CHANGED`.
This is immutable-run/metamorphic evidence, not independent Golden evidence.

The capture/alias/cancellation corrections and pre-run freshness gate also have
independent scoped Polytail PASS. These are local verdicts, not unit 11 completion.
`ab-format-ui-first-config-save-red.trx` reproduced the first-install gap:
Missing Config blocked two loaded TP files, and Save could not reevaluate them
after their paths were removed. The existing per-slot owner now retains an
internal `CapturedSource` using `SelectedFileContentInspection`. This means a
stable read only, not firmware acceptance: its stamp must match the blocked
declaration, while compilation, terminal inspection and executable accepted
bytes remain absent. The transport's `AcceptedBytes` names stable-read content;
it does not grant the outer slot's firmware `AcceptedBytes` authority.

The existing session lease owns source lifetime. Reapplication checks the same
snapshot, selected paths, stamps, revision, publication, route and fingerprint,
and rejects Checking or replaced sources. It derives Normal/Dummy from declared
slot membership, not whether a DP file happens to be selected. Sources then pass
through the same format admission, compilation, inspection and exact adoption.
Missing inputs remain blocked; neither a UI cache nor firmware-path reread is
introduced. Successful adoption replaces retained sources with exact accepted
inspection bytes. Cancellation and publication changes cannot publish old work.

`ab-format-ui-source-capture-green.trx` passed 27 cases, including Missing and
Invalid first Save and delayed pre-compilation success/failure after newer file
selection. Independent scoped architecture/Polytail review passed this source
retention boundary. A further real-file test found null byte arrays implicitly
converted into empty `ReadOnlyMemory` sources for unreadable files. Explicit
nullable conversion now preserves failure at both capture boundaries.
`ab-format-source-retention-boundaries.trx` and its first attempted green run
retain that red evidence (1 failed, 4 passed); `ab-format-source-null-preservation.trx`
passed all five cases after the complete correction.
`ab-format-runtime-source-retention.trx` passed all 50 runtime cases, including
partial TP, missing Normal DP, unreadable source, caller-buffer mutation, and
accepted/pre-compilation cancellation, publication and replacement races.
`ab-format-source-constructor-contract.trx` passed five constructor boundaries:
matching blocked source succeeds; mismatched stamp, compilation, firmware
accepted bytes, or unblocked readiness are rejected. Its earlier setup failures
were test fixture/assertion errors, not additional production defects.
`ab-format-ui-source-null-preservation.trx` reran all 27 Config/UI cases after
the null correction with zero failures/skips. The final independent scoped
source-retention review passed, including the new failure and race boundaries.

The broader existing-runtime selection was then executed:
`ab-format-existing-runtime-compatibility.trx` has 62 passed and 28 failed of
90 cases. Confirmed migration categories include old synchronous preparation
and inspection calls, IC-only single-profile lookup, declaration-time numeric
geometry assumptions and CLI configuration setup. These are required failures,
not waived or presumed harmless. The declaration membership test now names
exact map sets and includes all four new routes; the primary metadata test uses
isolated persisted Config and asynchronous preparation with matching A/B format,
while still comparing unchanged Backup-derived version observations. Other
runtime/CLI failures remain open until individually corrected and rerun.
`ab-format-declaration-primary-migration-green.trx` passed all 28 cases in
these two classes, closing four failures from that broader run and adding four
explicit route cases. The remaining 24 failures have not yet been rerun or
closed; this is not a new 90-case pass.

The next compatibility pass migrates single/batch file inspection tests to the
actual asynchronous service, retaining health, source-prefix and version/naming
assertions. NT51950's CMI test supplies both TP formats/counts and selects the
Common 3-IC map whose existing CMI locations it asserts. Numeric input-geometry
tests now require no numbers before format capture, then retain their exact
Common-profile size assertions after complete asynchronous preparation.
Exact-route delivery checks cover all five NT51950 and two NT51951 variants;
the Dummy topology test explicitly compiles its current single-chip test
capability and still asserts rejection before any source read.

This exposed a real summary defect: distinct map capacity count was incorrectly
used to decide whether AB summary compilation needed topology. Equal-capacity
Desay maps and the one-map Common exact-2 profile were therefore disclosed as
failed. `BuiltInV2Registration.CompileSummary` now chooses one representative
canonical map by capacity/map ID and reuses `HeadlessRouteSelection` to project
that map's topology before the existing compiler. It neither guesses from IC
names nor tries alternatives until one succeeds. Map-query/compile failures
remain failures; non-AB summary behavior is unchanged. Summary success is not
full variant execution or Golden evidence.

Scoped results: `ab-format-inspection-async-migration.trx` 10/10,
`ab-format-input-geometry-migration.trx` 5/5,
`ab-format-dummy-topology-migration.trx` 2/2, and
`ab-format-summary-cli-green.trx` 6/6 including the complete CLI profile list.
The original summary red and the CLI list's missing new-profile rows are
retained in their earlier TRX files.
`ab-format-existing-runtime-compatibility-second.trx` then executed the broader
selection again: 87 passed, 7 failed, 0 skipped of 94 cases. Five remaining
topology/951 runtime tests still enter the old synchronous preparation helper;
the CLI naming case lacks isolated persisted format configuration, and the
sparse-file test still expects the old exact-profile read limit. Their intended
behavior must be preserved through migration; these failures are not waived.

The final compatibility correction restores the existing compiled-input read
limit whenever the CLI discovery snapshot already has one exact capability;
only declaration-only discovery uses the declaration reader. This fixes the
NT51929 pre-materialization resource-limit regression without adding CLI size
rules or relaxing the original sparse-file assertion. The five topology/951
tests use isolated persisted Config and asynchronous preparation, preserving
their original issue codes and real execution checks. Common CLI naming now
covers both actual 2-IC (`0x85016`) and 3-IC (`0x45016`) B CMI locations.
`ab-format-existing-runtime-compatibility-green.trx` passed all 95 cases, zero
skips. Independent scoped review passed the seven formerly failing cases;
this closes that selection only, not the complete candidate.

The next unchanged Golden/readiness baseline
(`ab-format-golden-readiness-baseline.trx`) passed 13 and failed five of 18
cases. Three failures enter obsolete synchronous preparation; two public-host
cases have no isolated persisted Config. These failures occur before output
comparison. Original Golden bytes, SHA values and range assertions remain
unchanged while their runtime setup is migrated.

After isolated Config and asynchronous preparation, the next run passed 17/18.
The sole failure correctly rejects the historical NT51951 synthetic primary
`17/2A` with `AB_FORMAT_PRIMARY_INVALID`; it is not a changed output. Its original
shared-TP pattern and SHA `b84b63f30c964fad9818b612b77167bd9615cc31d6f72c1eab49f1b1579c8f32`
remain in exact-profile/real-processor full-output evidence alongside the
unchanged two-TP `e152...` case. Public-host rejection now verifies no output or
report and unchanged inputs. A distinct valid-primary synthetic case changes
only `0x22201` to the complement `0xE8` and compares complete production output
and SHA against the same hash-pinned Python reference. This new synthetic case
is not a replacement certified Golden or an update of either old SHA.

The original NT51950 BOE public-host fixture remains unmodified: valid primary
`80/7F`, format `0x84`, count 1 selects Common Single under defaults, preserving
its original full expected output and processor/report write-range checks.
`ab-format-golden-runtime-green.trx` passed all 20 cases, zero skipped. The prior
19/20 run failed only because the new rejection test expected software-error
exit 70 rather than the existing preparation-failure exit 1; production was not
changed for that correction. These tests include real processors, immutable
reference comparisons, missing/stale runtime rejection and legacy processor-free
execution; they are not the complete certified Golden or integration gate.
Independent scoped Golden/architecture/Polytail review passed these three test
files, including preserved original pins and the new admission/evidence split.

The broader Bootstrap AB/Config selection then passed 272/280; its eight
failures were old synchronous inspection entry points and incomplete route
identity lookup. Exact-route tests now bind IC, topology and map-set and cover
the four additional format routes. Shared source-identity tests keep the same
activation/inspection leases/adoption and all path-mutation, alias, conflicting
snapshot and complete-output assertions while awaiting the real inspector.
Their Standard Merge and retained DP Replace consumers migrate with that shared
helper. The extension negative uses real temporary files and still requires the
typed extension error, blocked Build and absent accepted bytes.
`ab-format-ab-shared-inspection-migration.trx` passed all 298 selected cases,
zero skipped, including those shared consumers. No firmware production code,
Golden input or expected bytes changed in these test migrations.
Independent scoped review passed these four shared-inspection/route test files.

The first wider desktop selection, `ab-format-ui-memory-report-baseline.trx`,
executed 592 cases: 572 passed, 20 failed, zero skipped (2m59s test duration).
Failures remain explicitly open: two Build-entry observer cases, four Settings
navigation-count cases, two pre-format geometry cases, four customer-info
template cases, three catalog-removal/topology cases, four input-reinspection
cases and one source-text spacious-panel assertion. These are failure groups,
not a conclusion that every failure is a stale test rather than a product bug.

The Build-entry observer was confirmed to perform an extra synchronous
inspection while counting batches, using a different test host. It now counts
the call to the same Presentation host's real async inspector without replacing
its result or rereading files. Its existing source-deletion/overwrite and
accepted-delivery evidence assertions remain unchanged; the focused rerun and
scoped review are required before closing these two cases.

`ab-format-build-entry-observation-green.trx` passed all seven Build-entry and
CtrlRAM display-projection cases. `ab-format-settings-navigation-migration.trx`
passed four viewport/theme/language cases after the navigation count included
the approved fifth Config entry; all existing alignment, scrolling and keyboard
assertions remain. These close six of the desktop baseline failures; the other
14 remain pending and the full 592-case selection has not been rerun.
Independent scoped correctness/architecture/Polytail review passed the three
observer/Settings test files at these exact local results; final unit and
integration review remain separate.

The subsequent memory/context migration preserves the pending-capacity contract:
before complete format inputs there is exactly one unavailable row; after
isolated Config and a valid TP pair, the existing rejected-DP test still asserts
the original compiled capacity and blocking result. Six new Common/Desay
950/951 capacity cases include the required DP input. Their first run failed
because that input was omitted from the new fixture; no production fallback was
added. `ab-format-ui-memory-capacity-green.trx` passed 8/8, and
`ab-format-ui-context-memory-migration.trx` passed 23/23, zero skips.

Catalog tests now distinguish removing only 950 (951 still supports AB) from
removing every AB route (Standard fallback). Removing generic `2-plus-ic` alone
must retain Cascade when exact `2-ic` remains; removing both removes Cascade.
The original selection, live ComboBox, inactive-context and rejection assertions
are retained, with three positive cases for these surviving routes. These and
the two readiness migrations close seven more baseline failures.

The remaining observer corrections use the same host's asynchronous inspector
and observe its actual batch inputs instead of invoking obsolete synchronous AB
inspection to manufacture results. Catalog reinspection still exercises blocked
in-flight work, cancellation, hidden-mode reactivation and publication-token
checks; the AB-active test explicitly saves isolated Config defaults. Existing
legacy fake-result consumers retain their separate callback seam unchanged.

The customer-info card tests use the approved Target Addr label and explicit
14/11 header/caption hierarchy, retaining keyboard, alignment, clipping and
tooltip assertions. The spacious-panel test now verifies the two shared memory
bars and their legends, rejecting the superseded persistent detail lists. Real
popup geometry/interaction tests run alongside this source-structure check;
the latter alone is not visual-fidelity evidence. No production UI or firmware
bytes changed in this batch.

`ab-format-ui-observer-card-migration.trx` passed 110/110, zero skips (32 seconds),
closing six more baseline failures in the affected narrow selection. It includes
methods named `CanonicalCatalogRefresh`, not every method in that partial
class's files; the `FreshToken*`/`CancelledFresh*` readiness cases, including the
last original failure, still require the broader run. The observer blocks before
inner inspection starts; it does not prove late completion after an old inner
result was already read. The run also includes Memory popup, Build-entry, card
and spacious-panel checks. The earlier compile-only attempts exposed an
unused observer parameter and a discard-shadowing typo; both were corrected
before this successful full compilation. Independent scoped review and the
broader baseline-class rerun were still pending at that narrow checkpoint;
this is not a fresh full UI, Golden, integration or release pass.

The broader run selects every method in the original baseline's 58 test
classes, rather than its narrower method filter. It executed 1,242 cases in
3m34s: 1,236 passed and six failed, zero skipped. The retained file is named
`ab-format-ui-memory-report-migration-green.trx`, but its actual outcome is
failed. Exact-name matching confirms all original 20 failing cases now pass,
including the previously omitted AB-to-hidden-Standard reinspection. The six
additional failures are outside that original failing set: old support-route
and Settings-navigation counts, another Standard-fallback fixture, another
persistent-coverage-list assertion, and two shared-style token checks. They
remain open until corrected and verified; a larger passing count does not waive
them or establish a full candidate pass.

The six newly exposed failures were then corrected without changing firmware
semantics. The matrix count includes the four declared format routes, the fifth
Settings navigation entry is retained, and the remaining fallback fixture now
explicitly removes all AB availability before expecting Standard. CtrlRAM's old
persistent-list assertion now checks the approved overview/position bindings.
Both shared-style checks remain unchanged: three existing XAML dictionaries
reuse the existing radius, spacing and font-size tokens with the same values.

The real Memory card checks caught a regression in that token substitution:
local `DynamicResource NfcFontSize14` rendered as the shared emphasis style's 12,
even though resource lookup returned 14 and another dispatcher drain did not
help. The two original local 14 values now use `StaticResource` for that fixed,
non-theme-dependent token; theme brushes remain dynamic. This records the
observed binding behavior, not a proven Avalonia internal cause. Both card title
and Technical details retain explicit 14 assertions and the caption remains 11.
`ab-format-ui-shared-style-green.trx` passed 54/54, zero skipped (17 seconds),
including the six new failures and Config/Memory rendered-control cases.
Complete actual renders are retained under `style-token-actual` in the same
local evidence directory. The subsequent title assertion, added Config
footer/hint font assertions and broader fixed-source rerun still need their
own results; the 54-case result does not implicitly cover later edits.

The expanded selection subsequently passed **1,242/1,242**, zero skips, in
3m55s (`ab-format-ui-memory-report-final-green.trx`), including the new Memory
title assertion. The final Config font checks separately exposed the hint
falling back to 13; it now uses the same fixed `StaticResource` 14 token.
Restoring the original footer literal 14 still rendered all three buttons at
13, confirming that the local setter was ineffective. Remove that setter and
retain the existing `semanticAction` owner's 13, without restyling the buttons.
The added real-control assertions now preserve those actual 13/14 values.
`ab-format-config-font-final-green.trx` passed **37/37**, zero skips (10 seconds),
after this final delta; complete renders are in `style-token-final`. The older
`ab-format-config-font-size-green.trx` is a retained **failed** probe, not a pass.
The 1,242-case pass predates that last Config-only delta; these are broad plus
affected-delta evidence, not a second full run on the final tree.

Independent scoped correctness/architecture/Polytail review passed the final
delta and reconciled the dirty production/contract paths with prior reviewed
lanes; no open P0/P1/P2 was identified in that checkpoint scope. Before its
first commit, the active record drops eight planned paths that never changed
from the integration base; this narrows admission and removes no source file.
Future work outside the actual checkpoint scope needs its own admission.

### Report capture unit 12 (implemented and locally verified 2026-09-15)

Independent architecture review admitted the existing Application run report's
optional immutable AB format summary, projected only from execution's captured
selection (identity/display label, configuration generation/source SHA, family
identity and A/B primary evidence). Reuse parent report profile/map/input facts;
do not add a second session-proof or re-read current Config while serializing.
The actual Application JSON and frozen canonical report schema are distinct
contracts. The [Application semantic extension](../contracts/composition-report-v1.md#execution-captured-ab-format-v116)
defines path-free A/B primary binding/structure/range/raw-byte evidence and
configuration/family identity. Old-report absence remains unknown, never
inferred from today's catalog. Record `AB-116-REPORT-FORMAT-12` admits the exact
existing execution/report chain plus one immutable projection type. Its internal
factory checks captured map/family and accepted input identities. No firmware
semantics, profile, input bytes or Preview-token contract change is admitted.
Source-generated metadata covers the new summary through the existing JSON
resolver chain; the pre-existing report graph retains its reflection fallback.
The initial real-host regression `ab-format-report-capture-red.trx` fails because
a successful AB run has no format JSON. This failing evidence is retained.

Review found and closed an A/B-role coherence gap: the factory now also checks
the member-scoped compiled canonical structure reference and expected A/B
execution space, not just input SHA/length. The `swapped-primary` and
`copied-b-primary` cases are preserved red in
`ab-format-report-primary-role-red.trx` (9 passed, 2 failed). Same-file A/B stays
valid; no ID-only fallback or extra decoder was introduced.

Final scoped evidence under `D:/NvtFwCombiner-TestArea/evidence/v116-ab-format`:
`ab-format-report-runtime-matrix-final.trx` **61/61**, no skips (9 seconds), and
`ab-format-report-application-final.trx` **152/152**, no skips (0.502 seconds).
These cover fresh/held-Save capture, 950/951 format variants, primary identity
negatives, actual report serialization, legacy omission, processor late failure,
request-copy compatibility and unchanged complete output/Preview token when
only Config audit data changes. The earlier 9/10 contract run used an invalid
test output name; using the compiled naming template fixes that fixture without
changing naming rules. The legal Normal-admission copy retains null; non-null
AB capture uses the other overload. This is scoped regression evidence, not a
fresh full Golden run or final integration approval. Record 12 remains
`design-active` until the frozen integration boundary.
Independent scoped correctness/architecture/Polytail review passed the same
production and final test evidence; P2 role coherence and P3 copy coverage are
closed with no remaining P0-P2 finding in unit 12. This permits its local
implementation checkpoint, not publication or final integration.

### Desay DP-size advisory unit 13 (implemented and locally verified 2026-09-15)

The two Desay profiles now declare `expectedOuterLengths: [1048576]` and
`unexpectedOuterLengthIssueCode: DESAY_DP_SIZE_WARNING` on the existing
`dp-ab-input` source-view-coverage rule. This is **1 MiB / 8 Mbit**, not 8 MiB.
Exact-size input is valid; oversized input warns without blocking Preview or
Build, and bytes after the declared source range are ignored. Required source
coverage remains independently blocking: these routes require the complete
`[0x0,0x100000)` DP view, so a shorter input is rejected, not padded.
Common retains its existing exact-size rule; Dummy has no DP size warning.

Profiles own this declaration. Existing compiler, inspector and engine owners
evaluate it; the existing execution-issue transport emits exactly one warning
in the run report. No Application/UI size branch, duplicate warning transport,
schema, compiler or engine change was needed. All three Desay routes are tested
to keep the advisory size equal to their compiled output capacity.

The initial code-only design overlooked two frozen schema requirements:
`expectedOuterLengths` and the warning code must be paired, and profile issue
codes must be uppercase identifiers. The engine fallback code
`input.address-space.length-unexpected` is not a legal profile declaration.
Independent design review corrected both assumptions within the original two
profile paths; intermediate schema failures remain diagnostic evidence, not
existing product defects or passes. The schema itself remains unchanged.

Desay profile versions advance from `0.2.0` to `0.2.1`, their bundle to
`1.1.6-ab-format.2`, and the canonical policy to `1.16.1`. Existing hash owners
recomputed bundle entries, all seven enclosing-bundle route fingerprints and
the two candidate compilation pins. `sync_derived.py --only reviewed-source-pins
--write` synchronized the loader/package/smoke/test hashes only. No route,
publication/evidence decision, Golden expected output, Common profile, family,
view, operation, processor or write range changed. Both profile JSON objects
match the preceding source after removing only their version and two advisory
properties.

Final scoped evidence under `D:/NvtFwCombiner-TestArea/evidence/v116-ab-format`:

- `ab-desay-dp-regression-final-pins.trx`: **200/200**, zero skips, 11 seconds.
  Includes the 12 new real-host advisory cases, existing AB Golden cases,
  independent complete-output/write-boundary cases, Dummy, format runtime and
  catalog regression. New negative cases are two Desay required-short cases
  and two Common oversized cases; they are not additional Common-short tests.
  Exact/oversized Desay builds compare complete output bytes and SHA, preserve
  original source files and record the ignored trailing range.
- `ab-desay-dp-trust-final.trx`: **24/24**, zero skips, 0.851 seconds.
- `python -m unittest tests.scripts.test_sync_derived.ReviewedSourcePinsTests
  tests.scripts.test_release_package_policy.ReleasePackagePolicyTests.test_capability_policy_is_hash_pinned_in_package_and_smoke_allowlists`:
  **6/6**, 1.211 seconds. No package was published or release policy relaxed.

Failed intermediate evidence is retained separately: initial `ab-desay-dp-size-red`
had three intended missing-advisory failures and two incorrect Common oversized
fixture assumptions. The file named `ab-desay-dp-size-green.trx` was **9/12**, not
a pass: three message assertions expected decimal while the existing report
formats expected sizes in hexadecimal. `ab-desay-dp-regression-final.trx` was
**198/200** before the two candidate provenance assertions were synchronized.
Only the final-pins run above is the complete passing 200-case selection.

Independent scoped correctness/architecture/Polytail review passed the final
unit, including the two corrected compilation pins and actual TRX counters;
no remaining P0-P2 finding was identified. This permits its local implementation
checkpoint only, not final integration or publication.

### Evidence synchronization unit 14 (2026-09-15)

On source `c3e68aff`, the unchanged `verify.py --release-golden` preflight
rejected the canonical manifest before executing tests. Its route evidence
still named three `v114-dummy` decisions and omitted the four new format
decisions. Synchronize those seven identities/fingerprints from the current
canonical policy, retaining `contract-only` and references to the admitted
profiles. No case manifest, payload, expected SHA, allowed difference, release
allowlist, runtime decision or validator is changed. The canonical validator's
existing test suite passes **80/80** (2.651 seconds) after this projection update;
the complete Golden execution is tracked separately below, not inferred from
that validator test count.

The canonical direct-output set contains **25** cases: Standard Merge 8, AB
Merge 3 and CtrlRAM Replace 14. Input-only and fact-scoped alias records do not
add output cases. The four new format routes retain independent synthetic
byte/write-boundary evidence and remain candidate/contract-only. This does not
grant direct-Golden certification; conversely, existing function-open candidate
rules do not require inventing a direct Golden for every new route merely to
perform owner-approved development integration. Exact-head firmware-owner
approval and the existing integration contract still apply.

The current-source Config selection separately passes **31/31**, zero skips
(`ab-format-config-final-candidate.trx`, 11 seconds). Complete production-control
renders at 1672x941 Light/Dark English and 980x640 Light Traditional Chinese
are retained under
`D:/NvtFwCombiner-TestArea/evidence/v116-ab-format/final-candidate-c3e68aff`.
These prove tested control behavior and measured bounds, not exact reference
fidelity. Visual comparison still shows different column spacing and smaller
shared action typography/sizing; the owner has been asked whether to preserve
the shared style or match the preview sizing. No new style choice is assumed.
The roadmap now distinguishes implemented work from these residual gates;
the experience rules explicitly retain the approved Desay size exception.

The subsequent `python scripts/verify.py --release-golden` run exited **1**:
Bootstrap passed **1440/1452**, with 12 failures and zero skips; GoldenRegression
passed **25/25**, with zero skips. The unchanged
`require_release_golden_results` case checker separately confirmed execution
and success of all **25 owner-certified complete-output cases** across those
results. This is case-level Golden evidence, not an overall gate pass. Original
TRX files are retained in
`D:/NvtFwCombiner-TestArea/evidence/v116-ab-format/unit14-golden-first`.
The 12 Bootstrap failures require diagnosis and correction before integration;
they are not waived or assumed to be obsolete fixtures. Independent scoped
review found no remaining unit-14 finding and permits its local implementation
checkpoint, with these aggregate failures explicitly outstanding.

### Remaining integration work

Report capture and DP advisory are locally verified implementation units.
Final aggregate coverage accounting, complete applicable Golden/write-range
verification, actual UI reference acceptance and integration remain required.
Records 11–13 remain `design-active` until the frozen integration boundary;
scoped tests do not satisfy the remaining firmware-owner evidence or certify
the entire candidate. No release or publication is authorized.
