# NVT FW Combiner Agent Instructions

## Mission and authority

Build deterministic firmware images. Correct bytes, one execution model,
explicit address spaces/ranges, constrained external processing, traceability,
and independent golden evidence outrank UI speed or code volume.

Use authority in this order:

1. Root and nearest nested `AGENTS.md`.
2. `SPEC.md`, accepted ADRs, contracts, schemas, profiles, and executable tests.
3. The matching repository-native skill through
   `docs/governance/agent-skill-routing.md`.

`refcode/` is immutable evidence only. Never commit real firmware, credentials,
generated releases, or private evidence except owner-approved golden fixtures
under the declared golden policy.

## Canonical commands

```text
python scripts/verify.py --structure-only
python scripts/verify.py --all
python -m pytest                  # from tools/crc-worker
```

Do not create a second repository verifier. Use
`docs/governance/development-execution-workflow.md` for preflight, narrow-test
selection, retry, and checkpoints.

## Immutable architecture and firmware rules

- Domain is pure. Application owns use cases through ports. Infrastructure
  implements adapters without redefining firmware semantics. UI/CLI send typed
  requests through the same Application services.
- Every workflow uses one planner/executor. Merge initializes blank bytes;
  Replace clones a required immutable reference.
- Profiles own regions, atomicity, access, mappings, overlap, processors,
  validations, integrity behavior, and output naming.
- All ranges are checked, half-open `[start, endExclusive)`, and name their
  address space. Never change bytes, order, ranges, CRC/header behavior,
  padding, truncation, or naming without authority and tests.
- `unknown` integrity is not `none` and cannot compile as supported.
- External processors modify only host-created staging copies. The host diffs
  before/after bytes and rejects writes outside declared ranges.
- General Merge/Replace compile explicit mappings into the same operation
  model; arbitrary scripts and per-run executable paths are forbidden.
- Support, family, topology/IC Count, metadata, and evidence are declared facts,
  never inferred from filename, PID, version, hash, or a golden observation.

Directory-specific rules live in the nearest nested `AGENTS.md`.

## Risk-adaptive gates

| Risk | Required local/review gate |
| --- | --- |
| R0 | Structure check; no mandatory subagent or full Polytail audit. |
| R1 | Narrow test, final gate, ordinary review. |
| R2 | R1 plus architecture/contract review and scoped Polytail. |
| R3 | R2 plus the human approval and independent evidence for the authority touched. |

Firmware-semantic R3 requires firmware-owner review, byte/golden evidence, and
exact write-range audit. Release/signing/permission R3 requires release-owner
and release-policy evidence. Changes touching both require both gates.

## Branch and review boundary

`main` is stable. Work uses the owner-selected integration branch and a
reviewable `feature/<version>/<topic>` branch when independent. Features merge
to their approved integration branch, never directly to `main`. Detailed
version, `0.10.x`, release, cleanup, PR, and owner-override rules live in
`docs/governance/branch-version-and-release-governance.md`.

Do not merge failing checks, P0/P1 findings, schema/profile drift, undeclared
mutations, or missing mandatory human evidence. GitHub mutations follow
`docs/governance/agent-issue-tracker.md`.

## Skills and completion

Repository-native NFC skills own firmware semantics; workflow skills only
organize work. Explicit skill invocation does not broaden filesystem, process,
GitHub, release, or firmware authority. Apply `.agents/skills/polytail/` to
non-trivial R1-R3 changes with scope proportional to touched authority.

Before a specification, architecture, terminology, or planning change becomes
an implementation goal or ticket rewrite, use `$grill-with-docs` while owner
decisions remain. Ask one decision at a time, record each accepted result in its
canonical owner, complete the document consistency audit, and only then run
`$to-tickets`. Use explicit `$grilling` when the owner requests a generic
decision interview. Do not restore a separate `domain-modeling` workflow.

A change is done only when its approved behavior is complete, tests and final
gate pass, production semantics have one owner, documentation/contracts agree,
temporary adapters have deletion criteria, private/generated data is absent,
and required review/evidence gates are recorded.
