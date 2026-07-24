---
name: ui-experience-change
description: Design or modify Merge/Replace screens, Display/TP HW/TP FW/General experiences, mapping editor, memory visualization, ViewModels, localization, accessibility, or UI smoke coverage. Do not implement firmware semantics in UI code.
---

# UI Experience Change

1. Read `SPEC.md`, ADR 0004, ADR 0005, experience/access policy and Presentation `AGENTS.md`.
2. Identify composition kind, experience, layout policy, inputs, region access and confirmation gates.
3. Obtain all behavior through typed bootstrap/application contracts; never infer IC, region or processor rules from labels/filenames.
4. Keep one mapping state shared by drag canvas and exact table/manual editing; require deterministic round-trip tests.
5. Display address space, source/target ranges, operation, overlap and processor effects before Build.
6. Add keyboard, focus, screen-reader, localization and high-contrast acceptance coverage.
7. Add ViewModel/unit tests plus the narrowest UI smoke/snapshot coverage available.
8. Run the narrow UI tests, `$polytail`, and the final gate required by the risk
   class. Report whether any core/profile contract changed and route that change
   through its authoritative skill.
