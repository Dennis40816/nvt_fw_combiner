"""Behavioral tests for stable release identity and immutable candidate policy."""

from __future__ import annotations

import importlib.util
import hashlib
import json
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "release_promotion_policy.py"
SPEC = importlib.util.spec_from_file_location("release_promotion_policy", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
SHA = "1" * 40
TREE = "2" * 40
TAG_OBJECT_SHA = "3" * 40
TAG_MESSAGE = "NVT FW Combiner v0.9.14\ncandidate-run: 99"


def valid_snapshot() -> dict[str, object]:
    return {
        "number": 214,
        "state": "MERGED",
        "mergedAt": "2026-07-22T01:00:00Z",
        "baseRefName": "main",
        "mergeCommitSha": SHA,
        "headSha": "4" * 40,
        "headTree": TREE,
        "reviewDecision": "APPROVED",
        "approvals": [
            {
                "reviewer": "independent-reviewer",
                "commitSha": "4" * 40,
                "submittedAt": "2026-07-22T00:55:00Z",
            }
        ],
        "ownerSelfApprovalException": False,
        "requiredChecks": [{"name": "dotnet / build-test", "bucket": "pass"}],
    }


class ReleasePromotionPolicyTests(unittest.TestCase):
    @staticmethod
    def git(repository: Path, *arguments: str) -> str:
        result = subprocess.run(
            ["git", "-C", str(repository), *arguments],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        return result.stdout.strip()

    def test_version_and_launcher_asset_name_sets_remain_separate_and_closed(self) -> None:
        version = "1.0.6"
        self.assertEqual(
            (
                "NvtFwCombiner-v1.0.6-win-x64.zip",
                "NvtFwCombiner-v1.0.6-win-x64.spdx.json",
                "NvtFwCombiner-v1.0.6-win-x64.provenance.json",
            ),
            MODULE._asset_names(version),
        )
        self.assertEqual(
            (
                "NvtFwCombiner-Launcher-v1.0.6-win-x64.exe",
                "NvtFwCombiner-Launcher-v1.0.6-win-x64.manifest.json",
                "NvtFwCombiner-Launcher-v1.0.6-win-x64.spdx.json",
                "NvtFwCombiner-Launcher-v1.0.6-win-x64.intoto.jsonl",
                "NvtFwCombiner-Launcher-v1.0.6-win-x64.sha256",
            ),
            MODULE._installer_asset_names(version),
        )
        self.assertEqual(
            (*MODULE._asset_names(version), *MODULE._installer_asset_names(version)),
            MODULE._candidate_asset_names(version),
        )
        for maintenance_version in (
            "0.9.17",
            "0.9.18",
            "0.9.19",
            "1.0.0",
            "1.0.5",
        ):
            with self.subTest(maintenance_version=maintenance_version):
                self.assertEqual(
                    MODULE._asset_names(maintenance_version),
                    MODULE._candidate_asset_names(maintenance_version),
                )

    def create_version_only_repository(
        self,
        root: Path,
        *,
        base_version: str = "1.0.0",
        extra_path: bool = False,
        rename_path: bool = False,
        mode_change: bool = False,
    ) -> None:
        self.git(root, "init", "--initial-branch=main")
        self.git(root, "config", "user.name", "Release Test")
        self.git(root, "config", "user.email", "release-test@example.invalid")
        (root / "VERSION").write_text(f"{base_version}\n", encoding="utf-8")
        (root / "note.txt").write_text("stable\n", encoding="utf-8")
        self.git(root, "add", "VERSION", "note.txt")
        self.git(root, "commit", "-m", "release 1.0.0")
        self.git(root, "tag", "-a", "v1.0.0", "-m", "NVT FW Combiner v1.0.0")
        (root / "VERSION").write_text("1.0.1\n", encoding="utf-8")
        if extra_path:
            (root / "extra.txt").write_text("not version-only\n", encoding="utf-8")
            self.git(root, "add", "extra.txt")
        if rename_path:
            self.git(root, "mv", "note.txt", "renamed.txt")
        self.git(root, "add", "VERSION")
        if mode_change:
            self.git(root, "update-index", "--chmod=+x", "VERSION")
        self.git(root, "commit", "-m", "release 1.0.1 version only")

    @staticmethod
    def write_version_package(
        path: Path,
        *,
        version: str,
        source_sha: str,
        stable_payload: bytes = b"stable-payload",
        extra_path: bool = False,
        product: str = "NVT FW Combiner",
        package_root: str = "",
    ) -> None:
        payloads = {
            "NvtFwCombiner.exe": f"application-{version}".encode(),
            "launcher/NvtFwCombiner.Launcher.exe": f"launcher-{version}".encode(),
            "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe": stable_payload,
            "README.txt": f"NVT FW Combiner {version}\nstable instructions\n".encode(),
            "LICENSE.txt": stable_payload,
        }
        files = [
            {
                "path": name,
                "size": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
                "role": "launcher"
                if name.startswith("launcher/")
                else "application"
                if name.endswith("Combiner.exe")
                else "document",
            }
            for name, content in payloads.items()
        ]
        manifest = {
            "schemaVersion": "1.2",
            "product": product,
            "version": version,
            "sourceCommit": source_sha,
            "sourceTag": f"v{version}",
            "provenanceAsset": f"NvtFwCombiner-v{version}-win-x64.provenance.json",
            "sbomAsset": f"NvtFwCombiner-v{version}-win-x64.spdx.json",
            "runtimeIdentifier": "win-x64",
            "files": files,
            "launcher": {
                "launcherVersion": version,
                "protocolVersion": 1,
                "executableRelativePath": "launcher/NvtFwCombiner.Launcher.exe",
                "size": len(payloads["launcher/NvtFwCombiner.Launcher.exe"]),
                "sha256": hashlib.sha256(
                    payloads["launcher/NvtFwCombiner.Launcher.exe"]
                ).hexdigest(),
            },
        }
        manifest_bytes = (json.dumps(manifest, indent=2) + "\n").encode()
        payloads["RELEASE-MANIFEST.json"] = manifest_bytes
        payloads["SHA256SUMS.txt"] = "".join(
            f"{hashlib.sha256(content).hexdigest()}  {name}\n"
            for name, content in payloads.items()
        ).encode()
        if extra_path:
            payloads["unexpected.txt"] = b"extra"
        with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for name, content in payloads.items():
                archive.writestr(f"{package_root}{name}", content)

    @staticmethod
    def candidate_arguments() -> dict[str, object]:
        return {
            "requested_sha": SHA,
            "workflow_sha": SHA,
            "workflow_ref": "refs/heads/main",
            "source_sha": SHA,
            "source_branch": "main",
            "source_version": "0.10.0",
            "main_sha": SHA,
            "source_tree": TREE,
            "repository_owner": "release-owner",
            "workflow_actor": "release-owner",
            "owner_self_approval_exception": False,
        }

    @staticmethod
    def version_only_snapshot() -> dict[str, object]:
        return {
            "sourceVersion": "1.0.1",
            "sourceSha": SHA,
            "baseTag": "v1.0.0",
            "baseTagCommit": "5" * 40,
            "baseVersion": "1.0.0",
            "parentShas": ["5" * 40],
            "changedPaths": ["VERSION"],
            "modeChanges": [],
        }

    def test_101_accepts_only_direct_version_file_content_change(self) -> None:
        MODULE.validate_version_only_upgrade(self.version_only_snapshot())

    def test_101_rejects_identity_lineage_and_diff_drift(self) -> None:
        cases = (
            ("sourceVersion", "1.0.2", "source VERSION"),
            ("sourceSha", "invalid", "source SHA"),
            ("baseTag", "v0.10.7", "base tag"),
            ("baseTagCommit", "invalid", "peeled commit SHA"),
            ("baseVersion", "0.10.7", "VERSION 1.0.0"),
            ("parentShas", [], "direct single-parent child"),
            ("parentShas", ["5" * 40, "6" * 40], "direct single-parent child"),
            ("parentShas", ["6" * 40], "direct single-parent child"),
            ("changedPaths", [], "canonical VERSION"),
            ("changedPaths", ["VERSION", "README.md"], "canonical VERSION"),
            ("changedPaths", ["RENAMED_VERSION"], "canonical VERSION"),
            ("modeChanges", ["mode change 100644 => 100755 VERSION"], "file modes"),
        )
        for key, value, message in cases:
            with self.subTest(key=key, value=value):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_version_only_upgrade(
                        {**self.version_only_snapshot(), key: value}
                    )

    def test_101_behavioral_git_lineage_accepts_only_version_file(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-lineage-"
        ) as temporary:
            repository = Path(temporary)
            self.create_version_only_repository(repository)

            MODULE.validate_version_only_lineage(repository)

    def test_101_behavioral_git_lineage_rejects_tag_and_diff_drift(self) -> None:
        scenarios = (
            ("wrong-base-version", {"base_version": "0.10.7"}, None),
            ("extra-path", {"extra_path": True}, None),
            ("renamed-path", {"rename_path": True}, None),
            ("mode-change", {"mode_change": True}, None),
            ("missing-tag", {}, "missing-tag"),
            ("lightweight-tag", {}, "lightweight-tag"),
            ("retargeted-tag", {}, "retargeted-tag"),
            ("non-direct-child", {}, "non-direct-child"),
        )
        for name, options, mutation in scenarios:
            with (
                self.subTest(name=name),
                tempfile.TemporaryDirectory(
                    prefix=f"nfc-version-only-{name}-"
                ) as temporary,
            ):
                repository = Path(temporary)
                self.create_version_only_repository(repository, **options)
                if mutation == "missing-tag":
                    self.git(repository, "tag", "-d", "v1.0.0")
                elif mutation == "lightweight-tag":
                    base = self.git(repository, "rev-parse", "HEAD^")
                    self.git(repository, "tag", "-d", "v1.0.0")
                    self.git(repository, "tag", "v1.0.0", base)
                elif mutation == "retargeted-tag":
                    self.git(repository, "tag", "-d", "v1.0.0")
                    self.git(
                        repository,
                        "tag",
                        "-a",
                        "v1.0.0",
                        "-m",
                        "retargeted",
                        "HEAD",
                    )
                elif mutation == "non-direct-child":
                    self.git(repository, "commit", "--allow-empty", "-m", "extra child")

                with self.assertRaises(ValueError):
                    MODULE.validate_version_only_lineage(repository)

    def test_101_behavioral_git_lineage_rejects_merge_parent(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nfc-version-only-merge-") as temporary:
            repository = Path(temporary)
            self.create_version_only_repository(repository)
            base = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            self.git(repository, "branch", "side", base)
            self.git(repository, "checkout", "side")
            (repository / "side.txt").write_text("side\n", encoding="utf-8")
            self.git(repository, "add", "side.txt")
            self.git(repository, "commit", "-m", "side")
            self.git(repository, "checkout", "main")
            self.git(repository, "merge", "--no-ff", "side", "-m", "merge")

            with self.assertRaisesRegex(ValueError, "single-parent"):
                MODULE.validate_version_only_lineage(repository)

    def test_101_package_equivalence_allows_declared_version_identity_only(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-package-test-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            candidate_sha = self.git(repository, "rev-parse", "HEAD")
            base_package = Path(temporary) / "base.zip"
            candidate_package = Path(temporary) / "candidate.zip"
            self.write_version_package(
                base_package,
                version="1.0.0",
                source_sha=base_sha,
            )
            self.write_version_package(
                candidate_package,
                version="1.0.1",
                source_sha=candidate_sha,
            )

            def version_reader(path: Path) -> tuple[str, str]:
                version = "1.0.1" if b"1.0.1" in path.read_bytes() else "1.0.0"
                return f"{version}.0", version

            with mock.patch.object(
                MODULE, "_read_windows_file_version", version_reader
            ):
                MODULE.validate_version_only_packages(
                    repository,
                    base_package,
                    candidate_package,
                    MODULE._sha256(base_package),
                )

    def test_101_package_equivalence_normalizes_versioned_archive_roots(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-root-test-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            candidate_sha = self.git(repository, "rev-parse", "HEAD")
            base_package = Path(temporary) / "base.zip"
            candidate_package = Path(temporary) / "candidate.zip"
            self.write_version_package(
                base_package,
                version="1.0.0",
                source_sha=base_sha,
                package_root="NvtFwCombiner-v1.0.0-win-x64/",
            )
            self.write_version_package(
                candidate_package,
                version="1.0.1",
                source_sha=candidate_sha,
                package_root="NvtFwCombiner-v1.0.1-win-x64/",
            )

            def version_reader(path: Path) -> tuple[str, str]:
                version = "1.0.1" if b"1.0.1" in path.read_bytes() else "1.0.0"
                return f"{version}.0", version

            with mock.patch.object(
                MODULE, "_read_windows_file_version", version_reader
            ):
                MODULE.validate_version_only_packages(
                    repository,
                    base_package,
                    candidate_package,
                    MODULE._sha256(base_package),
                )

    def test_101_reuses_only_manifest_bound_stable_worker(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-worker-test-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            base_package = Path(temporary) / "base.zip"
            destination = Path(temporary) / "out" / "Nfc.CrcWorker.exe"
            self.write_version_package(
                base_package,
                version="1.0.0",
                source_sha=base_sha,
                package_root="NvtFwCombiner-v1.0.0-win-x64/",
            )

            MODULE.extract_version_only_stable_payload(
                repository,
                base_package,
                destination,
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                MODULE._sha256(base_package),
            )

            self.assertEqual(destination.read_bytes(), b"stable-payload")

    def test_101_package_validation_uses_the_single_captured_base_zip(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nfc-version-only-capture-") as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            candidate_sha = self.git(repository, "rev-parse", "HEAD")
            base_package = Path(temporary) / "base.zip"
            candidate_package = Path(temporary) / "candidate.zip"
            self.write_version_package(base_package, version="1.0.0", source_sha=base_sha)
            self.write_version_package(candidate_package, version="1.0.1", source_sha=candidate_sha)
            expected_sha = MODULE._sha256(base_package)
            original_read_bytes = Path.read_bytes
            swapped = False

            def read_then_swap(path: Path) -> bytes:
                nonlocal swapped
                payload = original_read_bytes(path)
                if path.resolve() == base_package.resolve() and not swapped:
                    swapped = True
                    path.write_bytes(b"post-capture-counterfeit")
                return payload

            def version_reader(path: Path) -> tuple[str, str]:
                version = "1.0.1" if b"1.0.1" in original_read_bytes(path) else "1.0.0"
                return f"{version}.0", version

            with (
                mock.patch.object(Path, "read_bytes", read_then_swap),
                mock.patch.object(MODULE, "_read_windows_file_version", version_reader),
            ):
                MODULE.validate_version_only_packages(
                    repository, base_package, candidate_package, expected_sha
                )
            self.assertTrue(swapped)
            self.assertEqual(b"post-capture-counterfeit", base_package.read_bytes())

    def test_101_stable_payload_uses_the_single_captured_base_zip(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nfc-version-only-reuse-capture-") as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            base_package = Path(temporary) / "base.zip"
            destination = Path(temporary) / "out/Nfc.CrcWorker.exe"
            self.write_version_package(base_package, version="1.0.0", source_sha=base_sha)
            expected_sha = MODULE._sha256(base_package)
            original_read_bytes = Path.read_bytes
            swapped = False

            def read_then_swap(path: Path) -> bytes:
                nonlocal swapped
                payload = original_read_bytes(path)
                if path.resolve() == base_package.resolve() and not swapped:
                    swapped = True
                    path.write_bytes(b"post-capture-counterfeit")
                return payload

            with mock.patch.object(Path, "read_bytes", read_then_swap):
                MODULE.extract_version_only_stable_payload(
                    repository,
                    base_package,
                    destination,
                    "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                    expected_sha,
                )
            self.assertTrue(swapped)
            self.assertEqual(b"stable-payload", destination.read_bytes())

    def test_101_rejects_self_consistent_base_zip_with_wrong_external_digest(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-worker-digest-reject-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            base_package = Path(temporary) / "counterfeit.zip"
            self.write_version_package(
                base_package,
                version="1.0.0",
                source_sha=base_sha,
            )

            with self.assertRaisesRegex(ValueError, "independently supplied"):
                MODULE.extract_version_only_stable_payload(
                    repository,
                    base_package,
                    Path(temporary) / "out" / "Nfc.CrcWorker.exe",
                    "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                    "0" * 64,
                )

    def test_101_reuse_rejects_unapproved_payload(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-worker-reject-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            base_package = Path(temporary) / "base.zip"
            self.write_version_package(
                base_package,
                version="1.0.0",
                source_sha=base_sha,
            )

            with self.assertRaisesRegex(ValueError, "not approved"):
                MODULE.extract_version_only_stable_payload(
                    repository,
                    base_package,
                    Path(temporary) / "out.bin",
                    "LICENSE.txt",
                    MODULE._sha256(base_package),
                )

    def test_101_package_equivalence_rejects_inventory_and_stable_payload_drift(
        self,
    ) -> None:
        cases = (
            ("inventory", {"extra_path": True}),
            ("stable-payload", {"stable_payload": b"changed"}),
            ("manifest", {"product": "Different Product"}),
        )
        for name, options in cases:
            with (
                self.subTest(name=name),
                tempfile.TemporaryDirectory(
                    prefix=f"nfc-version-only-package-{name}-"
                ) as temporary,
            ):
                repository = Path(temporary) / "repository"
                repository.mkdir()
                self.create_version_only_repository(repository)
                base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
                candidate_sha = self.git(repository, "rev-parse", "HEAD")
                base_package = Path(temporary) / "base.zip"
                candidate_package = Path(temporary) / "candidate.zip"
                self.write_version_package(
                    base_package,
                    version="1.0.0",
                    source_sha=base_sha,
                )
                self.write_version_package(
                    candidate_package,
                    version="1.0.1",
                    source_sha=candidate_sha,
                    **options,
                )

                with (
                    mock.patch.object(
                        MODULE,
                        "_read_windows_file_version",
                        return_value=("1.0.1.0", "1.0.1"),
                    ),
                    self.assertRaises(ValueError),
                ):
                    MODULE.validate_version_only_packages(
                        repository,
                        base_package,
                        candidate_package,
                        MODULE._sha256(base_package),
                    )

    def test_accepts_only_exact_reviewed_main_identity(self) -> None:
        MODULE.validate_candidate_context(
            valid_snapshot(),
            **self.candidate_arguments(),
        )

    def test_accepts_exact_reviewed_0917_maintenance_identity(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.17",
                "source_version": "0.9.17",
            },
        )

    def test_accepts_exact_reviewed_0918_maintenance_identity(self) -> None:
        maintenance_sha = "6" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.18",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.18",
                "source_version": "0.9.18",
            },
        )

    def test_accepts_exact_reviewed_0919_maintenance_identity(self) -> None:
        maintenance_sha = "7" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.19",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.19",
                "source_version": "0.9.19",
            },
        )

    def test_rejects_unapproved_or_mismatched_maintenance_source(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }
        common = {
            **self.candidate_arguments(),
            "requested_sha": maintenance_sha,
            "source_sha": maintenance_sha,
        }
        for source_branch, source_version in (
            ("0.9.16", "0.9.16"),
            ("0.9.17", "0.9.18"),
            ("0.9.18", "0.9.17"),
            ("0.9.19", "0.9.18"),
        ):
            with self.subTest(
                source_branch=source_branch, source_version=source_version
            ):
                with self.assertRaisesRegex(ValueError, "approved maintenance"):
                    MODULE.validate_candidate_context(
                        snapshot,
                        **{
                            **common,
                            "source_branch": source_branch,
                            "source_version": source_version,
                        },
                    )

    def test_rejects_release_source_sha_or_pr_base_drift(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }
        arguments = {
            **self.candidate_arguments(),
            "requested_sha": maintenance_sha,
            "source_sha": maintenance_sha,
            "source_branch": "0.9.17",
            "source_version": "0.9.17",
        }

        with self.assertRaisesRegex(ValueError, "source SHAs must be identical"):
            MODULE.validate_candidate_context(
                snapshot,
                **{**arguments, "requested_sha": "6" * 40},
            )
        with self.assertRaisesRegex(ValueError, "selected release source branch"):
            MODULE.validate_candidate_context(
                {**snapshot, "baseRefName": "main"},
                **arguments,
            )
        with self.assertRaisesRegex(ValueError, "current protected main"):
            MODULE.validate_candidate_context(
                valid_snapshot(),
                **{
                    **self.candidate_arguments(),
                    "requested_sha": "6" * 40,
                    "source_sha": "6" * 40,
                },
            )

    def test_rejects_tree_review_and_required_check_drift(self) -> None:
        mutations = (
            ("headTree", "3" * 40, "tree differs"),
            ("reviewDecision", "CHANGES_REQUESTED", "not approved"),
            ("approvals", [], "no current-head approval"),
            (
                "approvals",
                [{"reviewer": "stale", "commitSha": "5" * 40}],
                "stale, malformed, or authored by Codex",
            ),
            ("requiredChecks", [{"name": "dotnet", "bucket": "fail"}], "not passing"),
        )
        for key, value, message in mutations:
            with self.subTest(key=key):
                snapshot = valid_snapshot()
                snapshot[key] = value
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        snapshot,
                        **self.candidate_arguments(),
                    )

    def test_rejects_non_main_workflow_or_stale_sha(self) -> None:
        for workflow_ref, workflow_sha, message in (
            ("refs/heads/feature", SHA, "dispatched from main"),
            ("refs/heads/main", "3" * 40, "current protected main"),
        ):
            with self.subTest(workflow_ref=workflow_ref, workflow_sha=workflow_sha):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        valid_snapshot(),
                        **{
                            **self.candidate_arguments(),
                            "workflow_sha": workflow_sha,
                            "workflow_ref": workflow_ref,
                        },
                    )

    def test_owner_self_approval_exception_is_explicit_and_owner_bound(self) -> None:
        snapshot = valid_snapshot()
        snapshot["reviewDecision"] = ""
        snapshot["approvals"] = []
        snapshot["authorLogin"] = "release-owner"
        snapshot["repositoryOwner"] = "release-owner"
        snapshot["workflowActor"] = "release-owner"
        snapshot["ownerSelfApprovalException"] = True
        snapshot["codexReview"] = {
            "source": "pull-review",
            "reviewer": f"{MODULE.CODEX_REVIEWER}[bot]",
            "commitSha": "4" * 40,
            "state": "COMMENTED",
            "submittedAt": "2026-07-22T00:59:00Z",
        }
        arguments = {
            **self.candidate_arguments(),
            "owner_self_approval_exception": True,
            "source_version": "1.0.8",
        }

        MODULE.validate_candidate_context(snapshot, **arguments)

        snapshot["reviewDecision"] = "REVIEW_REQUIRED"
        MODULE.validate_candidate_context(snapshot, **arguments)
        snapshot["reviewDecision"] = ""

        for key, value, message in (
            ("owner_self_approval_exception", False, "no current-head approval"),
            ("workflow_actor", "other-user", "must be dispatched"),
        ):
            with self.subTest(key=key):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        snapshot, **{**arguments, key: value}
                    )

        for key, value, message in (
            ("authorLogin", "other-user", "author the PR"),
            ("workflowActor", "other-user", "identity differs"),
            ("ownerSelfApprovalException", False, "not recorded"),
            ("reviewDecision", "CHANGES_REQUESTED", "not approved"),
            ("baseRefName", "other", "target the selected release source branch"),
            ("mergeCommitSha", "5" * 40, "merge commit is not the candidate"),
            ("headTree", "f" * 40, "tree differs from the candidate tree"),
            ("requiredChecks", [], "no required checks"),
            (
                "requiredChecks",
                [{"name": "dotnet / build-test", "bucket": "fail"}],
                "required checks are not passing",
            ),
        ):
            with self.subTest(key=key):
                mutated = {**snapshot, key: value}
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(mutated, **arguments)

        for overrides, message in (
            ({"requested_sha": "5" * 40}, "requested and checkout source SHAs"),
            (
                {"requested_sha": "5" * 40, "source_sha": "5" * 40},
                "main release source must be the current protected main SHA",
            ),
            ({"workflow_sha": "5" * 40}, "current protected main SHA"),
            (
                {"workflow_sha": "5" * 40, "main_sha": "5" * 40},
                "main release source must be the current protected main SHA",
            ),
        ):
            with self.subTest(overrides=overrides):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        snapshot, **{**arguments, **overrides}
                    )

        required_arguments = {**arguments, "source_version": "1.2.0"}
        MODULE.validate_candidate_context(snapshot, **required_arguments)

        for key, value, message in (
            ("codexReview", None, "no Codex review evidence"),
            (
                "codexReview",
                {**snapshot["codexReview"], "commitSha": "5" * 40},
                "Codex review is stale",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "state": "PENDING"},
                "Codex review is incomplete",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "source": "unknown"},
                "Codex review source is invalid",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "reviewer": "other-reviewer"},
                "Codex reviewer is invalid",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "submittedAt": ""},
                "Codex review has no submission time",
            ),
        ):
            with self.subTest(key=key):
                mutated = {**snapshot, key: value}
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(mutated, **required_arguments)

        issue_comment = {
            **snapshot["codexReview"],
            "source": "issue-comment",
            "reviewedCommitPrefix": ("4" * 40)[:10],
        }
        MODULE.validate_candidate_context(
            {**snapshot, "codexReview": issue_comment}, **required_arguments
        )
        with self.assertRaisesRegex(ValueError, "issue comment is stale"):
            MODULE.validate_candidate_context(
                {
                    **snapshot,
                    "codexReview": {
                        **issue_comment,
                        "reviewedCommitPrefix": "5" * 10,
                    },
                },
                **required_arguments,
            )

    def test_owner_codex_review_deferral_is_exactly_version_bounded(self) -> None:
        snapshot = valid_snapshot()
        snapshot["reviewDecision"] = ""
        snapshot["approvals"] = []
        snapshot["authorLogin"] = "release-owner"
        snapshot["repositoryOwner"] = "release-owner"
        snapshot["workflowActor"] = "release-owner"
        snapshot["ownerSelfApprovalException"] = True
        snapshot["codexReview"] = None
        arguments = {
            **self.candidate_arguments(),
            "owner_self_approval_exception": True,
        }

        for source_version in ("1.0.8", "1.0.9", "1.1.0", "1.1.999"):
            with self.subTest(source_version=source_version, expected="accepted"):
                MODULE.validate_candidate_context(
                    snapshot, **{**arguments, "source_version": source_version}
                )

        for source_branch, source_version in (
            ("0.9.19", "0.9.19"),
            ("main", "1.0.7"),
            ("main", "1.2.0"),
            ("main", "1.2.1"),
            ("main", "2.0.0"),
        ):
            with self.subTest(source_version=source_version, expected="rejected"):
                candidate_snapshot = {**snapshot, "baseRefName": source_branch}
                with self.assertRaisesRegex(ValueError, "no Codex review evidence"):
                    MODULE.validate_candidate_context(
                        candidate_snapshot,
                        **{
                            **arguments,
                            "source_branch": source_branch,
                            "source_version": source_version,
                        },
                    )

    def test_rejects_codex_as_an_ordinary_pr_approval(self) -> None:
        snapshot = valid_snapshot()
        snapshot["approvals"] = [
            {
                "reviewer": f"{MODULE.CODEX_REVIEWER}[bot]",
                "commitSha": "4" * 40,
                "submittedAt": "2026-07-22T00:55:00Z",
            }
        ]

        with self.assertRaisesRegex(ValueError, "authored by Codex"):
            MODULE.validate_candidate_context(snapshot, **self.candidate_arguments())

    def test_rejects_owner_exception_on_an_approved_pr(self) -> None:
        with self.assertRaisesRegex(ValueError, "must be disabled"):
            MODULE.validate_candidate_context(
                {**valid_snapshot(), "ownerSelfApprovalException": True},
                **{
                    **self.candidate_arguments(),
                    "owner_self_approval_exception": True,
                },
            )

    def test_fresh_promotion_requires_current_main_but_recovery_allows_advance(
        self,
    ) -> None:
        common = {
            "source_sha": SHA,
            "source_tree": TREE,
            "checkout_sha": SHA,
            "checkout_tree": TREE,
            "source_branch": "main",
            "source_version": "0.10.0",
            "source_branch_sha": SHA,
            "workflow_sha": SHA,
        }
        MODULE.validate_promotion_source_state(
            **common,
            tag_state="absent",
            main_sha=SHA,
            source_is_branch_ancestor=True,
        )
        with self.assertRaisesRegex(ValueError, "workflow authority"):
            MODULE.validate_promotion_source_state(
                **common,
                tag_state="absent",
                main_sha="4" * 40,
                source_is_branch_ancestor=True,
            )
        MODULE.validate_promotion_source_state(
            **common,
            tag_state="present",
            main_sha=SHA,
            source_is_branch_ancestor=True,
        )
        with self.assertRaisesRegex(ValueError, "release branch"):
            MODULE.validate_promotion_source_state(
                **common,
                tag_state="present",
                main_sha=SHA,
                source_is_branch_ancestor=False,
            )

    def test_maintenance_promotion_requires_exact_current_branch_head(self) -> None:
        maintenance_sha = "5" * 40
        common = {
            "source_sha": maintenance_sha,
            "source_tree": TREE,
            "checkout_sha": maintenance_sha,
            "checkout_tree": TREE,
            "source_branch": "0.9.19",
            "source_version": "0.9.19",
            "source_branch_sha": maintenance_sha,
            "workflow_sha": SHA,
            "main_sha": SHA,
            "source_is_branch_ancestor": True,
        }
        MODULE.validate_promotion_source_state(**common, tag_state="absent")
        with self.assertRaisesRegex(ValueError, "current release branch head"):
            MODULE.validate_promotion_source_state(
                **{**common, "source_branch_sha": "6" * 40},
                tag_state="absent",
            )

    def test_existing_tag_must_be_annotated_exact_and_candidate_bound(self) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
            "message": TAG_MESSAGE,
        }
        MODULE.validate_existing_tag(
            tag_ref,
            tag_object,
            expected_tag="v0.9.14",
            source_sha=SHA,
            expected_message=TAG_MESSAGE,
        )
        mutations = (
            ({"object": {"type": "commit", "sha": SHA}}, tag_object, "not annotated"),
            (tag_ref, {**tag_object, "sha": "4" * 40}, "different tag object"),
            (
                tag_ref,
                {**tag_object, "object": {"type": "commit", "sha": "4" * 40}},
                "target differs",
            ),
            (tag_ref, {**tag_object, "message": "conflict"}, "message differs"),
        )
        for ref, annotated, message in mutations:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_tag(
                        ref,
                        annotated,
                        expected_tag="v0.9.14",
                        source_sha=SHA,
                        expected_message=TAG_MESSAGE,
                    )

    def test_existing_tag_accepts_crlf_transport_but_rejects_logical_drift(
        self,
    ) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
            "message": TAG_MESSAGE.replace("\n", "\r\n"),
        }

        MODULE.validate_existing_tag(
            tag_ref,
            tag_object,
            expected_tag="v0.9.14",
            source_sha=SHA,
            expected_message=TAG_MESSAGE,
        )

        with self.assertRaisesRegex(ValueError, "message differs"):
            MODULE.validate_existing_tag(
                tag_ref,
                {
                    **tag_object,
                    "message": "NVT FW Combiner v0.9.14\r\ncandidate-run: 100",
                },
                expected_tag="v0.9.14",
                source_sha=SHA,
                expected_message=TAG_MESSAGE,
            )

    def test_existing_tag_rejects_every_non_transport_message_difference(self) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
        }
        mutations = (
            " " + TAG_MESSAGE,
            TAG_MESSAGE + " ",
            TAG_MESSAGE + "\n",
            TAG_MESSAGE.replace("\n", "\r"),
        )

        for message in mutations:
            with self.subTest(message=repr(message)):
                with self.assertRaisesRegex(ValueError, "message differs"):
                    MODULE.validate_existing_tag(
                        tag_ref,
                        {**tag_object, "message": message},
                        expected_tag="v0.9.14",
                        source_sha=SHA,
                        expected_message=TAG_MESSAGE,
                    )

    def test_existing_release_metadata_returns_zero_one_or_all_names_as_arrays(
        self,
    ) -> None:
        names = ["one.zip", "two.json", "three.json", "manifest.json", "assets.sha256"]
        for count in (0, 1, len(names)):
            with self.subTest(count=count):
                release = json.loads(
                    json.dumps(
                        {
                            "tagName": "v0.9.14",
                            "isDraft": False,
                            "isPrerelease": False,
                            "body": "complete notes\n",
                            "assets": [{"name": name} for name in names[:count]],
                        }
                    )
                )
                actual = MODULE.validate_existing_release(
                    release,
                    expected_tag="v0.9.14",
                    expected_body="complete notes",
                )
                self.assertIsInstance(actual, list)
                self.assertEqual(names[:count], actual)

        for key, value, message in (
            ("tagName", "v0.9.15", "tag differs"),
            ("isDraft", True, "still a draft"),
            ("isPrerelease", True, "is a prerelease"),
            ("body", "conflicting notes", "body conflicts"),
        ):
            with self.subTest(key=key):
                release = {
                    "tagName": "v0.9.14",
                    "isDraft": False,
                    "isPrerelease": False,
                    "body": "complete notes",
                    "assets": [],
                    key: value,
                }
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_release(
                        release,
                        expected_tag="v0.9.14",
                        expected_body="complete notes",
                    )

        with self.assertRaisesRegex(ValueError, "JSON array"):
            MODULE.plan_release_asset_recovery(
                Path("unused-manifest"),
                Path("unused-published"),
                "one.zip",
            )

    def test_manifest_detects_digest_identity_and_extra_asset_drift(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-candidate-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "assets"
            root.mkdir()
            version = "1.0.5"
            for name in MODULE._candidate_asset_names(version):
                (root / name).write_bytes(name.encode("utf-8"))
            notes = root / "RELEASE-NOTES.md"
            notes.write_text("release notes\n", encoding="utf-8")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest_path = MODULE.create_candidate_manifest(
                root,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            manifest = MODULE.verify_candidate_manifest(
                manifest_path,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
            )
            self.assertEqual(3, len(manifest["assets"]))

            (root / MODULE._asset_names(version)[0]).write_bytes(b"tampered")
            with self.assertRaisesRegex(ValueError, "size mismatch|digest mismatch"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="99",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )

    def test_launcher_era_manifest_requires_all_eight_payload_assets(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-launcher-candidate-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "assets"
            root.mkdir()
            version = "1.0.6"
            expected = MODULE._candidate_asset_names(version)
            for name in expected:
                (root / name).write_bytes(name.encode("utf-8"))
            notes = root / "RELEASE-NOTES.md"
            notes.write_text("release notes\n", encoding="utf-8")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")

            manifest_path = MODULE.create_candidate_manifest(
                root,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            manifest = MODULE.verify_candidate_manifest(
                manifest_path,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
            )

            self.assertEqual(8, len(manifest["assets"]))
            self.assertEqual(
                set(expected),
                {entry["name"] for entry in manifest["assets"]},
            )

    def test_manifest_rejects_wrong_run_and_unexpected_file(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-candidate-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "assets"
            root.mkdir()
            version = "0.9.14"
            for name in MODULE._candidate_asset_names(version):
                (root / name).write_bytes(b"asset")
            notes = root / "RELEASE-NOTES.md"
            notes.write_bytes(b"notes")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest_path = MODULE.create_candidate_manifest(
                root,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            with self.assertRaisesRegex(ValueError, "candidateRunId mismatch"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="100",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )
            (root / "unexpected.exe").write_bytes(b"extra")
            with self.assertRaisesRegex(ValueError, "closed asset set"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="99",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )

    def test_recovery_allows_only_missing_assets_and_rejects_conflicts(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-recovery-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            candidate = temporary_root / "candidate"
            published = temporary_root / "published"
            candidate.mkdir()
            published.mkdir()
            version = "0.9.14"
            for name in MODULE._candidate_asset_names(version):
                (candidate / name).write_bytes(name.encode("utf-8"))
            notes = candidate / "RELEASE-NOTES.md"
            notes.write_bytes(b"notes")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest = MODULE.create_candidate_manifest(
                candidate,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            present_name = MODULE._asset_names(version)[0]
            (published / present_name).write_bytes(
                (candidate / present_name).read_bytes()
            )

            missing = MODULE.plan_release_asset_recovery(
                manifest, published, [present_name]
            )

            self.assertNotIn(present_name, missing)
            self.assertEqual(4, len(missing))
            (published / present_name).write_bytes(b"conflict")
            with self.assertRaisesRegex(ValueError, "digest conflicts"):
                MODULE.plan_release_asset_recovery(manifest, published, [present_name])
            with self.assertRaisesRegex(ValueError, "unexpected assets"):
                MODULE.plan_release_asset_recovery(
                    manifest, published, ["unexpected.zip"]
                )

    def test_github_probe_accepts_only_json_success_or_verified_404(self) -> None:
        self.assertEqual(
            "present", MODULE.classify_github_probe(0, '{"ref":"refs/tags/v0.9.14"}')
        )
        self.assertEqual(
            "absent", MODULE.classify_github_probe(1, "gh: Not Found (HTTP 404)")
        )
        for exit_code, output, message in (
            (0, "not-json", "malformed JSON"),
            (1, "gh: Forbidden (HTTP 403)", "without a verified 404"),
            (1, "network timeout", "without a verified 404"),
            (1, "gh: server error (HTTP 500)", "without a verified 404"),
        ):
            with self.subTest(exit_code=exit_code, output=output):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.classify_github_probe(exit_code, output)


if __name__ == "__main__":
    unittest.main()
