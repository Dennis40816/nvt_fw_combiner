from pathlib import Path
import argparse

from ic_config import IC_CONFIGS
from combine import combine
from version import make_output_filename

DP_AB_DIR = Path("DP_AB")
TPA_DIR   = Path("TPA")
TPB_DIR   = Path("TPB")

COMBINER_VERSION = "1.0"


def format_size(size: int) -> str:
    return f"0x{size:X} ({size} bytes)"


def print_kv(label: str, value) -> None:
    print(f"{label:<16}: {value}")


def get_single_bin(folder: Path) -> tuple[Path, bytearray]:
    if not folder.is_dir():
        raise ValueError(f"{folder.resolve()} does not exist or is not a folder")

    files = sorted(
        (f for f in folder.iterdir() if f.is_file() and f.suffix.lower() == ".bin"),
        key=lambda f: f.name.lower()
    )

    if len(files) != 1:
        raise ValueError(
            f"{folder.resolve()} must contain exactly ONE bin file "
            f"(found {len(files)}): {files}"
        )

    return files[0], bytearray(files[0].read_bytes())


def make_unique_output_path(filename: str) -> Path:
    path = Path(filename)
    if not path.exists():
        return path

    index = 1
    while True:
        candidate = path.with_name(f"{path.stem}_{index}{path.suffix}")
        if not candidate.exists():
            return candidate
        index += 1


def print_ic_list():
    print("Available IC models")
    print("-------------------")
    for k, v in IC_CONFIGS.items():
        print(f"  {k:<6} : {v.name}")


def main():
    parser = argparse.ArgumentParser(
        description="AB Code Combiner",
        formatter_class=argparse.RawTextHelpFormatter,
        usage="%(prog)s --ic <IC_ID> [--debug [0|1|2]]",
    )

    parser.add_argument(
        "--ic",
        choices=IC_CONFIGS.keys(),
        metavar="<IC_ID>",
        help="Target IC model"
    )

    parser.add_argument(
        "--list-ic",
        action="store_true",
        help="List available IC models"
    )

    parser.add_argument(
        "--debug",
        nargs="?",
        const=1,
        default=1,
        type=int,
        choices=(0, 1, 2),
        help=(
            "Debug detail level:\n"
            "  0: summary only\n"
            "  1: ranges, CRC, and patch details (default)\n"
            "  2: level 1 plus hex dumps"
        )
    )

    args = parser.parse_args()

    # --------------------------------------------------------
    # List IC only
    # --------------------------------------------------------
    if args.list_ic:
        print_ic_list()
        return

    # --------------------------------------------------------
    # Normal flow requires --ic
    # --------------------------------------------------------
    if not args.ic:
        parser.error("--ic is required unless --list-ic is used")

    cfg = IC_CONFIGS[args.ic]

    try:
        # Load bins
        dp_ab_path, dp_ab = get_single_bin(DP_AB_DIR)
        a_code_path, a_code = get_single_bin(TPA_DIR)
        b_code_path, b_code = get_single_bin(TPB_DIR)

        # TPB range in flash
        tpb_start, tpb_end = cfg.get_tpb_flash_range()

        print("======================================")
        print("AB Code Combiner")
        print("======================================")
        print_kv("Version", COMBINER_VERSION)
        print_kv("IC", cfg.name)
        print_kv("Debug level", args.debug)
        print_kv("Output size", f"{format_size(len(dp_ab))} (from DP_AB)")
        print_kv("TPB output", f"0x{tpb_start:X} ~ 0x{tpb_end - 1:X}")

        print("\nInput files")
        print_kv("DP_AB", dp_ab_path)
        print_kv("DP_AB size", format_size(len(dp_ab)))
        print_kv("TPA", a_code_path)
        print_kv("TPA size", format_size(len(a_code)))
        print_kv("TPB", b_code_path)
        print_kv("TPB size", format_size(len(b_code)))

        # Combine
        output = combine(cfg, dp_ab, a_code, b_code, debug=args.debug)

        # Output filename
        filename = make_output_filename(dp_ab, a_code, b_code, cfg)
        output_path = make_unique_output_path(filename)
        output_path.write_bytes(output)
    except Exception as exc:
        print("--------------------------------------")
        print(f"ERROR: {exc}")
        raise SystemExit(1) from exc

    print("--------------------------------------")
    print(f"Output: {output_path}")
    print("Merge done")


if __name__ == "__main__":
    main()
