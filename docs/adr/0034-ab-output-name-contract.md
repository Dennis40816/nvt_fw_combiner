# ADR 0034: Render AB output names from execution snapshots

- Status: Partially superseded by [ADR 0036](0036-output-destination-and-ab-naming-v2.md)
  for automatic-name and collision behavior; immutable snapshot/token/report
  provenance requirements remain accepted
- Date: 2026-07-23
- Owners: Product owner, architecture owner, firmware owner
- Supersedes: The token-free AB output defaults in the `v0.9.14` pilot profiles
- Partially superseded by: ADR 0036 on 2026-07-23

## Context

The supported NT51919, NT51929, and NT51932 AB Merge profiles currently use a
static filename such as `nt51929-ab-merge.bin`.  The workbench can display DP1,
DP2, TPA, and TPB version facts, but those display values are not an execution
authority and must not become the source of a published filename.

The requested output form is:

```text
NT519xx_A_DmmmmTvvvv_B_DmmmmTvvvv_yyyyMMdd.bin
```

where each A token comes from DP bank 1 and TPA, and each B token comes from
DP bank 2 and TPB.  This is a profile/naming contract change, not a firmware
range, operation-order, relocation, CRC, header, or support-promotion change.

The existing Application request rejects profile templates containing tokens
before it reads the immutable artifacts.  Resolving the name in the UI or by a
second Bootstrap file read would permit stale display data and would break the
shared execution/report path.  It must instead use the accepted immutable
execution snapshots inside the Application run.

## Decision drivers

- A generated name must be traceable to the exact DP_AB, TPA, and TPB bytes
  consumed by the build or preview.
- DP and TP subversions must be preserved without encoding Jira/project display
  labels in an output filename.
- Existing explicit CLI/UI output-path overrides and atomic no-clobber behavior
  must remain intact.
- The first release must not promote NT51950 or NT51951, infer a route by
  filename, or introduce an AB-only executor.
- The rendered name, its token sources, and its date source must be visible to
  delivery/review automation through the run report.

## Decision

### Scope and admission

`v0.9.15` originally activated this naming contract only for already enabled AB
Merge profiles. Its first execution scope was the owner-approved
NT51919/NT51929/NT51932 perfect family.  The contract is designed as a typed
profile capability so future NT51950/NT51951 AB profiles can opt in only after
their independent runtime-admission, evidence, and firmware-owner gates pass.
Under the later identity-independent compiler rule in ADR 0015, the exact
`AbCodeV1` renderer plus Merge composition carries compiler authority; workflow
identity remains trusted data and policy/evidence controls which profiles may
declare the renderer. No profile is promoted by this ADR.

### Exact tokens and template

The rendered automatic filename is exactly:

```text
NT{ic}_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin
```

with these closed tokens:

| Token | Value | Execution source |
| --- | --- | --- |
| `ic` | Canonical five-digit IC member without a presentation alias, for example `51929`. | Compiled profile identity. |
| `dp-a` | `D` + uppercase two-digit hexadecimal DP major byte + uppercase, zero-padded two-digit hexadecimal DP minor-version nibble. Example: major `0x82`, minor `0x0` becomes `D8200`. | DP_AB accepted execution snapshot bank 1, `[0x00000, 0x40000)`, through the approved CMI reader for the compiled IC. |
| `tp-a` | `T` + uppercase two-digit hexadecimal firmware version byte + uppercase two-digit hexadecimal firmware sub-version byte. Example `0x80`, `0x04` becomes `T8004`. | TPA accepted execution snapshot through the canonical terminal-relative NVT FWConfig reader. |
| `dp-b` | Same encoding as `dp-a`. | DP_AB accepted execution snapshot bank 2, `[0x40000, 0x80000)`, through the same approved CMI reader. |
| `tp-b` | Same encoding as `tp-a`. | TPB accepted execution snapshot through the canonical terminal-relative NVT FWConfig reader. |
| `date` | UTC calendar date in invariant `yyyyMMdd` form. | The injected Application `ISystemClock.UtcNow`; the value is captured once for one run. |

For example, the values in the owner-provided illustration render as:

```text
NT51929_A_D8200T8004_B_D8301T8102_20260723.bin
```

The profile template contains only the typed token identifiers; the renderer
does not consume slot labels, version-pill text, Jira badges, filenames, file
paths, whole-file hashes, or user-entered display values.  DP/TP parsing uses
the same accepted snapshot boundary as execution, so an ignored trailing input
tail cannot affect the name.

### Unknown metadata behavior

Version metadata remains informational for AB route admission.  A missing,
unreadable, malformed, or invalid DP/TP version value renders the relevant
closed placeholder instead of fabricating a numeric value:

```text
DP: Dxxxx
TP: Txxxx
```

The run records one non-blocking `output-naming.metadata-unknown` diagnostic
per unknown token source and records the parser/status in report provenance.
It does not select, reject, or promote an AB route.  A short artifact still
fails before naming because its compiled execution range is unavailable.

### Override and collision behavior

`allowOverride: true` continues to permit an explicit UI/CLI output path.  An
override is a Windows-safe literal filename at the Application boundary; it is
not parsed as a template.  The engine still derives and reports the automatic
candidate name from execution snapshots, but commits the explicit override.
`allowOverride: false` remains an exact automatic-name policy.

