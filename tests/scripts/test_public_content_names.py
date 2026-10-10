"""Regression checks for restricted names in public tracked file names and text."""

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from generate_public_content_policy import build_policy, grown_files, read_words  # noqa: E402
from validate_repository import (  # noqa: E402
    PUBLIC_CONTENT_POLICY,
    PUBLIC_CONTENT_POLICY_SHA256,
    load_public_content_policy,
    public_content_policy_digest,
    validate_public_content_names,
)

WORD = "zetaword"
WORKBOOK = "IC_" + "FlashMap_20990101.xlsx"
PATTERN = (
    r"(?<![A-Za-z])IC(?:[\s_.\-]|%20)*Flash(?:[\s_.\-]|%20)*Map[^/\\\r\n\"'<>|*?]{0,80}?\.xls[xmb]?"
)
SHAPE_ERROR = [f"invalid public content policy shape: {PUBLIC_CONTENT_POLICY}"]
NUL_REASON = (
    "NUL byte in a file with an unlisted binary suffix; "
    "for a real binary format add the suffix to _PUBLIC_BINARY_SUFFIXES with governance-owner review"
)
GUARD_FILES = [
    "scripts/validate_repository.py",
    "scripts/generate_public_content_policy.py",
    "tests/scripts/test_public_content_names.py",
    "docs/governance/public-content-policy.md",
    "docs/contracts/release-manifest-v1.md",
]


