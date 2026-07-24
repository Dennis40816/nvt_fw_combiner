# Third-Party Notices

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
