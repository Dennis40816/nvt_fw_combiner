# Presentation instructions

- Render terminal typed Application decisions; extend the upstream owner when
  data is missing instead of adding a Presentation classifier.
- When an owner-approved reference exists, apply the `$ui-experience-change`
  reference-fidelity gate before any visual/layout production edit in XAML,
  styles/resources, View code, or controls. Without a reference, preserve the
  layout unless the approved scope explicitly owns redesign. See the
  `$ui-experience-change`
  [reference-fidelity gate](../../.agents/skills/ui-experience-change/SKILL.md#reference-fidelity-gate).
- Reuse shared controls, slot cards, information cards, memory-layout
  projection, and read-only Hex Viewport; do not create workflow copies.
- Keep mode/page state isolated; no global selection cache may leak between
  Merge, Replace, or AB experiences.
- UI changes require localization, keyboard, focus, screen-reader, contrast,
  hover/disabled states, and UI-smoke evidence as applicable.
