# External Tools

`external-tools/` contains owner-approved, hash-pinned tool packages. A package is copied into release artifacts only when the release allowlist explicitly includes every file; repository intake alone does not promote runtime or release support.

Current runtime and release package:

- `legacy-combiner/1.13.0/Combiner.exe`

Repository-only intake:

- `diff-nf-merge/1.0.0/`: owner-supplied cascade NF compiler package. It is not registered as a processor and is not copied into release artifacts. See its package manifest and README for the remaining evidence and safety gates.

## Adding A Combiner Version

1. Create `external-tools/legacy-combiner/<version>/`.
2. Copy the exact `Combiner.exe` for that version.
3. Compute SHA-256:

   ```powershell
   Get-FileHash -Algorithm SHA256 external-tools\legacy-combiner\<version>\Combiner.exe
   ```

4. Add `manifest.json` with a unique `toolBindingId`, exact string `toolVersion`, executable name, SHA-256, adapter id, input mode, timeout, and allowed outputs.
5. Add or update the matching `LegacyCombinerPostbuildCatalog` profile only after postbuild/mmap evidence is reviewed.
6. Add fake-runner tests for staging/argv/diff policy, and add real golden evidence when owner-approved firmware inputs are available.

Do not load tools from `refcode/`. Reference copies are evidence only.
