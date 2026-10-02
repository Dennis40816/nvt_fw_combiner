"""One-to-one scheduled topic pairing, independent of Git's similarity score."""

from __future__ import annotations

import re
from collections.abc import Sequence
from pathlib import PurePosixPath


def named_split_pairs(entries: Sequence[tuple[str, str, tuple[str, ...]]]) -> tuple[tuple[int, int], ...]:
    """Entries contain status, path and masked source; return unique index pairs.

    Pairing only chooses a comparison. The ordinary E3 classifier must still
    prove every resulting changed line mechanical, including test bodies.
    """
    identities = {}
    for index, (status, path, code) in enumerate(entries):
        name = PurePosixPath(path).name
        match = re.fullmatch(r"(\w+)\.(\w+(?:\.\w+)*)\.cs", name)
        if status not in {"D", "A"} or not match:
            continue
        declarations = [line for line in code if re.search(r"\b(?:class|struct|record|enum|interface)\s+\w+", line)]
        if declarations != ["public sealed partial class " + match[1]]:
            continue
        identities[index] = (PurePosixPath(path).parent, match[1], match[2])
    candidates = [(old, new) for old, old_id in identities.items() for new, new_id in identities.items()
                  if entries[old][0] == "D" and entries[new][0] == "A"
                  and old_id[0] == new_id[0] and old_id[2] == new_id[2] and old_id[1] != new_id[1]]
    return tuple((old, new) for old, new in candidates
                 if sum(a == old for a, _ in candidates) == sum(b == new for _, b in candidates) == 1)
