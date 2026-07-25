"""Tests that protect the code-size metric from source-ownership escapes."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as element_tree
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

from validate_repository import (  # noqa: E402
    evaluate_project_items,
    validate_evaluated_production_source_ownership,
    validate_production_source_ownership,
)


class ProductionSourceOwnershipTests(unittest.TestCase):
    def validate(self, relative: str, document: str) -> list[str]:
        errors: list[str] = []
        validate_production_source_ownership(
            relative, element_tree.fromstring(document), errors
        )
        return errors

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
        self.assertIn("evaluated Compile item", errors[0])

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


if __name__ == "__main__":
    unittest.main()
