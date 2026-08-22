---
name: ui-experience-change
description: Design or modify NFC Avalonia screens, approved-reference layouts, ViewModels, localization, accessibility, memory visualization, or UI smoke coverage. Enforce visual fidelity when the owner provides or approves a reference; do not implement firmware semantics in UI code.
---

# UI Experience Change

1. Read `SPEC.md`, ADR 0004, ADR 0005, experience/access policy and Presentation `AGENTS.md`.
2. Identify composition kind, experience, layout policy, inputs, region access and confirmation gates.
3. Obtain all behavior through typed bootstrap/application contracts; never infer IC, region or processor rules from labels/filenames.
4. Keep one mapping state shared by drag canvas and exact table/manual editing; require deterministic round-trip tests.
5. Display address space, source/target ranges, operation, overlap and processor effects before Build.
6. Add keyboard, focus, screen-reader, localization and high-contrast acceptance coverage.
7. Apply the reference-fidelity gate below whenever the owner supplies or approves a visual reference.
8. Add ViewModel/unit tests plus the narrowest UI smoke/snapshot coverage available.
9. Run the narrow UI tests, `$polytail`, and the final gate required by the risk
   class. Report whether any core/profile contract changed and route that change
   through its authoritative skill.

## Reference-fidelity gate

Treat an owner-approved reference as an acceptance contract, not design inspiration.

1. Record the canonical reference path plus its viewport, theme, language and representative data state. Separate legitimate state differences from visual deviations.
2. Before editing, inventory the reference and production render for layout hierarchy, anchored geometry, alignment, spacing, typography, borders, radii, colors and interaction states. Do not silently replace the reference information architecture with a generic component pattern.
3. Create a red-capable regression for the reported deviation. Prefer rendered snapshot or image comparison; otherwise assert measured control bounds and anchors with an explicit tolerance. XAML string searches and token-presence checks alone are not visual-fidelity evidence.
4. Render the production UI at the same viewport, theme, language and data state as the reference. Compare the complete surface, not cropped controls. Light and Dark may use different colors but must retain the same approved geometry unless the owner approves a theme-specific layout.
5. Keep the reference, actual render and comparison evidence together in the handoff. Do not report the UI complete while a material geometry, hierarchy or state mismatch remains.
6. If the reference conflicts with accessibility, localization, supported viewport constraints or an authoritative product contract, stop and present the exact conflict for owner resolution instead of silently changing the design.
