## Outcome

What changed and why.

## Scope

- Risk: declared in the authority block below
- Affected layers/routes:
- Non-goals:

## Owner search

- Existing semantic owner, callers and typed contract:
- Search evidence:
- Disposition: `reuse` / `extend-owner` / `reject-duplicate`
- Affected authority and paths:

## Authority (ADR 0080)

<!--
The `governance / authority` check reads the one `nfc-authority` block below
(docs/governance/development-execution-workflow.md, "Authority check").
- risk: R0 to R3, at least the floor of the changed paths; any role makes it R3.
- roles: every role the changed paths, a review record or a classification
  require (governance-owner, firmware-owner, release-owner); you may add more.
- evidence, one object per declared role:
    "firmware-owner":   {"golden": "<byte and Golden evidence>", "writeRanges": "<exact write-range audit>"}
    "release-owner":    {"release": "<release-owner evidence>"}
    "governance-owner": {"change": "<the rule, permission or approval-authority change>",
                         "classification": {"<unclassified path>": {"floor": "R3", "roles": []}}}
- implementationOwner and ownedPaths: the single writer and the surfaces it owns.

Review record (the reviewer posts it; it is not part of this description): a
pull request review of type COMMENT, created through the API with commit_id set
to the head, whose body holds one fenced block with the info string
nfc-review-record and this JSON object:
  {"head": "<40-character head SHA>", "reviewer": "<runtime>/<model>",
   "mode": "other-runtime | same-runtime-fresh-session",
   "verdict": "accept | accept-with-changes | reject", "openP0P1": 0,
   "state": "complete | incomplete", "addedRoles": []}
Posting the COMMENT record starts governance / authority through W1 without
editing the PR body; review edits/dismissals also start a run. Wait for the
current-head result and verify the live record. A new head needs a new record.
R0–R2 need no owner approval on a target whose R41 trial/cutover is complete;
R3 needs code-owner approval naming every required role. Unswitched targets
retain live rules. Enable --auto --merge --match-head-commit <head> only after
the gates pass; cancel auto-merge before a push or authority/record/evidence
change, repeat verification, then re-enable. The head guard applies at the call.
-->

```nfc-authority
{
  "risk": "<R0 | R1 | R2 | R3>",
  "roles": [],
  "implementationOwner": "<runtime/model, e.g. codex/gpt-6-sol>",
  "ownedPaths": ["<path>"],
  "evidence": {}
}
```

## Firmware and contract impact

- Firmware semantics changed: Yes / No
- Schema/API changed: Yes / No
- Golden evidence changed: Yes / No

## Verification

- Narrow:
- Final:
- Not run:

## Review gates

- Required reviewers:
- Residual evidence:
- Merge target:

## R2 architecture/contract evidence

- ADR/contract:
- Relevant behavioral tests:
- Scoped Polytail:
- Independent exact-head review record and session IDs:

## R3 role evidence and owner approval

- Required roles and authority change:
- Byte or golden evidence:
- Write ranges:
- Human approval:
- Support status:

## Waiver

None, or a statement under the [execution workflow](../docs/governance/development-execution-workflow.md#waivers):

- Head SHA and scope:
- Rule/tool, reason and risk:
- Owner, issue and approver:
- Creation date, expiry date and removal condition:
- Relevant authority owner's last-push approval:
