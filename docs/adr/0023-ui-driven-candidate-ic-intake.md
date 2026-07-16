# ADR 0023: Use one typed in-process candidate IC intake boundary

- Status: Proposed — owner acceptance required
- Date: 2026-07-16
- Owners: Product owner + architecture owner + security reviewer
- Target: v0.9.10, planned during v0.9.9
- Related: ADR 0001, ADR 0015, ADR 0018, ADR 0022, firmware-evidence-manifest-v1

## Context

The current `scripts/create_candidate_ic_intake.py` command accepts one declared
candidate evidence manifest plus explicit artifact bindings and emits exactly
four deterministic candidate-only records. It does not parse owner workbooks,
materialize firmware-family/profile/bundle documents, call the V2 compiler, or
provide a UI. It is also not part of the portable application package.

The v0.9.10 goal is broader: a user supplies declared Excel, mmap, BAT, sample
BIN, expected-output, and provenance evidence through the UI, then receives
validated candidate firmware-family/profile/bundle/evidence artifacts and
PR-ready diagnostics. The tool must not infer firmware facts, execute submitted
commands or macros, copy private firmware into Git, register runtime behavior,
or promote support.

Owner product direction recorded on 2026-07-16 requires an IC whose behavior is
expressible by the already approved schemas, operations, and processor allowlist
to be added through configuration/profile-pack data without rebuilding the C#
application. The existing trusted profile compiler still validates and compiles
those declarations into an execution plan at runtime; "no rebuild" does not
mean accepting uncompiled or unchecked firmware instructions. New operation
semantics, processor kinds, schemas, or executable tools still require a normal
application release. Signed profile-pack discovery, activation, and rollback
are separated into ADR 0024 and the v0.9.11 milestone. This direction does not
pre-accept the workbook format, UI, security, dependency, or signing choices
that remain open in this Proposed ADR.

The owner further clarified that the canonical IC data model and a versioned
`.xlsx` worksheet/table contract must be defined before workbook parsing or UI
authoring begins. The workbook and a later UI are two adapters over the same
typed request; neither is the source of firmware semantics. The owner also
confirmed that postbuild remains a standardized external-tool call. The current
Legacy Combiner stays in place, while a future newer `combiner.exe` is supplied
and reviewed outside this application rather than reimplemented here.

The portable package has no general Python runtime, and its reviewed size
ceiling leaves no room to bundle another Python distribution casually. Keeping
the current Python semantics and adding an independent C# implementation would
also create the duplicate module that the convergence program is removing.
A durable ownership and migration decision is therefore required before UI
implementation starts.

This intake use case has no composition kind, experience, IC support claim,
address space, range, or byte mutation. A later candidate parity run may invoke
the existing V2 compiler and composition engine, but only after the generated
candidate declarations are complete enough to compile.

## Decision drivers

- One canonical intake implementation shared by UI and headless replay.
- Offline, self-contained Windows packaging within the existing package ceiling.
- Deterministic output from explicit owner declarations and immutable snapshots.
- Reuse of the four-record candidate boundary and the one V2 compiler/engine.
- No UI-owned firmware semantics, filename inference, BAT/macro execution, or
  automatic support promotion.
- Exact provenance, missing-evidence, compiler, parity, and review diagnostics.
- A migration path that removes, rather than institutionalizes, duplicate logic.

## Considered options

1. **Typed in-process C# use case (recommended).** Reimplement the existing
   four-record semantics once behind Application contracts, prove byte-for-byte
   parity with the Python command, then retire Python as an executable product
   boundary. Infrastructure supplies constrained source readers and workspace
   writing; Profiles supplies the existing V2 validation/compiler authority.
2. **Bundle the existing Python command as another worker.** This retains two
   runtime stacks, increases package size materially, and still needs a typed C#
   host/UI contract.
3. **Require system Python.** This violates offline/self-contained packaging and
   makes the user environment part of deterministic behavior.
4. **Let the UI emit only a manifest or command line.** This preserves manual
   intermediate construction and does not meet the automatic IC-intake goal.
