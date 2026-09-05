# Repository Script Instructions

- `verify.py` is the canonical cross-platform verification entry point; wrappers must not weaken it.
- Scripts use explicit repository-rooted paths, strict error handling and non-zero failure codes.
- `install-dotnet.*` may download only the official `dotnet/install-scripts` installer at the immutable commit pinned in both scripts, and must install the exact stable SDK pinned by `global.json`.
- Verification/release scripts do not download unpinned tools or execute arbitrary commands.
- Packaging starts from an empty staging directory and enforces a closed allowlist.
- Never print secrets, firmware bytes, signing material or private artifact URLs.
- Release/security script behavior changes require focused tests or a deterministic dry-run fixture plus human review before integration/use. Inspection and non-behavioral comments follow the root task-scope rules; preparing an authorized patch does not require repeating an already resolved approval question.
