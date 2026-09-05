"""Behavioral tests for stable release identity and immutable candidate policy."""

from __future__ import annotations

import importlib.util
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import textwrap
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
REVIEW_HEAD_SHA = "4" * 40
TAG_MESSAGE = "NVT FW Combiner v0.9.14\ncandidate-run: 99"
REQUIRED_RELEASE_CHECKS = (
    "policy / polytail",
    "python-worker / verify",
    "dotnet / build-test",
)


def valid_repository_admission() -> dict[str, object]:
    return {
        "remoteMain": {"sha": SHA, "protected": True},
        "mainRulesPaginationComplete": True,
        "mainRules": [
            {
                "type": "required_status_checks",
                "parameters": {
                    "required_status_checks": [
                        {"context": name, "integration_id": 15368}
                        for name in REQUIRED_RELEASE_CHECKS
                    ]
                },
            }
        ],
        "checkRunsPaginationComplete": True,
        "checkRuns": [
            {
                "name": name,
                "headSha": REVIEW_HEAD_SHA,
                "appSlug": "github-actions",
                "status": "completed",
                "conclusion": "success",
            }
            for name in REQUIRED_RELEASE_CHECKS
        ],
        "reviewThreadsPaginationComplete": True,
        "reviewThreads": [],
        "tagRulesetsPaginationComplete": True,
        "tagRulesets": [
            {
                "id": 101,
                "target": "tag",
                "enforcement": "active",
                "conditions": {
                    "ref_name": {"include": ["refs/tags/v*"], "exclude": []}
                },
                "rules": [{"type": "update"}, {"type": "deletion"}],
            }
        ],
    }


def valid_v111_expected_assets() -> dict[str, dict[str, object]]:
    return {
        "one.zip": {"size": 3, "sha256": "a" * 64},
        "manifest.json": {"size": 4, "sha256": "b" * 64},
    }


def valid_v111_release() -> dict[str, object]:
    expected_assets = valid_v111_expected_assets()
    return {
        "tag_name": "v1.1.1",
        "draft": False,
        "prerelease": False,
        "immutable": True,
        "body": "complete notes\n",
        "assets": [
            {
                "name": name,
                "state": "uploaded",
                "size": metadata["size"],
                "digest": f"sha256:{metadata['sha256']}",
            }
            for name, metadata in expected_assets.items()
        ],
    }


