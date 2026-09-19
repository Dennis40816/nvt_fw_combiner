# Third-Party Notices

## Microsoft Visual C++ Runtime

Application packages from 1.1.8 include the unmodified Microsoft Corporation
`vcruntime140.dll`, x64, version `14.44.35211.0`, copyright Microsoft Corporation.
It is an app-local dependency of the unchanged Combiner tool, not part of this
project's MIT grant. Its 124544-byte SHA-256 is
`d5e4d9a3e835fa679450145d6a7d94e36573a509317111904d9b3712c30d9066`.

The file comes from Visual Studio 2022's `VC/Redist` directory. The
[official redistribution list](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)
permits distribution of listed unmodified files with a program by appropriately
licensed Visual Studio users, subject to the applicable license terms. The
publisher must confirm eligibility before distribution. This notice does not
grant additional rights. [Intake provenance](external-tools/legacy-combiner/README.md#app-local-microsoft-runtime)
records the source and Microsoft signature. Release manifests, hashes and the
SPDX file inventory include this DLL; its file license remains `NOASSERTION`,
not MIT.

## Other dependencies

NVT FW Combiner restores third-party packages from their official package registries. The stable release pipeline must generate a version-specific notice and SBOM from the resolved lock graph before publication.

Initial direct dependencies include Avalonia, CommunityToolkit.Mvvm, xUnit.net v3, Hatchling, Ruff, Pyright, Pylint, pytest, Hypothesis, coverage tooling, and PyInstaller. Their licenses remain governed by their respective projects; this inventory is not a substitute for release-time license scanning and legal review.

The repository-only `DiffNFMerge` intake includes `CommandLineParser` 2.9.1 (`lib/net45`), copyright Giacomo Stelluti Scala and contributors, licensed under the MIT License. The exact license text is preserved beside the tool package. `DiffNFMerge` is not currently copied into release artifacts.

The repository-adapted agent workflows under `.agents/skills/` include material
from [`mattpocock/skills`](https://github.com/mattpocock/skills), pinned to
commit `ed37663cc5fbef691ddfecd080dff42f7e7e350d`. Copyright Matt Pocock and
contributors, licensed under the MIT License. NFC adaptations preserve the
upstream workflow intent while applying this repository's firmware-safety,
authority, invocation, and verification rules. The upstream license text and
source inventory are preserved under
[`third-party/mattpocock-skills/`](third-party/mattpocock-skills/).
