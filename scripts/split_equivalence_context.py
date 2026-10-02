"""Git source context for conservative project-wide E3 binding checks."""

from __future__ import annotations

import re
from pathlib import PurePosixPath

try:
    from scripts.authority_check import AuthorityError
    from scripts.split_equivalence_bindings import alias_names, declarations, imported_namespaces, type_facts, value_kind, visible_facts
    from scripts.split_equivalence_source import class_name, code_lines
except ModuleNotFoundError:
    from authority_check import AuthorityError
    from split_equivalence_bindings import alias_names, declarations, imported_namespaces, type_facts, value_kind, visible_facts
    from split_equivalence_source import class_name, code_lines


def base_context(git, base, project, source_lines):
    """Always collect base imports/types, including helper-only diffs."""
    baseline, sources = set(), []
    paths = git.run("ls-tree", "-r", "-z", "--name-only", base, "--", str(project)).decode("utf-8").split("\0")
    for path in paths:
        if not path.endswith(".cs"):
            continue
        blob = git.blob(base, path)
        if blob is None:
            raise ValueError("missing baseline class source")
        lines = source_lines(blob[1].decode("utf-8"))
        sources.append((path, lines))
        for line in code_lines(lines):
            match = re.match(r"\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|unsafe)\s+)*class\s+(\w+)", line)
            if match:
                baseline.add(match[1])
    return frozenset(baseline), tuple(sources)


def head_context(git, head, project, changed, roots, after_sources):
    # All project types matter, even files whose names/classes do not match a
    # split root. Keep collection head context at its old, narrower boundary.
    context, binding, project_sources = [], [], []
    sources = dict(after_sources)
    helper_names = {item.name for lines in sources.values() for item in declarations(code_lines(lines)) if item.kind == "method"}
    binding_roots = roots | {target[1] for lines in sources.values() for line in code_lines(lines)
                             if (target := class_name(line))}
    for path in git.run("ls-tree", "-r", "-z", "--name-only", head, "--", str(project)).decode("utf-8").split("\0"):
        if path in changed or not path.endswith(".cs"):
            continue
        blob = git.blob(head, path)
        if blob is None:
            raise ValueError("missing head class source")
        lines = tuple(blob[1].decode("utf-8").removesuffix("\n").split("\n"))
        sources[path] = lines
        project_sources.append((path, lines))
        if any(re.search(r"\bclass\s+" + re.escape(root) + r"\b", line)
               for root in binding_roots for line in code_lines(lines)):
            binding.append((path, lines))
        if PurePosixPath(path).name.split(".")[0] in roots:
            context.append((path, lines))
    facts = type_facts(sources)
    aliases = alias_names(sources)
    global_imports = imported_namespaces(sources, global_only=True)
    unresolved = {item.type_name.strip().removesuffix("?") for lines in sources.values()
                  for item in declarations(code_lines(lines)) if item.nested and item.kind == "value"
                  and item.name in helper_names
                  and re.fullmatch(r"\w+\??", item.type_name.strip())
                  and value_kind(item.type_name, visible_facts(facts, lines, item.declaring_type, global_imports), aliases) == "unknown"}
    extra = set()
    if unresolved:
        # Type-kind evidence only, not equivalence outside --project. Unknown
        # dependency types still block; never guess that a custom type is not
        # a delegate. Read matching tracked sources at the actual head revision.
        pattern = r"\b(" + "|".join(sorted(unresolved)) + r")\b"
        try:
            paths = git.run("grep", "-l", "-E", pattern, head, "--", "src").decode("utf-8").splitlines()
        except AuthorityError as error:
            if str(error) != "git grep -l failed: 1":
                raise
            paths = []
        for entry in paths:
            path = entry.removeprefix(head + ":")
            if not path.endswith(".cs"):
                continue
            blob = git.blob(head, path)
            if blob is None:
                raise ValueError("missing dependency type source")
            lines = tuple(blob[1].decode("utf-8").removesuffix("\n").split("\n"))
            for name, kinds in type_facts({path: lines}).items():
                extra.update((name, kind) for kind in kinds)
    return tuple(context), tuple(binding), tuple(project_sources), tuple(sorted(extra))