5. **Automate Microsoft Excel through COM.** This requires an installed Excel
   application, is Windows/session dependent, and expands macro/process authority.

Options 2 through 5 are rejected. Option 1 is the proposed decision, but this
ADR remains Proposed until the owner accepts the decisions listed below.

## Proposed decision

### One pipeline and dependency direction

```text
Avalonia / thin CLI
  -> typed CandidateIcIntakeRequest
  -> Bootstrap candidate-intake facade (composition root only)
  -> Application candidate-intake use case
     -> immutable source-reader and workspace-writer ports
     -> candidate-profile-authoring port
     -> optional existing Application composition run for parity
  -> typed diagnostics and export result

Infrastructure adapters -> Application source/workspace ports
Bootstrap forwarding adapter -> Application authoring port
                             -> existing Profiles V2 normalizer/compiler
```

- **Presentation** collects declared values, artifact roles, and source locations.
  It does not open workbook/BIN bytes, parse mmap/BAT syntax, choose ranges,
  build processor commands, or reference Profiles.
- **Bootstrap/CLI** project file-picker or command inputs into the same typed
  request and invoke the same Application use case. Bootstrap is the only
  composition root: it creates the Infrastructure adapters and supplies one
  focused forwarding adapter that delegates authoring to Profiles without
  interpreting firmware facts. CLI remains a thin replay surface, not a second
  intake implementation.
- **Application** owns intake order, candidate-only policy, required declaration
  checks, missing-evidence classification, deterministic diagnostics, and the
  decision to request V2 validation/parity. It declares the immutable source,
  workspace, and candidate-profile-authoring ports and depends only on those
  ports plus Domain/Contracts types, not Profiles, filesystem, or Office
  libraries.
- **Infrastructure** snapshots declared files, calculates size/SHA-256, performs
  syntax-only source reading, and atomically writes one candidate workspace by
  implementing Application ports. Readers return source locations and literal
  values; they do not infer firmware meaning.
- **Profiles** remains independent of Application. It projects only
  user-confirmed typed facts into existing V2 contract documents, then invokes
  the existing normalizer/compiler. The Bootstrap forwarding adapter translates
  only between the Application port and this existing Profiles entry point; it
  owns no defaults, ranges, aliases, promotion, or processor rules. This is an
  authoring projection into the one compiler, not another firmware compiler or
  executor.
- **Domain and the composition engine** remain unchanged. The engine is called
  only for an explicitly requested synthetic/private parity check after an
  executable-candidate plan exists.

### Declared inputs

The request identifies every input by stable artifact id and logical role:

- workbook evidence with explicit worksheet and cell/table locations;
- mmap text with explicit source lines or sections;
- BAT command evidence as inert text with an owner-selected command block;
- sample input BINs and expected output BINs by role;
- owner/provenance records, candidate id/version, intended member/mode/capacity,
  topology, and fact-scoped aliases;
- an explicit generated-at value or injected clock for deterministic replay.

The tool may offer parser-discovered syntax locations for the user to select,
but an unconfirmed value remains unresolved. A filename, neighboring IC, common
offset, command name, or successful parse never becomes a firmware fact.

### Canonical model before workbook and UI

The first v0.9.10 authoring contract is a typed, format-neutral candidate IC
configuration model. A versioned workbook contract maps named tables to that
model without embedding JSON fragments, executable commands, or application
class names. The later UI produces the same typed request and therefore cannot
gain a second set of defaults or validation rules.

The workbook contract is defined and owner-reviewed before selecting a reader
library. Its initial table families are:

- candidate/family/member identity and intended modes;
- address spaces, capacities, regions, groups, and access/atomicity declarations;
- mode inputs, explicit mappings, output naming, and version locators;
- approved processor, tool-binding, and invocation-profile identifiers;
- fact dispositions, promotion impact, provenance, and exact evidence locations;
  and
- sample/expected-output case roles and immutable size/SHA-256 references.

