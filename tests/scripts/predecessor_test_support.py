"""Path-relative execution policy for comparator tests using fake hosts."""

import copy
from pathlib import Path


def published_inventory(tags=("v1.2.0", "v1.2.1"), *, collected="2026-10-02T00:00:00Z", pages=2):
    """Complete synthetic publication facts, with independently variable collection evidence."""
    return {"schemaVersion": "1.0", "kind": "predecessor-published-release-inventory",
            "repository": "Dennis40816/nvt_fw_combiner", "collectedAtUtc": collected,
            "complete": True, "pagesRead": pages,
            "releases": [{"id": index + 1, "tag": tag, "publishedAtUtc": "2026-10-01T00:00:00Z",
                          "draft": False, "prerelease": False, "complete": True}
                         for index, tag in enumerate(tags)]}


def contract_for_fake_processes(contract, temporary_root: Path):
    """Copy the contract, changing only the fake host's temporary path bound."""
    result = copy.deepcopy(contract)
    result["environment"]["temporaryRootMaxLength"] = len(str(temporary_root.resolve())) + 16
    return result
