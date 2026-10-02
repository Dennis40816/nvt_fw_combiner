"""Conservative import and primary-parameter inventory for E3 name guards."""

from __future__ import annotations

import re


def name_imports(clean):
    """Yield static targets and alias names, source lines and alias scopes."""
    text = "\n".join(clean)
    pattern = (r"\b(?:extern\s+alias\s+(?P<extern>@?\w+)|(?P<global>global\s+)?using\s+"
               r"(?:static\s+(?P<static>[^;]+)|(?P<alias>@?\w+)\s*=\s*[^;]+));")
    for match in re.finditer(pattern, text):
        number = text.count("\n", 0, match.start()) + 1
        alias = (match["alias"] or match["extern"] or "").removeprefix("@")
        owner = "<extern alias>" if match["extern"] else "<using alias>"
        yield bool(match["global"]), match["static"], alias, number, owner


def primary_parameters(clean, number, header_end):
    """Treat each type's primary parameter as an unproven member name.

    This also covers parameters captured by primary-constructor bodies. It
    does not claim that every parameter becomes a C# field or record property.
    """
    text = "\n".join(clean[number - 1:])
    tail = text[header_end:]
    generic = re.match(r"\s*<[^<>]*>", tail)
    offset = header_end + (generic.end() if generic else 0)
    match = re.match(r"\s*\(", text[offset:])
    if not match:
        return
    start = offset + match.end()
    stack = [")"]
    for index in range(start, len(text)):
        char = text[index]
        if char in "(<[":
            stack.append({"(": ")", "<": ">", "[": "]"}[char])
        elif char in ")>]":
            if not stack or stack[-1] != char:
                return
            stack.pop()
        if (char == "," and len(stack) == 1) or not stack:
            parameter = text[start:index].split("=", 1)[0].strip()
            if name := re.search(r"(@?\w+)\s*$", parameter):
                line = number + text.count("\n", 0, start + text[start:index].find(parameter))
                yield name[1].removeprefix("@"), parameter[:name.start()].strip(), line
            start = index + 1
        if not stack:
            return
