# ADR 0047: Application owns General occupancy and resource admission

- Status: Accepted
- Date: 2026-07-30
- Owners: Product owner + architecture owner
- Amends: ADR 0005
- Amended by: 2026-08-09 complete legacy-architecture retirement
- Issue: #250

2026-08-09 amendment: `GeneralAuthoringAdmissionResult`, its exact Parent
authority, occupancy, and resource-admission semantics remain accepted. The
shared seam is now consumed directly through the canonical Application
authoring/session path by Presentation, CLI, Saved Rule, memory projection, and
compilation. The Bootstrap compatibility Parent/projection described below is a
historical bridge and must be deleted rather than renamed once its live callers
migrate.

## Context

General Merge and General Replace already share a typed, half-open mapping
draft, but target overlap was still discovered after rows had been lowered
into ordered operations. The resulting diagnostic said that one operation
overlapped an "earlier" operation, so reordering the same stable mapping ids
changed the explanation. File mappings, hexadecimal overwrites, and fills also
had separate pre-allocation checks.

Saved Rules can narrow an exact Trusted Parent. They must not become another
owner for mapping limits, slot lengths, protected ranges, or execution.
Profile-owned POSTBUILD is not a user-authored mapping and must not enter a
ledger that exists to explain author actions.

## Decision

Application owns one immutable `GeneralAuthoringAdmissionResult` before
compilation or inline allocation.

1. Every file, hexadecimal overwrite, and hexadecimal fill row contributes its
   stable mapping id, source kind, named target address space, and half-open
   target range to one authored occupancy ledger.
2. Ledger comparison is independent of row sequence. Any byte intersection is
   a blocking typed issue containing both ids in ordinal order and the exact
   half-open intersection.
3. Application declares global technical ceilings for mapping count, total
   authored write bytes, whole-file length, and safe inline materialization.
4. The exact Trusted Parent declares semantic ceilings and named input-slot
   minimum, maximum, and optional discrete accepted lengths. An optional Saved
   Rule may only narrow that effective Parent contract. Empty intersections or
   attempted broadening block.
5. The resolved result, including effective limits, observed path-free input
   lengths, occupancy, and typed blockers, is the shared seam for Presentation,
   CLI/report, Saved Rule adapters, memory projection, and compilation.
   Application requests observations through an inward port. The filesystem
   adapter may answer that port but cannot define Parent limits, and downstream
   lowering must not inspect the file again or reconstruct admission.
6. Unreferenced source-file tails are allowed. They block only when the
   whole-file technical ceiling or resolved slot contract rejects the observed
   file.
7. Profile-owned POSTBUILD remains outside authored occupancy. Its compiled
   operation sequence, declared processor write authority, and staged mutation
   audit remain unchanged.

Current callers use a default technical ceiling of 4096 mappings,
`Int32.MaxValue` total write and whole-file bytes, and `0x800000` safely
materialized inline bytes. Exact Parent or Saved Rule limits can only reduce
those values.

At the #250 pre-retirement checkpoint, the General V2 profile schema did not
yet serialize its resource envelope. Until that schema is available, Bootstrap
projects a compatibility
Parent whose scalar limits equal the Application technical ceilings and whose
named file slots are the exact typed-draft slot ids with a profile-confirmed
`1..Int32.MaxValue` interval. This bridge never derives authority from an
observed file length. Saved Rule v1 likewise has no `accessEnvelope`; its
closed reviewed rows provided an exact compatibility narrowing for mapping
count and total authored bytes before the #254 retirement.

## Alternatives rejected

- Keep compiler-order diagnostics as the user contract: deterministic
  execution order does not make authoring overlap order-dependent.
- Maintain separate validators for file, overwrite, and fill rows: this can
  admit contradictory occupancy or drift between UI and CLI.
- Add POSTBUILD allowed-write ranges to authored occupancy: processor
  authority is profile-owned and has a different audit contract.
- Let a Saved Rule silently broaden or replace Parent limits: imported data is
  not firmware authority.

## Consequences

- General overlap is blocked with
  `general.admission.target-intersection`; reports no longer expose
  `overlaps earlier operation` for admitted General requests.
- The composition plan keeps its generic overlap invariant as defense for
  fixed/profile operations and non-General callers.
- Saved Rule v2 lifecycle tickets consume the resolver rather than recreate
  limit arithmetic.
- Inline fills fail before allocation when their target length exceeds the
  resolved safe materialization ceiling.

## Compatibility deletion

- `CreateCurrentGeneralTrustedParentPolicy` remains until the composition-profile
  schema declares a resource envelope and trusted-bundle projection supplies
  its exact typed Parent policy to the Application use case.
- Under the 2026-08-09 terminal-retirement program, that compatibility Parent
  and Bootstrap projection are deleted in the same slice that supplies and
  migrates every live caller to the exact typed Parent policy. They cannot be
  renamed or retained as fallback.
- Issue #254 deletes the Saved Rule v1 parser, exact-row projection, draft
  adapter, and standalone inspection path. Production accepts Saved Rule v2
  only and rejects v1 with `saved-rule.schema-version.unsupported`; there is no
  runtime migration or v1 emission path. The historical v1 schema remains
  evidence and cannot authorize inferred limits or Parent-slot matching.
- Issue #254 also deletes the zero-caller timestamp file-stamp compatibility
  adapter and obsolete General Merge fill-byte issue alias. The immutable
  content snapshot implementation remains the sole active file-stamp owner.
- Delete the remaining General-specific translation from compiler
  operation-overlap diagnostics and any duplicate Bootstrap/CLI resource
  checks only after General Merge, General Replace, Saved Rule,
  memory-projection, and desktop callers all consume the Application result.

The Domain plan overlap check remains; it protects compiled operation algebra
rather than authoring policy.

## Verification

- File/file, file/overwrite, overwrite/fill, containment, boundary-touch, and
  reordered-row intersections.
- Exact issue ids, both mapping ids, and half-open intersections.
- Mapping-count, total-write, overflow, whole-file, per-slot,
  safe-materialization, and empty-limit failures.
- Unreferenced file-tail acceptance and POSTBUILD exclusion.
- UI/CLI report, memory, Preview token, and compiler lowering consume
  the same admitted snapshot, including observed input lengths.
- Application and Bootstrap narrow suites, architecture tests, structure gate,
  final verifier, independent architecture/contract review, and scoped
  Polytail.
