"""Static regression checks for coverage-policy CI prerequisites."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

from coverage_configuration_policy import (  # noqa: E402
    validate_coverage_collector_pin,
    validate_coverage_exclusion_policy,
    validate_restored_test_coverage_collector_version,
)
from coverage_policy import load_baseline  # noqa: E402

CI_WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"
CI_WORKFLOW_TEMPLATE = ROOT / "docs" / "ci" / "workflow-templates" / "ci.yml"
SCHEDULED_SECURITY_TEMPLATE = (
    ROOT / "docs" / "ci" / "workflow-templates" / "scheduled-security.yml"
)
MAIN_PACKAGE_WORKFLOW = ROOT / ".github" / "workflows" / "main-package.yml"
VERIFIER = ROOT / "scripts" / "verify.py"


class CoverageCiContractTests(unittest.TestCase):
    def test_dotnet_job_fetches_the_fixed_coverage_baseline_revision(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        dotnet_job = workflow[workflow.index("  dotnet:") :]

        self.assertIn("fetch-depth: 0", dotnet_job)

    def test_dotnet_ci_uses_three_closed_producers_and_one_stable_finalizer(
        self,
    ) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("  dotnet-build:", workflow)
        self.assertIn("  dotnet-test:", workflow)
        self.assertIn("fail-fast: false", workflow)
        self.assertIn("shard: [bootstrap, ui, core]", workflow)
        self.assertIn("python scripts/verify.py --ci-dotnet-build", workflow)
        self.assertIn(
            "python scripts/verify.py --ci-dotnet-test-shard ${{ matrix.shard }}",
            workflow,
        )
        self.assertIn("  dotnet:", workflow)
        finalizer = workflow[workflow.index("  dotnet:") :]
        finalizer_header = finalizer[: finalizer.index("    steps:")]
        self.assertIn("name: dotnet / build-test", finalizer)
        self.assertIn("needs: [dotnet-build, dotnet-test]", finalizer_header)
        self.assertIn("if: >-\n      always() &&", finalizer_header)
        self.assertIn("python scripts/verify.py --ci-dotnet-finalize", finalizer)
        self.assertIn("pattern: dotnet-*-evidence", finalizer)
        self.assertIn("path: artifacts/ci-dotnet-downloads/", finalizer)
        self.assertNotIn("merge-multiple: true", finalizer)
        self.assertEqual(2, workflow.count("path: artifacts/ci-dotnet-upload/"))
        self.assertNotIn(".csproj", workflow)

    def test_reviewed_template_preserves_draft_and_artifact_topology(self) -> None:
        workflow = CI_WORKFLOW_TEMPLATE.read_text(encoding="utf-8")

        self.assertIn(
            "types: [opened, synchronize, reopened, ready_for_review]",
            workflow,
        )
        self.assertEqual(
            3,
            workflow.count(
                "if: github.event_name == 'push' || "
                "github.event.pull_request.draft == false"
            ),
        )
        self.assertIn("if: >-\n      always() &&", workflow)
        self.assertIn("path: artifacts/ci-dotnet-downloads/", workflow)
        self.assertNotIn("merge-multiple: true", workflow)
        self.assertNotIn("# immutable reviewed commit", workflow)

    def test_python_worker_preserves_the_required_gate_on_windows(self) -> None:
        for workflow_path in (CI_WORKFLOW, CI_WORKFLOW_TEMPLATE):
            with self.subTest(workflow=workflow_path):
                workflow = workflow_path.read_text(encoding="utf-8")
                python_worker = workflow[
                    workflow.index("  python-worker:") : workflow.index(
                        "  dotnet-build:"
                    )
                ]
                header = python_worker[: python_worker.index("    steps:")]

                self.assertIn("name: python-worker / verify", header)
                self.assertIn("runs-on: windows-latest", header)
                self.assertNotIn("ubuntu", header)
                self.assertIn(
                    "python scripts/verify.py --skip-dotnet --skip-structure",
                    python_worker,
                )

    def test_repository_validation_templates_install_pinned_yaml_dependency(
        self,
    ) -> None:
        install = (
            "python -m pip install --disable-pip-version-check "
            "--only-binary=:all: PyYAML==6.0.3"
        )
        for workflow_path in (CI_WORKFLOW, CI_WORKFLOW_TEMPLATE):
            workflow = workflow_path.read_text(encoding="utf-8")
            structure = workflow[
                workflow.index("  structure:") : workflow.index("  python-worker:")
            ]
            dotnet_build = workflow[
                workflow.index("  dotnet-build:") : workflow.index("  dotnet-test:")
            ]
            self.assertIn(install, structure)
            self.assertIn(install, dotnet_build)

        scheduled = SCHEDULED_SECURITY_TEMPLATE.read_text(encoding="utf-8")
        policy = scheduled[scheduled.index("  policy:") :]
        self.assertIn(install, policy)
        self.assertIn("python scripts/verify.py --structure-only", policy)

    def test_package_job_fetches_the_fixed_coverage_baseline_revision(self) -> None:
        workflow = MAIN_PACKAGE_WORKFLOW.read_text(encoding="utf-8")
        package_job = workflow[workflow.index("  package:") :]

        self.assertIn("python ./scripts/verify.py --all", package_job)
        self.assertIn("fetch-depth: 0", package_job)

    def test_structure_job_does_not_restore_or_own_evaluated_project_policy(
        self,
    ) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        structure_job = workflow[
            workflow.index("  structure:") : workflow.index("  python-worker:")
        ]

        self.assertNotIn("install-dotnet", structure_job)
        self.assertNotIn("dotnet restore", structure_job)
        self.assertIn("python scripts/verify.py --structure-only", structure_job)

    def test_dotnet_lane_restores_before_its_evaluated_source_check(self) -> None:
        verifier = VERIFIER.read_text(encoding="utf-8")
        dotnet_plan = verifier[
            verifier.index("def dotnet_build_commands(") : verifier.index(
                "def run_dotnet_commands("
            )
        ]

        self.assertEqual(1, dotnet_plan.count('"--evaluated-source-ownership-only"'))
        self.assertLess(
            dotnet_plan.index('[dotnet, "restore", str(SOLUTION)]'),
            dotnet_plan.index('"--evaluated-source-ownership-only"'),
        )

    def test_release_build_owns_style_and_analyzer_diagnostics(self) -> None:
        build_props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
        editor_config = (ROOT / ".editorconfig").read_text(encoding="utf-8")

        self.assertIn(
            "<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>",
            build_props,
        )
        self.assertIn(
            "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", build_props
        )
        self.assertIn("<AnalysisLevel>latest-recommended</AnalysisLevel>", build_props)
        self.assertIn("dotnet_analyzer_diagnostic.severity = warning", editor_config)

    def test_ci_retains_short_lived_real_coverage_evidence(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("name: python-coverage", workflow)
        self.assertIn("path: artifacts/coverage/python/", workflow)
        self.assertIn("name: dotnet-coverage", workflow)
        self.assertIn("path: artifacts/coverage/dotnet/", workflow)
        self.assertEqual(5, workflow.count("retention-days: 3"))

    def test_collector_pin_matches_the_baseline_and_test_reference(self) -> None:
        errors: list[str] = []

        validate_coverage_collector_pin(load_baseline(), errors, ROOT)

        self.assertEqual([], errors)

    def test_collector_pin_rejects_version_and_test_reference_drift(self) -> None:
        valid_reference = (
            '<PackageReference Include="coverlet.collector" PrivateAssets="all" />'
        )
        for central_version, reference in (
            ("6.0.5", valid_reference),
            ("6.0.4", ""),
            (
                "6.0.4",
                '<PackageReference Include="coverlet.collector" PrivateAssets="none" />',
            ),
        ):
            with self.subTest(
                central_version=central_version,
                has_reference=bool(reference),
            ):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    self.write_collector_fixture(root, central_version, reference)
                    errors: list[str] = []

                    validate_coverage_collector_pin(load_baseline(), errors, root)

                    self.assertTrue(
                        any("coverage collector" in error for error in errors)
                    )

    def test_collector_pin_rejects_central_update_override(self) -> None:
        valid_reference = (
            '<PackageReference Include="coverlet.collector" PrivateAssets="all" />'
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.write_collector_fixture(root, "6.0.4", valid_reference)
            (root / "Directory.Packages.props").write_text(
                """
