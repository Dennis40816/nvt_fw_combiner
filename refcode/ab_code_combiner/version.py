from datetime import datetime

NVT_SIG = b"NVT"
SIG_LEN = 3
FW_BACK_OFFSET = 0xFFF

FW_PREFIX = "T"
DP_PREFIX = "D"


def find_fw_version(code: bytearray):
    for i in range(len(code) - SIG_LEN, -1, -1):
        if code[i:i + SIG_LEN] == NVT_SIG:
            t_addr = i + (SIG_LEN - 1)
            fw_addr = t_addr - FW_BACK_OFFSET

            if fw_addr < 0:
                raise ValueError("FW version address underflow")

            return code[fw_addr]

    raise ValueError("FW version not found")


def format_fw(v: int) -> str:
    return f"{FW_PREFIX}{v:02X}"


def _read(buf, offset):
    return None if offset is None else buf[offset]


def _format_dp(main, sub):
    parts = []

    if main is not None:
        parts.append(f"{main:02X}")
    if sub is not None:
        parts.append(f"{sub:02X}")

    if not parts:
        return ""

    return DP_PREFIX + "".join(parts)


def read_dp_version_pair(dp_ab: bytearray, cfg):
    def read_one(base):
        main = _read(
            dp_ab,
            base + cfg.dp_a_ver_main_offset
            if cfg.dp_a_ver_main_offset is not None else None
        )
        sub = _read(
            dp_ab,
            base + cfg.dp_a_ver_sub_offset
            if cfg.dp_a_ver_sub_offset is not None else None
        )
        return _format_dp(main, sub)

    # DP A is always at base 0
    # DP B header base is defined in BIN space
    return read_one(0), read_one(cfg.dp_b_header_offset)


def make_output_filename(dp_ab, a_code, b_code, cfg):
    a_fw = format_fw(find_fw_version(a_code))
    b_fw = format_fw(find_fw_version(b_code))

    a_dp, b_dp = read_dp_version_pair(dp_ab, cfg)

    a_ver = f"{a_dp}{a_fw}" if a_dp else a_fw
    b_ver = f"{b_dp}{b_fw}" if b_dp else b_fw

    today = datetime.now().strftime("%Y%m%d")

    return f"{cfg.name}_Flashcode_A_{a_ver}_B_{b_ver}_{today}.bin"