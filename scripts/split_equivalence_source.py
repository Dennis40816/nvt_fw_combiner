"""Conservative C# source inspection shared by the E3 classifier."""

from __future__ import annotations

import re
from pathlib import PurePosixPath


def class_name(line: str) -> tuple[str, str] | None:
    match = re.fullmatch(r"((?:public sealed(?: partial)?|internal static(?: partial)?) class) (\w+)\r?", line)
    return match.groups() if match else None


def support_source(path: str, lines: tuple[str, ...], names: set[str]) -> bool:
    return any(PurePosixPath(path).name == name + ".cs" or
               re.fullmatch(re.escape(name) + r"\.\w+\.cs", PurePosixPath(path).name)
               for name in names) and sum(
                   bool((target := class_name(line)) and target[0].startswith("internal static") and target[1] in names)
                   for line in code_lines(lines)) == 1


def collection(line: str, definition: bool = False) -> bool:
    name = "CollectionDefinition" if definition else "Collection"
    argument = r'(?:nameof\(\w+(?:\.\w+)*\)|"[\w .-]+")'
    return re.fullmatch(r"\[" + name + r"\(" + argument + r"\)\]\r?", line) is not None


def masked_source(lines: tuple[str, ...]) -> tuple[tuple[str, ...], frozenset[int]]:
    """Mask literals/comments and track covered lines, including empty ones."""
    pattern = (r'\$*(?P<raw>"{3,})[\s\S]*?(?P=raw)|(?:\$?@|@\$)"(?:""|[^"])*"|'
               r'\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'|'
               r'/\*[\s\S]*?\*/|(?P<summary>///[^\n]*)|//[^\n]*')
    text = "\n".join(lines)
    covered: set[int] = set()
    def mask(match: re.Match[str]) -> str:
        if match["summary"] is not None:
            return match[0]
        first = text.count("\n", 0, match.start()) + 1
        covered.update(range(first, first + match[0].count("\n") + 1))
        return re.sub(r"[^\n]", " ", match[0])
    clean = re.sub(pattern, mask, text)
    return tuple(clean.split("\n")), frozenset(covered)


def code_lines(lines: tuple[str, ...]) -> tuple[str, ...]:
    return masked_source(lines)[0]


def has_test_attributes(clean: tuple[str, ...]) -> bool:
    """Conservatively detect Fact/Theory suffixes in already masked source."""
    return re.search(r"(?:\[|,)\s*(?:\w+:\s*)?(?:global::)?(?:@?\w+\.)*"
                     r"@?\w*(?:Fact|Theory)(?:Attribute)?\b", "\n".join(clean)) is not None


def namespace(lines: tuple[str, ...]) -> str | None:
    names = [match[1] for line in code_lines(lines)
             if (match := re.fullmatch(r"namespace (\w+(?:\.\w+)*);", line))]
    return names[0] if len(names) == 1 else None


def memberships(sources: dict[str, tuple[str, ...]]) -> dict[tuple[PurePosixPath, str], list[str]]:
    result: dict[tuple[PurePosixPath, str], list[str]] = {}
    for path, lines in sources.items():
        for number, line in enumerate(code_lines(lines)):
            if target := class_name(line):
                values = result.setdefault((PurePosixPath(path).parent, target[1]), [])
                previous = number - 1
                while previous >= 0 and (not lines[previous].strip() or lines[previous].startswith(("[", "///"))):
                    if lines[previous].startswith("[Collection("):
                        values.append(lines[previous])
                    previous -= 1
    return result


def class_at(clean: tuple[str, ...], number: int) -> str | None:
    """Find the outer class of a supported direct member, not its filename."""
    depth = 0
    name = None
    for line in clean[:number]:
        if depth == 0 and (match := re.search(r"\bclass\s+(\w+)", line)):
            name = match[1]
        depth += line.count("{") - line.count("}")
        if depth == 0 and "}" in line:
            name = None
    return name


def declared_names(clean: tuple[str, ...], excluded: frozenset[str] = frozenset()) -> frozenset[str]:
    """Inspect declarations, including public/instance/annotated members.

    This is a collision guard, not C# name binding. Literals and comments must
    already be masked; do not treat calls inside method bodies as declarations.
    """
    names: set[str] = set()
    depth = 0
    class_depths: list[tuple[int, str]] = []
    pending_class = None
    signature = ""
    continuation = False
    modifiers = r"(?:(?:public|private|protected|internal|static|readonly|const|volatile|async|virtual|override|abstract|sealed|partial|new|unsafe|extern|required)\s+)*"
    declaration = re.compile(modifiers + r"(?:\([^)]*\)|[\w.<>,?\[\] :]+?)\s+(@?\w+)"
                             r"(?:<[^<>]+>)?\s*(?:\(|=>|\{|=|;)")
    for line in clean:
        value = line.strip()
        is_type = re.search(r"\b(?:class|struct|record|interface|enum)\s+(\w+)", value)
        if class_depths and depth == class_depths[-1][0] and class_depths[-1][1] not in excluded:
            if is_type:
                names.add(is_type[1])
                signature = ""
            elif value and not value.startswith("///"):
                value = re.sub(r"^(?:\[[^\]]*\]\s*)+", "", value)
                if not continuation:
                    signature = (signature + " " + value).strip()
                    if match := declaration.match(signature):
                        names.add(match[1].removeprefix("@"))
                        signature = ""
                        continuation = True
                if any(token in value for token in ("{", "=>", ";")):
                    signature = ""
                    continuation = False
        else:
            signature = ""
            continuation = False
        if is_type:
            pending_class = is_type[1]
        if pending_class and "{" in line:
            class_depths.append((depth + 1, pending_class))
            pending_class = None
        depth += line.count("{") - line.count("}")
        while class_depths and depth < class_depths[-1][0]:
            class_depths.pop()
    return frozenset(names)
