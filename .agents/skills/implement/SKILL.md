---
name: implement
description: "Implement a piece of work based on a spec or set of tickets."
---

For NFC repository work, apply [Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before acting.

# Implement

Implement only the approved spec, issue, or ticket scope.

1. Run the `AGENTS.md` preflight and identify the owner-selected version or
   feature branch, risk class, affected NFC authority, acceptance criteria,
   evidence/human gates, narrow test, and final gate.
2. Read the full source request and the relevant code, contract/profile, and
   existing tests once. Resolve material ambiguity before it can change public
   or firmware behavior.
3. Work in the smallest independently verifiable vertical slice. Use `$tdd` at
   a declared public seam when behavior is executable; use independent
   `$golden-regression` evidence for firmware byte expectations.
4. After each slice, format, run the narrow test, review the diff, apply
   `$polytail`, and create the required phase commit with only owned files
   staged.
5. When the approved scope is complete, run `$code-review` against the fixed
   base and run the canonical final gate required by the risk class.

Completion requires all acceptance criteria, passing required checks, no
private/generated payloads, documented residual evidence gates, and a
reviewable commit history. Do not represent R2/R3 work as merge-ready before
its required reviewer or firmware-owner gate.
