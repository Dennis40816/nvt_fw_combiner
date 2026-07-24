---
name: to-spec
description: Turn the current conversation into a spec and publish it to the project issue tracker — no interview, just synthesis of what you've already discussed.
---

For NFC repository work, apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before acting.

This skill takes the current conversation context and codebase understanding and produces a spec (you may know this document as a PRD). Do NOT interview the user — just synthesize what you already know.

Use `docs/governance/agent-issue-tracker.md` as the live label and publication
authority. When an explicitly named GitHub target is in scope and
`ready-for-agent` is available, publish the completed spec with that label. If
the tracker records the label as missing, draft the spec and stop before GitHub
creation/labeling with that exact gate.

## Process

1. Explore the repo to understand the current state of the codebase, if you
   haven't already. Use NFC's canonical specification/contract/profile
   vocabulary throughout the spec, and respect relevant ADRs.

2. Derive the seams at which to test the feature from the conversation and the
   existing codebase. Existing seams should be preferred to new ones and the
   highest seam is preferred. If a necessary seam is still uncertain, record
   the assumption and validation need in Testing Decisions; do not pause to
   interview the user.

3. Write the spec using the template below. Publish it to an explicitly named
   project-issue target only when the tracker gate above permits that mutation;
   apply `ready-for-agent` with no additional triage. Otherwise return the
   drafted spec and the exact publication gate.

<spec-template>

## Problem Statement

The problem that the user is facing, from the user's perspective.

## Solution

The solution to the problem, from the user's perspective.

## User Stories

A LONG, numbered list of user stories. Each user story should be in the format of:

1. As an <actor>, I want a <feature>, so that <benefit>

<user-story-example>
1. As a mobile bank customer, I want to see balance on my accounts, so that I can make better informed decisions about my spending
</user-story-example>

This list of user stories should be extremely extensive and cover all aspects of the feature.

## Implementation Decisions

A list of implementation decisions that were made. This can include:

- The modules that will be built/modified
- The interfaces of those modules that will be modified
- Technical clarifications from the developer
- Architectural decisions
- Schema changes
- API contracts
- Specific interactions

Do NOT include specific file paths or code snippets. They may end up being outdated very quickly.

Exception: if a prototype produced a snippet that encodes a decision more precisely than prose can (state machine, reducer, schema, type shape), inline it within the relevant decision and note briefly that it came from a prototype. Trim to the decision-rich parts — not a working demo, just the important bits.

## Testing Decisions

A list of testing decisions that were made. Include:

- A description of what makes a good test (only test external behavior, not implementation details)
- Which modules will be tested
- Prior art for the tests (i.e. similar types of tests in the codebase)

## Out of Scope

A description of the things that are out of scope for this spec.

## Further Notes

Any further notes about the feature.

</spec-template>
