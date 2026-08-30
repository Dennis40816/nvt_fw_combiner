"""Regression tests for real coverage parsing and ratchet enforcement."""

from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.coverage_policy import (
    BASELINE_PATH,
    PRODUCTION_MODULES,
    ROOT,
    CoverageInventory,
    CoverageMeasure,
    CoverageSummary,
    _relative_source_path,
    changed_lines_from_zero_context_diff,
    changed_module_lines,
    load_baseline,
    parse_dotnet_cobertura_reports,
    parse_python_coverage_report,
    validate_inventory,
)


def summary(
    line_covered: int, line_total: int, branch_covered: int, branch_total: int
) -> CoverageSummary:
    return CoverageSummary(
        CoverageMeasure(line_covered, line_total),
        CoverageMeasure(branch_covered, branch_total),
    )


def baseline() -> dict[str, object]:
    dotnet = summary(9, 10, 8, 10).as_document()
    python = summary(19, 20, 9, 10).as_document()
    return {
        "schemaVersion": "1.0",
        "changeBaseRevision": "frozen",
        "languages": {
            "dotnet": {
                "overall": dotnet,
                "modules": {"Domain": dotnet, "Application": dotnet},
            },
            "python": {"overall": python, "modules": {"nfc_crc_worker": python}},
        },
        "changedModuleRatchets": {
            "Domain": {
                "baselineNonblankLines": 500,
                "lineMinimumPercent": 85,
                "branchMinimumPercent": 80,
            },
            "Application": {
                "baselineNonblankLines": 500,
                "lineMinimumPercent": 85,
                "branchMinimumPercent": 80,
            },
        },
    }


class CoveragePolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.write(
            "src/NvtFwCombiner.Domain/Thing.cs",
            "\n" * 9 + "internal sealed class Thing\n{\n}\n",
        )

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write(self, relative: str, content: str) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        return path

    def run_git(self, *arguments: str) -> str:
        return subprocess.run(
            ["git", *arguments],
            cwd=self.root,
            check=True,
            capture_output=True,
            text=True,
        ).stdout

    @staticmethod
    def branch(
        path: int, hits: int, *, line: int = 10, offset: int = 15
    ) -> dict[str, int]:
        return {
            "Line": line,
            "Offset": offset,
            "EndOffset": offset + 2,
            "Path": path,
            "Ordinal": 0,
            "Hits": hits,
        }

    @staticmethod
    def coverlet_json(classes: dict[str, object]) -> str:
        return json.dumps(
            {
                "NvtFwCombiner.Domain.dll": {
                    "src/NvtFwCombiner.Domain/Thing.cs": classes,
                }
            }
        )

    def test_parses_and_conservatively_merges_dotnet_cobertura_reports(self) -> None:
        domain_source_root = (self.root / "src/NvtFwCombiner.Domain").as_posix()
        first = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes><class name=\"Thing\" filename=\"src/NvtFwCombiner.Domain/Thing.cs\"><lines>
<line number=\"10\" hits=\"1\" branch=\"True\" condition-coverage=\"50% (1/2)\"><conditions><condition number=\"1\" type=\"jump\" coverage=\"50%\" /></conditions></line>
<line number=\"11\" hits=\"0\" />
</lines></class></classes></package></packages></coverage>"""
        second = f"""<coverage branches-covered="1" branches-valid="2"><sources><source>{domain_source_root}</source></sources><packages><package><classes><class name=\"Thing\" filename=\"Thing.cs\"><lines>
