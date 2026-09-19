# Local user data inventory

Inventory date: 2026-09-20. Owner requested this inventory as input to future
uninstall design. This is observed storage ownership and proposed cleanup
classification, not an implemented uninstaller or permission to delete data.

Default root: `%LOCALAPPDATA%\NvtFwCombiner` for the current Windows user.
Replacing/downloading an EXE does not itself remove these files. Explicit test
or host paths can differ; do not infer cleanup ownership from a filename alone.

| Relative path | Contents / producer | Future uninstall classification |
| --- | --- | --- |
| `preferences.v1.json` | UI preferences; `ShellPreferenceFileStore` via `LocalJsonDocument` | User settings; offer explicit keep/remove choice. |
| `report-history.v1.json` | Persisted run report JSON, metadata and output path references; `ReportHistoryFileStore` | User history; keep by default in proposed design, remove only with explicit data-cleanup choice. Referenced BIN/output paths are not owned cleanup targets. |
| `event-buffer-format.v1.json` | Saved recognition values and aliases; `CompositionHostServices` and `EventBufferFormatConfigurationStorage` | User configuration; preserve across upgrades; optional removal during uninstall. Absence/default behavior is owned by the [configuration contract](../contracts/event-buffer-format-configuration-v1.md). |
| `toolchain-runtime.v1.json` | Saved runtime selection; `CompositionHostServices` and `ToolchainRuntimeConfigurationStorage` | User configuration; optional removal. Never delete a user-selected external runtime directory. |
| `version-manager.v1.json` | Managed installation/version/activation/recovery state; `JsonVersionManagerStateStore` | Installation management state; future uninstall must resolve registered installations and recovery state before removing it. It is not merely a preference file. |
| `.version-manager.v1.json.<24-hex>.writer.lock` | Version-manager write coordination; `FileSystemVersionManagerWriteLease` | Runtime coordination artifact; consider only after all owning processes/leases are stopped. |
| `.<destination filename>.<GUID>.tmp` | Atomic-write scratch beside destinations; local file store and version-manager state store | Crash remnants only; future cleanup must validate exact app-owned targets and absence of live writers. Not a wildcard deletion instruction. |

## Coverage and implementation follow-up

Source inventory searched all `src/` C# uses of `LocalApplicationData`,
`ApplicationData`, `LOCALAPPDATA`, and the `LocalJsonDocument.GetDefaultPath`
callers. The five persistent filenames above cover the current direct default
writers found under this root. Version-manager's root resolver also honors the
`LOCALAPPDATA` environment value; other listed defaults use the platform folder
API. A future uninstaller must use each actual resolved location.

Future uninstall work should consume and update this inventory, present settings
and history cleanup separately from program removal, and inventory managed
installations outside this root separately. Unknown files, other Windows users,
firmware inputs, exported reports, output BINs and externally selected tools are
not implicitly owned by this root. New local-data writers need an inventory
entry as part of that future cleanup design. No current cleanup behavior or
version allocation is changed by this note.
