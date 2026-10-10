"""Regenerate docs/governance/public-content-policy.json; only SHA-256 values are written.

Restricted words come from a local text file kept outside the repository (one word per line, ``#`` starts a
comment line). The words already hashed in the policy are kept. Run this after cleaning lines, after adding
words, or after changing the workbook pattern, then review the diff and the printed digest. A run that would
add legacy matches without new words stops unless ``--allow-growth`` is given. See
docs/governance/public-content-policy.md.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections.abc import Iterable
from pathlib import Path
from typing import Any

import validate_repository as repository

_WORD = re.compile(r"[a-z0-9]+")
_PIN = re.compile(r'PUBLIC_CONTENT_POLICY_SHA256 = "[0-9a-f]{64}"')


def read_words(text: str) -> list[str]:
    """Return the folded words of a word list; a line that cannot match by hash stops the run.

    The error names the line number only, never the word.
    """
    words: list[str] = []
    for number, line in enumerate(repository.public_content_split_lines(text), 1):
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        word = repository.public_content_fold(stripped).casefold()
        if not _WORD.fullmatch(word):
            raise SystemExit(f"restricted word on line {number} must be one ASCII word of letters and digits")
        words.append(word)
    return words


def _entries(document: dict[str, Any]) -> dict[tuple[str, str], int]:
    """Allowance counts keyed by (path hash, line hash); a file name allowance has an empty line hash."""
    entries = {(path_hash, ""): count for path_hash, count in document["legacyPathNames"].items()}
    for path_hash, lines in document["legacyLines"].items():
        for line_hash, count in lines.items():
            entries[(path_hash, line_hash)] = count
    return entries


def build_policy(root: Path, files: Iterable[Path], words: Iterable[str], existing: dict[str, Any]) -> dict[str, Any]:
    """Return a policy document that allows exactly the matches present in ``files`` now."""
    word_hashes = set(existing["restrictedTokenSha256"])
    word_hashes |= {repository.public_content_sha256(word) for word in words}
    workbook = re.compile(existing["workbookNamePattern"], re.IGNORECASE)
    names: dict[str, int] = {}
    lines_by_file: dict[str, dict[str, int]] = {}
    for path in files:
        relative = path.relative_to(root).as_posix()
        if relative == repository.PUBLIC_CONTENT_POLICY:
            continue
        path_hash = repository.public_content_sha256(relative)
        hits = repository.public_content_match_count(relative, workbook, word_hashes)
        if hits:
            names[path_hash] = hits
        text_lines, problem = repository.public_content_text_lines(path)
        if problem:
            raise SystemExit(f"cannot scan public content ({problem}): {relative}")
        for text in text_lines or []:
            found = repository.public_content_match_count(text, workbook, word_hashes)
            if found:
                line_hash = repository.public_content_sha256(text)
                entry = lines_by_file.setdefault(path_hash, {})
                entry[line_hash] = entry.get(line_hash, 0) + found
    return {
        "schemaVersion": "1.0",
        "kind": "public-content-policy",
        "workbookNamePattern": existing["workbookNamePattern"],
        "restrictedTokenSha256": sorted(word_hashes),
        "legacyPathNames": dict(sorted(names.items())),
        "legacyLines": {key: dict(sorted(value.items())) for key, value in sorted(lines_by_file.items())},
    }


def grown_files(root: Path, files: Iterable[Path], before: dict[str, Any], after: dict[str, Any]) -> list[str]:
    """Relative paths that have a new allowance entry or a larger count in ``after`` than in ``before``."""
    old = _entries(before)
    grown_hashes = {key[0] for key, count in _entries(after).items() if count > old.get(key, 0)}
    return [
        relative
        for relative in (path.relative_to(root).as_posix() for path in files)
        if repository.public_content_sha256(relative) in grown_hashes
    ]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=(__doc__ or "").splitlines()[0])
    parser.add_argument("--words-file", type=Path, help="local file with extra restricted words, one per line")
    parser.add_argument("--pin", action="store_true", help="also set PUBLIC_CONTENT_POLICY_SHA256 in the validator")
    parser.add_argument("--allow-growth", action="store_true", help="accept more legacy matches without new words")
    args = parser.parse_args(argv)

    root = repository.ROOT
    policy_path = root / repository.PUBLIC_CONTENT_POLICY
    validator = root / "scripts" / "validate_repository.py"
    try:
        # The digest is checked by the validator, not here: this tool produces the next digest.
        current = json.loads(policy_path.read_text(encoding="utf-8"))
        with validator.open(encoding="utf-8", newline="") as handle:
            source = handle.read()
    except (OSError, ValueError) as exc:
        print(f"cannot read the policy or the validator: {exc}", file=sys.stderr)
        return 1
    errors: list[str] = []
    existing = repository.load_public_content_policy(
        root, errors, expected_digest=repository.public_content_policy_digest(current)
    )
    if existing is None:
        print("\n".join(errors), file=sys.stderr)
        return 1
    if args.pin and len(_PIN.findall(source)) != 1:
        print("PUBLIC_CONTENT_POLICY_SHA256 assignment not found exactly once", file=sys.stderr)
        return 1

    try:
        words = read_words(args.words_file.read_text(encoding="utf-8-sig")) if args.words_file else []
    except (OSError, UnicodeDecodeError) as exc:
        print(f"cannot read the word list: {type(exc).__name__}", file=sys.stderr)
        return 1
    if repository.public_content_policy_digest(current) != repository.PUBLIC_CONTENT_POLICY_SHA256:
        print("note: the policy file differs from the digest pinned in the validator (edited by hand?)")
    files = list(repository.repository_files())
    # Matches of the words the policy already holds must not grow without a review; only new words may add some.
    baseline = build_policy(root, files, [], existing)
    grown_by_old_words = grown_files(root, files, existing, baseline)
    if grown_by_old_words and not args.allow_growth:
        print("allowance would grow in:", file=sys.stderr)
        print("\n".join(f"  {relative}" for relative in grown_by_old_words), file=sys.stderr)
        print("Clean those lines, or review them and pass --allow-growth.", file=sys.stderr)
        return 2
    document = build_policy(root, files, words, existing) if words else baseline
    grown = grown_files(root, files, existing, document)
    digest = repository.public_content_policy_digest(document)
    if args.pin:
        updated = _PIN.sub(f'PUBLIC_CONTENT_POLICY_SHA256 = "{digest}"', source)
        validator.write_text(updated, encoding="utf-8", newline="")
    policy_path.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")

    def total(value: dict[str, Any]) -> int:
        return sum(value["legacyPathNames"].values()) + sum(sum(item.values()) for item in value["legacyLines"].values())

    print(
        f"policy written: {len(document['restrictedTokenSha256'])} word hashes, "
        f"legacy matches {total(existing)} -> {total(document)}"
    )
    for relative in grown:
        print(f"  allowance grew: {relative}")
    print(f"PUBLIC_CONTENT_POLICY_SHA256 = {digest}{' (pinned)' if args.pin else ''}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
