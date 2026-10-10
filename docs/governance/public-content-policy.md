# Public content policy

This public repository must not name restricted customer, panel-vendor or workbook items in tracked file names
or text. `scripts/validate_repository.py` enforces this with `validate_public_content_names`. The data lives in
[`public-content-policy.json`](public-content-policy.json).

## What the guard checks

- A workbook name that matches `workbookNamePattern`, in a file name or in any text line.
- A restricted word, matched by SHA-256 of the lower-cased word. The words are never written in the repository.
  The matcher folds text with NFKD, drops format characters, combining marks and filler characters, splits
  camel case, and also tries words joined across `_`, `.` and `-`.
- Text in UTF-8, Latin-1, and UTF-16 or UTF-32 with a mark. A file with a binary suffix from
  `_PUBLIC_BINARY_SUFFIXES` is skipped. Any other file that cannot be read as text is an error.

## Existing matches

Matches that were already in the tree are allowed only as hashes: a file name by the hash of its path, a line by
the hash of its path and of the whole line, each with a count. An allowance that no longer matches is an error,
so a cleaned line must leave the policy. Git history is not changed by this policy.

## What the hashes do not do

SHA-256 of a short word can be reversed with a dictionary, and the legacy list shows which files hold matches.
The hashes keep plain words out of the tree. They do not keep the words secret. A keyed hash from a CI secret is
the next step if secrecy is needed.

## Known limits

A word split over two lines, look-alike letters from another script, and words that are not ASCII are not found.
A word is found inside a camel-case identifier or one joined with `_`, `.` or `-` (up to four parts), but only
when it starts and ends at part boundaries. A word inside a single-case identifier or at the middle of a part is
not found. A workbook name after a letter prefix, such as `MyIC_...`, is not found.

## Changing the policy

`PUBLIC_CONTENT_POLICY_SHA256` in the validator pins the whole policy file. Every policy change is therefore a
validator change and needs governance-owner review.

1. Put new restricted words in a local text file outside the repository, one word per line.
2. Run `python scripts/generate_public_content_policy.py --words-file <that file> --pin`. Without a words
   file the tool keeps the existing words and re-lists the matches that remain. Run it after cleaning lines.
3. Review the diff. The count of legacy matches must not grow. A new restricted word grows it only by
   matches that already exist in the tree, and each one is a clean-up item.
4. Commit the policy file and the new digest together.

A run stops when the words the policy already holds would match more than their allowance, for a file name,
a line or a count, unless `--allow-growth` is given. Matches that come from new words are listed as growth in the
output. To change `workbookNamePattern`, edit it in the policy file by hand and run the tool with
`--pin --allow-growth`, then review the files it lists.

To add a binary suffix, add it to `_PUBLIC_BINARY_SUFFIXES` in the validator. Do this only for a real binary
format. A suffix of a text format must never be listed, because listed files are not scanned.

## Owner decisions

- 2026-10-10: clean the current files now. Golden case ids and test-data folder names stay as legacy
  allowances for now. The git history is not rewritten here.
- 2026-10-10: the restricted list is not written as plain text anywhere in the repository.
- 2026-10-10: hashes in the policy file are accepted as the first step.
