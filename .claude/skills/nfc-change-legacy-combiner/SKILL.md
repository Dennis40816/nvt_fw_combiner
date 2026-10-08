---
name: nfc-change-legacy-combiner
description: Add, rebuild, or replace a pinned legacy Combiner.exe version; use when the owner delivers a new Combiner executable or asks to modify its behavior.
---

# Change The Legacy Combiner

NFC calls the legacy `Combiner.exe` only through the constrained runner described in [`external-combiner-tool-runner.md`](../../../docs/architecture/external-combiner-tool-runner.md). The package rules are in [`external-tools/README.md`](../../../external-tools/README.md). A new executable is a new exact version. Never edit a pinned binary in place.

1. **Pick the case.** The owner delivered a finished executable, or the owner wants a source change and a rebuild. Delivered source and build recipes are private evidence until the owner approves publication. Ask the owner for the toolchain and flags before any rebuild. Keep the delivered source and the build outside the public repository.
2. **Identify the binary.** Record the size and SHA-256 in the private asset repository. Do not trust a version string inside the binary. Use a version string that the owner names, and never parse it as a number.
3. **Get the approvals.** Ask the owner for the execution approval and a trust review of the exact binary, and ask whether it may ship. Until the owner says yes, nobody runs it, and its hash and size stay out of this repository. It is evidence only.
4. **Prove parity.** List the profiles that reference the old and the new `toolBindingId`. Use only the routes of those profiles. Run the old and the new executable with each profile's exact command plan on host-created staging copies of the canonical Golden inputs and the owner samples of those routes, in the test area. Do not run it on Standard Merge, DP Replace, or other cases that bind no Combiner. Compare complete outputs byte by byte. Report each difference range. A difference needs a bound that the owner declares.
5. **Package the version.** Create `external-tools/legacy-combiner/<version>/` with `Combiner.exe` and `manifest.json`. The manifest holds:
   - a unique `toolBindingId` and the exact `toolVersion`;
   - the SHA-256, the adapter id, and the input mode;
   - the argument template, the timeout, and the allowed extra outputs.

   Add the app-local runtime only with a reviewed identity and notice.
6. **Register the version.** Update `external-tools/catalog.json` with size and SHA-256 for every file. Update the packager, the release allowlist, and the independent release smoke so they pin the same bytes. Profiles reference `toolBindingId` only, never an executable path.
7. **Add postbuild data last.** Add or change a postbuild catalog profile only after the postbuild and map evidence is reviewed.
8. **Test and gate.** Add fake-runner tests for staging, argv, and the diff policy. Add real Golden runs when the owner approves the inputs. The executable ships in the release payload, so this is R3. It needs the firmware owner and the release owner.

Do not load a tool from `refcode/`. Do not run the executable on repository files. Do not treat a successful run as support for any IC.
