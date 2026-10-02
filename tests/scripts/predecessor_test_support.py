"""Path-relative execution policy for comparator tests using fake hosts."""

import copy
from pathlib import Path


def contract_for_fake_processes(contract, temporary_root: Path):
    """Copy the contract, changing only the fake host's temporary path bound."""
    result = copy.deepcopy(contract)
    result["environment"]["temporaryRootMaxLength"] = len(str(temporary_root.resolve())) + 16
    return result
