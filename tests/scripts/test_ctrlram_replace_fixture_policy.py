"""Active CtrlRAM fixture admission policy regressions."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

import verify_ctrlram_replace_fixture as fixture_verifier  # noqa: E402


class CtrlRamReplaceFixturePolicyTests(unittest.TestCase):
    def test_active_verifier_rejects_every_retired_ic_before_payload_access(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            for retired_ic_id in sorted(
                fixture_verifier.RETIRED_PRODUCTION_IC_IDS
            ):
                with self.subTest(ic=retired_ic_id):
                    with self.assertRaisesRegex(
                        ValueError,
                        rf"{retired_ic_id} is retired .* active CtrlRAM fixture verifier",
                    ):
                        fixture_verifier.verify_case(
                            root,
                            {"ic": retired_ic_id},
                            0,
                        )

    def test_active_authority_tables_contain_no_retired_ic(self) -> None:
        active_ic_ids = (
            set(fixture_verifier.FWCONFIG_STARTS)
            | set(fixture_verifier.DEFAULT_POSTBUILD_CATEGORIES)
            | set(fixture_verifier.VERSIONED_POSTBUILD_ICS)
            | {
                ic_id
                for ic_id, _ in fixture_verifier.VERSIONED_POSTBUILD_CATEGORIES
            }
        )

        self.assertTrue(
            fixture_verifier.RETIRED_PRODUCTION_IC_IDS.isdisjoint(active_ic_ids)
        )


if __name__ == "__main__":
    unittest.main()
