# Presentation instructions

- Do not define or infer firmware facts, offsets, support, evidence, or
  processor rules. Consume typed Application snapshots only.
- Preserve current layout unless the issue explicitly owns visual redesign.
- Reuse shared controls, slot cards, information cards, memory-layout
  projection, and read-only Hex Viewport; do not create workflow copies.
- Keep mode/page state isolated; no global selection cache may leak between
  Merge, Replace, or AB experiences.
- UI changes require localization, keyboard, focus, screen-reader, contrast,
  hover/disabled states, and UI-smoke evidence as applicable.
- Follow the canonical UI specifications for typography, colors, and layout.
- First test: `NvtFwCombiner.UiSmoke.Tests`.
