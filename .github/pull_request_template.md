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
A new head needs a new record.
-->

```nfc-authority
{
  "risk": "<R0 | R1 | R2 | R3>",
  "roles": [],
  "implementationOwner": "<agent runtime or person>",
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

## R2/R3 evidence

- ADR/contract:
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