<line number=\"10\" hits=\"0\" branch=\"True\" condition-coverage=\"50% (1/2)\"><conditions><condition number=\"1\" type=\"jump\" coverage=\"50%\" /></conditions></line>
</lines></class></classes></package></packages></coverage>"""
        self.write("reports/a/coverage.cobertura.xml", first)
        self.write("reports/b/coverage.cobertura.xml", second)
        self.write(
            "reports/a/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1, "11": 0},
                            "Branches": [self.branch(0, 1), self.branch(1, 0)],
                        }
                    }
                }
            ),
        )
        self.write(
            "reports/b/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 0},
                            "Branches": [self.branch(0, 0), self.branch(1, 1)],
                        }
                    }
                }
            ),
        )
        generated = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes><class filename='src/NvtFwCombiner.Domain/obj/Generated.cs'><lines>
<line number='1' hits='1' />
</lines></class></classes></package></packages></coverage>"""
        self.write("reports/generated/coverage.cobertura.xml", generated)
        self.write("reports/generated/coverage.json", "{}")

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 2, 2, 2), inventory.overall)
        self.assertEqual(summary(1, 2, 2, 2), inventory.modules["Domain"])

    def test_parses_coverlet_deterministic_virtual_root_report(self) -> None:
        report = r"""<coverage branches-covered="0" branches-valid="0"><sources><source>\</source></sources><packages><package><classes>
<class name="Thing" filename="/_/src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" />
</lines></class>
<class name="Generated" filename="/_/src/NvtFwCombiner.Domain/obj/Generated.g.cs"><lines>
<line number="1" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 0, 0), inventory.overall)
        self.assertEqual(summary(1, 1, 0, 0), inventory.modules["Domain"])

    def test_classifies_distribution_launcher_as_bootstrap_host(self) -> None:
        self.write(
            "src/NvtFwCombiner.DistributionLauncher/Program.cs",
            "internal static class Program;\n",
        )
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Program" filename="src/NvtFwCombiner.DistributionLauncher/Program.cs"><lines>
<line number="1" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 0, 0), inventory.overall)
        self.assertEqual(summary(1, 1, 0, 0), inventory.modules["Bootstrap"])

    def test_classifies_distribution_launcher_physical_axaml_as_bootstrap(self) -> None:
        self.write(
            "src/NvtFwCombiner.DistributionLauncher/App.axaml",
            "<Application />\n",
        )
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="App" filename="src/NvtFwCombiner.DistributionLauncher/App.axaml"><lines>
<line number="1" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 0, 0), inventory.overall)
        self.assertEqual(summary(1, 1, 0, 0), inventory.modules["Bootstrap"])

    def test_canonicalizes_linked_axaml_and_preserves_assembly_branch_identity(
        self,
    ) -> None:
        physical = "src/NvtFwCombiner.Presentation.Avalonia/Styles/ThemeTokens.axaml"
        logical = "src/NvtFwCombiner.DistributionLauncher/Styles/ThemeTokens.axaml"
        self.write(physical, "<ResourceDictionary />\n")
        self.write(
            "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
<AvaloniaResource Include="..\\NvtFwCombiner.Presentation.Avalonia\\Styles\\ThemeTokens.axaml" Link="Styles\\ThemeTokens.axaml" />
</ItemGroup></Project>""",
        )
        report = f"""<coverage branches-covered="3" branches-valid="6"><packages>
<package name="NvtFwCombiner.DistributionLauncher"><classes>
<class name="ThemeTokens" filename="{logical}"><lines>
<line number="1" hits="1" branch="True" condition-coverage="50% (1/2)" />
</lines></class>
</classes></package>
<package name="NvtFwCombiner.Presentation.Avalonia"><classes>
<class name="ThemeTokens" filename="{physical}"><lines>
<line number="1" hits="0" branch="True" condition-coverage="50% (2/4)" />
</lines></class>
</classes></package>
</packages></coverage>"""
        distribution_branches = [
            self.branch(0, 1, line=1),
            self.branch(1, 0, line=1),
        ]
        presentation_branches = [
            self.branch(0, 1, line=1),
            self.branch(1, 1, line=1),
            self.branch(2, 0, line=1),
            self.branch(3, 0, line=1),
        ]
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            json.dumps(
                {
                    "NvtFwCombiner.DistributionLauncher.dll": {
                        logical: {
                            "ThemeTokens": {
                                "System.Void ThemeTokens::Load()": {
                                    "Lines": {"1": 1},
                                    "Branches": distribution_branches,
                                }
                            }
                        }
                    },
                    "NvtFwCombiner.Presentation.Avalonia.dll": {
                        physical: {
                            "ThemeTokens": {
                                "System.Void ThemeTokens::Load()": {
                                    "Lines": {"1": 0},
                                    "Branches": presentation_branches,
                                }
                            }
                        }
                    },
                }
            ),
        )

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 3, 6), inventory.overall)
        self.assertEqual(
            summary(1, 1, 3, 6),
            inventory.modules["PresentationAvalonia"],
        )
        self.assertNotIn("Bootstrap", inventory.modules)

    def test_rejects_invalid_linked_axaml_aliases(self) -> None:
        project = (
            "src/NvtFwCombiner.DistributionLauncher/"
            "NvtFwCombiner.DistributionLauncher.csproj"
        )
        logical = "src/NvtFwCombiner.DistributionLauncher/Styles/Linked.axaml"
        report = f"""<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Linked" filename="{logical}"><lines><line number="1" hits="1" /></lines></class>
</classes></package></packages></coverage>"""
        cases = {
            "missing": (
                '<AvaloniaResource Include="Missing.axaml" Link="Styles\\Linked.axaml" />',
                "not a current physical AXAML file",
                "",
            ),
            "escaping": (
                '<AvaloniaResource Include="..\\..\\outside.axaml" Link="Styles\\Linked.axaml" />',
                "outside declared production roots",
                "",
            ),
            "escaping_link": (
                '<AvaloniaResource Include="Source.axaml" Link="..\\Linked.axaml" />',
                "Link escapes its declared production project",
                "",
            ),
            "non_axaml": (
                '<AvaloniaResource Include="Linked.txt" Link="Styles\\Linked.axaml" />',
                "must map AXAML to AXAML",
                "",
            ),
            "dynamic_metadata": (
                '<AvaloniaResource Include="%(Filename).axaml" Link="Styles\\Linked.axaml" />',
                "must be static",
                "",
            ),
            "conditioned_resource": (
                '<AvaloniaResource Include="Source.axaml" Link="Styles\\Linked.axaml" Condition="true" />',
                "must be static",
                "",
            ),
            "conditioned_item_group": (
                '<AvaloniaResource Include="Source.axaml" Link="Styles\\Linked.axaml" />',
                "must be static",
                ' Condition="true"',
            ),
        }
        for name, (resource, message, item_group_attributes) in cases.items():
            with self.subTest(name=name):
                case_root = self.root / name
                case_project = case_root / project
                case_project.parent.mkdir(parents=True, exist_ok=True)
                case_project.write_text(
                    f'<Project Sdk="Microsoft.NET.Sdk"><ItemGroup{item_group_attributes}>'
                    f"{resource}</ItemGroup></Project>",
                    encoding="utf-8",
                )
                if name == "escaping_link":
                    (case_project.parent / "Source.axaml").write_text(
                        "<ResourceDictionary />\n", encoding="utf-8"
                    )
                elif name == "non_axaml":
                    (case_project.parent / "Linked.txt").write_text(
                        "not axaml\n", encoding="utf-8"
                    )
                report_path = case_root / "reports/coverage.cobertura.xml"
                report_path.parent.mkdir(parents=True, exist_ok=True)
                report_path.write_text(report, encoding="utf-8")
                (report_path.parent / "coverage.json").write_text(
                    "{}", encoding="utf-8"
                )
                with self.assertRaisesRegex(ValueError, message):
                    parse_dotnet_cobertura_reports(
                        case_root / "reports",
                        case_root,
                    )

    def test_rejects_ambiguous_or_undeclared_axaml_alias(self) -> None:
        logical = "src/NvtFwCombiner.DistributionLauncher/Styles/Linked.axaml"
        for name in ("First.axaml", "Second.axaml"):
            self.write(
                f"src/NvtFwCombiner.Presentation.Avalonia/Styles/{name}",
                "<ResourceDictionary />\n",
            )
        self.write(
            "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
            """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
<AvaloniaResource Include="..\\NvtFwCombiner.Presentation.Avalonia\\Styles\\First.axaml" Link="Styles\\Linked.axaml" />
<AvaloniaResource Include="..\\NvtFwCombiner.Presentation.Avalonia\\Styles\\Second.axaml" Link="Styles\\Linked.axaml" />
</ItemGroup></Project>""",
        )
        report = f"""<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Linked" filename="{logical}"><lines><line number="1" hits="1" /></lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        with self.assertRaisesRegex(ValueError, "ambiguous AvaloniaResource alias"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.write(
            "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
            '<Project Sdk="Microsoft.NET.Sdk" />',
        )
        with self.assertRaisesRegex(ValueError, "undeclared AXAML coverage alias"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_classifies_platform_as_infrastructure_adapter(self) -> None:
        self.write(
            "src/NvtFwCombiner.Platform/ProcessLaunchGate.cs",
            "internal static class ProcessLaunchGate;\n",
        )
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="ProcessLaunchGate" filename="src/NvtFwCombiner.Platform/ProcessLaunchGate.cs"><lines>
<line number="1" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 0, 0), inventory.overall)
        self.assertEqual(summary(1, 1, 0, 0), inventory.modules["Infrastructure"])

    def test_every_production_csharp_project_root_has_one_module_owner(self) -> None:
        project_roots = {
            project.parent.relative_to(ROOT)
            for project in (ROOT / "src").glob("*/*.csproj")
        }
        owned_roots = [
            directory
            for directories in PRODUCTION_MODULES.values()
            for directory in directories
        ]

        self.assertEqual(project_roots, set(owned_roots))
        self.assertEqual(len(owned_roots), len(set(owned_roots)))

    def test_requires_paired_cobertura_and_coverlet_json_reports(self) -> None:
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)

        with self.assertRaisesRegex(ValueError, "must pair"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_rejects_coverlet_json_that_omits_an_uncovered_branch_outcome(
        self,
    ) -> None:
        report = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" branch="True" condition-coverage="50% (1/2)" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [self.branch(0, 1)],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "branch evidence does not match"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_rejects_coverlet_json_branch_without_a_cobertura_line(self) -> None:
        report = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1, line=999),
                                self.branch(1, 0, line=999),
                            ],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "no paired coverage/source line"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_rejects_json_branch_on_a_cobertura_nonbranch_line(self) -> None:
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1),
                                self.branch(1, 0),
                            ],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "overall JSON branch evidence"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_accepts_source_mapped_json_branch_accounted_by_cobertura_root(
        self,
    ) -> None:
        report = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" branch="False" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1),
                                self.branch(1, 0),
                            ],
                        }
                    }
                }
            ),
        )

        inventory = parse_dotnet_cobertura_reports(
            self.root / "reports",
            self.root,
        )

        self.assertEqual(summary(1, 1, 1, 2), inventory.overall)

    def test_rejects_cobertura_source_that_is_not_a_physical_file(self) -> None:
        report = """<coverage branches-covered="0" branches-valid="0"><packages><package><classes>