Exact worksheet/table names, required columns, cell types, row keys, ordering,
null rules, cross-table references, and workbook limits belong to a separately
versioned `candidate-ic-workbook-v1` contract with positive/negative fixtures.
Sample BINs, expected outputs, mmap/BAT files, and owner records remain separate
explicitly bound artifacts; the workbook references their stable artifact ids
and never embeds private firmware bytes. A formula result, merged-cell layout,
formatting, worksheet position, or filename cannot supply a hidden default.

The normalized typed configuration is still candidate-only. It must validate
against the intake contract, project only explicit facts to existing approved
V2 documents, and pass the existing compiler before it can produce a candidate
workspace. A workbook or UI submission never becomes an installed profile pack
or a support claim directly.

### Source-reader constraints

- Workbook input is read-only. Macro execution, formula evaluation, external
  links, Office automation, and mutable shared-workbook state are forbidden.
- The accepted workbook formats are an owner decision. The recommended first
  contract is strict `.xlsx`; macro-bearing `.xlsm` and legacy binary `.xls`
  remain rejected until a separately reviewed reader and package impact exist.
- ZIP/XML workbook readers enforce entry-count, entry-size, aggregate-size,
  compression-ratio, path, relationship, and duplicate-cell limits.
- mmap readers preserve exact source lines and report ambiguous/unsupported
  syntax instead of choosing one interpretation.
- BAT readers tokenize only the selected evidence block into displayable argv
  records. They never invoke a shell, expand environment variables, follow
  `call`, load sidecars implicitly, or execute `combiner.exe`.
- BIN readers produce immutable size/hash snapshots. Private sample and expected
  bytes are never copied into the candidate workspace or Git.

Adding a workbook/parser dependency requires a separate reviewed dependency and
package-size decision. No dependency is implied by accepting this ADR.

### External postbuild boundary

Candidate IC configuration names only an already installed and allowlisted
`processorId`, `toolBindingId`, and `invocationProfileId`. It cannot carry an
executable path or free-form argv. BAT text is evidence used to review or select
that binding; it is not runtime configuration.

The current parity authority remains the exact Legacy Combiner 1.13 command and
the existing constrained staging runner. A future newer `combiner.exe` is a
separately versioned external tool package with release-owned hashes, manifest,
invocation profiles, read/write authority, owner evidence, and clean-machine
review. Replacing that package does not move its postbuild algorithm into this
application. If the replacement fits the already approved processor protocol,
IC configuration may select its reviewed binding without an application
rebuild; a new protocol, processor semantics, or write authority still requires
an application release and normal R2/R3 gates.

### Candidate workspace and contracts

The existing four-record export remains a versioned compatibility boundary. The
caller selects one existing empty output directory, and a successful export
leaves exactly these four files at its root:

```text
candidate-bundle-rows.json
candidate-evidence-manifest.json
missing-evidence.json
validation-report.json
```

For the same logical request, the C# use case must reproduce those four bytes
before the Python oracle is retired. It may return generated family/profile/
bundle/evidence previews and PR diagnostics as typed result data, but it may not
persist extra files or relocate the four records under a subdirectory.

Persisting generated artifacts or a PR-ready inventory requires a separately
versioned `candidate-workspace-v1` contract, schema, negative tests, and
architecture/security review. That future wrapper must preserve the exact
four-record compatibility export as an explicit surface rather than silently
replacing or relocating it. It may contain only artifacts produced without
inference and validated against already approved V2 schemas. Missing facts omit
an artifact or retain the lowest representable blocked promotion stage; they
never receive defaults. PR diagnostics may record relative paths, SHA-256
values, compiler/parity results, required reviewers, and residual evidence
gates, but never run Git, open a PR, copy private evidence, or authorize
promotion. This ADR does not authorize that wrapper schema or modify shared V2
schemas/compiler behavior.

### Candidate, compiler, and promotion boundary

- Generated family/profile/bundle documents are staging artifacts, not trusted
  bundle registrations.
- Existing V2 schema validation, family normalization, map resolution, and plan
  compilation run without a runtime registration side effect.