def sha(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def stale(path: str, *lines: str) -> str:
    hashes = ", ".join(sorted(sha(line)[:12] for line in lines))
    return (
        f"stale public content allowance in {path} (line hashes {hashes}); "
        f"remove the cleaned lines from {PUBLIC_CONTENT_POLICY} and delete the file key when no line is left"
    )


def policy(names: dict | None = None, lines: dict | None = None, words: list[str] | None = None) -> dict:
    return {
        "schemaVersion": "1.0",
        "kind": "public-content-policy",
        "workbookNamePattern": PATTERN,
        "restrictedTokenSha256": sorted(sha(word) for word in (words or [WORD])),
        "legacyPathNames": names or {},
        "legacyLines": lines or {},
    }


def write(tmp_path: Path, files: dict[str, str | bytes]) -> list[Path]:
    paths = []
    for name, content in files.items():
        path = tmp_path / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(content, bytes):
            path.write_bytes(content)
        else:
            path.write_text(content, encoding="utf-8")
        paths.append(path)
    return paths


def run(tmp_path: Path, files: dict[str, str | bytes], document: dict) -> list[str]:
    errors: list[str] = []
    validate_public_content_names(write(tmp_path, files), errors, root=tmp_path, policy=document)
    return errors


def flagged(*numbers: int, path: str = "a.md") -> list[str]:
    return [f"restricted public name in {path}:{number}" for number in numbers]


def test_rejects_new_restricted_word_and_workbook_name(tmp_path: Path) -> None:
    errors = run(tmp_path, {"a.md": f"fine\ncase {WORD}-d82\n", "b.md": f"see {WORKBOOK}\n"}, policy())

    assert errors == ["restricted public name in a.md:2", "restricted public name in b.md:1"]


def test_workbook_name_variants_are_rejected(tmp_path: Path) -> None:
    text = (
        "ic_flash" + "map_20990101.xlsx\n"
        "IC-Flash" + "Map-20990101.xlsx\n"
        "IC_FLASH" + "MAP_1.XLS\n"
        "IC Flash" + "Map 2099.07.01.xlsx\n"
        "IC%20Flash" + "Map_1.xlsx\n"
        "IC_Flash" + "Map_20990101 (1).xlsx\n"
        "IC Flash " + "Map.xlsm\n"
        "x_IC_Flash" + "Map_1.xlsx\n"
        "20990101IC_Flash" + "Map.xlsx\n"
    )

    assert run(tmp_path, {"a.md": text}, policy()) == flagged(1, 2, 3, 4, 5, 6, 7, 8, 9)
    assert run(tmp_path, {"b.md": "magic flash" + "map note.xlsx\nPUBLIC_Flash" + "Map_1.xlsx\n"}, policy()) == []


def test_rejects_words_in_camel_case_digits_joined_parts_and_long_identifiers(tmp_path: Path) -> None:
    text = (
        "AlphaZetawordBeta\nZETAWORD1720\nzeta_word\nZeta.Word\nMyZetaWordTests\n"
        "ze_ta_wo_rd\nAlphaZetawordCtrlRamReplaceTests\n"
    )

    assert run(tmp_path, {"a.md": text}, policy()) == flagged(1, 2, 3, 4, 5, 6, 7)


def test_join_is_limited_to_four_parts(tmp_path: Path) -> None:
    assert run(tmp_path, {"a.md": "z_e_ta_wo_rd\n"}, policy()) == []


def test_rejects_invisible_accented_and_compatibility_forms(tmp_path: Path) -> None:
    invisible = [0x200B, 0x2060, 0x200D, 0x034F, 0xFE0F, 0x3164, 0x2800]
    text = "".join("zeta" + chr(code) + "word\n" for code in invisible)
    accented = "zetaw" + chr(0xF3) + "rd\n"
    full_width = "".join(chr(0xFF00 + ord(char) - 0x20) for char in "zeta") + "word\n"

    assert run(tmp_path, {"a.md": text}, policy()) == flagged(1, 2, 3, 4, 5, 6, 7)
    assert run(tmp_path, {"b.md": accented + full_width}, policy()) == flagged(1, 2, path="b.md")


def test_allows_unrelated_words_that_contain_the_word(tmp_path: Path) -> None:
    errors = run(tmp_path, {"a.md": f"{WORD}s and un{WORD}\nzeta word\n"}, policy())

    assert errors == []


def test_only_single_case_hex_blobs_are_not_split(tmp_path: Path) -> None:
    document = policy(words=["deadbeef"])
    text = "id " + "0123456789" + "deadbeef" + "01234\ndeadbeef\nDeadBeefFaceCafeBeadFace\n"

    assert run(tmp_path, {"a.md": text}, document) == flagged(2, 3)


def test_line_numbers_follow_cr_lf_and_crlf_only(tmp_path: Path) -> None:
    text = ("a" + chr(0x2028) + "b\r\nc" + chr(0x0B) + "d\r" + WORD + "\n").encode("utf-8")

    assert run(tmp_path, {"a.md": text}, policy()) == flagged(3)


def test_rejects_restricted_file_names_unless_listed(tmp_path: Path) -> None:
    name = f"docs/{WORD}-case.md"
    listed = policy(names={sha(name): 1})
    over_listed = policy(names={sha(name): 2})

    assert run(tmp_path, {name: "clean\n"}, policy()) == [f"restricted public name in file name {name}"]
    assert run(tmp_path, {name: "clean\n"}, listed) == []
    assert run(tmp_path, {name: "clean\n"}, over_listed) == [
        f"stale public content file name allowance {sha(name)[:12]}"
    ]
    assert run(tmp_path, {"a.md": "clean\n"}, policy(names={sha("a.md"): 1})) == [
        f"stale public content file name allowance {sha('a.md')[:12]}"
    ]


def test_allows_exact_legacy_line_but_not_a_second_use(tmp_path: Path) -> None:
    line = f"old {WORD} case"
    document = policy(lines={sha("a.md"): {sha(line): 1}})

    assert run(tmp_path, {"a.md": f"{line}\n"}, document) == []
    assert run(tmp_path, {"a.md": f"{line}\n{line}\n"}, document) == flagged(2)
    assert run(tmp_path, {"a.md": f"{line} {WORD}\n"}, document) == flagged(1) + [stale("a.md", line)]


def test_legacy_line_is_bound_to_its_file(tmp_path: Path) -> None:
    line = f"old {WORD} case"
    document = policy(lines={sha("a.md"): {sha(line): 1}})

    errors = run(tmp_path, {"a.md": "clean\n", "b.md": f"{line}\n"}, document)

    assert errors == [stale("a.md", line)] + flagged(1, path="b.md")


def test_reports_allowance_for_a_missing_or_binary_file(tmp_path: Path) -> None:
    gone = policy(lines={sha("gone.md"): {sha(f"old {WORD}"): 1}})
    binary = policy(lines={sha("a.bin"): {sha(f"old {WORD}"): 1}})

    assert run(tmp_path, {"a.md": "clean\n"}, gone) == [
        f"stale public content allowance for a removed file {sha('gone.md')[:12]}"
    ]
    assert run(tmp_path, {"a.bin": b"\0\x01"}, binary) == [
        "unscannable file has a public content allowance: a.bin",
    ]


def test_scans_utf16_utf32_and_legacy_encodings_and_reports_unreadable_text(tmp_path: Path) -> None:
    errors = run(
        tmp_path,
        {
            "a.txt": ("see " + WORD + "\n").encode("utf-16"),
            "b.bin": WORD.encode() + b"\0\x01",
            "c.txt": b"\xff\xfe\xfa" + WORD.encode(),
            "d.txt": b"ok\0" + WORD.encode(),
            "e.txt": ("caf" + chr(0xE9) + " " + WORD + "\n").encode("cp1252"),
            "f.txt": b"\xef\xbb\xbfclean\n",
            "g.txt": ("see " + WORD + "\n").encode("utf-32"),
            "h.txt": ("\0see " + WORD + "\n").encode("utf-16"),
            "i.txt": b"\xff\xfe\x00\x00\x00\x00\x11\x00z\x00\x00\x00",
        },
        policy(),
    )

    assert errors == [
        "restricted public name in a.txt:1",
        "cannot scan public content (undecodable UTF-16 or UTF-32 file): c.txt",
        f"cannot scan public content ({NUL_REASON}): d.txt",
        "restricted public name in e.txt:1",
        "restricted public name in g.txt:1",
        "restricted public name in h.txt:1",
        "cannot scan public content (undecodable UTF-16 or UTF-32 file): i.txt",
    ]


def test_generated_policy_allows_exactly_the_present_matches(tmp_path: Path) -> None:
    paths = write(tmp_path, {"a.md": f"old {WORD}\nold {WORD}\nclean\n", f"{WORD}.md": "clean\n", "b.bin": b"\0"})
    existing = policy(words=["other"])

    document = build_policy(tmp_path, paths, [WORD], existing)
    errors: list[str] = []
    validate_public_content_names(paths, errors, root=tmp_path, policy=document)

    assert errors == []
    assert document["restrictedTokenSha256"] == sorted([sha("other"), sha(WORD)])
    assert document["legacyPathNames"] == {sha(f"{WORD}.md"): 1}
    assert document["legacyLines"] == {sha("a.md"): {sha(f"old {WORD}"): 2}}


def test_word_list_is_folded() -> None:
    text = "# comment\n\n  Alpha\n" + chr(0xFEFF) + "Zeta" + chr(0x200B) + "Word\n"

    assert read_words(text) == ["alpha", "zetaword"]


def test_bad_word_list_line_is_reported_by_number_without_the_word() -> None:
    for text in ("a-b\n", "ok\nacme corp\n", "caf" + chr(0xE9) + chr(0x4E2D) + "\n"):
        try:
            read_words(text)
        except SystemExit as exc:
            assert "line" in str(exc)
            assert "acme" not in str(exc)
            assert "a-b" not in str(exc)
        else:
            raise AssertionError(f"accepted a word list line: {text!r}")


def test_growth_is_reported_per_file(tmp_path: Path) -> None:
    paths = write(tmp_path, {"a.md": f"old {WORD}\n", "b.md": "clean\n"})
    before = policy()
    after = build_policy(tmp_path, paths, [], before)

    assert grown_files(tmp_path, paths, before, after) == ["a.md"]
    assert grown_files(tmp_path, paths, after, after) == []


def test_a_replaced_line_counts_as_growth_even_when_the_total_is_equal(tmp_path: Path) -> None:
    old_line = f"old {WORD} one"
    new_line = f"new {WORD} two"
    paths = write(tmp_path, {"a.md": f"{new_line}\n"})
    before = policy(lines={sha("a.md"): {sha(old_line): 1}})
    after = build_policy(tmp_path, paths, [], before)

    assert grown_files(tmp_path, paths, before, after) == ["a.md"]


def test_guard_files_hold_no_allowance_of_their_own() -> None:
    document = json.loads((ROOT / PUBLIC_CONTENT_POLICY).read_text(encoding="utf-8"))

    for relative in GUARD_FILES:
        assert (ROOT / relative).is_file(), relative
        assert sha(relative) not in document["legacyLines"], relative
        assert sha(relative) not in document["legacyPathNames"], relative


def test_committed_policy_matches_the_pinned_digest() -> None:
    errors: list[str] = []

    document = load_public_content_policy(ROOT, errors)

    assert errors == []
    assert document is not None
    assert public_content_policy_digest(document) == PUBLIC_CONTENT_POLICY_SHA256


def load(tmp_path: Path, document: object, *, digest: str | None = None, raw: str | None = None) -> list[str]:
    target = tmp_path / PUBLIC_CONTENT_POLICY
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(raw if raw is not None else json.dumps(document), encoding="utf-8")
    if digest is None:
        digest = public_content_policy_digest(document) if isinstance(document, dict) else "0" * 64
    errors: list[str] = []
    load_public_content_policy(tmp_path, errors, expected_digest=digest)
    return errors


def test_policy_shape_is_strict(tmp_path: Path) -> None:
    good = policy(words=["alpha", "beta"])

    assert load(tmp_path, good) == []
    assert load(tmp_path, {**good, "extra": "plain text"}) == SHAPE_ERROR
    assert load(tmp_path, {**good, "legacyLines": {sha("a"): {sha("b"): "1"}}}) == SHAPE_ERROR
    assert load(tmp_path, {**good, "legacyPathNames": {sha("p"): 0}}) == SHAPE_ERROR
    assert load(tmp_path, {**good, "restrictedTokenSha256": [sha(WORD).upper()]}) == SHAPE_ERROR
    assert load(tmp_path, {**good, "restrictedTokenSha256": good["restrictedTokenSha256"][::-1]}) == SHAPE_ERROR
    assert load(tmp_path, {**good, "workbookNamePattern": 7}) == SHAPE_ERROR


def test_policy_rejects_a_bad_pattern_duplicate_keys_and_a_changed_digest(tmp_path: Path) -> None:
    good = policy()
    kind = '"kind": "public-content-policy"'
    duplicate = json.dumps(good).replace(kind, '"kind": "x", ' + kind)

    assert load(tmp_path, {**good, "workbookNamePattern": "("})[0].startswith(
        f"invalid workbook name pattern in {PUBLIC_CONTENT_POLICY}:"
    )
    assert load(tmp_path, good, raw=duplicate)[0].startswith(f"invalid JSON {PUBLIC_CONTENT_POLICY}:")
    assert load(tmp_path, good, digest="0" * 64) == [
        f"{PUBLIC_CONTENT_POLICY} differs from the digest pinned in the validator; "
        f"review the change, then set PUBLIC_CONTENT_POLICY_SHA256 to {public_content_policy_digest(good)}"
    ]
