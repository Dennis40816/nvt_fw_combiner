# Avalonia Presentation Instructions

- MVVM only; code-behind is limited to view lifecycle and generated initialization.
- ViewModels call application contracts and never mutate firmware, calculate ranges or choose processors.
- Drag canvas and exact mapping table share one state model.
- Preserve Display/TP HW/TP FW access restrictions in rendered controls, while relying on compiler validation as the security boundary.
- Add accessibility, localization and UI smoke coverage for user-visible changes.
