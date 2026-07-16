# Owner Golden Handoff

This is the single owner-evidence drop area. The required files and exact
existing paths are listed in [`TODO.md`](TODO.md).

Rules:

- Use the existing workflow/IC/case folders. Do not create a second `v0.9.9`
  intake tree for a case that already has a handoff folder.
- Do not resend files marked as already tracked in `TODO.md`.
- Incoming payloads are ignored while unreviewed. After hash, byte, provenance,
  privacy, and firmware-owner review, every accepted replay input and expected
  output is committed under the canonical `testdata/golden/<workflow>/`
  fixture with a manifest.
- Keep technical project/product identifiers and original technical filenames.
  Remove personal names, emails, account ids, home/user-profile paths, and
  document author metadata.
- Do not commit licensed tools, credentials, signing material, or unrelated
  archives. Record the tool version and SHA-256 when a tool is not already
  pinned by the repository.
- Evidence intake never promotes runtime support by itself.

## Optional automated intake

For a genuinely new IC/mode with no existing handoff folder, run:

```text
python scripts/intake_ic_reference.py --source <owner-drop-folder> --ic NT51950 --mode ctrlram-replace --case single --owner firmware-owner --source-ref <archive-or-ticket>
```

This generates hashes and next-step notes under an ignored local intake run. It
does not change C# code, profiles, or support status.