<class name="Missing" filename="src/NvtFwCombiner.Domain/DoesNotExist.cs"><lines>
<line number="1" hits="1" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write("reports/coverage.json", "{}")

        with self.assertRaisesRegex(ValueError, "not a current physical file"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_rejects_fabricated_extra_coverlet_json_branch_outcome(self) -> None:
        report = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" branch="True" condition-coverage="50% (1/2)" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1),
                                self.branch(1, 0),
                                self.branch(2, 1),
                            ],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "branch evidence does not match"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_rejects_coverlet_hits_that_disagree_with_cobertura(self) -> None:
        report = """<coverage branches-covered="0" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" branch="True" condition-coverage="0% (0/2)" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1),
                                self.branch(1, 1),
                            ],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "branch evidence does not match"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_preserves_distinct_branch_identities_on_one_source_line(self) -> None:
        report = """<coverage branches-covered="2" branches-valid="4"><packages><package><classes>
<class name=\"First\" filename=\"src/NvtFwCombiner.Domain/Thing.cs\"><lines>
<line number=\"10\" hits=\"1\" branch=\"True\" condition-coverage=\"50% (1/2)\"><conditions><condition number=\"1\" type=\"jump\" coverage=\"50%\" /></conditions></line>
</lines></class>
<class name=\"Second\" filename=\"src/NvtFwCombiner.Domain/Thing.cs\"><lines>
<line number=\"10\" hits=\"0\" branch=\"True\" condition-coverage=\"50% (1/2)\"><conditions><condition number=\"2\" type=\"jump\" coverage=\"50%\" /></conditions></line>
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "First": {
                        "System.Void First::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [
                                self.branch(0, 1, offset=15),
                                self.branch(1, 0, offset=15),
                            ],
                        }
                    },
                    "Second": {
                        "System.Void Second::Run()": {
                            "Lines": {"10": 0},
                            "Branches": [
                                self.branch(0, 1, offset=25),
                                self.branch(1, 0, offset=25),
                            ],
                        }
                    },
                }
            ),
        )

        inventory = parse_dotnet_cobertura_reports(self.root / "reports", self.root)

        self.assertEqual(summary(1, 1, 2, 4), inventory.overall)

    def test_rejects_branch_report_without_an_exact_json_identity(self) -> None:
        report = """<coverage branches-covered="1" branches-valid="2"><packages><package><classes>
<class name="Thing" filename="src/NvtFwCombiner.Domain/Thing.cs"><lines>
<line number="10" hits="1" branch="True" condition-coverage="50% (1/2)" />
</lines></class>
</classes></package></packages></coverage>"""
        self.write("reports/coverage.cobertura.xml", report)
        invalid_branch = self.branch(0, 1)
        del invalid_branch["Offset"]
        self.write(
            "reports/coverage.json",
            self.coverlet_json(
                {
                    "Thing": {
                        "System.Void Thing::Run()": {
                            "Lines": {"10": 1},
                            "Branches": [invalid_branch],
                        }
                    }
                }
            ),
        )

        with self.assertRaisesRegex(ValueError, "invalid Coverlet JSON branch record"):
            parse_dotnet_cobertura_reports(self.root / "reports", self.root)

    def test_parses_python_json_branch_and_line_summary(self) -> None:
        self.write("tools/crc-worker/src/nfc_crc_worker/core.py", "pass\n")
        report = {
            "files": {
                "src/nfc_crc_worker/core.py": {
                    "summary": {
                        "covered_lines": 8,
                        "num_statements": 10,
                        "covered_branches": 3,
                        "num_branches": 4,
                    }
                },
                "tools/crc-worker/tests/test_core.py": {
                    "summary": {
                        "covered_lines": 1,
                        "num_statements": 1,
                        "covered_branches": 0,
                        "num_branches": 0,
                    }
                },
                "src/nfc_crc_worker/__pycache__/generated.py": {
                    "summary": {
                        "covered_lines": 1,
                        "num_statements": 1,
                        "covered_branches": 0,
                        "num_branches": 0,
                    }
                },
            }
        }
        path = self.write("python.json", json.dumps(report))

        inventory = parse_python_coverage_report(path, self.root)

        self.assertEqual(summary(8, 10, 3, 4), inventory.overall)
        self.assertEqual(summary(8, 10, 3, 4), inventory.modules["nfc_crc_worker"])

    def test_rejects_python_report_that_omits_owned_worker_source(self) -> None:
        self.write("tools/crc-worker/src/nfc_crc_worker/core.py", "pass\n")
        self.write("tools/crc-worker/src/nfc_crc_worker/untested.py", "pass\n")
        report = {
            "files": {
                "src/nfc_crc_worker/core.py": {
                    "summary": {
                        "covered_lines": 1,
                        "num_statements": 1,
                        "covered_branches": 0,
                        "num_branches": 0,
                    }
                }
            }
        }
        report_path = self.write("coverage.json", json.dumps(report))

        with self.assertRaisesRegex(ValueError, "source inventory mismatch"):
            parse_python_coverage_report(report_path, self.root)

    def test_rejects_boolean_python_summary_values(self) -> None:
        summary_values = {
            "covered_lines": 1,
            "num_statements": 1,
            "covered_branches": 0,
            "num_branches": 0,
        }
        for field in tuple(summary_values):
            with self.subTest(field=field):
                malformed = dict(summary_values)
                malformed[field] = True
                report = {
                    "files": {
                        "src/nfc_crc_worker/core.py": {"summary": malformed},
                    }
                }
                path = self.write(f"{field}.json", json.dumps(report))

                with self.assertRaisesRegex(ValueError, "invalid summary values"):
                    parse_python_coverage_report(path, self.root)

    def test_rejects_duplicate_python_source_aliases(self) -> None:
        source = self.write(
            "tools/crc-worker/src/nfc_crc_worker/core.py",
            "def crc():\n    return 0\n",
        )
        source_summary = {
            "covered_lines": 2,
            "num_statements": 2,
            "covered_branches": 0,
            "num_branches": 0,
        }
        report = {
            "files": {
                source.as_posix(): {"summary": source_summary},
                "tools/crc-worker/src/nfc_crc_worker/core.py": {
                    "summary": source_summary
                },
            }
        }
        path = self.write("duplicate-aliases.json", json.dumps(report))

        with self.assertRaisesRegex(ValueError, "duplicate source aliases"):
            parse_python_coverage_report(path, self.root)

    def test_rejects_out_of_repository_coverage_source(self) -> None:
        external = self.root.parent / "external" / "src" / "nfc_crc_worker" / "core.py"
        report = {
            "files": {
                str(external): {
                    "summary": {
                        "covered_lines": 1,
                        "num_statements": 1,
                        "covered_branches": 0,
                        "num_branches": 0,
                    }
                }
            }
        }
        path = self.write("python.json", json.dumps(report))

        with self.assertRaisesRegex(ValueError, "outside the repository root"):
            parse_python_coverage_report(path, self.root)

    def test_rejects_repository_prefixed_path_traversal(self) -> None:
        for spelling in (
            "src/../../outside.cs",
            r"src\..\..\outside.cs",
            "./src/../../outside.cs",
            "/_/src/../../outside.cs",
        ):
            with self.subTest(spelling=spelling):
                with self.assertRaisesRegex(ValueError, "outside the repository root"):
                    _relative_source_path(spelling, self.root)

    def test_resolves_coverlet_deterministic_virtual_root(self) -> None:
        actual = _relative_source_path(
            "/_/src/NvtFwCombiner.Infrastructure/obj/Generated.g.cs",
            self.root,
            ("\\",),
        )

        self.assertEqual(
            "src/NvtFwCombiner.Infrastructure/obj/Generated.g.cs",
            actual,
        )

    def test_rejects_ambiguous_coverage_source_roots(self) -> None:
        with self.assertRaisesRegex(ValueError, "coverage report source is ambiguous"):
            _relative_source_path(
                "Probe.cs",
                self.root,
                (str(self.root / "src/First"), str(self.root / "src/Second")),
            )

    def test_counts_zero_context_substitution_once(self) -> None:
        diff = """@@ -10,3 +10,4 @@
@@ -20 +21,0 @@
@@ -30,0 +31,5 @@
"""

        self.assertEqual(10, changed_lines_from_zero_context_diff(diff))

    def test_changed_module_ignores_large_project_metadata_diff(self) -> None:
        module = Path("src/NvtFwCombiner.Domain")
        self.write(f"{module}/Thing.cs", "class Thing {}\n")
        self.write(f"{module}/packages.lock.json", '{"version": 1}\n')
        self.run_git("init", "--quiet")
        self.run_git("add", ".")
        self.run_git(
            "-c",
            "user.name=Coverage Test",
            "-c",
            "user.email=coverage@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "baseline",
        )
        baseline_revision = self.run_git("rev-parse", "HEAD").strip()
        self.write(f"{module}/packages.lock.json", "metadata\n" * 100)

        changed = changed_module_lines(self.root, baseline_revision, module)

        self.assertEqual(0, changed)

    def test_changed_module_counts_blank_lines_in_untracked_source(self) -> None:
        module = Path("src/NvtFwCombiner.Domain")
        self.write(f"{module}/Thing.cs", "class Thing {}\n")
        self.run_git("init", "--quiet")
        self.run_git("add", ".")
        self.run_git(
            "-c",
            "user.name=Coverage Test",
            "-c",
            "user.email=coverage@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "baseline",
        )
        baseline_revision = self.run_git("rev-parse", "HEAD").strip()
        self.write(f"{module}/New.cs", "\n\nclass New {}\n")

        changed = changed_module_lines(self.root, baseline_revision, module)

        self.assertEqual(3, changed)

    def test_changed_module_counts_tracked_mixed_case_csharp_extension(self) -> None:
        module = Path("src/NvtFwCombiner.Domain")
        self.write(f"{module}/Thing.CS", "class Thing {}\n")
        self.run_git("init", "--quiet")
        self.run_git("add", ".")
        self.run_git(
            "-c",
            "user.name=Coverage Test",
            "-c",
            "user.email=coverage@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "baseline",
        )
        baseline_revision = self.run_git("rev-parse", "HEAD").strip()
        self.write(f"{module}/Thing.CS", "class Thing\n{\n}\n")

        changed = changed_module_lines(self.root, baseline_revision, module)

        self.assertEqual(3, changed)

    def test_changed_module_reads_utf8_diff_independent_of_windows_locale(self) -> None:
        module = Path("src/NvtFwCombiner.Domain")
        self.write(f"{module}/Thing.cs", "class Thing {}\n")
        self.run_git("init", "--quiet")
        self.run_git("add", ".")
        self.run_git(
            "-c",
            "user.name=Coverage Test",
            "-c",
            "user.email=coverage@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "baseline",
        )
        baseline_revision = self.run_git("rev-parse", "HEAD").strip()
        self.write(f"{module}/Thing.cs", "// → 中文\nclass Thing {}\n")

        changed = changed_module_lines(self.root, baseline_revision, module)

        self.assertEqual(1, changed)

    def test_rejects_overall_coverage_regression(self) -> None:
        current = CoverageInventory(
            summary(8, 10, 8, 10), {"Domain": summary(8, 10, 8, 10)}
        )

        errors = validate_inventory(current, baseline(), "dotnet")

        self.assertTrue(
            any("dotnet overall line coverage" in error for error in errors)
        )

    def test_substantial_changed_module_requires_no_regression_and_approved_floors(
        self,
    ) -> None:
        current = CoverageInventory(
            summary(9, 10, 8, 10),
            {"Domain": summary(8, 10, 7, 10), "Application": summary(9, 10, 8, 10)},
        )

        errors = validate_inventory(current, baseline(), "dotnet", {"Domain": 20})

        self.assertTrue(
            any("Domain changed-module line coverage" in error for error in errors)
        )
        self.assertTrue(any("below 80%" in error for error in errors))

    def test_small_module_change_does_not_invoke_changed_module_floor(self) -> None:
        current = CoverageInventory(
            summary(9, 10, 8, 10),
            {"Domain": summary(8, 10, 7, 10), "Application": summary(9, 10, 8, 10)},
        )

        errors = validate_inventory(current, baseline(), "dotnet", {"Domain": 19})

        self.assertEqual([], errors)

    def test_missing_baseline_module_fails_inventory_integrity(self) -> None:
        current = CoverageInventory(
            summary(9, 10, 8, 10), {"Domain": summary(9, 10, 8, 10)}
        )

        errors = validate_inventory(current, baseline(), "dotnet")

        self.assertTrue(any("Application is missing" in error for error in errors))

    def test_missing_python_baseline_module_fails_inventory_integrity(self) -> None:
        current = CoverageInventory(summary(19, 20, 9, 10), {})

        errors = validate_inventory(current, baseline(), "python")

        self.assertTrue(any("nfc_crc_worker is missing" in error for error in errors))

    def test_rejects_boolean_coverage_counts_in_baseline(self) -> None:
        document = json.loads(BASELINE_PATH.read_text(encoding="utf-8"))
        document["languages"]["dotnet"]["overall"]["lines"]["covered"] = True
        path = self.write("coverage-baseline.json", json.dumps(document))

        with self.assertRaisesRegex(ValueError, "integer covered and total"):
            load_baseline(path)

    def test_missing_production_assembly_fails_inventory_integrity(self) -> None:
        baseline_document = load_baseline()
        current = CoverageInventory(
            summary(1, 1, 0, 0), {"Domain": summary(1, 1, 0, 0)}
        )

        errors = validate_inventory(current, baseline_document, "dotnet")

        self.assertTrue(any("Cli is missing" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