The automatic name never gains a hidden counter, timestamp, or random suffix.
The existing atomic writer rejects an existing destination by default.  An
explicit `--overwrite` remains the sole opt-in overwrite authority.  This
keeps a collision visible rather than silently publishing an ambiguous AB
artifact.

### Date/time-zone and Preview-to-Build behavior

`date` is UTC, not the host-local calendar, so tests and reports do not depend
on a workstation time-zone setting.  `startedAtUtc` is the single run time
authority; the renderer uses its UTC date for every automatic token in that
run.  A Preview token binds the rendered filename and its token provenance.
A Build using that preview token must reproduce the same automatic filename;
crossing a UTC date boundary requires a new Preview rather than silently
changing the publication target.

### Report provenance

The run report gains a typed output-naming provenance object.  It contains:

- the compiled profile template and renderer kind/version;
- automatic candidate filename, actual committed/requested filename, and
  whether the actual name was automatic or an explicit override;
- canonical IC token, four DP/TP tokens, known/unknown state, and parser
  identity for each token;
- the source address space and accepted execution snapshot identity already
  represented by the input report entries; and
- `resolvedAtUtc`/`dateSource: utc` plus the profile compilation fingerprint.

It contains no local input path and no UI presentation string.  The report
therefore lets review automation correlate a proposed or committed name with
the same artifact hashes and immutable ranges the composition engine used.

### Architecture

The compiler lowers a profile-declared AB output-name strategy and its source
slots into a typed `CompiledOutputNamingRequirement`.  Application owns when a
run resolves the name and binds it into reports, Preview tokens, and output
commit.  A narrow Application port/adaptor reads only the already accepted
immutable snapshots using the existing CMI and FWConfig readers.  Bootstrap
selects the directory and passes an explicit override intent; it does not
pre-read firmware or render version text.  Domain remains free of file I/O,
wall-clock access, and presentation dependencies.

This uses the existing `composition-profile-v2` output-template and
required-token fields.  The compiler recognizes only this exact AB Code v1
template/token set as an executable typed renderer; all other token templates
remain deferred.  Existing token-free `reject` templates retain their current
static behavior, and `replace-underscore` remains non-executable.

## Compatibility and migration

- Existing AB automatic defaults change from the three static lowercase names
  to the canonical mixed-case automatic format above.
- Explicit caller output paths, `--overwrite`, atomic promotion, input
  immutability, and existing output directory policy are unchanged.
- Non-AB profiles and existing token-free V2 profiles are behaviorally
  unchanged.
- Stored Preview tokens bound to a pre-`v0.9.15` static output name cannot
  authorize a new naming-contract build; callers must preview again.
- NT51950/NT51951 receive no profile/runtime registration or support-stage
  change in this migration.

## Verification matrix

| Area | Required evidence |
| --- | --- |
| Token rendering | All four known values render upper-case, zero-padded bytes/nibbles; mixed A/B values remain distinct; Jira is absent. |
| Source authority | Changing UI/presentation text or input filenames cannot change the result; changing an accepted DP bank or TP snapshot value changes only its matching token. |
| Snapshot boundary | DP1 and DP2 use their independent half-open banks; TP terminal metadata and DP CMI metadata in ignored trailing tails are not consumed. |
| Unknowns | Each unavailable/invalid DP or TP value yields the exact `Dxxxx`/`Txxxx` placeholder and a non-blocking, reportable diagnostic; short inputs remain build-blocking before output. |
| Time | A fake UTC clock proves invariant `yyyyMMdd`; a Preview/Build date change produces a Preview-token mismatch rather than a silent target change. |
| Override/collision | Automatic name commits by default, an explicit literal override wins when allowed, no-overwrite collision fails atomically, and `--overwrite` is the only clobber path. |
| Report/review | Preview and Build report the same resolved tokens/provenance, bind the compilation fingerprint and input snapshot summaries, and expose no local path. |
| Regression | Existing token-free V2 profiles, AB byte placement/relocation, source immutability, and current NT51919/NT51929/NT51932 admission remain unchanged. |
| Future candidates | Contract/schema tests prove NT51950/NT51951 cannot gain runtime support merely by the renderer being available. |

## Alternatives rejected

- **Format UI version pills into a filename:** presentation may be stale and is
  not an execution authority.
- **Parse source filenames or Jira/project labels:** those values are evidence
  or display facts, not profile-owned metadata.
- **Use a Bootstrap pre-read:** it duplicates I/O and can diverge from the
  snapshot actually composed.
- **Fail every build on unknown version metadata:** conflicts with the current
  AB policy that version metadata is informational and non-selecting.
- **Add collision suffixes automatically:** produces an unreviewed name that
  no longer identifies the requested artifact set.
- **Use the host-local date:** makes the same UTC run produce different output
  names on differently configured workstations.

## Approval and activation gates

The product/firmware owner accepted the compiler/Application/port boundary,
CMI-bank and terminal-relative TP sources, unknown-placeholder behavior, and
UTC calendar decision on 2026-07-23.  Architecture review remains required for
the implemented compiler/Application/report/Preview-token change, and the normal
profile, narrow behavior, regression, Polytail, CI, and Codex-review gates
remain required before integration.  No approval here promotes NT51950 or
NT51951, changes AB bytes, or waives any later release gate.
