# Avalonia Presentation Instructions

- MVVM only; code-behind is limited to view lifecycle and generated initialization.
- ViewModels call application contracts and never mutate firmware, calculate ranges or choose processors.
- Drag canvas and exact mapping table share one state model.
- Preserve DP/CtrlRAM/General Replace access restrictions in rendered controls, while relying on compiler validation as the security boundary.
- Add accessibility, localization and UI smoke coverage for user-visible changes.

## Control consistency

- Treat `Styles/MainWindowControlStyles.axaml` as the canonical library for shared surface, text, badge, mapping-index, footer and read-only raw-text roles. Use its semantic class for every new or modified instance of an existing role; do not duplicate its visual property set inline.
- A `Border` remains valid for layout, a shape, a timeline node or a data-bound dynamic surface. For a text-bearing status, count or outcome badge, use `Label.reportBadge` (or `Label.slotBadge` for slot state) so content alignment, minimum height and accessibility semantics do not drift.
- Raw payloads, JSON and technical evidence that users may inspect or copy use `TextBox.readOnlyRaw`. It is read-only, multiline and has explicit horizontal and vertical scrollbars; do not wrap a `TextBlock` in an ad-hoc `ScrollViewer` for this role.
- Direct visual attributes are limited to one-off, data-bound, or geometric requirements. Repeated colors, typography, border and padding bundles must be promoted to the shared control style first. Hex/mapping grid geometry may stay explicit, but must use the shared text/index classes when its role matches one.
- When a shared role changes, update the XAML style-contract smoke coverage in the same change. Do not introduce a C# wrapper for a purely visual token; add a View only when it owns behavior, lifecycle or a stable data contract.
- Shell bars, roomy panels, content panels, list rows, settings rows, headings, helper text, status text and technical values have shared roles in `MainWindowControlStyles.axaml`. Check that library before adding a visual property bundle; a new XAML template must compose those roles rather than reproduce their setters inline.
