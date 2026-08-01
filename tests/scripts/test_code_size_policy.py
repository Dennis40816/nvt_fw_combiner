"""Behavioral tests for source-size review findings."""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from scripts.code_size_policy import (
    CodeSizeLimits,
    measure_code_size,
    review_code_size_policy,
)


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

    def limits(
        self,
        *,
        production: int,
        duplicates: int = 0,
        partial_max: int = 100,
        exact_partials: dict[str, int] | None = None,
        named_partial_maximums: dict[str, int] | None = None,
        runtime_baseline: int | None = None,
        runtime_target: int | None = None,
    ) -> CodeSizeLimits:
        return CodeSizeLimits(
            production_nonblank=production,
            duplicate_json_nonblank=duplicates,
            partial_type_default_max=partial_max,
            partial_type_exact_ratchets=exact_partials or {},
            partial_type_named_maximums=named_partial_maximums or {},
            runtime_production_baseline=runtime_baseline,
            runtime_production_target=runtime_target,
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
        findings = self.review(
            self.limits(production=5, runtime_baseline=4, runtime_target=2)
        )

        self.assertEqual(2, snapshot.runtime_production_files)
        self.assertEqual(4, snapshot.runtime_production_nonblank)
        self.assertTrue(
            any(
                "runtime production metric: 2 files / 4 nonblank lines "
                "(baseline 4, delta +0; final target <= 2)" in finding
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


if __name__ == "__main__":
    unittest.main()
