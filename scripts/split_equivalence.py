"""Produce E1/E3 evidence for ADR 0079 mechanical test-class splits.

E1 preserves method arguments and multiplicity. A mapping normally uses exact
method identities; topics need explicit identities because discovery has no
source paths. E3 is deliberately a conservative C# subset, not a C# parser.
Unsupported syntax remains unclassified. Counts are signed changed lines,
except rename, which counts path pairs. This tool establishes neither E2 nor
E4 to E7. Unchanged lines are not examined for equivalence: a constructor in an
unchanged root partial that topic classes no longer share is invisible. Paths
outside --project are not examined. Plain added/removed using directives and
helper moves get no name-binding check beyond rejecting moved members whose
simple name remains declared outside the support class in touched/split files.
E1 without --mapping does not check the declared new class.
New support files classify constants and readonly fields as support_member_move;
established support files retain the legacy helper_move kind for single-line
readonly fields, keeping historical replay counts stable.
Collection attributes may be added once per new split class and on the source
class's root file. Accepted declaration pairs identify the single source class;
all split classes must retain the same collection membership.
Added memberships require a definition added by this diff or the source class's base collection.

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
    from scripts.split_equivalence_moves import Candidate, canonical_body, member_positions, member_spans, pair_moves
    from scripts.split_equivalence_paths import named_split_pairs
    from scripts.split_equivalence_source import (
        class_name, collection, code_lines, masked_source, namespace, memberships, class_at, declared_names,
    )
except ModuleNotFoundError:  # Direct invocation from scripts/.
    from authority_check import AuthorityError, Git, strict_json
    from split_equivalence_moves import Candidate, canonical_body, member_positions, member_spans, pair_moves
    from split_equivalence_paths import named_split_pairs
    from split_equivalence_source import (
        class_name, collection, code_lines, masked_source, namespace, memberships, class_at, declared_names,
    )


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
            raise ValueError(f"invalid discovery line: {line!r}; strip the preamble and keep the test names"
                             ' (and optionally the unindented "The following Tests are available:" header)')
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
    base_sources: tuple[tuple[str, tuple[str, ...]], ...] = ()
    binding_sources: tuple[tuple[str, tuple[str, ...]], ...] = ()


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
    pairs = named_split_pairs([(file.status, file.old_path if file.status == "D" else file.new_path,
                                code_lines(file.before if file.status == "D" else file.after)) for file in result])
    consumed: set[int] = set()
    for old_index, new_index in pairs:
        old, new = result[old_index], result[new_index]
        before, after = git.blob(base, old.old_path), git.blob(head, new.new_path)
        if before is None or after is None:
            raise ValueError("missing named split source blob")
        # Compare the two blobs directly; do not relax rename detection for other paths.
        patch = git.run("diff", *options, "-U0", before[0], after[0]).decode("utf-8")
        metadata = tuple(line for file in (old, new) for line in file.lines if line.side == "!")
        result[old_index] = replace(old, status="R", new_path=new.new_path, after=new.after,
                                    lines=patch_lines(patch) + metadata)
        consumed.add(new_index)
    result = [file for index, file in enumerate(result) if index not in consumed]
    if any(class_name(line.text) or line.text.startswith("/// <summary>")
           for file in result for line in file.lines):
        baseline: set[str] = set()
        base_sources: list[tuple[str, tuple[str, ...]]] = []
        paths = git.run("ls-tree", "-r", "-z", "--name-only", base, "--", str(path)).decode("utf-8").split("\0")
        for name in paths:
            if not name.endswith(".cs"):
                continue
            blob = git.blob(base, name)
            if blob is None:
                raise ValueError("missing baseline class source")
            source = source_lines(blob[1].decode("utf-8"))
            base_sources.append((name, source))
            for line in code_lines(source):
                match = re.match(r"\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|unsafe)\s+)*class\s+(\w+)", line)
                if match:
                    baseline.add(match[1])
        result = [replace(file, existing_classes=frozenset(baseline), base_sources=tuple(base_sources)) for file in result]
    roots = {target[1] for file in result for line in code_lines(file.before)
             if (target := class_name(line))}
    roots.update(match[1].rsplit(".", 1)[-1] for file in result for line in file.lines
                 if (match := re.fullmatch(r"global using static (?:global::)?([\w.]+);", line.text)))
    binding_roots = roots | {target[1] for file in result for line in code_lines(file.after)
                             if (target := class_name(line))}
    changed = {file.new_path for file in result}
    context: list[tuple[str, tuple[str, ...]]] = []
    binding_context: list[tuple[str, tuple[str, ...]]] = []
    if binding_roots:
        for name in git.run("ls-tree", "-r", "-z", "--name-only", head, "--", str(path)).decode("utf-8").split("\0"):
            if name not in changed and name.endswith(".cs"):
                blob = git.blob(head, name)
                if blob is None:
                    raise ValueError("missing head class source")
                source = source_lines(blob[1].decode("utf-8"))
                if PurePosixPath(name).name.split(".")[0] in roots:
                    context.append((name, source))
                if any(re.search(r"\bclass\s+" + re.escape(root) + r"\b", line)
                       for root in binding_roots for line in code_lines(source)):
                    binding_context.append((name, source))
    result = [replace(file, head_sources=tuple(context), binding_sources=tuple(binding_context)) for file in result]
    return tuple(result)


def using(line: str, support_class: str | None = None) -> bool:
    if re.fullmatch(r"(?:global )?using (?:global::)?\w+(?:\.\w+)*;", line):
        return True
    match = re.fullmatch(r"global using static (?:global::)?(\w+(?:\.\w+)*);", line)
    return bool(match and support_class and match[1] == support_class)


def definition(lines: tuple[str, ...]) -> bool:
    content = [line.rstrip("\r") for line in lines if line.strip() and not using(line)]
    if content and re.fullmatch(r"namespace \w+(?:\.\w+)*;", content[0]):
        content.pop(0)
    if content and re.fullmatch(r"/// <summary>.+</summary>", content[0]):
        content.pop(0)
    return (len(content) == 4 and collection(content[0], True)
            and re.fullmatch(r"public sealed class \w+", content[1]) is not None
            and content[2:] == ["{", "}"])


def members(lines: tuple[str, ...], include_annotated: bool = False,
            containers: frozenset[str] = frozenset()) -> tuple[tuple[int, int], ...]:
    return tuple((m.start, m.end) for m in member_spans(lines, code_lines(lines), include_annotated, containers)
                 if m.kind == "helper_move" or (m.kind == "support_member_move"
                                                and " static readonly " in lines[m.start - 1]))


def support_source(path: str, lines: tuple[str, ...], names: set[str]) -> bool:
    return any(PurePosixPath(path).name == name + ".cs" or
               re.fullmatch(re.escape(name) + r"\.\w+\.cs", PurePosixPath(path).name)
               for name in names) and sum(
                   bool((target := class_name(line)) and target[0].startswith("internal static") and target[1] in names)
                   for line in code_lines(lines)) == 1


def skeleton(lines: tuple[str, ...], names: set[str], source_partial: bool = False) -> set[int]:
    clean = code_lines(lines)
    declarations = [i for i, line in enumerate(clean) if (target := class_name(line))
                    and (target[0] == "public sealed partial class" if source_partial else
                         target[0].startswith("internal static")) and target[1] in names]
    if len(declarations) != 1 or namespace(lines) is None:
        return set()
    start = declarations[0]
    if start + 1 >= len(lines) or lines[start + 1] != "{":
        return set()
    allowed = {start + 1, start + 2}
    if not source_partial and start and re.fullmatch(r"/// <summary>.+</summary>", lines[start - 1]):
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
    return allowed if not source_partial or (closed and depth == 0) else set()


def e3(files: Sequence[FileDiff], support_class: str | None = None,
       allow_directory_move: bool = False) -> dict[str, object]:
    if not files:
        raise ValueError("no changed path in project")
    kinds = ("rename", "class_declaration", "summary", "using", "support_accessibility",
             "helper_move", "collection_attribute", "collection_definition", "blank",
             "support_member_move", "support_type_move", "support_file_skeleton", "emptied_partial_removed")
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
    base_sources = dict(source for file in files for source in file.base_sources)
    base_sources.update((file.old_path, file.before) for file in files if file.status != "A")
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
    unrelated_moves: list[dict[str, str]] = []
    reasons: dict[tuple[int, int], str] = {}
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
            elif (using(line.text, support_identity)
                  and file.status != "D"
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
    original_memberships = memberships({path: lines for path, lines in base_sources.items()
                                       if any((namespace(lines), name) in source_identities
                                              for _, name in source_classes)})
    new_collections = {line.text.replace("CollectionDefinition(", "Collection(", 1)
                       for index, file in enumerate(files) for offset, line in enumerate(file.lines)
                       if line.side == "+" and collection(line.text, True)
                       and marked.get((index, offset)) == "collection_definition"}
    original_collections = {item for key in source_classes for item in original_memberships.get(key, [])}
    if source_classes:
        values = {key: class_memberships.get(key, []) for key in sorted(split_classes)}
        if not single_source or len({tuple(value) for value in values.values()}) != 1 or any(
                len(value) > 1 or any(not collection(item) for item in value) for value in values.values()):
            collection_failures.append({"classes": sorted({key[1] for key in values}),
                                        "memberships": {str(key[0]) + "/" + key[1]: value for key, value in values.items()}})
        for item in sorted(collection_names - new_collections - original_collections):
            collection_failures.append({"classes": sorted({key[1] for key in values}), "collection": item,
                                        "reason": "collection is neither newly defined nor the source class's base collection"})
    allowed_origins = {(namespace(file.after), target[1]) for index, file in enumerate(files)
                       for offset, line in enumerate(file.lines)
                       if line.side == "+" and marked.get((index, offset)) == "class_declaration"
                       and (target := class_name(line.text))} | source_identities
    if not allowed_origins:
        # Standalone helper replays identify a single source without declaration changes.
        origins = {(namespace(file.before), target[1]) for file in files for line in code_lines(file.before)
                   if (target := class_name(line)) and target[1] not in support_names}
        if len(origins) == 1:
            allowed_origins = origins
    removed: list[Candidate] = []
    added: list[Candidate] = []
    for index, file in enumerate(files):
        if index in blocked or not (file.old_path.endswith(".cs") and file.new_path.endswith(".cs")):
            continue
        for side, source, output in (("-", file.before, removed), ("+", file.after, added)):
            if side == "+" and not support_source(file.new_path, file.after, support_names):
                continue
            for member in member_spans(source, code_lines(source), containers=frozenset(support_names)):
                changed = {line.number: offset for offset, line in enumerate(file.lines) if line.side == side}
                available = {offset for offset in changed.values() if marked.get((index, offset)) in {None, "blank"}}
                positions = member_positions(source, member, changed, available)
                if positions is None:
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
    pairs = pair_moves(removed, added)
    moved_types = {new.member.name for old, new in pairs if old.member.kind == "support_type_move"
                   and namespace(files[old.file].before) == namespace(files[new.file].after)}
    binding_sources = dict(source for file in files for source in file.binding_sources)
    binding_sources.update(head_sources)
    remaining_names = {path: declared_names(code_lines(lines), frozenset(support_names | moved_types)
                                            if support_source(path, lines, support_names) else frozenset())
                       for path, lines in binding_sources.items() if path.endswith(".cs")}
    for old, new in pairs:
        if (namespace(files[old.file].before) is None
                or namespace(files[old.file].before) != namespace(files[new.file].after)):
            continue
        collisions = sorted(path for path, names in remaining_names.items() if old.member.name in names)
        if collisions:
            reason = f"same-named member remains in {collisions[0]}: overload resolution may change"
            for candidate in (old, new):
                for offset in candidate.positions:
                    marked.pop((candidate.file, offset), None)
                    reasons[candidate.file, offset] = reason
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
        origin = class_at(code_lines(files[old.file].before), old.member.start)
        if (namespace(files[old.file].before), origin) not in allowed_origins:
            unrelated_moves.append({**moved[-1], "old_class": origin})
    move_kinds = {"helper_move", "support_member_move", "support_type_move"}
    for index, file in enumerate(files):
        if file.status != "D" or index in blocked or not single_source:
            continue
        directory, name = next(iter(source_classes))
        if PurePosixPath(file.old_path).parent != directory or (namespace(file.before), name) not in source_identities:
            continue
        allowed = skeleton(file.before, {name}, source_partial=True)
        if (allowed and any(marked.get((index, offset)) in move_kinds for offset in range(len(file.lines)))
                and all(line.side == "-" and not line.text.endswith("\r") and
                        (line.number in allowed or marked.get((index, offset)) in move_kinds | {"blank"})
                        for offset, line in enumerate(file.lines))):
            for offset, line in enumerate(file.lines):
                if line.number in allowed:
                    marked[index, offset] = "emptied_partial_removed"
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
                                     "side": line.side, "line": line.number, "text": line.text,
                                     **({"reason": reasons[index, offset]} if (index, offset) in reasons else {})})
    return {"passed": not (unclassified or collection_failures or unrelated_moves), "changed_paths": len(files), "counts": counts,
            "unclassified": unclassified, "collection_consistency": collection_failures, "moves": moved,
            "moves_from_unrelated_classes": unrelated_moves}


class Parser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise ValueError(message)


def main(argv: Sequence[str] | None = None) -> int:
    try:
        parser = Parser(description=__doc__)
        commands = parser.add_subparsers(dest="command", required=True)
        first = commands.add_parser("e1", help="Compare discovery identities and multiplicity.", description=__doc__)
        first.add_argument("--before", type=Path, required=True, help="Base discovery text: test names and optional headers.")
        first.add_argument("--after", type=Path, required=True, help="Head discovery text in the same format as --before.")
        first.add_argument("--mapping", type=Path, help="JSON mapping of identities to declared old/new classes; without it the declared new class is not checked.")
        third = commands.add_parser("e3", help="Classify changed lines inside one project.", description=__doc__)
        third.add_argument("--base", required=True, help="Base Git commit; must be an ancestor of --head.")
        third.add_argument("--head", required=True, help="Head Git commit to compare with --base.")
        third.add_argument("--project", required=True, help="Repository-relative project path; paths outside it are not examined.")
        third.add_argument("--support-class", help="Simple name of the shared static support class.")
        third.add_argument("--allow-directory-move", action="store_true", help="Allow C# renames across directories; all other checks still apply.")
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