def valid_snapshot() -> dict[str, object]:
    return {
        "number": 214,
        "state": "MERGED",
        "mergedAt": "2026-07-22T01:00:00Z",
        "baseRefName": "main",
        "mergeCommitSha": SHA,
        "headSha": REVIEW_HEAD_SHA,
        "headTree": TREE,
        "reviewDecision": "APPROVED",
        "approvals": [
            {
                "reviewer": "independent-reviewer",
                "commitSha": REVIEW_HEAD_SHA,
                "submittedAt": "2026-07-22T00:55:00Z",
            }
        ],
        "ownerSelfApprovalException": False,
        "requiredChecks": [{"name": "dotnet / build-test", "bucket": "pass"}],
        "repositoryAdmission": valid_repository_admission(),
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

    @staticmethod
    def write_fake_admission_gh(root: Path) -> tuple[Path, Path]:
        tools = root / "tools"
        tools.mkdir()
        call_log = root / "gh-calls.jsonl"
        fake_gh = tools / "fake_gh.py"
        fake_gh.write_text(
            textwrap.dedent(
                f"""
                import json
                import os
                import sys
                from pathlib import Path

                args = sys.argv[1:]
                scenario = os.environ.get("FAKE_GH_SCENARIO", "success")
                with Path(os.environ["FAKE_GH_LOG"]).open("a", encoding="utf-8") as log:
                    log.write(json.dumps(args) + "\\n")

                def emit(value):
                    print(json.dumps(value))
                    raise SystemExit(0)

                if not args or args[0] != "api":
                    raise SystemExit(91)
                if args[1] == "graphql":
                    query = next(value[6:] for value in args if value.startswith("query="))
                    fields = {{
                        args[index + 1].split("=", 1)[0]: args[index + 1].split("=", 1)[1]
                        for index, value in enumerate(args[:-1])
                        if value == "-F"
                    }}
                    cursor = fields.get("cursor")
                    if "reviewThreads(first" in query:
                        if cursor is None:
                            page = {{
                                "data": {{"repository": {{"pullRequest": {{"reviewThreads": {{
                                    "pageInfo": {{
                                        "hasNextPage": True,
                                        "endCursor": "thread-page-1"
                                    }},
                                    "nodes": [{{
                                        "id": "THREAD_RESOLVED",
                                        "isResolved": True,
                                        "isOutdated": False
                                    }}]
                                }}}}}}}}
                            }}
                            if scenario == "graphql_errors":
                                page["errors"] = [{{"message": "simulated"}}]
                            elif scenario == "missing_page_info":
                                page["data"]["repository"]["pullRequest"][
                                    "reviewThreads"
                                ].pop("pageInfo")
                            elif scenario == "blank_cursor":
                                page["data"]["repository"]["pullRequest"][
                                    "reviewThreads"
                                ]["pageInfo"]["endCursor"] = " "
                            emit(page)
                        page = {{
                            "data": {{"repository": {{"pullRequest": {{"reviewThreads": {{
                                "pageInfo": {{
                                    "hasNextPage": False,
                                    "endCursor": "thread-page-2"
                                }},
                                "nodes": [{{
                                    "id": "THREAD_OPEN",
                                    "isResolved": False,
                                    "isOutdated": False
                                }}]
                            }}}}}}}}
                        }}
                        if scenario == "repeated_cursor":
                            page["data"]["repository"]["pullRequest"][
                                "reviewThreads"
                            ]["pageInfo"] = {{
                                "hasNextPage": True,
                                "endCursor": "thread-page-1"
                            }}
                        emit(page)
                    if "node(id:" in query:
                        if cursor is None:
                            emit({{
                                "data": {{"node": {{"comments": {{
                                    "pageInfo": {{
                                        "hasNextPage": True,
                                        "endCursor": "comment-page-1"
                                    }},
                                    "nodes": [{{"body": "[P2] root"}}]
                                }}}}}}
                            }})
                        emit({{
                            "data": {{"node": {{"comments": {{
                                "pageInfo": {{
                                    "hasNextPage": False,
                                    "endCursor": "comment-page-2"
                                }},
                                "nodes": [{{
                                    "body": os.environ.get("FAKE_LATE_PRIORITY", "[P2] later")
                                }}]
                            }}}}}}
                        }})
                    raise SystemExit(92)

                endpoint = next(value for value in args[1:] if value.startswith("repos/"))
                form = {{
                    args[index + 1].split("=", 1)[0]: args[index + 1].split("=", 1)[1]
                    for index, value in enumerate(args[:-1])
                    if value == "-f" and "=" in args[index + 1]
                }}
                page_number = int(form.get("page", "1"))
                if endpoint == "repos/owner/repository/branches/main":
                    if scenario == "gh_failure":
                        raise SystemExit(17)
                    if scenario == "malformed_json":
                        print("{{")
                        raise SystemExit(0)
                    if scenario == "duplicate_json_key":
                        print('{{"commit": {{}}, "protected": true, "protected": false}}')
                        raise SystemExit(0)
                    emit({{"commit": {{"sha": "{SHA}"}}, "protected": True}})
                if endpoint == "repos/owner/repository/rules/branches/main":
                    pages = {{
                        1: [{{"type": "creation"}}],
                        2: [{{
                            "type": "required_status_checks",
                            "parameters": {{"required_status_checks": [
                                {{"context": name}}
                                for name in {REQUIRED_RELEASE_CHECKS!r}
                            ]}}
                        }}]
                    }}
                    emit(pages.get(page_number, []))
                if endpoint == "repos/owner/repository/rulesets":
                    emit({{1: [{{"id": 17}}], 2: [{{"id": 29}}]}}.get(page_number, []))
                if endpoint in (
                    "repos/owner/repository/rulesets/17",
                    "repos/owner/repository/rulesets/29",
                ):
                    identifier = int(endpoint.rsplit("/", 1)[1])
                    qualifying = identifier == 29
                    bypass = []
                    if qualifying and os.environ.get("FAKE_VISIBLE_BYPASS"):
                        bypass = [{{"actor_id": 1}}]
                    emit({{
                        "id": (
                            17
                            if scenario == "ruleset_detail_id_mismatch" and qualifying
                            else identifier
                        ),
                        "target": "tag",
                        "enforcement": "active" if qualifying else "evaluate",
                        "bypass_actors": bypass,
                        "conditions": {{
                            "ref_name": {{"include": ["refs/tags/v*"], "exclude": []}}
                        }},
                        "rules": (
                            [{{"type": "update"}}, {{"type": "deletion"}}]
                            if qualifying else [{{"type": "creation"}}]
                        )
                    }})
                if endpoint == (
                    "repos/owner/repository/commits/{REVIEW_HEAD_SHA}/check-runs"
                ):
                    runs = [
                        {{
                            "name": name,
                            "head_sha": "{REVIEW_HEAD_SHA}",
                            "app": {{"slug": "github-actions"}},
                            "status": "completed",
                            "conclusion": "success"
                        }}
                        for name in {REQUIRED_RELEASE_CHECKS!r}
                    ]
                    if scenario == "duplicate_success":
                        runs.extend(dict(run) for run in runs)
                    elif scenario == "in_progress_unrelated":
                        runs.append({{
                            "name": "release / candidate",
                            "head_sha": "{REVIEW_HEAD_SHA}",
                            "app": {{"slug": "github-actions"}},
                            "status": "in_progress",
                            "conclusion": None
                        }})
                    elif scenario == "completed_null":
                        runs.append({{
                            "name": "release / candidate",
                            "head_sha": "{REVIEW_HEAD_SHA}",
                            "app": {{"slug": "github-actions"}},
                            "status": "completed",
                            "conclusion": None
                        }})
                    elif scenario == "required_pending":
                        runs[0] = {{**runs[0], "status": "in_progress", "conclusion": None}}
                    total_count = len(runs) + 1 if scenario == "truncated_checks" else len(runs)
                    if scenario == "low_total_late_duplicate":
                        page_runs = {{1: runs, 2: [runs[0]]}}.get(page_number, [])
                    else:
                        page_runs = {{1: runs[:2], 2: runs[2:]}}.get(page_number, [])
                    emit({{"total_count": total_count, "check_runs": page_runs}})
                raise SystemExit(93)
                """
            ).lstrip(),
            encoding="utf-8",
        )
        interpreter = str(Path(sys.executable).resolve())
        (tools / "gh.cmd").write_text(
            f'@"{interpreter}" "%~dp0fake_gh.py" %*\r\n', encoding="utf-8"
        )
        return tools, call_log

    def run_admission_collector(
        self,
        root: Path,
        *,
        late_priority: str = "[P2] later",
        visible_bypass: bool = False,
        scenario: str = "success",
    ) -> subprocess.CompletedProcess[str]:
        tools, call_log = self.write_fake_admission_gh(root)
        environment = os.environ.copy()
        environment.update(
            {
                "PATH": str(tools) + os.pathsep + environment["PATH"],
                "FAKE_GH_LOG": str(call_log),
                "FAKE_LATE_PRIORITY": late_priority,
                "FAKE_GH_SCENARIO": scenario,
            }
        )
        if visible_bypass:
            environment["FAKE_VISIBLE_BYPASS"] = "1"
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "collect-repository-admission",
                "--repository",
                "owner/repository",
                "--pull-request",
                "406",
                "--main-sha",
                SHA,
                "--review-head-sha",
                REVIEW_HEAD_SHA,
                "--expected-tag",
                "v1.1.1",
                "--output",
                str(root / "admission.json"),
            ],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            env=environment,
        )

    def test_version_and_launcher_asset_name_sets_remain_separate_and_closed(
        self,
    ) -> None:
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
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-capture-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            candidate_sha = self.git(repository, "rev-parse", "HEAD")
            base_package = Path(temporary) / "base.zip"
            candidate_package = Path(temporary) / "candidate.zip"
            self.write_version_package(
                base_package, version="1.0.0", source_sha=base_sha
            )
            self.write_version_package(
                candidate_package, version="1.0.1", source_sha=candidate_sha
            )
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
        with tempfile.TemporaryDirectory(
            prefix="nfc-version-only-reuse-capture-"
        ) as temporary:
            repository = Path(temporary) / "repository"
            repository.mkdir()
            self.create_version_only_repository(repository)
            base_sha = self.git(repository, "rev-parse", "v1.0.0^{commit}")
            base_package = Path(temporary) / "base.zip"
            destination = Path(temporary) / "out/Nfc.CrcWorker.exe"
            self.write_version_package(
                base_package, version="1.0.0", source_sha=base_sha
            )
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

    def test_101_rejects_self_consistent_base_zip_with_wrong_external_digest(
        self,
    ) -> None:
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

    def test_live_branch_authority_requires_protected_main_and_exact_new_tag_source(
        self,
    ) -> None:
        snapshot = {
            "remoteMain": {"sha": SHA, "protected": True},
            "remoteSource": {"sha": REVIEW_HEAD_SHA},
            "tagState": "absent",
        }
        MODULE.validate_live_branch_authority(
            snapshot,
            main_sha=SHA,
            source_sha=REVIEW_HEAD_SHA,
        )

        mutations = (
            ({"remoteSource": snapshot["remoteSource"]}, "remote main"),
            (
                {
                    **snapshot,
                    "remoteMain": {"sha": "5" * 40, "protected": True},
                },
                "remote main SHA differs",
            ),
            (
                {**snapshot, "remoteMain": {"protected": True}},
                "remote main SHA",
            ),
            (
                {**snapshot, "remoteMain": {"sha": SHA}},
                "remote main must be protected",
            ),
            (
                {**snapshot, "remoteMain": {"sha": SHA, "protected": False}},
                "remote main must be protected",
            ),
            (
                {**snapshot, "remoteMain": {"sha": SHA, "protected": "true"}},
                "remote main must be protected",
            ),
            (
                {**snapshot, "remoteMain": {"sha": SHA, "protected": 1}},
                "remote main must be protected",
            ),
            (
                {
                    "remoteMain": snapshot["remoteMain"],
                    "tagState": "absent",
                },
                "remote source",
            ),
            ({**snapshot, "remoteSource": None}, "remote source"),
            ({**snapshot, "remoteSource": []}, "remote source"),
            ({**snapshot, "remoteSource": {}}, "remote source SHA"),
            (
                {**snapshot, "remoteSource": {"sha": "not-a-sha"}},
                "remote source SHA",
            ),
            (
                {**snapshot, "remoteSource": {"sha": "5" * 40}},
                "new stable tag.*current release branch head",
            ),
            (
                {key: value for key, value in snapshot.items() if key != "tagState"},
                "tag state",
            ),
            ({**snapshot, "tagState": None}, "tag state"),
            ({**snapshot, "tagState": "unknown"}, "tag state"),
        )
        for mutated, message in mutations:
            with self.subTest(message=message, mutated=mutated):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_live_branch_authority(
                        mutated,
                        main_sha=SHA,
                        source_sha=REVIEW_HEAD_SHA,
                    )

    def test_live_branch_authority_allows_only_proven_forward_recovery(self) -> None:
        advanced_sha = "5" * 40
        exact_snapshot = {
            "remoteMain": {"sha": SHA, "protected": True},
            "remoteSource": {"sha": REVIEW_HEAD_SHA},
            "tagState": "present",
        }
        MODULE.validate_live_branch_authority(
            exact_snapshot,
            main_sha=SHA,
            source_sha=REVIEW_HEAD_SHA,
        )

        comparison = {
            "baseSha": REVIEW_HEAD_SHA,
            "headSha": advanced_sha,
            "mergeBaseSha": REVIEW_HEAD_SHA,
            "status": "ahead",
        }
        advanced_snapshot = {
            **exact_snapshot,
            "remoteSource": {"sha": advanced_sha},
            "sourceComparison": comparison,
        }
        MODULE.validate_live_branch_authority(
            advanced_snapshot,
            main_sha=SHA,
            source_sha=REVIEW_HEAD_SHA,
        )

        with self.assertRaises(ValueError):
            MODULE.validate_live_branch_authority(
                {
                    key: value
                    for key, value in advanced_snapshot.items()
                    if key != "sourceComparison"
                },
                main_sha=SHA,
                source_sha=REVIEW_HEAD_SHA,
            )

        malformed_comparisons = (
            None,
            [],
            {},
            {**comparison, "baseSha": "6" * 40},
            {**comparison, "headSha": "6" * 40},
            {**comparison, "mergeBaseSha": "6" * 40},
            {**comparison, "status": "behind"},
            {**comparison, "status": "diverged"},
        )
        for malformed in malformed_comparisons:
            with self.subTest(malformed=malformed):
                with self.assertRaises(ValueError):
                    MODULE.validate_live_branch_authority(
                        {**advanced_snapshot, "sourceComparison": malformed},
                        main_sha=SHA,
                        source_sha=REVIEW_HEAD_SHA,
                    )

    @staticmethod
    def source_ci_evidence() -> dict:
        run = {
            "id": 80, "run_attempt": 2, "workflow_id": 12,
            "head_sha": SHA, "head_branch": "main", "event": "push",
            "path": ".github/workflows/ci.yml",
            "repository": {"full_name": "owner/repo"},
            "head_repository": {"full_name": "owner/repo"},
            "created_at": "2026-09-05T02:00:00Z",
            "status": "completed", "conclusion": "success",
        }
        return {
            "repository": "owner/repo", "run": run,
            "runsPaginationComplete": True, "jobsPaginationComplete": True,
            "jobs": [
                {"id": index + 100, "run_id": 80, "head_sha": SHA, "name": name,
                 "status": "completed", "conclusion": "success"}
                for index, name in enumerate(REQUIRED_RELEASE_CHECKS)
            ],
        }

    def test_v113_rejects_review_head_ci_without_actual_source_ci(self) -> None:
        # A green PR tree is not a successful CI run on its final merge commit.
        with self.assertRaisesRegex(ValueError, "source CI"):
            MODULE.validate_candidate_context(
                valid_snapshot(),
                **{**self.candidate_arguments(), "source_version": "1.1.3"},
            )

    def test_v113_accepts_only_exact_source_workflow_and_attempt(self) -> None:
        source = self.source_ci_evidence()
        snapshot = valid_snapshot()
        snapshot["repositoryAdmission"]["sourceCi"] = source
        MODULE.validate_candidate_context(
            snapshot, **{**self.candidate_arguments(), "source_version": "1.1.3"},
        )
        for field, value in (
            ("head_sha", REVIEW_HEAD_SHA), ("head_branch", "feature"),
            ("path", ".github/workflows/other.yml"), ("event", "pull_request"),
            ("status", "in_progress"), ("conclusion", "failure"),
            ("run_attempt", 0), ("id", True), ("workflow_id", "12"),
            ("repository", {"full_name": "other/repo"}),
            ("head_repository", {"full_name": "fork/repo"}),
            ("created_at", "2026-99-05T02:00:00Z"),
        ):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "source CI"):
                MODULE.validate_source_ci(
                    {**source, "run": {**source["run"], field: value}}, source_sha=SHA,
                )
        for field in ("runsPaginationComplete", "jobsPaginationComplete"):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "source CI"):
                MODULE.validate_source_ci({**source, field: False}, source_sha=SHA)
        for jobs in (
            [], source["jobs"][:-1], source["jobs"] + [source["jobs"][0]],
            source["jobs"] + [{**source["jobs"][0], "id": 500}],
            [{**job, "head_sha": REVIEW_HEAD_SHA} for job in source["jobs"]],
            [{**job, "run_id": 81} for job in source["jobs"]],
            [{**job, "conclusion": "skipped"} for job in source["jobs"]],
        ):
            with self.subTest(jobs=jobs), self.assertRaisesRegex(ValueError, "source CI"):
                MODULE.validate_source_ci({**source, "jobs": jobs}, source_sha=SHA)

    def test_legacy_admission_does_not_require_or_interpret_source_ci(self) -> None:
        for source in (None, {"run": "malformed legacy-irrelevant evidence"}):
            snapshot = valid_snapshot()
            snapshot["repositoryAdmission"]["sourceCi"] = source
            MODULE.validate_candidate_context(
                snapshot, **{**self.candidate_arguments(), "source_version": "1.1.2"},
            )

    def test_admission_cli_forwards_actual_source_sha_to_the_collector(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "admission.json"
            with (
                mock.patch.object(sys, "argv", [
                    "release_promotion_policy.py", "collect-repository-admission",
                    "--repository", "owner/repo", "--pull-request", "7",
                    "--main-sha", SHA, "--review-head-sha", REVIEW_HEAD_SHA,
                    "--source-sha", SHA, "--expected-tag", "v1.1.3", "--output", str(output),
                ]),
                mock.patch.object(MODULE, "collect_repository_admission", return_value={}) as collect,
            ):
                self.assertEqual(0, MODULE.main())
            collect.assert_called_once_with(
                repository="owner/repo", pull_request=7, main_sha=SHA,
                review_head_sha=REVIEW_HEAD_SHA, source_sha=SHA, expected_tag="v1.1.3",
            )

    def test_source_ci_collector_reads_latest_attempt_and_confirms_identity(self) -> None:
        source = self.source_ci_evidence()
        run, jobs = source["run"], source["jobs"]
        raw_run = {
            **run,
            "actor": {"login": "sentinel-run-actor"},
            "head_commit": {"author": {"email": "sentinel@example.invalid"}},
            "repository": {
                **run["repository"],
                "name": "sentinel-run-repository",
            },
            "head_repository": {
                **run["head_repository"],
                "name": "sentinel-head-repository",
            },
        }
        raw_jobs = [
            {
                **job,
                "runner_name": "sentinel-runner",
                "steps": [{"name": "sentinel-step"}],
                "html_url": "https://example.invalid/sentinel-job",
            }
            for job in jobs
        ]
        responses = [
            {"total_count": 1, "workflow_runs": [raw_run]},
            {"total_count": 1, "workflow_runs": []}, raw_run,
            {"total_count": 3, "jobs": raw_jobs[:2]},
            {"total_count": 3, "jobs": raw_jobs[2:]},
            {"total_count": 3, "jobs": []}, raw_run,
        ]
        with mock.patch.object(MODULE, "_read_github_json", side_effect=responses) as read:
            actual = MODULE._collect_source_ci("owner/repo", SHA)
        self.assertEqual(source, actual)
        serialized = json.dumps(actual)
        for sentinel in (
            "sentinel-run-actor",
            "sentinel@example.invalid",
            "sentinel-run-repository",
            "sentinel-head-repository",
            "sentinel-runner",
            "sentinel-step",
            "sentinel-job",
        ):
            self.assertNotIn(sentinel, serialized)
        calls = [call.args[0] for call in read.call_args_list]
        self.assertIn(f"head_sha={SHA}", calls[0])
        self.assertIn("branch=main", calls[0])
        self.assertIn("event=push", calls[0])
        self.assertEqual(
            "repos/owner/repo/actions/runs/80/attempts/2/jobs", calls[3][3],
        )
        self.assertEqual(calls[2], calls[-1])
        self.assertTrue(all(call[0] == "api" for call in calls))

    def test_source_ci_collector_rejects_newer_red_or_ambiguous_runs_and_drift(self) -> None:
        source = self.source_ci_evidence()
        run = source["run"]
        older = {**run, "id": 70, "created_at": "2026-09-04T02:00:00Z"}
        for runs, after in (
            ([older, {**run, "conclusion": "failure"}], run),
            ([older, {**run, "status": "queued", "conclusion": None}], run),
            ([run, {**run, "id": 81}], run),
            ([run, run], run),
            ([run], {**run, "run_attempt": 3}),
            ([run], {**run, "head_sha": REVIEW_HEAD_SHA}),
        ):
            responses = [
                {"total_count": len(runs), "workflow_runs": runs},
                {"total_count": len(runs), "workflow_runs": []}, runs[-1],
                {"total_count": 3, "jobs": source["jobs"]},
                {"total_count": 3, "jobs": []}, after,
            ]
            with self.subTest(runs=runs, after=after), \
                mock.patch.object(MODULE, "_read_github_json", side_effect=responses), \
                self.assertRaisesRegex(ValueError, "source CI"):
                MODULE._collect_source_ci("owner/repo", SHA)

    def test_source_ci_inventory_rejects_incomplete_and_changing_pages(self) -> None:
        for responses in (
            [{"total_count": 1, "workflow_runs": []}],
            [{"total_count": True, "workflow_runs": []}],
            [{"total_count": 1, "workflow_runs": [None]}],
            [{"total_count": 1, "workflow_runs": [{"id": 1}]},
             {"total_count": 2, "workflow_runs": []}],
        ):
            with self.subTest(responses=responses), \
                mock.patch.object(MODULE, "_read_github_json", side_effect=responses), \
                self.assertRaisesRegex(ValueError, "source CI"):
                MODULE._collect_source_ci("owner/repo", SHA)

    def test_v111_repository_admission_accepts_only_exact_protected_policy(
        self,
    ) -> None:
        snapshot = valid_snapshot()
        MODULE.validate_candidate_context(
            snapshot,
            **{**self.candidate_arguments(), "source_version": "1.1.1"},
        )

        admission = valid_repository_admission()
        cases = (
            (
                {**admission, "remoteMain": {"sha": SHA, "protected": False}},
                "protected",
            ),
            (
                {**admission, "remoteMain": {"sha": "5" * 40, "protected": True}},
                "remote main SHA",
            ),
            ({**admission, "mainRulesPaginationComplete": False}, "main rules"),
            ({**admission, "checkRunsPaginationComplete": False}, "check runs"),
            (
                {**admission, "reviewThreadsPaginationComplete": False},
                "review threads",
            ),
            ({**admission, "tagRulesetsPaginationComplete": False}, "tag rulesets"),
            ({**admission, "mainRules": []}, "required status checks"),
            (
                {
                    **admission,
                    "mainRules": [
                        {
                            "type": "required_status_checks",
                            "parameters": {
                                "required_status_checks": [
                                    {"context": REQUIRED_RELEASE_CHECKS[0]}
                                ]
                            },
                        }
                    ],
                },
                "required status checks",
            ),
            ({**admission, "checkRuns": admission["checkRuns"][:-1]}, "check runs"),
            ({**admission, "tagRulesets": []}, "stable tag"),
        )
        for mutated_admission, message in cases:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        {**snapshot, "repositoryAdmission": mutated_admission},
                        **{**self.candidate_arguments(), "source_version": "1.1.1"},
                    )

    def test_v111_repository_admission_accepts_multiple_exact_passing_suites(
        self,
    ) -> None:
        admission = valid_repository_admission()
        MODULE.validate_repository_admission(
            {
                **admission,
                "checkRuns": [
                    *admission["checkRuns"],
                    *(dict(check) for check in admission["checkRuns"]),
                ],
            },
            main_sha=SHA,
            review_head_sha=REVIEW_HEAD_SHA,
            expected_tag="v1.1.1",
        )

    def test_v111_tag_ruleset_inventory_rejects_malformed_entries_after_match(
        self,
    ) -> None:
        admission = valid_repository_admission()
        qualifying = admission["tagRulesets"][0]
        malformed_entries = (
            None,
            {**qualifying, "id": 0},
            {**qualifying, "id": "101"},
        )

        for malformed in malformed_entries:
            with self.subTest(malformed=malformed):
                with self.assertRaisesRegex(ValueError, "stable tag ruleset"):
                    MODULE.validate_repository_admission(
                        {
                            **admission,
                            "tagRulesets": [qualifying, malformed],
                        },
                        main_sha=SHA,
                        review_head_sha=REVIEW_HEAD_SHA,
                        expected_tag="v1.1.1",
                    )

    def test_v111_tag_ruleset_inventory_rejects_duplicate_identity_after_match(
        self,
    ) -> None:
        admission = valid_repository_admission()
        qualifying = admission["tagRulesets"][0]
        duplicate = {**qualifying}

        with self.assertRaisesRegex(ValueError, "stable tag ruleset.*(identity|id)"):
            MODULE.validate_repository_admission(
                {**admission, "tagRulesets": [qualifying, duplicate]},
                main_sha=SHA,
                review_head_sha=REVIEW_HEAD_SHA,
                expected_tag="v1.1.1",
            )

    def test_v111_visible_tag_ruleset_bypass_actors_must_be_exactly_empty(
        self,
    ) -> None:
        admission = valid_repository_admission()
        qualifying = admission["tagRulesets"][0]

        for accepted in (qualifying, {**qualifying, "bypass_actors": []}):
            with self.subTest(accepted=accepted):
                MODULE.validate_repository_admission(
                    {**admission, "tagRulesets": [accepted]},
                    main_sha=SHA,
                    review_head_sha=REVIEW_HEAD_SHA,
                    expected_tag="v1.1.1",
                )

        for rejected in (None, {}, "none", 0, [{"actor_id": 1}]):
            with self.subTest(rejected=rejected):
                with self.assertRaisesRegex(ValueError, "bypass actors"):
                    MODULE.validate_repository_admission(
                        {
                            **admission,
                            "tagRulesets": [{**qualifying, "bypass_actors": rejected}],
                        },
                        main_sha=SHA,
                        review_head_sha=REVIEW_HEAD_SHA,
                        expected_tag="v1.1.1",
                    )

    def test_repository_admission_collector_uses_complete_read_only_evidence(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="release-admission-cli-") as temporary:
            root = Path(temporary)
            result = self.run_admission_collector(root)
            self.assertEqual(0, result.returncode, result.stderr)

            snapshot = json.loads((root / "admission.json").read_text(encoding="utf-8"))
            self.assertEqual(2, len(snapshot["mainRules"]))
            self.assertEqual(3, len(snapshot["checkRuns"]))
            self.assertEqual(2, len(snapshot["reviewThreads"]))
            self.assertEqual(2, len(snapshot["tagRulesets"]))
            self.assertEqual(
                "[P2] root\n[P2] later", snapshot["reviewThreads"][1]["body"]
            )
            self.assertTrue(snapshot["reviewThreads"][1]["commentsPaginationComplete"])

            calls = [
                json.loads(line)
                for line in (root / "gh-calls.jsonl")
                .read_text(encoding="utf-8")
                .splitlines()
            ]
            self.assertEqual(4, sum(call[1] == "graphql" for call in calls))
            self.assertTrue(all(call[0] == "api" for call in calls))
            self.assertFalse(
                any(
                    token.upper() in {"POST", "PATCH", "PUT", "DELETE"}
                    for call in calls
                    for token in call
                ),
                calls,
            )

    def test_repository_admission_collector_preserves_unrelated_pending_check(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-admission-pending-"
        ) as temporary:
            root = Path(temporary)
            result = self.run_admission_collector(
                root,
                scenario="in_progress_unrelated",
            )
            self.assertEqual(0, result.returncode, result.stderr)
            snapshot = json.loads(
                (root / "admission.json").read_text(encoding="utf-8")
            )
            pending = [
                run
                for run in snapshot["checkRuns"]
                if run["name"] == "release / candidate"
            ]
            self.assertEqual(1, len(pending))
            self.assertEqual("in_progress", pending[0]["status"])
            self.assertIsNone(pending[0]["conclusion"])

    def test_repository_admission_collector_rejects_invalid_null_or_required_pending(
        self,
    ) -> None:
        for scenario, message in (
            ("completed_null", "malformed"),
            ("required_pending", "check runs"),
        ):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(
                prefix="release-admission-invalid-check-"
            ) as temporary:
                root = Path(temporary)
                result = self.run_admission_collector(root, scenario=scenario)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(message, result.stderr)
                self.assertFalse((root / "admission.json").exists())

    def test_repository_admission_collector_fails_on_late_p1_or_visible_bypass(
        self,
    ) -> None:
        cases = (
            ({"late_priority": "[P1] later"}, "one priority"),
            ({"visible_bypass": True}, "bypass actors"),
        )
        for arguments, message in cases:
            with self.subTest(message=message):
                with tempfile.TemporaryDirectory(
                    prefix="release-admission-cli-reject-"
                ) as temporary:
                    root = Path(temporary)
                    result = self.run_admission_collector(root, **arguments)
                    self.assertNotEqual(0, result.returncode)
                    self.assertIn(message, result.stderr)
                    self.assertFalse((root / "admission.json").exists())

    def test_repository_admission_collector_fails_closed_on_transport_and_pagination(
        self,
    ) -> None:
        cases = (
            ("gh_failure", "could not be read"),
            ("malformed_json", "malformed JSON"),
            ("duplicate_json_key", "malformed JSON"),
            ("truncated_checks", "pagination is incomplete"),
            ("low_total_late_duplicate", "totals are contradictory"),
            ("ruleset_detail_id_mismatch", "detail id contradicts"),
            ("graphql_errors", "GraphQL errors"),
            ("missing_page_info", "pageInfo"),
            ("blank_cursor", "cursor is blank"),
            ("repeated_cursor", "did not advance"),
        )
        for scenario, message in cases:
            with self.subTest(scenario=scenario):
                with tempfile.TemporaryDirectory(
                    prefix="release-admission-cli-fail-closed-"
                ) as temporary:
                    root = Path(temporary)
                    result = self.run_admission_collector(root, scenario=scenario)
                    self.assertNotEqual(0, result.returncode)
                    self.assertIn(message, result.stderr)
                    self.assertFalse((root / "admission.json").exists())

    def test_rest_pagination_stops_at_the_hard_page_limit(self) -> None:
        with mock.patch.object(
            MODULE,
            "_read_github_json",
            return_value=[{"type": "creation"}],
        ) as read_page:
            with self.assertRaisesRegex(ValueError, "page limit"):
                MODULE._read_github_paginated_array(
                    "repos/owner/repository/rules/branches/main",
                    "applied main rules evidence",
                )
        self.assertEqual(MODULE.MAX_GITHUB_PAGES, read_page.call_count)

    def test_read_only_guard_rejects_compact_mutation_methods(self) -> None:
        with mock.patch.object(MODULE, "_execute_gh") as execute:
            for arguments in (
                ["api", "--method=DELETE", "repos/owner/repository/rulesets"],
                ["api", "-XPOST", "repos/owner/repository/rulesets"],
                ["api", "--method"],
            ):
                with self.subTest(arguments=arguments):
                    with self.assertRaisesRegex(
                        ValueError, "attempted a mutation|command is invalid"
                    ):
                        MODULE._run_read_only_gh(arguments, "repository evidence")
        execute.assert_not_called()

    def test_collector_rejects_dot_repository_segments_before_transport(self) -> None:
        with mock.patch.object(MODULE, "_read_github_json") as read:
            for repository in ("./repository", "owner/.."):
                with self.subTest(repository=repository):
                    with self.assertRaisesRegex(ValueError, "identity is malformed"):
                        MODULE.collect_repository_admission(
                            repository=repository,
                            pull_request=406,
                            main_sha=SHA,
                            review_head_sha=REVIEW_HEAD_SHA,
                            expected_tag="v1.1.1",
                        )
        read.assert_not_called()

    def test_v110_ci_promotion_is_rejected_without_rewriting_history(self) -> None:
        snapshot = valid_snapshot()
        snapshot.pop("repositoryAdmission")
        with self.assertRaisesRegex(ValueError, "manual-only operator release"):
            MODULE.validate_candidate_context(
                snapshot,
                **{**self.candidate_arguments(), "source_version": "1.1.0"},
            )
        with self.assertRaisesRegex(ValueError, "manual-only operator release"):
            MODULE.validate_promotion_source_state(
                source_sha=SHA,
                source_tree=TREE,
                checkout_sha=SHA,
                checkout_tree=TREE,
                source_branch="main",
                source_version="1.1.0",
                source_branch_sha=SHA,
                workflow_sha=SHA,
                main_sha=SHA,
                tag_state="present",
                source_is_branch_ancestor=True,
            )
        with self.assertRaisesRegex(ValueError, "no repository admission evidence"):
            MODULE.validate_candidate_context(
                snapshot,
                **{**self.candidate_arguments(), "source_version": "1.1.1"},
            )

        self.assertEqual(
            ["historical.zip"],
            MODULE.validate_existing_release(
                {
                    "tag_name": "v1.1.0",
                    "draft": False,
                    "prerelease": False,
                    "immutable": False,
                    "body": "historical notes",
                    "assets": [{"name": "historical.zip"}],
                },
                expected_tag="v1.1.0",
                expected_body="historical notes",
            ),
        )

    def test_v111_repository_admission_rejects_bad_required_check_evidence(
        self,
    ) -> None:
        admission = valid_repository_admission()
        first = admission["checkRuns"][0]
        for key, value in (
            ("headSha", "5" * 40),
            ("appSlug", "external-app"),
            ("status", "in_progress"),
            ("conclusion", "failure"),
        ):
            with self.subTest(key=key):
                mutated = {
                    **admission,
                    "checkRuns": [
                        *admission["checkRuns"],
                        {**first, key: value},
                    ],
                }
                with self.assertRaisesRegex(ValueError, "check runs"):
                    MODULE.validate_repository_admission(
                        mutated,
                        main_sha=SHA,
                        review_head_sha=REVIEW_HEAD_SHA,
                        expected_tag="v1.1.1",
                    )

    def test_v111_review_threads_allow_parallel_p2_p3_but_block_p0_p1(self) -> None:
        admission = valid_repository_admission()
        for body in ("[P2] Follow-up", "![P3 Badge](badge-url) Polish later"):
            with self.subTest(body=body, expected="accepted"):
                MODULE.validate_repository_admission(
                    {
                        **admission,
                        "reviewThreads": [
                            {
                                "isResolved": False,
                                "isOutdated": False,
                                "commentsPaginationComplete": True,
                                "body": body,
                            }
                        ],
                    },
                    main_sha=SHA,
                    review_head_sha=REVIEW_HEAD_SHA,
                    expected_tag="v1.1.1",
                )

        MODULE.validate_repository_admission(
            {
                **admission,
                "reviewThreads": [
                    {
                        "isResolved": True,
                        "isOutdated": False,
                        "commentsPaginationComplete": True,
                        "body": "no marker",
                    }
                ],
            },
            main_sha=SHA,
            review_head_sha=REVIEW_HEAD_SHA,
            expected_tag="v1.1.1",
        )

        for body, message in (
            ("[P0] Critical", "unresolved P0"),
            ("![P1 Badge](badge-url) Must fix", "unresolved P1"),
            ("Needs classification", "classifiable priority"),
            ("[P2] Text also says [P3]", "one priority"),
        ):
            with self.subTest(body=body, expected="rejected"):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_repository_admission(
                        {
                            **admission,
                            "reviewThreads": [
                                {
                                    "isResolved": False,
                                    "isOutdated": True,
                                    "commentsPaginationComplete": True,
                                    "body": body,
                                }
                            ],
                        },
                        main_sha=SHA,
                        review_head_sha=REVIEW_HEAD_SHA,
                        expected_tag="v1.1.1",
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
        snapshot["repositoryAdmission"]["sourceCi"] = self.source_ci_evidence()
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

        for source_version in ("1.0.8", "1.0.9", "1.1.999"):
            with self.subTest(source_version=source_version, expected="accepted"):
                snapshot["repositoryAdmission"]["sourceCi"] = self.source_ci_evidence()
                MODULE.validate_candidate_context(
                    snapshot, **{**arguments, "source_version": source_version}
                )

        with self.assertRaisesRegex(ValueError, "manual-only operator release"):
            MODULE.validate_candidate_context(
                snapshot, **{**arguments, "source_version": "1.1.0"}
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

    def test_v111_release_requires_immutable_exact_rest_asset_metadata(self) -> None:
        expected_assets = valid_v111_expected_assets()
        release = valid_v111_release()

        self.assertEqual(
            list(expected_assets),
            MODULE.validate_existing_release(
                release,
                expected_tag="v1.1.1",
                expected_body="complete notes",
                expected_assets=expected_assets,
            ),
        )

        mutations = (
            ({**release, "immutable": False}, "immutable"),
            ({**release, "immutable": "true"}, "immutable"),
            (
                {key: value for key, value in release.items() if key != "immutable"},
                "immutable",
            ),
            (
                {
                    **release,
                    "assets": [
                        {**release["assets"][0], "state": "new"},
                        release["assets"][1],
                    ],
                },
                "uploaded state",
            ),
            (
                {
                    **release,
                    "assets": [
                        {**release["assets"][0], "size": 4},
                        release["assets"][1],
                    ],
                },
                "size",
            ),
            (
                {
                    **release,
                    "assets": [
                        {**release["assets"][0], "digest": f"sha256:{'c' * 64}"},
                        release["assets"][1],
                    ],
                },
                "digest",
            ),
            ({**release, "assets": release["assets"][:-1]}, "asset set"),
            (
                {
                    **release,
                    "assets": [
                        *release["assets"],
                        {
                            "name": "extra.json",
                            "state": "uploaded",
                            "size": 1,
                            "digest": f"sha256:{'d' * 64}",
                        },
                    ],
                },
                "asset set",
            ),
        )
        for mutated, message in mutations:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_release(
                        mutated,
                        expected_tag="v1.1.1",
                        expected_body="complete notes",
                        expected_assets=expected_assets,
                    )

    def test_v111_release_rejects_duplicate_or_unsafe_rest_asset_names(self) -> None:
        release = valid_v111_release()
        expected_assets = valid_v111_expected_assets()
        first_asset = release["assets"][0]

        duplicate_assets = [*release["assets"], dict(first_asset)]
        with self.assertRaisesRegex(ValueError, "repeats an asset name"):
            MODULE.validate_existing_release(
                {**release, "assets": duplicate_assets},
                expected_tag="v1.1.1",
                expected_body="complete notes",
                expected_assets=expected_assets,
            )

        for unsafe_name in (
            "",
            ".",
            "../one.zip",
            "folder/one.zip",
            r"folder\one.zip",
            r"C:\absolute.zip",
        ):
            with self.subTest(unsafe_name=unsafe_name):
                unsafe_assets = [
                    {**first_asset, "name": unsafe_name},
                    release["assets"][1],
                ]
                with self.assertRaisesRegex(ValueError, "asset name is invalid"):
                    MODULE.validate_existing_release(
                        {**release, "assets": unsafe_assets},
                        expected_tag="v1.1.1",
                        expected_body="complete notes",
                        expected_assets=expected_assets,
                    )

    def test_v111_release_rejects_malformed_rest_size_and_digest_metadata(
        self,
    ) -> None:
        release = valid_v111_release()
        expected_assets = valid_v111_expected_assets()
        first_asset = release["assets"][0]
        missing_digest = {
            key: value for key, value in first_asset.items() if key != "digest"
        }
        mutations = (
            ("size", True, "size"),
            ("size", -1, "size"),
            ("size", "3", "size"),
            ("digest", None, "digest"),
            ("digest", f"sha256:{'A' * 64}", "digest"),
            ("digest", f"SHA256:{'a' * 64}", "digest"),
            ("digest", "a" * 64, "digest"),
        )

        for key, value, message in mutations:
            with self.subTest(key=key, value=value):
                malformed_assets = [
                    {**first_asset, key: value},
                    release["assets"][1],
                ]
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_release(
                        {**release, "assets": malformed_assets},
                        expected_tag="v1.1.1",
                        expected_body="complete notes",
                        expected_assets=expected_assets,
                    )

        with self.assertRaisesRegex(ValueError, "digest"):
            MODULE.validate_existing_release(
                {**release, "assets": [missing_digest, release["assets"][1]]},
                expected_tag="v1.1.1",
                expected_body="complete notes",
                expected_assets=expected_assets,
            )

    def test_v111_release_rejects_malformed_expected_asset_metadata(self) -> None:
        release = valid_v111_release()
        valid_expected = valid_v111_expected_assets()
        with self.assertRaisesRegex(ValueError, "Release metadata is malformed"):
            MODULE.validate_existing_release(
                [],
                expected_tag="v1.1.1",
                expected_body="complete notes",
                expected_assets=valid_expected,
            )
        malformed_expected_assets = (
            None,
            [],
            {**valid_expected, "one.zip": None},
            {**valid_expected, "one.zip": []},
            {**valid_expected, "one.zip": {"size": True, "sha256": "a" * 64}},
            {**valid_expected, "one.zip": {"size": -1, "sha256": "a" * 64}},
            {**valid_expected, "one.zip": {"size": "3", "sha256": "a" * 64}},
            {**valid_expected, "one.zip": {"size": 3}},
            {**valid_expected, "one.zip": {"size": 3, "sha256": "A" * 64}},
            {
                **valid_expected,
                "one.zip": {"size": 3, "sha256": f"sha256:{'a' * 64}"},
            },
        )

        for malformed in malformed_expected_assets:
            with self.subTest(malformed=malformed):
                with self.assertRaisesRegex(ValueError, "candidate asset"):
                    MODULE.validate_existing_release(
                        release,
                        expected_tag="v1.1.1",
                        expected_body="complete notes",
                        expected_assets=malformed,
                    )

    def test_v110_release_does_not_retroactively_require_strict_rest_fields(
        self,
    ) -> None:
        self.assertEqual(
            ["historical.zip"],
            MODULE.validate_existing_release(
                {
                    "tag_name": "v1.1.0",
                    "draft": False,
                    "prerelease": False,
                    "immutable": "not-a-boolean",
                    "body": "historical notes",
                    "assets": [
                        {
                            "name": "historical.zip",
                            "state": None,
                            "size": True,
                            "digest": "not-a-digest",
                        }
                    ],
                },
                expected_tag="v1.1.0",
                expected_body="historical notes",
                expected_assets={"historical.zip": None},
            ),
        )

    def test_published_asset_metadata_adds_manifest_and_outer_checksum(self) -> None:
        with tempfile.TemporaryDirectory(prefix="published-metadata-") as temporary:
            root = Path(temporary)
            payload = b"payload"
            payload_name = "one.zip"
            manifest = root / "NvtFwCombiner-v1.1.1-candidate.json"
            manifest.write_text(
                json.dumps(
                    {
                        "version": "1.1.1",
                        "assets": [
                            {
                                "name": payload_name,
                                "size": len(payload),
                                "sha256": hashlib.sha256(payload).hexdigest(),
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            checksum = root / "NvtFwCombiner-v1.1.1-assets.sha256"
            checksum.write_bytes(b"outer")

            metadata = MODULE.expected_published_asset_metadata(manifest)

            self.assertEqual(
                {payload_name, manifest.name, checksum.name}, set(metadata)
            )
            for path in (manifest, checksum):
                self.assertEqual(path.stat().st_size, metadata[path.name]["size"])
                self.assertEqual(MODULE._sha256(path), metadata[path.name]["sha256"])

    def test_published_asset_metadata_rejects_duplicate_manifest_entries(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="published-metadata-duplicate-"
        ) as temporary:
            root = Path(temporary)
            manifest = root / "NvtFwCombiner-v1.1.1-candidate.json"
            checksum = root / "NvtFwCombiner-v1.1.1-assets.sha256"
            checksum.write_bytes(b"outer")
            entry = {"name": "one.zip", "size": 3, "sha256": "a" * 64}
            manifest.write_text(
                json.dumps({"version": "1.1.1", "assets": [entry, entry]}),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(ValueError, "repeats an asset name"):
                MODULE.expected_published_asset_metadata(manifest)

    def test_published_asset_metadata_rejects_manifest_or_checksum_name_conflict(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="published-metadata-name-conflict-"
        ) as temporary:
            root = Path(temporary)
            manifest = root / "NvtFwCombiner-v1.1.1-candidate.json"
            checksum = root / "NvtFwCombiner-v1.1.1-assets.sha256"
            checksum.write_bytes(b"outer")

            for conflicting_name in (manifest.name, checksum.name):
                with self.subTest(conflicting_name=conflicting_name):
                    manifest.write_text(
                        json.dumps(
                            {
                                "version": "1.1.1",
                                "assets": [
                                    {
                                        "name": conflicting_name,
                                        "size": 3,
                                        "sha256": "a" * 64,
                                    }
                                ],
                            }
                        ),
                        encoding="utf-8",
                    )
                    with self.assertRaisesRegex(
                        ValueError, "repeats a published asset name"
                    ):
                        MODULE.expected_published_asset_metadata(manifest)

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

    def test_candidate_manifest_closes_collected_source_ci_evidence(self) -> None:
        source = self.source_ci_evidence()
        run, jobs = source["run"], source["jobs"]
        raw_run = {
            **run,
            "actor": {"login": "sentinel-run-actor"},
            "head_commit": {"author": {"email": "sentinel@example.invalid"}},
            "repository": {
                **run["repository"],
                "name": "sentinel-run-repository",
            },
            "head_repository": {
                **run["head_repository"],
                "name": "sentinel-head-repository",
            },
        }
        raw_jobs = [
            {
                **job,
                "runner_name": "sentinel-runner",
                "steps": [{"name": "sentinel-step"}],
                "html_url": "https://example.invalid/sentinel-job",
            }
            for job in jobs
        ]
        responses = [
            {"total_count": 1, "workflow_runs": [raw_run]},
            {"total_count": 1, "workflow_runs": []}, raw_run,
            {"total_count": 3, "jobs": raw_jobs[:2]},
            {"total_count": 3, "jobs": raw_jobs[2:]},
            {"total_count": 3, "jobs": []}, raw_run,
        ]
        with tempfile.TemporaryDirectory(
            prefix="release-candidate-source-ci-"
        ) as temporary, mock.patch.object(
            MODULE, "_read_github_json", side_effect=responses
        ):
            temporary_root = Path(temporary)
            asset_dir = temporary_root / "assets"
            asset_dir.mkdir()
            version = "1.1.3"
            for name in MODULE._candidate_asset_names(version):
                (asset_dir / name).write_bytes(name.encode("utf-8"))
            notes = asset_dir / "RELEASE-NOTES.md"
            notes.write_text("release notes\n", encoding="utf-8")
            review = valid_snapshot()
            review["repositoryAdmission"]["sourceCi"] = MODULE._collect_source_ci(
                "owner/repo", SHA
            )
            review_path = temporary_root / "review.json"
            review_path.write_text(json.dumps(review), encoding="utf-8")

            manifest_path = MODULE.create_candidate_manifest(
                asset_dir,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review_path,
            )

            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            actual_source = manifest["reviewEvidence"]["repositoryAdmission"]["sourceCi"]
            self.assertEqual(source, actual_source)
            serialized = json.dumps(manifest)
            for sentinel in (
                "sentinel-run-actor",
                "sentinel@example.invalid",
                "sentinel-run-repository",
                "sentinel-head-repository",
                "sentinel-runner",
                "sentinel-step",
                "sentinel-job",
            ):
                self.assertNotIn(sentinel, serialized)

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
