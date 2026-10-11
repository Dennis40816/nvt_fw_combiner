"""Verified copy primitives shared by the public PowerShell assembly entries."""
import argparse
from pathlib import Path
import shutil
import sys

if __package__:
    from .golden_pin import (FixedArgumentParser, DEFAULT_BASELINE, ROOT, UNBOUND_PIN, load_pin,
                             filesystem_path, pin_message, regular_files, reject_links, require_bound_pin,
                             safe_relative, verify)
else:
    from golden_pin import (FixedArgumentParser, DEFAULT_BASELINE, ROOT, UNBOUND_PIN, load_pin,
                            filesystem_path, pin_message, regular_files, reject_links, require_bound_pin,
                            safe_relative, verify)


def copy_pinned_tree(source: Path, destination: Path, pin: dict) -> dict:
    source, destination = filesystem_path(source), filesystem_path(destination)
    reject_links(destination)
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError("Golden copy failed.")
    verify(source, pin)
    for value in pin["paths"]:
        target = destination.joinpath(*safe_relative(value).parts)
        reject_links(target)
        if target.exists():
            regular_files(destination, [value])
    for path in regular_files(source, pin["paths"]):
        target = destination / path.relative_to(source)
        reject_links(target)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target, follow_symlinks=False)
    verify(source, pin)
    return verify(destination, pin)


def main() -> int:
    parser = FixedArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("mode", choices=("export", "assemble"))
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    try:
        pin = load_pin(args.baseline)
        if args.mode == "export":
            observed = copy_pinned_tree(args.root, args.directory / pin["assetsPath"], pin)
        else:
            require_bound_pin(pin)
            observed = copy_pinned_tree(args.directory / pin["assetsPath"], args.root, pin)
        print(pin_message(observed))
        return 0
    except (OSError, ValueError) as error:
        print(UNBOUND_PIN if str(error) == UNBOUND_PIN else
              "Golden export failed." if args.mode == "export" else "Golden assembly failed.",
              file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
