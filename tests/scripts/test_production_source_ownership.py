"""Tests that protect the code-size metric from source-ownership escapes."""

from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as element_tree
from pathlib import Path
import sys
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

from coverage_configuration_policy import (  # noqa: E402
    validate_evaluated_test_coverage_collector,
)
import validate_repository as repository_validator  # noqa: E402
from validate_repository import (  # noqa: E402
    evaluate_project_items,
    is_solution_test_project,
    validate_evaluated_nonproduction_source_ownership,
    validate_evaluated_production_source_ownership,
    validate_production_source_ownership,
)


class ProductionSourceOwnershipTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.project_directory = Path(self.temporary_directory.name) / "Product"
        self.project_directory.mkdir()
        self.sdks_directory = Path(self.temporary_directory.name) / "Sdks"

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def validate(self, relative: str, document: str) -> list[str]:
        errors: list[str] = []
        validate_production_source_ownership(
            relative, element_tree.fromstring(document), errors
        )
        return errors

    def test_solution_test_project_identity_does_not_depend_on_project_property(
        self,
    ) -> None:
        self.assertTrue(
            is_solution_test_project(
                "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj"
            )
        )
        self.assertFalse(
            is_solution_test_project(
                "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj"
            )
        )
        self.assertFalse(
            is_solution_test_project(
                "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj"
            )
        )

    def test_rejects_external_production_compile_include(self) -> None:
        errors = self.validate(
            "src/Product/Product.csproj",
            '<Project><ItemGroup><Compile Include="../../scripts/Hidden.cs" /></ItemGroup></Project>',
        )

        self.assertEqual(1, len(errors))
        self.assertIn("explicit Compile include", errors[0])

    def test_rejects_source_generating_analyzer_in_production(self) -> None:
        errors = self.validate(
            "src/Product/Product.csproj",
            '<Project><ItemGroup><Analyzer Include="Generator.dll" /></ItemGroup></Project>',
        )

        self.assertEqual(1, len(errors))
        self.assertIn("source-generating analyzer", errors[0])

    def test_does_not_apply_to_test_project_source_links(self) -> None:
        errors = self.validate(
            "tests/Product.Tests/Product.Tests.csproj",
            '<Project><ItemGroup><Compile Include="../TestSupport/Fixture.cs" /></ItemGroup></Project>',
        )

        self.assertEqual([], errors)

    def test_rejects_nonproduction_project_compiling_production_source(
        self,
    ) -> None:
        root = Path(self.temporary_directory.name)
        source = root / "src/Product/Thing.cs"
        source.parent.mkdir(parents=True)
        source.write_text("internal sealed class Thing {}\n", encoding="utf-8")
        errors: list[str] = []

        validate_evaluated_nonproduction_source_ownership(
            "tests/Product.Tests/Product.Tests.csproj",
            {"Compile": [{"FullPath": str(source)}]},
            root,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("duplicate production source", errors[0])

    def test_rejects_imported_compile_source_outside_production_project(self) -> None:
        errors: list[str] = []
        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            ROOT / "src" / "Product",
            {
                "Compile": [
                    {"FullPath": str(ROOT / "scripts" / "Hidden.cs")},
                ],
                "Analyzer": [],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("physical C#", errors[0])

    def test_accepts_physical_csharp_inside_production_project(self) -> None:
        source = self.project_directory / "Thing.cs"
        source.write_text("internal sealed class Thing {}\n", encoding="utf-8")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [{"FullPath": str(source)}],
                "Analyzer": [],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual([], errors)

    def test_rejects_in_project_non_csharp_compile_item(self) -> None:
        source = self.project_directory / "Thing.txt"
        source.write_text("compiled as C#\n", encoding="utf-8")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [{"FullPath": str(source)}],
                "Analyzer": [],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("physical C#", errors[0])

    def test_rejects_in_project_generated_compile_item(self) -> None:
        source = self.project_directory / "obj" / "Generated.cs"
        source.parent.mkdir()
        source.write_text("internal sealed class Generated {}\n", encoding="utf-8")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [{"FullPath": str(source)}],
                "Analyzer": [],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("physical C#", errors[0])

    def test_rejects_package_provided_evaluated_analyzer(self) -> None:
        errors: list[str] = []
        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            ROOT / "src" / "Product",
            {
                "Compile": [],
                "Analyzer": [
                    {
                        "Identity": "generator.dll",
                        "IsImplicitlyDefined": "false",
                    }
                ],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("evaluated analyzer", errors[0])

    def test_rejects_forged_implicit_analyzer_metadata(self) -> None:
        analyzer = self.project_directory / "generator.dll"
        analyzer.write_bytes(b"not an SDK analyzer")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [],
                "Analyzer": [
                    {
                        "Identity": str(analyzer),
                        "FullPath": str(analyzer),
                        "DefiningProjectFullPath": str(
                            self.project_directory / "forged.targets"
                        ),
                        "IsImplicitlyDefined": "true",
                    }
                ],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("evaluated analyzer", errors[0])

    def test_accepts_selected_sdk_allowlisted_analyzer(self) -> None:
        sdk_root = self.sdks_directory / "Microsoft.NET.Sdk"
        analyzer = sdk_root / "analyzers/Microsoft.CodeAnalysis.NetAnalyzers.dll"
        target = sdk_root / "targets/Microsoft.NET.Sdk.Analyzers.targets"
        analyzer.parent.mkdir(parents=True)
        target.parent.mkdir(parents=True)
        analyzer.write_bytes(b"SDK analyzer")
        target.write_text("<Project />", encoding="utf-8")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [],
                "Analyzer": [
                    {
                        "Identity": str(analyzer),
                        "FullPath": str(analyzer),
                        "DefiningProjectFullPath": str(target),
                        "IsImplicitlyDefined": "true",
                    }
                ],
            },
            self.sdks_directory,
            errors,
        )

        self.assertEqual([], errors)

    def test_accepts_only_the_exact_pinned_package_analyzer_asset(self) -> None:
        root = Path(self.temporary_directory.name)
        analyzer = (
            root
            / ".packages/avalonia/12.0.5/analyzers/dotnet/cs"
            / "Avalonia.Generators.dll"
        )
        analyzer.parent.mkdir(parents=True)
        analyzer.write_bytes(b"pinned generator")
        item = {
            "Identity": str(analyzer),
            "FullPath": str(analyzer),
            "NuGetPackageId": "Avalonia",
            "NuGetPackageVersion": "12.0.5",
        }
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {"Compile": [], "Analyzer": [item]},
            self.sdks_directory,
            errors,
            root,
        )

        self.assertEqual([], errors)

    def test_rejects_allowlisted_package_analyzer_through_external_link(
        self,
    ) -> None:
        base = Path(self.temporary_directory.name)
        repository_root = base / "repository"
        linked_version = repository_root / ".packages/avalonia/12.0.5"
        linked_version.parent.mkdir(parents=True)
        external_version = base / "external/avalonia/12.0.5"
        analyzer = external_version / "analyzers/dotnet/cs" / "Avalonia.Generators.dll"
        analyzer.parent.mkdir(parents=True)
        analyzer.write_bytes(b"external generator")
        try:
            linked_version.symlink_to(external_version, target_is_directory=True)
        except OSError as exc:
            self.skipTest(f"directory links are unavailable: {exc}")
        errors: list[str] = []

        validate_evaluated_production_source_ownership(
            "src/Product/Product.csproj",
            self.project_directory,
            {
                "Compile": [],
                "Analyzer": [
                    {
                        "Identity": str(
                            linked_version / analyzer.relative_to(external_version)
                        ),
                        "FullPath": str(
                            linked_version / analyzer.relative_to(external_version)
                        ),
                        "NuGetPackageId": "Avalonia",
                        "NuGetPackageVersion": "12.0.5",
                    }
                ],
            },
            self.sdks_directory,
            errors,
            repository_root,
        )

        self.assertEqual(1, len(errors))
        self.assertIn("evaluated analyzer", errors[0])

    def test_requires_restored_assets_before_evaluating_production_items(self) -> None:
        errors: list[str] = []
        result = evaluate_project_items(
            ROOT / "src" / "NvtFwCombiner.Unrestored" / "Unrestored.csproj",
            errors,
        )

        self.assertIsNone(result)
        self.assertEqual(1, len(errors))
        self.assertIn("requires restored assets", errors[0])

    def test_evaluates_restored_items_in_release_configuration(self) -> None:
        root = Path(self.temporary_directory.name)
        project = root / "src/Product/Product.csproj"
        (project.parent / "obj").mkdir(parents=True)
        (project.parent / "obj/project.assets.json").write_text(
            "{}",
            encoding="utf-8",
        )
        project.write_text("<Project />", encoding="utf-8")
        output = json.dumps(
            {
                "Properties": {"MSBuildSDKsPath": str(self.sdks_directory)},
                "Items": {
                    "Compile": [],
                    "Analyzer": [],
                    "PackageReference": [],
                },
            }
        )
        completed = subprocess.CompletedProcess(
            ["dotnet", "msbuild"],
            0,
            stdout=output,
            stderr="",
        )
        errors: list[str] = []

        with (
            patch.object(repository_validator, "ROOT", root),
            patch.object(
                repository_validator.subprocess,
                "run",
                return_value=completed,
            ) as run_command,
        ):
            result = repository_validator.evaluate_project_items(project, errors)

        self.assertIsNotNone(result)
        self.assertEqual([], errors)
        self.assertIn(
            "-property:Configuration=Release",
            run_command.call_args.args[0],
        )
        self.assertIn(
            "-target:ResolveLockFileAnalyzers",
            run_command.call_args.args[0],
        )

    def test_restored_contracts_evaluate_non_test_support_projects(self) -> None:
        root = Path(self.temporary_directory.name)
        source = root / "src/Product/Thing.cs"
        source.parent.mkdir(parents=True)
        source.write_text("internal sealed class Thing {}\n", encoding="utf-8")
        support_project = "tests/Product.Support/Product.Support.csproj"
        evaluated = repository_validator.EvaluatedProjectItems(
            {
                "Compile": [{"FullPath": str(source)}],
                "Analyzer": [],
                "PackageReference": [],
            },
            self.sdks_directory,
        )
        baseline = {
            "collection": {
                "dotnet": {
                    "collector": "coverlet.collector",
                    "version": "6.0.4",
                }
            }
        }
        errors: list[str] = []

        with (
            patch.object(repository_validator, "ROOT", root),
            patch.object(
                repository_validator,
                "EXPECTED_PROJECT_REFERENCES",
                {support_project: set()},
            ),
            patch.object(repository_validator, "load_baseline", return_value=baseline),
            patch.object(
                repository_validator,
                "evaluate_project_items",
                return_value=evaluated,
            ) as evaluate,
        ):
            repository_validator.validate_restored_project_contracts(errors)

        evaluate.assert_called_once_with(root / support_project, errors)
        self.assertEqual(1, len(errors))
        self.assertIn("duplicate production source", errors[0])

    def test_accepts_centrally_defined_test_coverage_collector(self) -> None:
        errors: list[str] = []

        validate_evaluated_test_coverage_collector(
            "tests/Product.Tests/Product.Tests.csproj",
            {
                "PackageReference": [
                    {
                        "Identity": "coverlet.collector",
                        "PrivateAssets": "all",
                        "DefiningProjectFullPath": str(ROOT / "Directory.Build.props"),
                    }
                ]
            },
            "coverlet.collector",
            ROOT,
            errors,
        )

        self.assertEqual([], errors)

    def test_rejects_missing_or_locally_overridden_test_coverage_collector(
        self,
    ) -> None:
        fixtures = (
            [],
            [
                {
                    "Identity": "coverlet.collector",
                    "PrivateAssets": "all",
                    "DefiningProjectFullPath": str(
                        ROOT / "tests/Product.Tests/Product.Tests.csproj"
                    ),
                    "VersionOverride": "6.0.5",
                }
            ],
        )
        for references in fixtures:
            with self.subTest(references=references):
                errors: list[str] = []

                validate_evaluated_test_coverage_collector(
                    "tests/Product.Tests/Product.Tests.csproj",
                    {"PackageReference": references},
                    "coverlet.collector",
                    ROOT,
                    errors,
                )

                self.assertEqual(1, len(errors))
                self.assertIn("centrally defined coverage collector", errors[0])


if __name__ == "__main__":
    unittest.main()