<Project><ItemGroup>
  <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  <PackageVersion Update="coverlet.collector" Version="6.0.5" />
</ItemGroup></Project>
""".strip(),
                encoding="utf-8",
            )
            errors: list[str] = []

            validate_coverage_collector_pin(load_baseline(), errors, root)

            self.assertTrue(any("coverage collector" in error for error in errors))

    def test_restored_collector_version_must_match_the_baseline(self) -> None:
        for resolved_version, expected_errors in (("6.0.4", 0), ("6.0.5", 1)):
            with self.subTest(resolved_version=resolved_version):
                with tempfile.TemporaryDirectory() as temporary:
                    assets_file = Path(temporary) / "project.assets.json"
                    assets_file.write_text(
                        json.dumps(
                            {
                                "libraries": {
                                    f"coverlet.collector/{resolved_version}": {}
                                }
                            }
                        ),
                        encoding="utf-8",
                    )
                    errors: list[str] = []

                    validate_restored_test_coverage_collector_version(
                        "tests/Product.Tests/Product.Tests.csproj",
                        assets_file,
                        "coverlet.collector",
                        "6.0.4",
                        errors,
                    )

                    self.assertEqual(expected_errors, len(errors))

    def test_collector_pin_rejects_python_coverage_omit_configuration(
        self,
    ) -> None:
        valid_reference = (
            '<PackageReference Include="coverlet.collector" PrivateAssets="all" />'
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.write_collector_fixture(root, "6.0.4", valid_reference)
            pyproject = root / "tools/crc-worker/pyproject.toml"
            pyproject.write_text(
                pyproject.read_text(encoding="utf-8")
                + '\nomit = ["src/nfc_crc_worker/untested.py"]\n',
                encoding="utf-8",
            )
            errors: list[str] = []

            validate_coverage_collector_pin(load_baseline(), errors, root)

            self.assertTrue(any("Python coverage source" in error for error in errors))

    def test_python_coverage_override_config_is_rejected_from_pytest_addopts(
        self,
    ) -> None:
        configurations = (
            (
                "pytest-ini",
                "tools/crc-worker/pytest.ini",
                "[pytest]\naddopts = --cov-config=coverage-alt\n",
            ),
            (
                "hidden-pytest-ini",
                "tools/crc-worker/.pytest.ini",
                "[pytest]\naddopts = --cov-config=coverage-alt\n",
            ),
            (
                "tox-ini",
                "tools/crc-worker/tox.ini",
                "[pytest]\naddopts = --cov-config=coverage-alt\n",
            ),
            (
                "setup-cfg",
                "tools/crc-worker/setup.cfg",
                "[tool:pytest]\naddopts = --cov-config=coverage-alt\n",
            ),
            (
                "pyproject-compatibility",
                "tools/crc-worker/pyproject.toml",
                "[tool.pytest.ini_options]\n"
                'addopts = ["-ra", "--cov-config=coverage-alt"]\n',
            ),
            (
                "pyproject-native",
                "tools/crc-worker/pyproject.toml",
                '[tool.pytest]\naddopts = ["-ra", "--cov-config=coverage-alt"]\n',
            ),
        )
        for label, relative, content in configurations:
            with self.subTest(label=label):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    configuration = root / relative
                    configuration.parent.mkdir(parents=True, exist_ok=True)
                    configuration.write_text(content, encoding="utf-8")
                    alternate_configuration = configuration.parent / "coverage-alt"
                    alternate_configuration.write_text(
                        "[run]\nomit = untested.py\n[report]\nfail_under = 0\n",
                        encoding="utf-8",
                    )
                    errors: list[str] = []

                    validate_coverage_exclusion_policy(
                        root,
                        [relative, alternate_configuration.relative_to(root)],
                        errors,
                    )

                    self.assertEqual(1, len(errors))
                    self.assertIn("pytest addopts", errors[0])

    def test_collector_pin_rejects_an_additional_packaged_worker_root(self) -> None:
        valid_reference = (
            '<PackageReference Include="coverlet.collector" PrivateAssets="all" />'
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.write_collector_fixture(root, "6.0.4", valid_reference)
            pyproject = root / "tools/crc-worker/pyproject.toml"
            pyproject.write_text(
                pyproject.read_text(encoding="utf-8").replace(
                    'packages = ["src/nfc_crc_worker"]',
                    'packages = ["src/nfc_crc_worker", "src/hidden_runtime"]',
                ),
                encoding="utf-8",
            )
            errors: list[str] = []

            validate_coverage_collector_pin(load_baseline(), errors, root)

            self.assertTrue(
                any("coverage-owned src/nfc_crc_worker" in error for error in errors)
            )

    def test_collector_pin_rejects_hatch_force_include_runtime_escape(self) -> None:
        valid_reference = (
            '<PackageReference Include="coverlet.collector" PrivateAssets="all" />'
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.write_collector_fixture(root, "6.0.4", valid_reference)
            pyproject = root / "tools/crc-worker/pyproject.toml"
            pyproject.write_text(
                pyproject.read_text(encoding="utf-8")
                + (
                    "\n[tool.hatch.build.targets.wheel.force-include]\n"
                    '"src/hidden_runtime.py" = '
                    '"nfc_crc_worker/hidden_runtime.py"\n'
                ),
                encoding="utf-8",
            )
            errors: list[str] = []

            validate_coverage_collector_pin(load_baseline(), errors, root)

            self.assertTrue(
                any("closed Hatch build configuration" in error for error in errors)
            )

    def test_coverage_exclusion_policy_rejects_source_and_filter_escape_hatches(
        self,
    ) -> None:
        fixtures = {
            "src/Product/Hidden.cs": (
                "using System.Diagnostics.CodeAnalysis;\n"
                "[ExcludeFromCodeCoverage]\ninternal sealed class Hidden {}\n"
            ),
            "filters/ExcludeByFile.props": (
                "<Project><PropertyGroup><ExcludeByFile>**/Hidden.cs</ExcludeByFile>"
                "</PropertyGroup></Project>"
            ),
            "filters/Exclude.props": (
                "<Project><PropertyGroup><exclude>[Product]*</exclude>"
                "</PropertyGroup></Project>"
            ),
            "filters/Include.props": (
                "<Project><PropertyGroup><iNcLuDe>[Product]*</iNcLuDe>"
                "</PropertyGroup></Project>"
            ),
            "filters/SkipAutoProps.props": (
                "<Project><PropertyGroup><skipautoprops>true</skipautoprops>"
                "</PropertyGroup></Project>"
            ),
            "coverage.runsettings": "<RunSettings />",
            "tools/crc-worker/.coveragerc": "[run]\nomit = untested.py\n",
            "tox.ini": "[coverage:run]\nomit = untested.py\n",
            "scripts/verify.py": "dotnet test --settings coverage.xml\n",
            ".github/workflows/ci.yml": "run: dotnet test -p:Exclude=[Product]*\n",
            ".github/workflows/python.yml": (
                "run: pytest --cov-config=tools/crc-worker/.coveragerc\n"
            ),
            ".github/workflows/python-env.yml": (
                "env:\n  COVERAGE_RCFILE: coverage-alt.ini\n"
            ),
            ".github/workflows/pytest-env.yml": ("env:\n  PYTEST_ADDOPTS: --no-cov\n"),
            ".github/workflows/runsettings.yml": (
                "run: dotnet test -- "
                "DataCollectionRunSettings.DataCollectors.DataCollector."
                "Configuration.Exclude=[Product]*\n"
            ),
        }
        for relative, content in fixtures.items():
            with self.subTest(relative=relative):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_text(content, encoding="utf-8")
                    errors: list[str] = []

                    validate_coverage_exclusion_policy(root, [relative], errors)

                    self.assertEqual(1, len(errors))
                    self.assertIn("coverage", errors[0])

    def test_coverage_exclusion_policy_accepts_canonical_verifier_guard(
        self,
    ) -> None:
        errors: list[str] = []

        validate_coverage_exclusion_policy(ROOT, [VERIFIER], errors)

        self.assertEqual([], errors)

    def test_coverage_exclusion_policy_accepts_normal_production_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "src/Product/Thing.cs"
            source.parent.mkdir(parents=True)
            source.write_text("internal sealed class Thing {}\n", encoding="utf-8")
            project = root / "src/Product/Product.csproj"
            project.write_text(
                '<Project><ItemGroup><Compile Include="Thing.cs" /></ItemGroup>'
                "<PropertyGroup><SkipAutoProps>false</SkipAutoProps></PropertyGroup>"
                "</Project>",
                encoding="utf-8",
            )
            verifier = root / "scripts/verify.py"
            verifier.parent.mkdir()
            verifier.write_text(
                "dotnet test -- "
                "DataCollectionRunSettings.DataCollectors.DataCollector."
                "Configuration.Format=json,cobertura\n",
                encoding="utf-8",
            )
            errors: list[str] = []

            validate_coverage_exclusion_policy(
                root,
                [
                    "src/Product/Thing.cs",
                    "src/Product/Product.csproj",
                    "scripts/verify.py",
                ],
                errors,
            )

            self.assertEqual([], errors)

    @staticmethod
    def write_collector_fixture(
        root: Path, central_version: str, reference: str
    ) -> None:
        (root / "tools/crc-worker").mkdir(parents=True)
        (root / "tools/crc-worker/pyproject.toml").write_text(
            """
[project]
name = "fixture"
version = "0"
[project.optional-dependencies]
dev = ["coverage==7.14.3", "pytest-cov==6.3.0"]
[tool.hatch.build.targets.wheel]
packages = ["src/nfc_crc_worker"]
[tool.pytest.ini_options]
addopts = "-ra --strict-config --strict-markers"
[tool.coverage.run]
branch = true
patch = ["subprocess"]
source = ["nfc_crc_worker"]
[tool.coverage.report]
fail_under = 95
show_missing = true
skip_covered = false
""".strip(),
            encoding="utf-8",
        )
        (root / "Directory.Packages.props").write_text(
            (
                "<Project><ItemGroup><PackageVersion "
                f'Include="coverlet.collector" Version="{central_version}" />'
                "</ItemGroup></Project>"
            ),
            encoding="utf-8",
        )
        (root / "Directory.Build.props").write_text(
            (
                "<Project><ItemGroup Condition=\"'$(IsTestProject)' == 'true'\">"
                f"{reference}</ItemGroup></Project>"
            ),
            encoding="utf-8",
        )


if __name__ == "__main__":
    unittest.main()
