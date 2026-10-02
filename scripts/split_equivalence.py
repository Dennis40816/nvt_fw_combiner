"""Produce E1/E3 evidence for ADR 0079 mechanical test-class splits.

E1 preserves method arguments and multiplicity. A mapping normally uses exact
method identities; topics need explicit identities because discovery has no
source paths. E3 is deliberately a conservative C# subset, not a C# parser.
Unsupported syntax remains unclassified. Counts are signed changed lines,
except rename, which counts path pairs. This tool does not establish E7.
New support files classify constants and readonly fields as support_member_move;
established support files retain the legacy helper_move kind for single-line
readonly fields, keeping historical replay counts stable.
Collection attributes may be added once per new split class and on the source
class's root file. Accepted declaration pairs identify the single source class;
all split classes must retain the same collection membership.

The blank separator kind is an addition to the plan's E3 list, justified by
the pilot's seven separators; blanks inside multiline literals never qualify.
Known conservative rejections requiring a human decision: merging identical
helpers, multiline summaries, adding
partial, and files without a final newline. E1 rejects raw multiline dotnet test
--list-tests preambles, Outer+Inner names, generic methods, and mappings for the
same method identity in two old classes. Mappings name fully qualified old_class
and new_class, either per identity or per topic with an identities list.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from collections.abc import Sequence
from dataclasses import dataclass, replace
from pathlib import Path, PurePosixPath

try:
    from scripts.authority_check import AuthorityError, Git, strict_json
    from scripts.split_equivalence_moves import Candidate, canonical_body, member_spans, pair_moves
except ModuleNotFoundError:  # Direct invocation from scripts/.
    from authority_check import AuthorityError, Git, strict_json
    from split_equivalence_moves import Candidate, canonical_body, member_spans, pair_moves


@dataclass(frozen=True)
class Identity:
    class_name: str
    method: str


def discovery_input(text: str) -> tuple[tuple[Identity, ...], tuple[str, ...]]:
    """Only explicit, unindented colon-terminated preambles are headers."""
    result: list[Identity] = []
    headers: list[str] = []
    for line in text.splitlines():
        line = line.removeprefix("\ufeff")
        value = line.strip()
        if not value:
            continue
        match = re.fullmatch(r"([\w]+(?:\.[\w]+)*)\.([\w]+(?:\(.*\))?)", value)
        if match:
            result.append(Identity(*match.groups()))
        elif not result and line == line.lstrip() and value.endswith((":", "：")):
            headers.append(line)
        else:
            raise ValueError(f"invalid discovery line: {line!r}")
    if not result:
        raise ValueError("discovery contains no fully qualified tests")
    return tuple(result), tuple(headers)


def discovery(text: str) -> tuple[Identity, ...]:
    return discovery_input(text)[0]


def method_mapping(value: object, identities: set[str]) -> dict[str, tuple[str, str]]:
    """Expand explicit old/new fully qualified class pairs, optionally by topic."""
    if not isinstance(value, dict):
        raise ValueError("mapping must be a JSON object")
    result: dict[str, tuple[str, str]] = {}
    for key, target in value.items():
        methods = [key]
        if isinstance(target, dict) and set(target) == {"old_class", "new_class", "identities"}:
            methods = target["identities"]
            target = {name: target[name] for name in ("old_class", "new_class")}
        if (not isinstance(methods, list) or not methods
                or not isinstance(target, dict) or set(target) != {"old_class", "new_class"}
                or any(not isinstance(name, str) or not re.fullmatch(r"\w+(?:\.\w+)+", name)
                       for name in target.values())):
            raise ValueError(f"invalid mapping entry: {key!r}")
        for method in methods:
            if not isinstance(method, str) or method not in identities or method in result:
                raise ValueError(f"unknown or repeated mapping identity: {method!r}")
            result[method] = (target["old_class"], target["new_class"])
    if result.keys() != identities:
        raise ValueError(f"mapping lacks identities: {sorted(identities - result.keys())}")
    return result


def e1(before: str, after: str, mapping: object | None = None) -> dict[str, object]:
    (old, old_headers), (new, new_headers) = discovery_input(before), discovery_input(after)
    left, right = Counter(item.method for item in old), Counter(item.method for item in new)
    expected = method_mapping(mapping, set(left)) if mapping is not None else {}
    if expected and any(len({item.class_name for item in old if item.method == method}) > 1 for method in left):
        raise ValueError("mapping identity occurs in multiple old classes")
    def unexpected(items: tuple[Identity, ...], side: int) -> list[dict[str, object]]:
        return [{"identity": item.method, "actual": item.class_name,
                 "expected": expected[item.method][side] if item.method in expected else None}
                for item in items if expected and (item.method not in expected
                                                   or expected[item.method][side] != item.class_name)]
    wrong_old, wrong_new = unexpected(old, 0), unexpected(new, 1)
    def roots(items: tuple[Identity, ...]) -> Counter[tuple[str, str]]:
        return Counter((item.class_name.split(".")[0], item.method) for item in items)
    root_changes = sorted((roots(new) - roots(old)).elements())
    missing, added = sorted((left - right).elements()), sorted((right - left).elements())
    return {"passed": not (missing or added or wrong_old or wrong_new or root_changes
                           or len(old_headers) != len(new_headers)), "before_count": len(old),
            "after_count": len(new), "missing": missing, "added": added,
            "moved-from-unexpected-class": wrong_old, "moved-to-unexpected-class": wrong_new,
            "namespace_root_differences": root_changes,
            "skipped_headers": {"before": list(old_headers), "after": list(new_headers)}}


@dataclass(frozen=True)
class Line:
    side: str
    number: int
    text: str
    hunk: int = 0


@dataclass(frozen=True)
class FileDiff:
    status: str
    old_path: str
    new_path: str
    before: tuple[str, ...]
    after: tuple[str, ...]
    lines: tuple[Line, ...]
    existing_classes: frozenset[str] = frozenset()
    head_sources: tuple[tuple[str, tuple[str, ...]], ...] = ()


def source_lines(text: str) -> tuple[str, ...]:
    """Retain CR characters: whitespace changes must not hide in helper moves."""
    return tuple(text.removesuffix("\n").split("\n")) if text else ()


def patch_lines(patch: str) -> tuple[Line, ...]:
    result: list[Line] = []
    old = new = remaining_old = remaining_new = hunk = 0
    for line in patch.split("\n"):
        match = re.match(r"@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", line)
        if match:
            hunk += 1
            if remaining_old or remaining_new:
                raise ValueError("truncated diff hunk")
            old, old_count, new, new_count = match.groups()
            old, new = int(old), int(new)
            remaining_old = int(old_count) if old_count is not None else 1
            remaining_new = int(new_count) if new_count is not None else 1
        elif line.startswith("@@"):
            raise ValueError("invalid diff hunk header")
        elif line.startswith("\\ No newline"):
            result.append(Line("!", 0, line))
        elif remaining_old or remaining_new:
            if line.startswith("-") and remaining_old:
                result.append(Line("-", old, line[1:], hunk))
                old, remaining_old = old + 1, remaining_old - 1
            elif line.startswith("+") and remaining_new:
                result.append(Line("+", new, line[1:], hunk))
                new, remaining_new = new + 1, remaining_new - 1
            else:
                raise ValueError(f"invalid zero-context diff line: {line!r}")
        elif line.startswith(("old mode", "new mode", "new file mode", "deleted file mode")):
            if line not in ("new file mode 100644", "deleted file mode 100644"):
                result.append(Line("!", 0, line))
        elif line.startswith(("Binary files", "GIT binary patch")):
            result.append(Line("!", 0, line))
        elif line.startswith(("+", "-")) and not line.startswith(("+++ ", "--- ")):
            raise ValueError("changed line outside a diff hunk")
    if remaining_old or remaining_new:
        raise ValueError("truncated diff hunk")
    return tuple(result)


def name_list(data: bytes) -> tuple[tuple[str, str, str], ...]:
    if data and not data.endswith(b"\0"):
        raise ValueError("unterminated Git name list")
    fields = data.decode("utf-8").split("\0")[:-1]
    result: list[tuple[str, str, str]] = []
    index = 0
    while index < len(fields):
        status = fields[index]
        if not re.fullmatch(r"[AMD]|R\d{1,3}", status):
            raise ValueError(f"unsupported Git status: {status!r}")
        size = 2 if status.startswith("R") else 1
        paths = fields[index + 1:index + 1 + size]
        if len(paths) != size or not all(paths):
            raise ValueError("truncated Git name list")
        result.append((status[0], paths[0], paths[-1]))
        index += size + 1
    return tuple(result)


def read_diff(git: Git, base: str, head: str, project: str) -> tuple[FileDiff, ...]:
    base, head = git.commit(base), git.commit(head)
    if git.merge_base(base, head) != base:
        raise ValueError("base must be an ancestor of head")
    path = PurePosixPath(project.replace("\\", "/"))
    if not project or path.is_absolute() or ".." in path.parts or ":" in project:
        raise ValueError("project must be a repository-relative path")
    if not (git.run("ls-tree", "-z", "--name-only", base, "--", str(path))
            or git.run("ls-tree", "-z", "--name-only", head, "--", str(path))):
        raise ValueError("project does not exist at either revision")
    options = ("--find-renames", "-l0", "--no-ext-diff", "--no-textconv", "--no-color", "--ignore-submodules=none")
    names = name_list(git.run("diff", *options, "--name-status", "-z", base, head, "--", str(path)))
    if not names:
        raise ValueError("no changed path in project")
    result: list[FileDiff] = []
    for status, old_path, new_path in names:
        patch = git.run("diff", *options, "-U0", base, head, "--",
                        *dict.fromkeys((old_path, new_path))).decode("utf-8")
        if sum(line.startswith("diff --git ") for line in patch.split("\n")) != 1:
            raise ValueError("expected one patch for each changed path pair")
        before = git.blob(base, old_path) if status != "A" else None
        after = git.blob(head, new_path) if status != "D" else None
        if (status != "A" and before is None) or (status != "D" and after is None):
            raise ValueError("missing Git source blob")
        result.append(FileDiff(status, old_path, new_path,
                               source_lines(before[1].decode("utf-8")) if before else (),
                               source_lines(after[1].decode("utf-8")) if after else (), patch_lines(patch)))
    if any(class_name(line.text) or line.text.startswith("/// <summary>")
           for file in result for line in file.lines):
        baseline: set[str] = set()
        paths = git.run("ls-tree", "-r", "-z", "--name-only", base, "--", str(path)).decode("utf-8").split("\0")
        for name in paths:
            if not name.endswith(".cs"):
                continue
            blob = git.blob(base, name)
            if blob is None:
                raise ValueError("missing baseline class source")
            for line in code_lines(source_lines(blob[1].decode("utf-8"))):
                match = re.match(r"\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|unsafe)\s+)*class\s+(\w+)", line)
                if match:
                    baseline.add(match[1])
        result = [replace(file, existing_classes=frozenset(baseline)) for file in result]
    roots = {target[1] for file in result for line in code_lines(file.before)
             if (target := class_name(line))}
    roots.update(match[1].rsplit(".", 1)[-1] for file in result for line in file.lines
                 if (match := re.fullmatch(r"global using static (?:global::)?([\w.]+);", line.text)))
    changed = {file.new_path for file in result}
    context: list[tuple[str, tuple[str, ...]]] = []
    if roots:
        for name in git.run("ls-tree", "-r", "-z", "--name-only", head, "--", str(path)).decode("utf-8").split("\0"):
            if name not in changed and name.endswith(".cs") and PurePosixPath(name).name.split(".")[0] in roots:
                blob = git.blob(head, name)
                if blob is None:
                    raise ValueError("missing head class source")
                context.append((name, source_lines(blob[1].decode("utf-8"))))
    result = [replace(file, head_sources=tuple(context)) for file in result]
    return tuple(result)


def class_name(line: str) -> tuple[str, str] | None:
    match = re.fullmatch(r"((?:public sealed(?: partial)?|internal static(?: partial)?) class) (\w+)\r?", line)
    return match.groups() if match else None


def using(line: str, support_class: str | None = None) -> bool:
    if re.fullmatch(r"(?:global )?using (?:global::)?\w+(?:\.\w+)*;", line):
        return True
    match = re.fullmatch(r"global using static (?:global::)?(\w+(?:\.\w+)*);", line)
    return bool(match and support_class and match[1] == support_class)


def collection(line: str, definition: bool = False) -> bool:
    name = "CollectionDefinition" if definition else "Collection"
    argument = r'(?:nameof\(\w+(?:\.\w+)*\)|"[\w .-]+")'
    return re.fullmatch(r"\[" + name + r"\(" + argument + r"\)\]\r?", line) is not None


def definition(lines: tuple[str, ...]) -> bool:
    content = [line.rstrip("\r") for line in lines if line.strip() and not using(line)]
    if content and re.fullmatch(r"namespace \w+(?:\.\w+)*;", content[0]):
        content.pop(0)
    if content and re.fullmatch(r"/// <summary>.+</summary>", content[0]):
        content.pop(0)
    return (len(content) == 4 and collection(content[0], True)
            and re.fullmatch(r"public sealed class \w+", content[1]) is not None
            and content[2:] == ["{", "}"])


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


def members(lines: tuple[str, ...], include_annotated: bool = False,
            containers: frozenset[str] = frozenset()) -> tuple[tuple[int, int], ...]:
    return tuple((m.start, m.end) for m in member_spans(lines, code_lines(lines), include_annotated, containers)
                 if m.kind == "helper_move" or (m.kind == "support_member_move"
                                                and " static readonly " in lines[m.start - 1]))


def namespace(lines: tuple[str, ...]) -> str | None:
    names = [match[1] for line in code_lines(lines)
             if (match := re.fullmatch(r"namespace (\w+(?:\.\w+)*);", line))]
    return names[0] if len(names) == 1 else None


def support_source(path: str, lines: tuple[str, ...], names: set[str]) -> bool:
    return any(PurePosixPath(path).name == name + ".cs" or
               re.fullmatch(re.escape(name) + r"\.\w+\.cs", PurePosixPath(path).name)
               for name in names) and sum(
                   bool((target := class_name(line)) and target[0].startswith("internal static") and target[1] in names)
                   for line in code_lines(lines)) == 1


def skeleton(lines: tuple[str, ...], names: set[str]) -> set[int]:
    clean = code_lines(lines)
    declarations = [i for i, line in enumerate(clean) if (target := class_name(line))
                    and target[0].startswith("internal static") and target[1] in names]
    if len(declarations) != 1 or namespace(lines) is None:
        return set()
    start = declarations[0]
    if start + 1 >= len(lines) or lines[start + 1] != "{":
        return set()
    allowed = {start + 1, start + 2}
    if start and re.fullmatch(r"/// <summary>.+</summary>", lines[start - 1]):
        allowed.add(start)
    depth = 0
    closed = False
    for i, line in enumerate(clean):
        if depth == 0 and (using(lines[i]) or re.fullmatch(r"namespace \w+(?:\.\w+)*;", line)):
            allowed.add(i + 1)
        if not closed and i > start + 1 and depth == 1 and line == "}":
            allowed.add(i + 1)
            closed = True
        depth += line.count("{") - line.count("}")
    return allowed


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


def e3(files: Sequence[FileDiff], support_class: str | None = None,
       allow_directory_move: bool = False) -> dict[str, object]:
    if not files:
        raise ValueError("no changed path in project")
    kinds = ("rename", "class_declaration", "summary", "using", "support_accessibility",
             "helper_move", "collection_attribute", "collection_definition", "blank",
             "support_member_move", "support_type_move", "support_file_skeleton")
    counts = dict.fromkeys(kinds, 0)
    marked: dict[tuple[int, int], str] = {}
    old_classes = {value[1] for file in files for line in code_lines(file.before) if (value := class_name(line))}
    old_classes.update(name for file in files for name in file.existing_classes)
    support_names = {value[1] for file in files for line in code_lines(file.after)
                     if (value := class_name(line)) and value[0].startswith("internal static")}
    if support_class is not None:
        support_names &= {support_class}
    if len(support_names) > 1:
        raise ValueError("multiple support classes; specify --support-class")
    head_sources = dict(source for file in files for source in file.head_sources)
    head_sources.update((file.new_path, file.after) for file in files if file.status != "D")
    support_names.update({support_class} if support_class and any(
        support_source(path, lines, {support_class}) for path, lines in head_sources.items()) else set())
    qualified_support = {namespace(lines) + "." + support_class for path, lines in head_sources.items()
                         if support_class and namespace(lines) and support_source(path, lines, {support_class})}
    support_identity = next(iter(qualified_support)) if len(qualified_support) == 1 else None
    blocked: set[int] = set()
    collection_names = {line.text for file in files for line in file.lines
                        if line.side == "+" and collection(line.text)}
    class_collections: Counter[str] = Counter()
    collection_candidates: dict[tuple[int, int], tuple[str, bool]] = {}
    summaries: Counter[str] = Counter()
    moved: list[dict[str, str]] = []
    for index, file in enumerate(files):
        csharp = file.old_path.endswith(".cs") and file.new_path.endswith(".cs")
        directory_move = PurePosixPath(file.old_path).parent != PurePosixPath(file.new_path).parent
        if file.status == "R" and csharp and (allow_directory_move or not directory_move):
            counts["rename"] += 1
        (before_code, before_mask), (after_code, after_mask) = masked_source(file.before), masked_source(file.after)
        if (any('"' in line and not line.lstrip().startswith("///") for line in (*before_code, *after_code))
                or (file.status == "R" and directory_move and not allow_directory_move)):
            blocked.add(index)
            continue
        is_support = support_source(file.new_path, file.after, support_names)
        support_skeleton = (skeleton(file.after, support_names)
                            if is_support and file.status == "A" and support_class is not None else set())
        is_definition = file.status == "A" and definition(file.after)
        has_tests = re.search(r"(?:\[|,)\s*(?:\w+:\s*)?(?:global::)?(?:@?\w+\.)*"
                              r"@?\w*(?:Fact|Theory)(?:Attribute)?\b", "\n".join(before_code)) is not None
        removed_collection = any(line.side == "-" and collection(line.text) for line in file.lines)
        added_classes = {line.number for line in file.lines if line.side == "+" and class_name(line.text)}
        for number, declaration in enumerate(after_code):
            if target := class_name(declaration):
                previous = number - 1
                while previous >= 0 and collection(file.after[previous]):
                    class_collections[target[1]] += 1
                    previous -= 1
        for offset, line in enumerate(file.lines):
            key = index, offset
            if line.side not in {"+", "-"} or not csharp or line.text.endswith("\r"):
                continue
            if not line.text.strip():
                if line.number not in (before_mask if line.side == "-" else after_mask):
                    marked[key] = "blank"
            elif not (before_code if line.side == "-" else after_code)[line.number - 1].strip():
                continue
            elif (line.side == "+" and using(line.text, support_identity)
                  and (not (is_support and file.status == "A") or line.number in support_skeleton)):
                marked[key] = "using"
            elif line.side == "+" and line.number in support_skeleton:
                marked[key] = "support_file_skeleton"
            elif is_definition:
                marked[key] = "collection_definition"
            elif collection(line.text):
                target = class_name(after_code[line.number]) if line.number < len(after_code) else None
                if (line.side == "+" and not removed_collection and len(collection_names) == 1
                        and target):
                    collection_candidates[key] = target[1], line.number + 1 in added_classes
            elif (new_class := class_name(line.text)) and line.side == "+":
                partners = [(position, item) for position, item in enumerate(file.lines)
                            if item.side == "-" and class_name(item.text)
                            and item.hunk > 0 and item.hunk == line.hunk and class_name(item.text)[1] != new_class[1]
                            and new_class[1] not in old_classes
                            and before_code[item.number - 1].strip()
                            and (index, position) not in marked]
                if len(partners) == 1:
                    position, item = partners[0]
                    old_class = class_name(item.text)
                    if old_class and not item.text.endswith("\r") and (new_class[0] == old_class[0] or
                            (is_support and new_class[1] in support_names and not has_tests)):
                        marked[key] = marked[index, position] = "class_declaration"
            elif line.side == "+" and re.fullmatch(r"/// <summary>.+</summary>\r?", line.text):
                following = list(file.after[line.number:line.number + 2])
                if following and collection(following[0]):
                    following.pop(0)
                target = class_name(following[0]) if following else None
                if target and target[1] not in old_classes and summaries[target[1]] == 0:
                    summaries[target[1]] += 1
                    marked[key] = "summary"
        if is_support:
            old_starts = dict(members(file.before, True, frozenset(support_names)))
            new_starts = dict(members(file.after, True, frozenset(support_names)))
            for offset, line in enumerate(file.lines):
                if line.side == "+" and line.number in new_starts and re.match(r"    internal static \S", line.text):
                    for position, old in enumerate(file.lines):
                        if (old.side == "-" and old.number in old_starts and re.match(r"    private static \S", old.text)
                                and old.text.replace("private", "internal", 1) == line.text
                                and (line.text, *file.before[old.number:old_starts[old.number]])
                                == file.after[line.number - 1:new_starts[line.number]]
                                and (index, position) not in marked):
                            marked[index, offset] = marked[index, position] = "support_accessibility"
                            if file.old_path != file.new_path:
                                name = next(member.name for member in member_spans(
                                    file.before, before_code, True, frozenset(support_names)) if member.start == old.number)
                                moved.append({"kind": "support_accessibility", "name": name,
                                              "old_file": file.old_path, "new_file": file.new_path})
                            break
    source_classes = {(PurePosixPath(file.old_path).parent, target[1])
                      for index, file in enumerate(files)
                      for offset, line in enumerate(file.lines)
                      if line.side == "-" and marked.get((index, offset)) == "class_declaration"
                      and (target := class_name(line.text))}
    source_identities = {(namespace(file.before), target[1])
                         for index, file in enumerate(files) for offset, line in enumerate(file.lines)
                         if line.side == "-" and marked.get((index, offset)) == "class_declaration"
                         and (target := class_name(line.text))}
    single_source = len(source_classes) == len(source_identities) == 1
    if source_classes and not single_source:
        marked = {key: kind for key, kind in marked.items() if kind != "class_declaration"}
    for key, (name, changed_declaration) in collection_candidates.items():
        file = files[key[0]]
        path = PurePosixPath(file.new_path)
        source = ((path.parent, name) in source_classes and file.status == "M"
                  and file.old_path == file.new_path and path.name == name + ".cs"
                  and code_lines(file.before).count(file.after[file.lines[key[1]].number]) == 1
                  and sum(class_name(line) is not None for line in code_lines(file.after)) == 1)
        accepted = any(item.side == "+" and item.number == file.lines[key[1]].number + 1
                       and marked.get((key[0], i)) == "class_declaration" for i, item in enumerate(file.lines))
        if single_source and class_collections[name] == 1 and (source or (changed_declaration and accepted)):
            marked[key] = "collection_attribute"
    class_memberships = memberships(head_sources)
    split_classes = set(source_classes)
    split_classes.update((PurePosixPath(file.new_path).parent, target[1])
                         for index, file in enumerate(files) for offset, line in enumerate(file.lines)
                         if line.side == "+" and marked.get((index, offset)) == "class_declaration"
                         and (target := class_name(line.text)) and not target[0].startswith("internal static"))
    collection_failures: list[dict[str, object]] = []
    if source_classes:
        values = {key: class_memberships.get(key, []) for key in sorted(split_classes)}
        if not single_source or len({tuple(value) for value in values.values()}) != 1 or any(
                len(value) > 1 or any(not collection(item) for item in value) for value in values.values()):
            collection_failures.append({"classes": sorted({key[1] for key in values}),
                                        "memberships": {str(key[0]) + "/" + key[1]: value for key, value in values.items()}})
    removed: list[Candidate] = []
    added: list[Candidate] = []
    for index, file in enumerate(files):
        if index in blocked or not (file.old_path.endswith(".cs") and file.new_path.endswith(".cs")):
            continue
        for side, source, output in (("-", file.before, removed), ("+", file.after, added)):
            if side == "+" and not support_source(file.new_path, file.after, support_names):
                continue
            for member in member_spans(source, code_lines(source), containers=frozenset(support_names)):
                positions = tuple(offset for offset, line in enumerate(file.lines)
                                  if line.side == side and member.start <= line.number <= member.end)
                if len(positions) != member.end - member.start + 1 or any(
                        marked.get((index, p)) not in {None, "blank"} for p in positions):
                    continue
                body = canonical_body(source, member, side == "-")
                if body is None:
                    continue
                prefix_positions: list[int] = []
                prefix: list[str] = []
                previous = member.start - 1
                while previous > 0:
                    offsets = [i for i, line in enumerate(file.lines) if line.side == side and line.number == previous]
                    if len(offsets) != 1 or marked.get((index, offsets[0])) not in {None, "blank"}:
                        break
                    text = source[previous - 1]
                    if text.strip() and not text.lstrip().startswith("//"):
                        break
                    prefix.insert(0, text)
                    prefix_positions.insert(0, offsets[0])
                    previous -= 1
                output.append(Candidate(index, member, body, positions, tuple(prefix), tuple(prefix_positions)))
    for old, new in pair_moves(removed, added):
        if (namespace(files[old.file].before) is None
                or namespace(files[old.file].before) != namespace(files[new.file].after)):
            continue
        kind = old.member.kind
        # Preserve the pre-existing single-line field kind in established support files.
        if (kind == "support_member_move" and old.member.start == old.member.end
                and " static readonly " in old.body[0] and files[new.file].status != "A"):
            kind = "helper_move"
        for candidate in (old, new):
            for offset in candidate.positions:
                marked[candidate.file, offset] = kind
        new_prefix = tuple("    " + line if line else line for line in new.prefix) if new.member.top_level else new.prefix
        if old.prefix == new_prefix:
            for candidate in (old, new):
                for offset in candidate.prefix_positions:
                    marked[candidate.file, offset] = kind
        moved.append({"kind": kind, "name": old.member.name,
                      "old_file": files[old.file].old_path, "new_file": files[new.file].new_path})
    unclassified: list[dict[str, object]] = []
    for index, file in enumerate(files):
        if file.status == "R" and not (file.old_path.endswith(".cs") and file.new_path.endswith(".cs")):
            unclassified.append({"file": file.new_path, "side": "!", "line": 0,
                                 "text": f"non-C# rename: {file.old_path} -> {file.new_path}"})
        if file.status == "R" and index in blocked and not file.lines:
            unclassified.append({"file": file.new_path, "side": "!", "line": 0,
                                 "text": f"unapproved rename: {file.old_path} -> {file.new_path}"})
        for offset, line in enumerate(file.lines):
            if kind := marked.get((index, offset)):
                counts[kind] += 1
            else:
                unclassified.append({"file": file.old_path if line.side == "-" else file.new_path,
                                     "side": line.side, "line": line.number, "text": line.text})
    return {"passed": not (unclassified or collection_failures), "changed_paths": len(files), "counts": counts,
            "unclassified": unclassified, "collection_consistency": collection_failures, "moves": moved}


class Parser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise ValueError(message)


def main(argv: Sequence[str] | None = None) -> int:
    try:
        parser = Parser(description=__doc__)
        commands = parser.add_subparsers(dest="command", required=True)
        first = commands.add_parser("e1")
        first.add_argument("--before", type=Path, required=True)
        first.add_argument("--after", type=Path, required=True)
        first.add_argument("--mapping", type=Path)
        third = commands.add_parser("e3")
        third.add_argument("--base", required=True)
        third.add_argument("--head", required=True)
        third.add_argument("--project", required=True)
        third.add_argument("--support-class")
        third.add_argument("--allow-directory-move", action="store_true")
        args = parser.parse_args(argv)
        if args.command == "e1":
            mapping = strict_json(args.mapping.read_bytes(), "mapping") if args.mapping else None
            if args.mapping and not isinstance(mapping, dict):
                raise ValueError("mapping must be a JSON object")
            report = e1(args.before.read_text(encoding="utf-8-sig"),
                        args.after.read_text(encoding="utf-8-sig"), mapping)
        else:
            git = Git(Path(__file__).resolve().parents[1])
            base, head = git.commit(args.base), git.commit(args.head)
            report = e3(read_diff(git, base, head, args.project), args.support_class, args.allow_directory_move)
            report.update(base=base, head=head, project=args.project, support_class=args.support_class)
        print(json.dumps(report, ensure_ascii=True, indent=2))
        return 0 if report["passed"] else 1
    except (ValueError, OSError, UnicodeError, AuthorityError) as error:
        print(json.dumps({"passed": False, "error": str(error)}, ensure_ascii=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
