"""Tests that protect the code-size metric from source-ownership escapes."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as element_tree
from pathlib import Path
import sys

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from validate_repository import validate_production_source_ownership  # noqa: E402


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


if __name__ == "__main__":
    unittest.main()
