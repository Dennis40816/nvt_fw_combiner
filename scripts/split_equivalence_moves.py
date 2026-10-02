"""Conservative, byte-preserving member extraction and one-to-one matching."""

from __future__ import annotations

import re
from collections.abc import Sequence
from dataclasses import dataclass


@dataclass(frozen=True)
class Member:
    start: int
    end: int
    name: str
    kind: str
    top_level: bool = False


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
            previous = start - 1
            while previous >= 0 and (not clean[previous].strip() or lines[previous].lstrip().startswith("///")):
                previous -= 1
            annotated = previous >= 0 and (clean[previous].lstrip().startswith("[")
                                            or clean[previous].rstrip().endswith("]"))
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
                signature = re.match(r"(?:private|internal) static (?:partial )?"
                                     r"(?:\([^)]*\)|[\w.<>,?\[\] ]+?)\s+(\w+)"
                                     r"(?:<[^<>]+>)?\s*(\(|=>|=|;)", declaration)
                name = signature[1] if signature else ""
                kind = "helper_move"
                semicolon = bool(signature and signature[2] == "=")
            if kind and name and (include_annotated or not annotated):
                end = member_end(clean, start, semicolon)
                if end is not None:
                    result.append(Member(start + 1, end, name, kind, top))
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
    return tuple(body)


def pair_moves(removed: Sequence[Candidate], added: Sequence[Candidate]) -> tuple[tuple[Candidate, Candidate], ...]:
    available = list(added)
    pairs: list[tuple[Candidate, Candidate]] = []
    for old in removed:
        for index, new in enumerate(available):
            if (old.file != new.file and old.member.kind == new.member.kind
                    and old.body == new.body):
                pairs.append((old, new))
                available.pop(index)
                break
    return tuple(pairs)
