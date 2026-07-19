# External Tools

`external-tools/` contains owner-approved, hash-pinned tool packages. `catalog.json`
is the closed repository/package identity inventory: it records each tool's source,
runtime status, release status, package path, command authority, and read/write
authority. A package is copied into release artifacts only when both the catalog and
the release allowlist explicitly include every file; repository intake alone does not
promote runtime or release support.

Current runtime and release package:

- generated `crc-worker/0.1.0/Nfc.CrcWorker.exe` release payload (built from `tools/crc-worker/`; not stored as a repository binary)
- `legacy-combiner/1.13.0/Combiner.exe`

Repository-only intake:

- `diff-nf-merge/1.0.0/`: owner-supplied cascade NF compiler package. It is not registered as a processor and is not copied into release artifacts. See its package manifest and README for the remaining evidence and safety gates.

Package-contract-only generated payload:

- `crc-worker/0.1.0/Nfc.CrcWorker.exe` is generated from `tools/crc-worker/`
  during packaging. It implements the Protocol 1.0 calculation contract, has no
  filesystem write authority, is not stored as a repository binary, and is not a
  production firmware transform route.

The catalog itself is repository governance evidence and is not copied into the
release package. The packager and repository validator must agree with its exact
`releasePackagePaths`; every shipped executable remains below `external-tools/`.

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
