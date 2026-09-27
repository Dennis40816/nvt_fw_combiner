---
name: nfc-composition-experience-change
description: Change active composition authoring policy, region access or General mapping contracts through the canonical experience policy and shared compiler.
---

# Composition Experience Change

Read the [experience and access policy](../../../docs/architecture/experience-and-access-policy.md),
ADRs 0003-0005 and their amendments, the affected profiles and request schema.
Those owners define the active experiences; historical personas do not grant
current access. State the affected composition kind, initializer, experience,
IC/topology, region access and processor dependencies before changing policy.

Preserve these invariants:

- Profiles and their compiler enforce allowed regions, atomicity, protected
  ranges and overlap. UI visibility renders those decisions and is not the
  only guard. The executor uses the shared operation model without branching
  on experience or audience.
- CtrlRAM eligibility comes from physical `owner = tp`, `kind = ctrlram`
  facts or approved groups consisting only of those regions. DP/LDC facts
  survive dedicated DP Replace retirement; they acquire no new execution path.
- General mappings have explicit source/target address spaces and half-open
  ranges, sequence, overlap policy and reason. Canvas and exact table entry
  share one state and compile to normal operations.
- Input bindings remain distinct from logical views. Arbitrary scripts,
  per-run processor paths and filename-derived firmware facts are forbidden.

Test changed allowed and denied requests at the compiler/Application boundary,
including malformed ranges and affected round trips. Add UI checks only where
presentation changes. Use skill `nfc-review` and the root risk gates; report
compatibility, access changes and any missing firmware authority.
