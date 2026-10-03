"""Conservative, byte-preserving member extraction and one-to-one matching."""

from __future__ import annotations

import re
from collections.abc import Sequence
from dataclasses import dataclass

try:
    from scripts.split_equivalence_source import code_lines
except ModuleNotFoundError:
    from split_equivalence_source import code_lines


@dataclass(frozen=True)
class Member:
    start: int
    end: int
    name: str
    kind: str
    top_level: bool = False
    suppression_start: int | None = None


def member_end(clean: tuple[str, ...], start: int, semicolon: bool) -> int | None:
    """Return a complete member's inclusive one-based end; never guess."""
    stack: list[str] = []
    for number in range(start, len(clean)):
        line = clean[number]
        for position, char in enumerate(line):
            if char in "([{":
                stack.append(char)
            elif char in ")]}":
                if not stack or stack.pop() != {")": "(", "]": "[", "}": "{"}[char]:
                    return None
                if char == "}" and not stack and not semicolon:
                    return number + 1 if not line[position + 1:].strip() else None
            elif char == ";" and not stack:
                return number + 1 if not line[position + 1:].strip() else None
            elif char in "\"'":
                return None
    return None


def member_spans(lines: tuple[str, ...], clean: tuple[str, ...],
                 include_annotated: bool = False,
                 containers: frozenset[str] = frozenset()) -> tuple[Member, ...]:
    """Only direct four-space class members, or top-level internal types."""
    result: list[Member] = []
    depth = 0
    occupied_until = 0
    for start, line in enumerate(clean):
        direct = depth == 1 and line.startswith("    ") and not line.startswith("     ")
        top = depth == 0 and line.startswith("internal ")
        if start >= occupied_until and (direct or top):
            declaration = line[4:] if direct else line
            type_match = re.match(r"(?:private|internal) (?:(?:sealed|static|abstract|readonly|ref|partial) )*"
                                  r"(?:class|record(?: class| struct)?|struct|enum|interface) (\w+)", declaration)
            delegate = re.match(r"(?:private|internal) delegate .+?\b(\w+)\s*(?:<[^<>]+>)?\(", declaration)
            field = re.match(r"(?:private|internal) (?:const|static readonly) .+?\b(\w+)\s*(?:=|;)", declaration)
            helper = re.match(r"(?:private|internal) static \S", declaration) if not top else None
            name, kind, semicolon = "", "", False
            if type_match or delegate:
                name = (type_match or delegate)[1]
                kind = "support_type_move"
                semicolon = delegate is not None
                if top and name in containers:
                    kind = ""
            elif field and not top:
                name, kind, semicolon = field[1], "support_member_move", True
            elif helper:
                # Only join a return-type line with the following indented method header.
                if (start + 1 < len(clean) and re.fullmatch(
                        r"(?:private|internal) static (?:partial )?(?:\([^)]*\)|[\w.<>,?\[\] ]+)", declaration)
                        and re.match(r"        \w+(?:<[^<>]+>)?\s*\(", clean[start + 1])):
                    declaration += " " + clean[start + 1].strip()
                signature = re.match(r"(?:private|internal) static (?:partial )?"
                                     r"(?:\([^)]*\)|[\w.<>,?\[\] ]+?)\s+(\w+)"
                                     r"(?:<[^<>]+>)?\s*(\(|=>|=|;)", declaration)
                name = signature[1] if signature else ""
                kind = "helper_move"
                semicolon = bool(signature and signature[2] == "=")
            suppression_start = None
            # Only the scheduled two-line CA2000 method suppression, immediately before its signature.
            if (not include_annotated and kind == "helper_move" and signature and signature[2] == "("
                    and start >= 2 and lines[start - 2] ==
                    '    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification ='
                    and re.fullmatch(r'        "(?:\\.|[^"\\\r])*"\)\]', lines[start - 1])):
                suppression_start = start - 1
            previous = suppression_start - 2 if suppression_start is not None else start - 1
            while previous >= 0 and (not clean[previous].strip() or lines[previous].lstrip().startswith("///")):
                previous -= 1
            annotated = previous >= 0 and (clean[previous].lstrip().startswith("[")
                                            or clean[previous].rstrip().endswith("]"))
            if kind and name and (include_annotated or not annotated):
                end = member_end(clean, start, semicolon)
                if end is not None:
                    result.append(Member(start + 1, end, name, kind, top, suppression_start))
                    occupied_until = end
        depth += line.count("{") - line.count("}")
    return tuple(result)


