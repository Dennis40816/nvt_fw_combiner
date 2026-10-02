"""Fail-closed recognition of helper-only source partials converted in place.

Reuse the E3 member extractor: every non-comment outer body line must belong
to a complete static/const member or nested type. Nested types may contain
instance members. Unknown declarations, annotations and directives fail closed.
"""

from __future__ import annotations

import re
from collections import Counter
from typing import TYPE_CHECKING

try:
    from scripts.split_equivalence_moves import member_spans
    from scripts.split_equivalence_source import class_name, code_lines, namespace, has_test_attributes
except ModuleNotFoundError:
    from split_equivalence_moves import member_spans
    from split_equivalence_source import class_name, code_lines, namespace, has_test_attributes

if TYPE_CHECKING:
    from scripts.split_equivalence import FileDiff


def partial_conversion(file: FileDiff, support_class: str | None,
                       source_identities: set[tuple[str | None, str]],
                       head_sources: dict[str, tuple[str, ...]]) -> dict[int, str]:
    """Return diff offsets only when the whole file proves this exact shape."""
    if (not support_class or file.status != "M" or file.old_path != file.new_path
            or not file.new_path.endswith(".cs") or len(file.before) != len(file.after)):
        return {}
    before, after = code_lines(file.before), code_lines(file.after)
    declarations = [(i, target) for i, line in enumerate(before) if (target := class_name(line))]
    heads = [(i, target) for i, line in enumerate(after) if (target := class_name(line))]
    if len(declarations) != 1 or len(heads) != 1:
        return {}
    start, old = declarations[0]
    if (old[0] != "public sealed partial class" or old[1] == support_class
            or source_identities != {(namespace(file.before), old[1])}
            or namespace(file.before) is None
            or heads != [(start, ("internal static partial class", support_class))]
            or has_test_attributes(before)
            or any(line.lstrip().startswith("#") for line in before)):
        return {}
    # Inspect every declaring file, including unchanged project context. Do not
    # infer partial status from a filename or from just the converted file.
    for source in head_sources.values():
        for line in code_lines(source):
            if re.search(r"\bclass\s+" + re.escape(support_class) + r"\b", line):
                if line != "internal static partial class " + support_class:
                    return {}
    if start + 1 >= len(before) or before[start + 1] != "{":
        return {}
    nonempty = [i for i, line in enumerate(before) if line.strip() and not line.lstrip().startswith("///")]
    end = nonempty[-1]
    if end <= start + 1 or before[end] != "}":
        return {}
    members = member_spans(file.before, before, include_annotated=True)
    covered = {i for member in members for i in range(member.start - 1, member.end)}
    if any(member.top_level or member.start <= start + 2 or member.end > end for member in members):
        return {}
    if any(line.strip() and not line.lstrip().startswith("///") and i not in covered
           for i, line in enumerate(before[start + 2:end], start + 2)):
        return {}
    if any(line.strip() and not line.lstrip().startswith("///")
           and not re.fullmatch(r"(?:global )?using (?:global::)?\w+(?:\.\w+)*;|namespace \w+(?:\.\w+)*;", line)
           for line in before[:start]):
        return {}
    changes = {start + 1: "support_partial_conversion"}
    starts = {member.start for member in members}
    for number, (left, right) in enumerate(zip(file.before, file.after), 1):
        if number == start + 1 or left == right:
            continue
        if (number not in starts or not left.startswith("    private ")
                or left.replace("    private ", "    internal ", 1) != right):
            return {}
        changes[number] = "support_accessibility"
    # Pair by unchanged line position, never a matching token elsewhere. Also
    # require both sides exactly once and reject mode/EOF metadata and CRLF.
    expected = Counter((side, number) for number in changes for side in ("-", "+"))
    if Counter((line.side, line.number) for line in file.lines) != expected:
        return {}
    if any(line.text.endswith("\r") or line.text != (file.before if line.side == "-" else file.after)[line.number - 1]
           for line in file.lines):
        return {}
    return {offset: changes[line.number] for offset, line in enumerate(file.lines)}
