# Third-Party Notices

NVT FW Combiner restores third-party packages from their official package registries. The stable release pipeline must generate a version-specific notice and SBOM from the resolved lock graph before publication.

Initial direct dependencies include Avalonia, CommunityToolkit.Mvvm, xUnit.net v3, Hatchling, Ruff, Pyright, Pylint, pytest, Hypothesis, coverage tooling, and PyInstaller. Their licenses remain governed by their respective projects; this inventory is not a substitute for release-time license scanning and legal review.

The repository-only `DiffNFMerge` intake includes `CommandLineParser` 2.9.1 (`lib/net45`), copyright Giacomo Stelluti Scala and contributors, licensed under the MIT License. The exact license text is preserved beside the tool package. `DiffNFMerge` is not currently copied into release artifacts.