- Incomplete declarations produce deterministic blocker diagnostics.
- A candidate plan may reach only the evidence-supported promotion stage. It
  cannot become `Supported` through intake.
- Full-byte sample/expected comparison uses the one composition engine only when
  every required input, processor binding, range, and integrity fact is explicit.
  A mismatch adds evidence; it never rewrites the candidate to match output.
- Promotion remains a separately reviewed repository change with normal R2/R3,
  golden, tool-command, and firmware-owner gates.

NT51950/NT51951 AB candidates retain full DP container initialization, require
no `map.txt`, and never authorize C# to write the AB header CRC. Python-reference
parity remains against the exact Legacy Combiner 1.13 command. NT51919/NT51932
remain blocked without direct evidence or an approved fact-scoped alias package.

### UI placement and interaction

The intake entry is a secondary **New IC candidate** surface under Settings. It
does not add a fourth top-level tab. The proposed flow is:

1. Candidate identity and intended IC/member/mode declarations.
2. Evidence slot cards for workbook, mmap, BAT, samples, expected output, and
   provenance.
3. Explicit source-location/fact binding with unresolved values visible.
4. Validation review showing source hashes, missing evidence, generated artifact
   preview, V2 compile eligibility, and optional parity result.
5. Export of the closed PR-ready workspace after confirmation.

Technical JSON and diagnostics use the existing read-only raw-text style.
English remains the default with Traditional Chinese resources, and the flow
requires keyboard, focus, screen-reader, high-contrast, cancellation, and large-
diagnostic coverage. UI controls never hide a server/core validation failure.

### Python-to-C# convergence

The Python command remains the comparison oracle only during migration:

1. Lock its current valid/invalid vectors and exact four-record bytes.
2. Implement the typed C# use case against those vectors.
3. Require exact semantic/output parity plus architecture/security review.
4. Route UI and CLI exclusively through the C# use case.
5. Retire the Python executable boundary and its packaging path. Historical Git
   revisions and retained fixtures provide evidence; production does not keep
   two implementations for convenience.

The migration must not copy Python validation logic into Application,
Infrastructure, and the Bootstrap forwarding adapter. Format parsing remains
in Infrastructure adapters; candidate policy has one Application owner;
firmware document normalization and compilation retain one Profiles owner.

## Consequences

### Positive

- UI and headless intake share one offline implementation and one output model.
- Package growth avoids a second Python runtime.
- Firmware facts remain explicit, reviewable, and compiler-checked.
- Existing four-record evidence stays replayable while v0.9.10 adds generated
  candidate artifacts and PR diagnostics.
- The migration has an explicit end state with no permanent duplicate module.

### Negative / trade-offs

- A strict workbook reader and safe source-location UI are non-trivial work.
- The four-record parity migration temporarily maintains Python and C# paths.
- Some owner workbooks or BAT dialects will fail closed until their syntax is
  explicitly supported and tested.
- Candidate materialization cannot hide missing firmware evidence, so some
  exports will contain diagnostics without a complete profile/bundle.

### Risks and mitigations

- Parser output mistaken for firmware truth -> require explicit user binding and
  keep unconfirmed observations unresolved.
- Duplicate C#/Python semantics -> exact parity phase followed by enforced Python
  retirement before v0.9.10 completion.
- Package growth -> no general Python runtime; dependency/package measurement is
  a gate before accepting a new parser package.
- UI bypasses core validation -> typed request only plus architecture tests.
- Private firmware leaks -> hash/size snapshots only and closed-inventory tests.
- Candidate silently becomes supported -> no registration side effect, monotonic
  blocked promotion, and support-matrix exclusion tests.
- Malicious workbook/archive/command text -> bounded readers, no execution, and
  traversal/reparse/archive-bomb negative tests.

## Compatibility and migration

1. The owner and required security/dependency reviewers accept this ADR and the
   open choices below.
