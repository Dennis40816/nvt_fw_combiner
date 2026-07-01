# External Tools

`external-tools/` contains owner-approved runtime tool packages. These files may be copied into release artifacts when the manifest and SHA-256 match.

Current package:

- `legacy-combiner/1.13.0/Combiner.exe`

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
