"""Produce E1/E3 evidence for ADR 0079 mechanical test-class splits.

E1 preserves method arguments and multiplicity. A mapping normally uses exact
method identities; topics need explicit identities because discovery has no
source paths. E3 is deliberately a conservative C# subset, not a C# parser.
Unsupported syntax remains unclassified. Counts are signed changed lines,
except rename, which counts path pairs. This tool does not establish E7.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

try:
    from scripts.authority_check import AuthorityError, Git, strict_json
except ModuleNotFoundError:  # Direct invocation from scripts/.
    from authority_check import AuthorityError, Git, strict_json


@dataclass(frozen=True)
class Identity:
    class_name: str
    method: str


def discovery(text: str) -> tuple[Identity, ...]:
    """Accept one optional, language-independent header and fully qualified cases."""
    result: list[Identity] = []
    header = False
    for line in text.splitlines():
        value = line.strip().removeprefix("\ufeff")
        if not value:
            continue
        match = re.fullmatch(r"([\w]+(?:\.[\w]+)*)\.([\w]+(?:\(.*\))?)", value)
        if match:
            result.append(Identity(*match.groups()))
        elif not result and not header:
            header = True
        else:
            raise ValueError(f"invalid discovery line: {line!r}")
    if not result:
        raise ValueError("discovery contains no fully qualified tests")
    return tuple(result)


def method_mapping(value: object, identities: set[str]) -> dict[str, str]:
    """Expand {identity: class} or {topic: {class: ..., identities: [...]}}."""
    if not isinstance(value, dict):
        raise ValueError("mapping must be a JSON object")
    result: dict[str, str] = {}
    for key, target in value.items():
        methods = [key]
        if isinstance(target, dict) and set(target) == {"class", "identities"}:
            methods, target = target["identities"], target["class"]
        if (not isinstance(methods, list) or not methods
                or not isinstance(target, str)
                or not re.fullmatch(r"\w+(?:\.\w+)*", target)):
            raise ValueError(f"invalid mapping entry: {key!r}")
        for method in methods:
            if not isinstance(method, str) or method not in identities or method in result:
                raise ValueError(f"unknown or repeated mapping identity: {method!r}")
            result[method] = target
    if result.keys() != identities:
        raise ValueError(f"mapping lacks identities: {sorted(identities - result.keys())}")
    return result


def e1(before: str, after: str, mapping: object | None = None) -> dict[str, object]:
    old, new = discovery(before), discovery(after)
    left, right = Counter(item.method for item in old), Counter(item.method for item in new)
    expected = method_mapping(mapping, set(left)) if mapping is not None else {}
    unexpected = [{"identity": item.method, "actual": item.class_name,
                   "expected": expected.get(item.method)} for item in new if expected
                  and expected.get(item.method) not in (item.class_name, item.class_name.rsplit(".", 1)[-1])]
    missing, added = sorted((left - right).elements()), sorted((right - left).elements())
    return {"passed": not (missing or added or unexpected), "before_count": len(old),
            "after_count": len(new), "missing": missing, "added": added,
            "moved-to-unexpected-class": unexpected}


@dataclass(frozen=True)
class Line:
    side: str
    number: int
    text: str


@dataclass(frozen=True)
class FileDiff:
    status: str
    old_path: str
    new_path: str
    before: tuple[str, ...]
    after: tuple[str, ...]
    lines: tuple[Line, ...]
    existing_classes: frozenset[str] = frozenset()


def source_lines(text: str) -> tuple[str, ...]:
    """Retain CR characters: whitespace changes must not hide in helper moves."""
    return tuple(text.removesuffix("\n").split("\n")) if text else ()


def patch_lines(patch: str) -> tuple[Line, ...]:
    result: list[Line] = []
    old = new = remaining_old = remaining_new = 0
    for line in patch.split("\n"):
        match = re.match(r"@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", line)
        if match:
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
                result.append(Line("-", old, line[1:]))
                old, remaining_old = old + 1, remaining_old - 1
            elif line.startswith("+") and remaining_new:
                result.append(Line("+", new, line[1:]))
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
    path = PurePosixPath(project.replace("\\", "/"))
    if not project or path.is_absolute() or ".." in path.parts or ":" in project:
        raise ValueError("project must be a repository-relative path")
    if not (git.run("ls-tree", "-z", "--name-only", base, "--", str(path))
            or git.run("ls-tree", "-z", "--name-only", head, "--", str(path))):
        raise ValueError("project does not exist at either revision")
    options = ("--find-renames", "-l0", "--no-ext-diff", "--no-textconv", "--no-color")
    names = name_list(git.run("diff", *options, "--name-status", "-z", base, head, "--", str(path)))
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
    if any(line.side == "+" and line.text.startswith("/// <summary>")
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
        result = [FileDiff(file.status, file.old_path, file.new_path, file.before, file.after,
                           file.lines, frozenset(baseline)) for file in result]
    return tuple(result)


def class_name(line: str) -> tuple[str, str] | None:
    match = re.fullmatch(r"((?:public sealed(?: partial)?|internal static partial) class) (\w+)\r?", line)
    return match.groups() if match else None


def using(line: str) -> bool:
    return re.fullmatch(r"(?:global )?using (?:static |\w+ = )?(?:global::)?\w+(?:\.\w+)*;\r?", line) is not None


def collection(line: str, definition: bool = False) -> bool:
    name = "CollectionDefinition" if definition else "Collection"
    argument = r'(?:nameof\(\w+(?:\.\w+)*\)|"[\w .-]+")'
    option = r"(?:, DisableParallelization = (?:true|false))?" if definition else ""
    return re.fullmatch(r"\[" + name + r"\(" + argument + option + r"\)\]\r?", line) is not None


def definition(lines: tuple[str, ...]) -> bool:
    content = [line.rstrip("\r") for line in lines if line.strip() and not using(line)]
    if content and re.fullmatch(r"namespace \w+(?:\.\w+)*;", content[0]):
        content.pop(0)
    if content and re.fullmatch(r"/// <summary>.+</summary>", content[0]):
        content.pop(0)
    return (len(content) == 4 and collection(content[0], True)
            and re.fullmatch(r"public sealed class \w+", content[1]) is not None
            and content[2:] == ["{", "}"])


def code_lines(lines: tuple[str, ...]) -> tuple[str, ...]:
    """Mask literal/comment contents without shifting line numbers; retain XML summaries."""
    pattern = (r'(?P<raw>"{3,})[\s\S]*?(?P=raw)|@"(?:""|[^"])*"|'
               r'"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'|'
               r'/\*[\s\S]*?\*/|(?P<summary>///[^\n]*)|//[^\n]*')
    clean = re.sub(pattern, lambda match: match[0] if match["summary"] is not None
                   else re.sub(r"[^\n]", " ", match[0]), "\n".join(lines))
    return tuple(clean.split("\n"))


def members(lines: tuple[str, ...], include_annotated: bool = False) -> tuple[tuple[int, int], ...]:
    """Find complete static helper spans, refusing ambiguous strings/comments."""
    spans: list[tuple[int, int]] = []
    clean_lines = code_lines(lines)
    for start, line in enumerate(lines):
        if not re.match(r"    (?:private|internal) static \S", clean_lines[start]):
            continue
        if not include_annotated and start and lines[start - 1].lstrip().startswith("["):
            # Attributes can carry semantics; accessibility-only pairs still work below.
            continue
        clean = "\n".join(clean_lines[start:])
        stack: list[str] = []
        number = start
        for position, char in enumerate(clean):
            if char == "\n":
                number += 1
            elif char in "([{":
                stack.append(char)
            elif char in ")]}":
                if not stack or stack.pop() != {")": "(", "]": "[", "}": "{"}[char]:
                    break
                if char == "}" and not stack:
                    end = clean.find("\n", position)
                    if not clean[position + 1:end if end >= 0 else None].strip():
                        spans.append((start + 1, number + 1))
                    break
            elif char == ";" and not stack:
                end = clean.find("\n", position)
                if not clean[position + 1:end if end >= 0 else None].strip():
                    spans.append((start + 1, number + 1))
                break
            elif char in "\"'":
                break
    return tuple(spans)


def e3(files: Sequence[FileDiff], support_class: str | None = None) -> dict[str, object]:
    kinds = ("rename", "class_declaration", "summary", "using", "support_accessibility",
             "helper_move", "collection_attribute", "collection_definition", "blank")
    counts = dict.fromkeys(kinds, 0)
    marked: dict[tuple[int, int], str] = {}
    old_classes = {value[1] for file in files for line in code_lines(file.before) if (value := class_name(line))}
    old_classes.update(name for file in files for name in file.existing_classes)
    support_names = {value[1] for file in files for line in code_lines(file.after)
                     if (value := class_name(line)) and value[0] == "internal static partial class"}
    if support_class is not None:
        support_names &= {support_class}
    if len(support_names) > 1:
        raise ValueError("multiple support classes; specify --support-class")
    summaries: Counter[str] = Counter()
    for index, file in enumerate(files):
        if file.status == "R":
            counts["rename"] += 1
        before_code, after_code = code_lines(file.before), code_lines(file.after)
        is_support = (sum(class_name(line) is not None for line in after_code) == 1
                      and any(class_name(line) == ("internal static partial class", name)
                              for line in after_code for name in support_names))
        is_definition = file.status in {"A", "D"} and definition(file.after or file.before)
        for offset, line in enumerate(file.lines):
            key = index, offset
            if line.side not in {"+", "-"} or not file.new_path.endswith(".cs"):
                continue
            if not line.text.strip():
                marked[key] = "blank"
            elif not (before_code if line.side == "-" else after_code)[line.number - 1].strip():
                continue
            elif using(line.text):
                marked[key] = "using"
            elif is_definition:
                marked[key] = "collection_definition"
            elif collection(line.text):
                marked[key] = "collection_attribute"
            elif (new_class := class_name(line.text)) and line.side == "+":
                partners = [(position, item) for position, item in enumerate(file.lines)
                            if item.side == "-" and class_name(item.text)
                            and before_code[item.number - 1].strip()
                            and (index, position) not in marked]
                if len(partners) == 1:
                    position, item = partners[0]
                    old_class = class_name(item.text)
                    if old_class and (new_class[0] == old_class[0] or
                                      (is_support and new_class[1] in support_names)):
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
            old_starts = {start for start, _ in members(file.before, True)}
            new_starts = {start for start, _ in members(file.after, True)}
            for offset, line in enumerate(file.lines):
                if line.side == "+" and line.number in new_starts and re.match(r"    internal static \S", line.text):
                    for position, old in enumerate(file.lines):
                        if (old.side == "-" and old.number in old_starts and re.match(r"    private static \S", old.text)
                                and old.text.replace("private", "internal", 1) == line.text
                                and (index, position) not in marked):
                            marked[index, offset] = marked[index, position] = "support_accessibility"
                            break
    # Match whole removed helpers to whole added helpers; consume each occurrence once.
    removed: list[tuple[int, tuple[str, ...], tuple[int, ...]]] = []
    added: list[tuple[int, tuple[str, ...], tuple[int, ...]]] = []
    for index, file in enumerate(files):
        for side, source, output in (("-", file.before, removed), ("+", file.after, added)):
            if not (file.old_path if side == "-" else file.new_path).endswith(".cs"):
                continue
            clean = code_lines(source)
            if side == "+" and not (sum(class_name(line) is not None for line in clean) == 1
                                    and any(class_name(line) == ("internal static partial class", name)
                                            for line in clean for name in support_names)):
                continue
            for start, end in members(source):
                positions = tuple(offset for offset, line in enumerate(file.lines)
                                  if line.side == side and start <= line.number <= end)
                if len(positions) == end - start + 1 and all(marked.get((index, p)) in {None, "blank"} for p in positions):
                    body = list(source[start - 1:end])
                    if side == "-" and body[0].startswith("    private static "):
                        body[0] = body[0].replace("private", "internal", 1)
                    elif not body[0].startswith("    internal static "):
                        continue
                    output.append((index, tuple(body), positions))
    for index, body, positions in removed:
        for candidate, (target, new_body, new_positions) in enumerate(added):
            if body == new_body and index != target:
                for owner, offsets in ((index, positions), (target, new_positions)):
                    for offset in offsets:
                        marked[owner, offset] = "helper_move"
                added.pop(candidate)
                break
    unclassified: list[dict[str, object]] = []
    for index, file in enumerate(files):
        for offset, line in enumerate(file.lines):
            if kind := marked.get((index, offset)):
                counts[kind] += 1
            else:
                unclassified.append({"file": file.old_path if line.side == "-" else file.new_path,
                                     "side": line.side, "line": line.number, "text": line.text})
    return {"passed": not unclassified, "changed_paths": len(files), "counts": counts,
            "unclassified": unclassified}


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
        args = parser.parse_args(argv)
        if args.command == "e1":
            mapping = strict_json(args.mapping.read_bytes(), "mapping") if args.mapping else None
            if args.mapping and not isinstance(mapping, dict):
                raise ValueError("mapping must be a JSON object")
            report = e1(args.before.read_text(encoding="utf-8-sig"),
                        args.after.read_text(encoding="utf-8-sig"), mapping)
        else:
            git = Git(Path(__file__).resolve().parents[1])
            report = e3(read_diff(git, args.base, args.head, args.project), args.support_class)
        print(json.dumps(report, ensure_ascii=True, indent=2))
        return 0 if report["passed"] else 1
    except (ValueError, OSError, UnicodeError, AuthorityError) as error:
        print(json.dumps({"passed": False, "error": str(error)}, ensure_ascii=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
