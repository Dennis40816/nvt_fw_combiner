"""Tests that protect the code-size metric from source-ownership escapes."""

from __future__ import annotations

import tempfile
import unittest
import xml.etree.ElementTree as element_tree
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

from coverage_configuration_policy import (  # noqa: E402
    validate_evaluated_test_coverage_collector,
)
from validate_repository import (  # noqa: E402
    evaluate_project_items,
    is_solution_test_project,
    validate_evaluated_production_source_ownership,
    validate_production_source_ownership,
)


class ProductionSourceOwnershipTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.project_directory = Path(self.temporary_directory.name) / "Product"
        self.project_directory.mkdir()

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
            errors,
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
