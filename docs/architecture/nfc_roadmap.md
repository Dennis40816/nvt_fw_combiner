# NFC Roadmap

Status: active owner roadmap, amended 2026-07-25.

This file owns future milestone order and release boundaries only. It does not
repeat product requirements, architecture decisions, firmware facts, skill
inventories, issue acceptance criteria, or historical release notes.

Canonical detail lives in:

- [`SPEC.md`](../../SPEC.md) for product scope and accepted requirements;
- [`CHANGELOG.md`](../../CHANGELOG.md) and dated release records for completed
  history;
- [`verification-report.md`](../references/verification-report.md) for the
  current package/release evidence state and open gates;
- the [`0.10.x maintainability design`](0.10.x-maintainability-working-design.md)
  and its linked continuations for architecture reasoning;
- [`ADR 0021`](../adr/0021-code-size-ratchet-and-convergence.md) for the
  production-code measurement and target;
- the [`agent skill inventory`](../governance/agent-skill-inventory.md) and
  [`routing contract`](../governance/agent-skill-routing.md) for adopted
  workflow skills; and
- the [`0.10.x ticket dependency plan`](../governance/0.10.x-ticket-dependency-plan.md)
  plus GitHub issue bodies for implementation order and acceptance criteria.

## `0.10.0`: planning and governance baseline

`0.10.0` reconciles its original `v0.9.15` planning baseline with the complete
reviewed `v0.9.16` hot-fix. Its scope is M0/M1 inventory, accepted
architecture and terminology, skill/workflow governance, validation standards,
the owner-updated FlashMap evidence reference, and the approved ticket
dependency plan.

It does not allocate or implement a production Support Matrix,
release-recovery, Error, Report, or maintainability slice. The annotated-tag
newline defect and visible clean-Windows UI-smoke gap remain explicit in the
verification report; neither is represented as closed by this planning
release.

## `0.10.1` through `0.10.5`: owner-allocated implementation

The approved GitHub issues named in the
[`0.10.x` ticket dependency plan](../governance/0.10.x-ticket-dependency-plan.md)
and each issue's `Blocked by` edges control the implementation frontier. Issue
numbers are not assumed to be a contiguous range; later approved tickets such
as #207, #214, #219, and #221 remain first-class program work. Dependency depth
is not a release version. The owner allocates only dependency-ready,
reviewable slices after considering risk, evidence, file ownership, and
available reviewers. The owner release allocation recorded on 2026-08-02 is:

1. `v0.10.1` closes the headless canonical foundation.
2. `v0.10.2` completes the entire remaining approved refactoring graph through
   #197, including deferred UI adoption, compatibility and legacy-runtime
   deletion, all four Core Convergence slices, and the hard 22,607-line gate.
3. `v0.10.3` re-measures that result and audits whether any ownership or code
   can be removed or expressed more simply without weakening evidence.
4. `v0.10.4` unifies every preload under one observable, cancellable, bounded,
   and user-controllable performance lifecycle.
5. `v0.10.5` reserves a configured-path update screen so routine delivery does
   not require repackaging and email. Its trust, rollback, version,
   network/share, and release-policy contract remains deferred to a later owner
   specification discussion, so no implementation ticket is created yet.

The complete refactoring release `v0.10.2` is downstream of #197 and all applicable
architecture, firmware-owner, golden, package, clean-Windows, protected-CI, and
release-owner gates. No roadmap entry may waive those gates or move, overwrite,
or redefine an existing stable tag or asset. The later audit, performance, and
update-experience releases do not reopen #197 or weaken its retained gate.

## `0.11.0`: AB certification and family evidence

`0.11.0` starts only from the latest official reviewed `v0.10.x` predecessor;
the current owner sequence places it after the reserved `v0.10.5` milestone.
It keeps independent certification tracks
for:

1. NT51950 AB `1 IC` and `Cascade`;
2. selector-free NT51951 AB; and
3. Perfect-family and `ldc-tp-only` evidence scope.

One topology, IC, workflow, or fact-scoped alias never certifies another beyond
its owner-approved evidence scope.

## Later owner queue

Only work not already owned by the current ticket dependency plan remains here:

- saved/customized-rule persistence and import after canonical typed mapping
  authoring exists, without arbitrary scripts; and
- IC family/rule authoring UI after the trusted-bundle and evidence models are
  implemented and reviewed.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
