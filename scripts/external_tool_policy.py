"""Closed repository and release inventories for external tool payloads."""

from pathlib import PurePosixPath


ALLOWED_EXTERNAL_TOOL_BINARY_PAYLOADS = {
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/CommandLine.dll"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/Combiner.exe"),
}
APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS = {
    PurePosixPath("external-tools/README.md"),
    PurePosixPath("external-tools/legacy-combiner/README.md"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/Combiner.exe"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/manifest.json"),
}
APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS = APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS | {
    PurePosixPath("external-tools/diff-nf-merge/README.md"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/CommandLine.dll"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe.config"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/LICENSE.CommandLineParser.md"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/package-manifest.json"),
}