2. Define typed Application request/result/issue contracts and the three port
   families plus the format-neutral candidate IC configuration contract without
   UI, a Profiles project reference, or a new generated workspace schema.
3. Define and owner-review `candidate-ic-workbook-v1` as a strict mapping to the
   typed configuration before selecting or adding a workbook reader.
4. Implement immutable Infrastructure readers and atomic workspace writing with
   no new runtime dependency; add cross-platform security tests.
5. Implement a focused Bootstrap forwarding adapter that delegates candidate
   projection to existing Profiles V2 DTO/normalizer/compiler boundaries. Any
   new candidate-workspace schema is reviewed separately.
6. Prove Python/C# four-record parity, keep the exact four-file root export, then
   remove Python from the product path.
7. Review and version `candidate-workspace-v1` before persisting generated or
   PR-ready outputs; typed previews do not pre-authorize that contract.
8. Add a thin CLI replay handler and the Settings UI flow over the same typed
   configuration use case.
9. Run clean-package size/security smoke and keep the existing package ceiling.
10. Keep every generated candidate unregistered until its normal evidence and
   owner-review PR is approved.

Early v0.9.10 architecture/test work may proceed under the pre-tag candidate
policy, but it cannot merge into a release branch before the reviewed v0.9.9
predecessor is established.

## Verification plan

- Architecture tests for dependency direction, one Application intake policy,
  no Application-to-Profiles reference, no Presentation filesystem/process/
  Profiles references, a semantics-free Bootstrap forwarding adapter, and no
  second compiler.
- Exact Python/C# parity for all current success and negative intake vectors,
  including duplicate keys, path escapes, reparse points, lock files, hash/size
  mismatch, competing output, interruption, and deterministic replay.
- Reader tests for workbook archive limits, macros/formulas/external links,
  duplicate/ambiguous cells, mmap ambiguity, BAT metacharacters, and proof that
  no submitted command or macro executes.
- Contract tests proving generated documents validate against existing V2 schemas,
  incomplete facts remain blocked, and no schema/allowlist changes occur.
- Application tests proving candidate compilation uses the existing compiler and
  optional parity uses the existing composition engine without support promotion.
- Full-byte synthetic/private parity tests when sufficient evidence exists;
  expected-output mismatch must fail without altering declarations.
- Workspace security tests for immutable sources, the exact four-file root
  export, closed inventory, atomic output, private-payload exclusion,
  cancellation, and reparse/TOCTOU resistance. A later versioned workspace
  wrapper requires its own schema and negative tests.
- ViewModel/UI smoke tests for keyboard/focus/accessibility/localization, large
  diagnostics, cancellation, and identical typed requests from UI/CLI fixtures.
- `python scripts/verify.py --all`, Polytail, independent architecture/security
  review, package-size smoke, and all applicable R3 firmware-owner gates.

## Release impact

- v0.9.9 gains an architecture decision only; no runtime, package, schema,
  firmware, support, or UI behavior changes.
- v0.9.10 remains candidate-only and support-neutral.
- Hand-written production C#/AXAML must remain within the active milestone
  ratchet, and the portable package must remain under 58,076,715 bytes unless
  the owner explicitly changes the reviewed ceiling.
- No new runtime dependency, external authority, top-level navigation item, or
  release artifact is authorized by this Proposed ADR.

## Decisions and reviews required for acceptance

1. Accept the typed in-process C# use case as canonical, with Python retained
   only until exact four-record parity and then retired.
2. Accept strict `.xlsx` as the initial workbook contract, or identify required
   `.xlsm`/legacy `.xls` cases before reader design starts.
3. Approve the versioned sheet/table/column mapping after the format-neutral
   candidate IC configuration contract is reviewed; UI work starts afterward.
4. Accept **Settings → New IC candidate** as a secondary surface without adding
   a fourth top-level tab.
5. Require the security reviewer to accept the workbook/archive, mmap/BAT,
   private-payload, process-authority, and workspace threat model, and require a
   dependency/release reviewer to accept the selected reader dependency and
   measured package impact (or confirm that the initial slice adds none).
