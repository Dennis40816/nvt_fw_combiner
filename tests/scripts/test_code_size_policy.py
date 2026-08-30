"""Behavioral tests for source-size review findings."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from scripts.code_size_policy import (
    CodeSizeLimits,
    DEFAULT_LIMITS,
    measure_code_size,
    review_code_size_policy,
    validate_code_size_policy,
)


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


class CodeSizePolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        (self.root / "src/Product").mkdir(parents=True)
        (self.root / "profiles").mkdir()
        (self.root / "docs/contracts").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write(self, relative_path: str, content: str) -> None:
        path = self.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def test_frozen_release_baseline_emits_no_full_production_warning(self) -> None:
        snapshot = measure_code_size(REPOSITORY_ROOT)

        self.assertEqual(133_488, snapshot.production_nonblank)
        self.assertEqual(95_830, snapshot.runtime_production_nonblank)
        self.assertEqual(20_632, snapshot.domain_profiles_nonblank)
        self.assertEqual(41_047, snapshot.application_nonblank)
        self.assertEqual(4_347, snapshot.bootstrap_cli_nonblank)
        self.assertEqual(29_804, snapshot.infrastructure_contracts_worker_nonblank)
        self.assertEqual(snapshot.production_nonblank, DEFAULT_LIMITS.production_nonblank)
        self.assertEqual([], validate_code_size_policy(REPOSITORY_ROOT))
        self.assertFalse(
            any(
                "production source nonblank lines exceeded threshold" in finding
                for finding in review_code_size_policy(REPOSITORY_ROOT)
            )
        )

    def test_launcher_structure_record_matches_the_canonical_exact_ledger(self) -> None:
        record = json.loads(
            (REPOSITORY_ROOT / "docs/governance/change-records/LAUNCHER-STRUCTURE-RATCHET-104-01.json")
            .read_text(encoding="utf-8")
        )
        evidence = " ".join(
            [
                *record["searchEvidence"],
                record["terminalContract"],
                record["designReview"]["evidence"],
            ]
        )

        for value in (129_509, 91_851, 20_632, 40_425, 4_323, 26_471):
            self.assertIn(f"{value:,}", evidence)

    def test_default_policy_reports_ratchets_without_final_targets(self) -> None:
        findings = review_code_size_policy(self.root)

        self.assertTrue(
            any("runtime production metric" in finding for finding in findings)
        )
        self.assertTrue(
            any("Domain + Profiles metric" in finding for finding in findings)
        )
        self.assertTrue(any("Application metric" in finding for finding in findings))
        self.assertTrue(
            any(
                "Bootstrap + CLI + Desktop host metric" in finding
                for finding in findings
            )
        )
        self.assertTrue(
            any(
                "Infrastructure + Contracts + CRC worker metric" in finding
                for finding in findings
            )
        )
        self.assertFalse(any("final target" in finding for finding in findings))

    def limits(
        self,
        *,
        production: int,
        duplicates: int = 0,
        partial_max: int = 100,
        exact_partials: dict[str, int] | None = None,
        named_partial_maximums: dict[str, int] | None = None,
        runtime_baseline: int | None = None,
        runtime_ratchet: int | None = None,
        domain_profiles_ratchet: int | None = None,
        application_ratchet: int | None = None,
        bootstrap_cli_ratchet: int | None = None,
        infrastructure_contracts_worker_ratchet: int | None = None,
        full_production_ratchet: int | None = None,
        runtime_production_allowance: int = 0,
        application_allowance: int = 0,
        bootstrap_cli_allowance: int = 0,
        infrastructure_contracts_worker_allowance: int = 0,
        full_production_allowance: int = 0,
    ) -> CodeSizeLimits:
        return CodeSizeLimits(
            production_nonblank=production,
            duplicate_json_nonblank=duplicates,
            partial_type_default_max=partial_max,
            partial_type_exact_ratchets=exact_partials or {},
            partial_type_named_maximums=named_partial_maximums or {},
            runtime_production_baseline=runtime_baseline,
            runtime_production_ratchet=runtime_ratchet,
            domain_profiles_ratchet=domain_profiles_ratchet,
            application_ratchet=application_ratchet,
            bootstrap_cli_ratchet=bootstrap_cli_ratchet,
            infrastructure_contracts_worker_ratchet=(
                infrastructure_contracts_worker_ratchet
            ),
            full_production_ratchet=full_production_ratchet,
            runtime_production_allowance=runtime_production_allowance,
            application_allowance=application_allowance,
            bootstrap_cli_allowance=bootstrap_cli_allowance,
            infrastructure_contracts_worker_allowance=(
                infrastructure_contracts_worker_allowance
            ),
            full_production_allowance=full_production_allowance,
        )

    def review(self, limits: CodeSizeLimits) -> list[str]:
        return review_code_size_policy(self.root, limits)

    def test_accepts_an_exact_baseline_and_excludes_generated_directories(self) -> None:
        self.write("src/Product/Program.cs", "namespace Product;\nclass Program {}\n")
        self.write("src/Product/bin/Generated.cs", "one\ntwo\nthree\n")

        snapshot = measure_code_size(self.root)

        self.assertEqual(1, snapshot.production_files)
        self.assertEqual(2, snapshot.production_nonblank)
        self.assertEqual([], self.review(self.limits(production=2)))

    def test_reports_production_growth_above_threshold_and_accepts_reduction(
        self,
    ) -> None:
        self.write("src/Product/Program.cs", "one\ntwo\n")

        growth = self.review(self.limits(production=1))
        reduction = self.review(self.limits(production=3))

        self.assertTrue(
            any("exceeded threshold: 2 > 1" in finding for finding in growth)
        )
        self.assertEqual([], reduction)

    def test_counts_only_redundant_exact_json_copies(self) -> None:
        content = '{\n  "schemaVersion": "1.0"\n}\n'
        self.write("profiles/profile.json", content)
        self.write("docs/contracts/profile.json", content)

        snapshot = measure_code_size(self.root)

        self.assertEqual(1, snapshot.duplicate_json_groups)
        self.assertEqual(1, snapshot.duplicate_json_copies)
        self.assertEqual(3, snapshot.duplicate_json_nonblank)
        growth = self.review(self.limits(production=0, duplicates=2))
        reduction = self.review(self.limits(production=0, duplicates=4))

        self.assertTrue(any("grew: 3 > ratchet 2" in finding for finding in growth))
        self.assertTrue(
            any(
                "consider lowering the ratchet from 4 to 3" in finding
                for finding in reduction
            )
        )

    def test_reports_default_and_exact_partial_aggregate_thresholds(self) -> None:
        declaration = "namespace Product;\npublic partial class Workbench {}\n"
        self.write("src/Product/Workbench.One.cs", declaration)
        self.write("src/Product/Workbench.Two.cs", declaration)

        default_growth = self.review(self.limits(production=4, partial_max=3))
        default_equal = self.review(self.limits(production=4, partial_max=4))
        default_reduction = self.review(self.limits(production=4, partial_max=5))
        exact_equal = self.review(
            self.limits(
                production=4,
                partial_max=3,
                exact_partials={"Product.Workbench": 4},
            )
        )
        exact_growth = self.review(
            self.limits(
                production=4,
                partial_max=3,
                exact_partials={"Product.Workbench": 3},
            )
        )
        exact_reduction = self.review(
            self.limits(
                production=4,
                partial_max=3,
                exact_partials={"Product.Workbench": 5},
            )
        )

        self.assertTrue(any("threshold is 3" in finding for finding in default_growth))
        self.assertEqual([], default_equal)
        self.assertEqual([], default_reduction)
        self.assertEqual([], exact_equal)
        self.assertTrue(
            any("grew: 4 > ratchet 3" in finding for finding in exact_growth)
        )
        self.assertTrue(
            any(
                "consider lowering the ratchet from 5 to 4" in finding
                for finding in exact_reduction
            )
        )

    def test_named_partial_threshold_accepts_reduction_without_rebaselining(
        self,
    ) -> None:
        declaration = "namespace Product;\npublic partial class Workbench {}\n"
        self.write("src/Product/Workbench.One.cs", declaration)
        self.write("src/Product/Workbench.Two.cs", declaration)

        accepted = self.review(
            self.limits(
                production=4,
                partial_max=3,
                named_partial_maximums={"Product.Workbench": 5},
            )
        )
        growth = self.review(
            self.limits(
                production=4,
                partial_max=5,
                named_partial_maximums={"Product.Workbench": 3},
            )
        )

        self.assertEqual([], accepted)
        self.assertTrue(
            any(
                "partial aggregate Product.Workbench exceeded threshold: 4 > 3"
                in finding
                for finding in growth
            )
        )

    def test_tracks_each_partial_type_once_per_source_file(self) -> None:
        self.write(
            "src/Product/Workbench.One.cs",
            "namespace Product;\n"
            "public partial class Auxiliary {}\n"
            "public partial class Workbench {}\n"
            "public partial class Workbench {}\n",
        )
        self.write(
            "src/Product/Workbench.Two.cs",
            "namespace Product;\npublic partial class Workbench {}\n",
        )

        snapshot = measure_code_size(self.root)
        workbench = next(
            aggregate
            for aggregate in snapshot.partial_types
            if aggregate.name == "Product.Workbench"
        )

        self.assertEqual(2, workbench.file_count)
        self.assertEqual(6, workbench.nonblank_lines)

    def test_measures_only_non_ui_runtime_csharp_and_worker_python(self) -> None:
        self.write("src/Product/Program.cs", "one\n\ntwo\n")
        self.write(
            "src/NvtFwCombiner.Presentation.Avalonia/View.cs",
            "excluded\nfrom-runtime-metric\n",
        )
        self.write("src/Product/View.axaml", "counted-by-existing-metric\n")
        self.write("tools/crc-worker/src/worker.py", "worker\n\nline\n")
        self.write("tools/crc-worker/src/__pycache__/cached.py", "ignored\n")

        snapshot = measure_code_size(self.root)
        findings = self.review(self.limits(production=5, runtime_baseline=4))

        self.assertEqual(2, snapshot.runtime_production_files)
        self.assertEqual(4, snapshot.runtime_production_nonblank)
        self.assertTrue(
            any(
                "runtime production metric: 2 files / 4 nonblank lines "
                "(baseline 4, delta +0)" in finding
                for finding in findings
            )
        )

    def test_worker_runtime_owns_package_names_but_omits_cache_and_env_dirs(
        self,
    ) -> None:
        for directory in ("release", "artifacts", "bin", "obj"):
            self.write(
                f"tools/crc-worker/src/nfc_crc_worker/{directory}/runtime.py",
                "owned = True\n",
            )
        for directory in (
            ".mypy_cache",
            ".pytest_cache",
            ".ruff_cache",
            ".venv",
            "__pycache__",
            "venv",
        ):
            self.write(
                f"tools/crc-worker/src/nfc_crc_worker/{directory}/runtime.py",
                "ignored = True\n",
            )

        snapshot = measure_code_size(self.root)

        self.assertEqual(4, snapshot.runtime_production_files)
        self.assertEqual(4, snapshot.runtime_production_nonblank)

    def test_core_ratchets_measure_exact_roots_and_fail_growth(self) -> None:
        self.write("src/NvtFwCombiner.Domain/Domain.cs", "one\ntwo\n")
        self.write("src/NvtFwCombiner.Profiles/Profile.cs", "one\ntwo\nthree\n")
        self.write("src/Product/Program.cs", "outside-slice\n")

        snapshot = measure_code_size(self.root)
        limits = self.limits(
            production=6,
            runtime_ratchet=5,
            domain_profiles_ratchet=4,
        )

        self.assertEqual(2, snapshot.domain_profiles_files)
        self.assertEqual(5, snapshot.domain_profiles_nonblank)
        self.assertEqual(
            [
                "code-size runtime production grew: 6 > ratchet 5",
                "code-size Domain + Profiles slice grew: 5 > ratchet 4",
            ],
            validate_code_size_policy(self.root, limits),
        )
        self.assertEqual(
            [
                "code-size runtime production improved: lower ratchet 7 to 6",
                "code-size Domain + Profiles slice improved: lower ratchet 6 to 5",
            ],
            validate_code_size_policy(
                self.root,
                self.limits(
                    production=6,
                    runtime_ratchet=7,
                    domain_profiles_ratchet=6,
                ),
            ),
        )
        self.assertTrue(
            any(
                "Domain + Profiles metric: 2 files / 5 nonblank lines "
                "(ratchet 4)" in finding
                for finding in review_code_size_policy(self.root, limits)
            )
        )

    def test_full_production_ratchet_fails_growth_and_requires_lower_ratchet(
        self,
    ) -> None:
        self.write("src/Product/Program.cs", "one\ntwo\n")

        self.assertEqual(
            ["code-size full production grew: 2 > ratchet 1"],
            validate_code_size_policy(
                self.root,
                self.limits(production=2, full_production_ratchet=1),
            ),
        )
        self.assertEqual(
            ["code-size full production improved: lower ratchet 3 to 2"],
            validate_code_size_policy(
                self.root,
                self.limits(production=2, full_production_ratchet=3),
            ),
        )

    def test_approved_allowance_preserves_historical_ratchet_and_fails_new_growth(
        self,
    ) -> None:
        self.write("src/Product/Program.cs", "one\ntwo\nthree\n")
        limits = self.limits(
            production=3,
            full_production_ratchet=2,
            full_production_allowance=1,
        )

        self.assertEqual([], validate_code_size_policy(self.root, limits))
        self.write("src/Product/Program.cs", "one\ntwo\nthree\nfour\n")
        self.assertEqual(
            ["code-size full production grew: 4 > ratchet 3"],
            validate_code_size_policy(self.root, limits),
        )

    def test_all_slice_ratchets_reject_cross_slice_relocation(self) -> None:
        self.write("src/NvtFwCombiner.Domain/Domain.cs", "domain\n")
        self.write("src/NvtFwCombiner.Application/App.cs", "application\n")
        self.write("src/NvtFwCombiner.Bootstrap/Wiring.cs", "bootstrap\n")
        self.write("src/NvtFwCombiner.Desktop/Program.cs", "desktop-host\n")
        self.write("src/NvtFwCombiner.Infrastructure/Adapter.cs", "infrastructure\n")
        snapshot = measure_code_size(self.root)
        limits = self.limits(
            production=5,
            runtime_ratchet=5,
            domain_profiles_ratchet=1,
            application_ratchet=1,
            bootstrap_cli_ratchet=2,
            infrastructure_contracts_worker_ratchet=1,
        )
        self.assertEqual(2, snapshot.bootstrap_cli_files)
        self.assertEqual(2, snapshot.bootstrap_cli_nonblank)
        self.assertEqual([], validate_code_size_policy(self.root, limits))

        self.write(
            "src/NvtFwCombiner.Application/App.cs",
            "application\nrelocated-bootstrap-line\n",
        )
        self.write("src/NvtFwCombiner.Bootstrap/Wiring.cs", "")

        self.assertEqual(
            [
                "code-size Application slice grew: 2 > ratchet 1",
                "code-size Bootstrap + CLI + Desktop host slice improved: "
                "lower ratchet 2 to 1",
            ],
            validate_code_size_policy(self.root, limits),
        )

    def test_complete_slice_ratchets_reject_unallocated_runtime_source(self) -> None:
        self.write("src/NvtFwCombiner.Domain/Domain.cs", "domain\n")
        self.write("src/NvtFwCombiner.Application/App.cs", "application\n")
        self.write("src/NvtFwCombiner.Bootstrap/Wiring.cs", "bootstrap\n")
        self.write("src/NvtFwCombiner.Infrastructure/Adapter.cs", "infrastructure\n")
        self.write("src/Unallocated/Hidden.cs", "unallocated\n")

        self.assertEqual(
            ["code-size runtime slice allocation mismatch: 4 != total 5"],
            validate_code_size_policy(
                self.root,
                self.limits(
                    production=5,
                    runtime_ratchet=5,
                    domain_profiles_ratchet=1,
                    application_ratchet=1,
                    bootstrap_cli_ratchet=1,
                    infrastructure_contracts_worker_ratchet=1,
                ),
            ),
        )

    def test_launcher_bootstrap_is_allocated_to_existing_host_slice(self) -> None:
        self.write("src/NvtFwCombiner.LauncherBootstrap/Program.cs", "bootstrap-anchor\n")
        snapshot = measure_code_size(self.root)

        self.assertEqual(1, snapshot.bootstrap_cli_files)
        self.assertEqual(1, snapshot.bootstrap_cli_nonblank)
        self.assertEqual(
            [],
            validate_code_size_policy(
                self.root,
                self.limits(
                    production=1,
                    runtime_ratchet=1,
                    domain_profiles_ratchet=0,
                    application_ratchet=0,
                    bootstrap_cli_ratchet=1,
                    infrastructure_contracts_worker_ratchet=0,
                ),
            ),
        )

    def test_platform_is_allocated_to_infrastructure_slice(self) -> None:
        self.write("src/NvtFwCombiner.Platform/ProcessLaunchGate.cs", "platform-anchor\n")
        snapshot = measure_code_size(self.root)

        self.assertEqual(1, snapshot.infrastructure_contracts_worker_files)
        self.assertEqual(1, snapshot.infrastructure_contracts_worker_nonblank)
        self.assertEqual(
            [],
            validate_code_size_policy(
                self.root,
                self.limits(
                    production=1,
                    runtime_ratchet=1,
                    domain_profiles_ratchet=0,
                    application_ratchet=0,
                    bootstrap_cli_ratchet=0,
                    infrastructure_contracts_worker_ratchet=1,
                ),
            ),
        )

    def test_distribution_launcher_is_allocated_to_existing_host_slice(self) -> None:
        self.write("src/NvtFwCombiner.DistributionLauncher/Program.cs", "launcher-anchor\n")
        snapshot = measure_code_size(self.root)

        self.assertEqual(1, snapshot.bootstrap_cli_files)
        self.assertEqual(1, snapshot.bootstrap_cli_nonblank)
        self.assertEqual(
            [],
            validate_code_size_policy(
                self.root,
                self.limits(
                    production=1,
                    runtime_ratchet=1,
                    domain_profiles_ratchet=0,
                    application_ratchet=0,
                    bootstrap_cli_ratchet=1,
                    infrastructure_contracts_worker_ratchet=0,
                ),
            ),
        )


if __name__ == "__main__":
    unittest.main()
