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
MAIN_PACKAGE_WORKFLOW = ROOT / ".github" / "workflows" / "main-package.yml"
VERIFIER = ROOT / "scripts" / "verify.py"


class CoverageCiContractTests(unittest.TestCase):
    def test_dotnet_job_fetches_the_fixed_coverage_baseline_revision(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        dotnet_job = workflow[workflow.index("  dotnet:") :]

        self.assertIn("fetch-depth: 0", dotnet_job)

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
        dotnet_lane = verifier[
            verifier.index("def verify_dotnet(") : verifier.index(
                "def verify_windows_process_orchestration("
            )
        ]

        self.assertEqual(1, dotnet_lane.count('"--evaluated-source-ownership-only"'))
        self.assertLess(
            dotnet_lane.index('[dotnet, "restore", str(SOLUTION)]'),
            dotnet_lane.index('"--evaluated-source-ownership-only"'),
        )

    def test_ci_retains_short_lived_real_coverage_evidence(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("name: python-coverage", workflow)
        self.assertIn("path: artifacts/coverage/python/", workflow)
        self.assertIn("name: dotnet-coverage", workflow)
        self.assertIn("path: artifacts/coverage/dotnet/", workflow)
        self.assertEqual(3, workflow.count("retention-days: 3"))

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
            "scripts/verify.py": "dotnet test --settings coverage.xml\n",
            ".github/workflows/ci.yml": "run: dotnet test -p:Exclude=[Product]*\n",
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