@dataclass(frozen=True)
class Candidate:
    file: int
    member: Member
    body: tuple[str, ...]
    positions: tuple[int, ...]
    prefix: tuple[str, ...] = ()
    prefix_positions: tuple[int, ...] = ()


def members(lines: tuple[str, ...], include_annotated: bool = False,
            containers: frozenset[str] = frozenset()) -> tuple[tuple[int, int], ...]:
    return tuple((m.start, m.end) for m in member_spans(lines, code_lines(lines), include_annotated, containers)
                 if m.kind == "helper_move" or (m.kind == "support_member_move"
                                                and " static readonly " in lines[m.start - 1]))


def canonical_body(lines: tuple[str, ...], member: Member, removed: bool) -> tuple[str, ...] | None:
    body = list(lines[member.start - 1:member.end])
    access = "private" if removed else "internal"
    indent = "" if member.top_level else "    "
    if not body[0].startswith(indent + access + " ") or any(line.endswith("\r") for line in body):
        return None
    if member.top_level:
        # Exactly one level: compare after restoring four spaces on every nonempty line.
        body = ["    " + line if line else line for line in body]
    if removed:
        body[0] = body[0].replace("private", "internal", 1)
    suppression = lines[member.suppression_start - 1:member.start - 1] if member.suppression_start is not None else ()
    return suppression + tuple(body)


def pair_moves(removed: Sequence[Candidate], added: Sequence[Candidate]) -> tuple[tuple[Candidate, Candidate], ...]:
    available = list(added)
    pairs: list[tuple[Candidate, Candidate]] = []
    used: set[tuple[int, int]] = set()
    for old in removed:
        for index, new in enumerate(available):
            if (old.file != new.file and old.member.kind == new.member.kind
                    and old.body == new.body and not any((candidate.file, position) in used
                        for candidate in (old, new) for position in candidate.positions)):
                pairs.append((old, new))
                available.pop(index)
                used.update((candidate.file, position) for candidate in (old, new) for position in candidate.positions)
                break
    return tuple(pairs)


def member_positions(source: tuple[str, ...], member: Member, changed: dict[int, int],
                     available: set[int]) -> tuple[int, ...] | None:
    """Reanchor identical lines only when the complete preserved stream is equal.

    Git can keep a moved member's closing brace and delete an earlier identical
    brace. Exchange their attribution, never their text. The declaration must
    itself change, and full canonical member comparison still decides a move.
    """
    first = member.suppression_start or member.start
    if any(number not in changed or changed[number] not in available for number in range(first, member.start + 1)):
        return None
    positions = {number: changed[number] for number in range(first, member.end + 1) if number in changed}
    if any(offset not in available for offset in positions.values()):
        return None
    deleted = set(changed)
    kept = tuple(line for number, line in enumerate(source, 1) if number not in deleted)
    for missing in range(first, member.end + 1):
        if missing in positions:
            continue
        alternatives = sorted((number for number, offset in changed.items()
                               if not first <= number <= member.end and offset in available
                               and number in deleted and source[number - 1] == source[missing - 1]),
                              key=lambda number: abs(number - missing))
        for number in alternatives:
            replacement = (deleted - {number}) | {missing}
            if tuple(line for i, line in enumerate(source, 1) if i not in replacement) == kept:
                positions[missing] = changed[number]
                deleted = replacement
                break
        else:
            return None
    return tuple(positions[number] for number in range(first, member.end + 1))
