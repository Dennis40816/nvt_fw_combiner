---
name: composition-experience-change
description: Change Display, TP HW, TP FW, General Merge, or General Replace authoring policy, region access, mapping editor contracts, or persona UI. Do not add workflow-specific byte execution or rely on UI-only restrictions.
---

# Composition Experience Change

1. Read ADR 0003, ADR 0004, ADR 0005, the canonical variable model, profile/request schemas, and the affected IC region catalog.
2. State composition kind, experience, initializer, audience, allowed regions, forbidden regions, atomicity, and processor dependencies.
3. Preserve the locked policies: Display DP parts/TP whole; TP HW CtrlRAM only/DP whole; TP FW non-CtrlRAM TP/DP whole; General explicit mappings inside approved ranges.
4. Implement enforcement in profile compiler/application policy. UI visibility is secondary and cannot be the only guard.
5. General mapping changes must preserve one state model for canvas and exact table/manual entry and compile to normal operations.
6. Reject arbitrary scripts, user-provided commands/processor paths, filename inference, implicit overlap, or unbounded ranges.
7. Add positive and cross-persona negative tests, request/schema tests, round-trip tests, and stable issue codes.
8. Run affected checks and Polytail. Report access-policy and compatibility impact.
