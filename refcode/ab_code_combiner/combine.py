import struct

DEBUG_INFO = 0
DEBUG_DETAIL = 1
DEBUG_TRACE = 2


def format_hex(value):
    if value < 0:
        return f"-0x{-value:X}"
    return f"0x{value:X}"


def format_range(start, end):
    if end <= start:
        return f"{format_hex(start)} ~ {format_hex(end)} (empty)"
    return f"{format_hex(start)} ~ {format_hex(end - 1)}"


class Reporter:
    def __init__(self, debug=DEBUG_INFO):
        self.debug = debug

    def banner(self, title):
        print(f"\n================ {title} ================")

    def step(self, index, total, title):
        print(f"\n[{index}/{total}] {title}")

    def info(self, message):
        print(message)

    def detail(self, label, value, level=DEBUG_DETAIL):
        if self.debug >= level:
            print(f"    {label:<14}: {value}")

    def trace(self, message):
        if self.debug >= DEBUG_TRACE:
            print(message)


def dump_hex(log, title, data, base_addr):
    if log.debug < DEBUG_TRACE:
        return

    log.trace(f"    {title}:")
    for offset in range(0, len(data), 16):
        chunk = data[offset:offset + 16]
        hex_str = " ".join(f"{b:02X}" for b in chunk)
        log.trace(f"      0x{base_addr + offset:08X}  {hex_str}")


def validate_range(name, start, end, limit, data_len=None):
    if start < 0 or end < start:
        raise ValueError(
            f"{name} range is invalid: start={format_hex(start)}, end={format_hex(end)}"
        )

    if end > limit:
        raise ValueError(
            f"{name} range {format_range(start, end)} exceeds limit {format_hex(limit)}"
        )

    expected = end - start
    if data_len is not None and expected != data_len:
        raise ValueError(
            f"{name} size mismatch: range={format_hex(expected)}, data={format_hex(data_len)}"
        )


def validate_u32_offset(name, buf, offset):
    if offset is None:
        raise ValueError(f"{name} offset is not configured")
    validate_range(name, offset, offset + 4, len(buf))


def read_u32_le(buf, offset):
    validate_u32_offset("u32 read", buf, offset)
    return struct.unpack_from("<I", buf, offset)[0]


def write_u32_le(buf, offset, value):
    validate_u32_offset("u32 write", buf, offset)
    if value < 0 or value > 0xFFFFFFFF:
        raise ValueError(f"u32 write value out of range: {format_hex(value)}")
    struct.pack_into("<I", buf, offset, value)


def crc32_mpeg2(data: bytes) -> int:
    crc = 0xFFFFFFFF
    poly = 0x04C11DB7

    for b in data:
        crc ^= (b << 24)
        for _ in range(8):
            if crc & 0x80000000:
                crc = ((crc << 1) ^ poly) & 0xFFFFFFFF
            else:
                crc = (crc << 1) & 0xFFFFFFFF

    return crc  # no final XOR


def get_crc_config(cfg):
    if cfg.tp_crc_addr_offset is None and cfg.tp_crc_range is None:
        return None

    if cfg.tp_crc_addr_offset is None or cfg.tp_crc_range is None:
        raise ValueError(
            "tp_crc_addr_offset and tp_crc_range must be configured together"
        )

    return cfg.tp_crc_addr_offset, cfg.tp_crc_range


def validate_crc_config(name, buf, cfg):
    crc_config = get_crc_config(cfg)
    if crc_config is None:
        return None

    crc_offset, (start, end) = crc_config
    validate_range(f"{name} CRC range", start, end, len(buf))
    validate_u32_offset(f"{name} CRC offset", buf, crc_offset)
    return crc_offset, start, end


def relocate_u32(name, value, shift):
    new_value = value + shift
    if new_value < 0 or new_value > 0xFFFFFFFF:
        raise ValueError(
            f"{name} relocation out of u32 range: {format_hex(value)} + "
            f"{format_hex(shift)} = {format_hex(new_value)}"
        )
    return new_value


def patch_b_code(b_code: bytearray, cfg, log):
    """
    Patch TPB internal addresses.
    TP layout is identical between A/B, so offsets are shared.
    """

    shift = cfg.tpb_flash_reloc_offset - cfg.tpa_flash_reloc_offset

    log.detail("Reloc shift", format_hex(shift))

    for name, offset in (
        ("B ILM offset", cfg.tp_ilm_start_addr_offset),
        ("B DLM offset", cfg.tp_dlm_start_addr_offset),
        ("B DIFF offset", cfg.tp_diff_dlm_start_addr_offset),
    ):
        validate_u32_offset(name, b_code, offset)

    ilm = read_u32_le(b_code, cfg.tp_ilm_start_addr_offset)
    new_ilm = relocate_u32("ILM", ilm, shift)
    write_u32_le(b_code, cfg.tp_ilm_start_addr_offset, new_ilm)
    log.detail("ILM", f"{format_hex(ilm)} -> {format_hex(new_ilm)}")

    dlm = read_u32_le(b_code, cfg.tp_dlm_start_addr_offset)
    new_dlm = relocate_u32("DLM", dlm, shift)
    write_u32_le(b_code, cfg.tp_dlm_start_addr_offset, new_dlm)
    log.detail("DLM", f"{format_hex(dlm)} -> {format_hex(new_dlm)}")

    diff = read_u32_le(b_code, cfg.tp_diff_dlm_start_addr_offset)
    new_diff = relocate_u32("DIFF", diff, shift)
    write_u32_le(b_code, cfg.tp_diff_dlm_start_addr_offset, new_diff)
    log.detail("DIFF", f"{format_hex(diff)} -> {format_hex(new_diff)}")


def check_a_crc(a_code: bytearray, cfg, log):
    crc_config = validate_crc_config("A", a_code, cfg)
    if crc_config is None:
        log.detail("A CRC", "not configured")
        return

    crc_offset, start, end = crc_config

    calc_crc = crc32_mpeg2(a_code[start:end])
    stored_crc = read_u32_le(a_code, crc_offset)

    log.detail("A CRC range", format_range(start, end))
    log.detail("A CRC offset", format_hex(crc_offset))
    log.detail("A CRC expect", f"0x{stored_crc:08X}")
    log.detail("A CRC calc", f"0x{calc_crc:08X}")
    dump_hex(log, "A CRC data", a_code[start:end], start)

    if calc_crc != stored_crc:
        raise ValueError(
            f"A CRC mismatch: expect=0x{stored_crc:08X}, calc=0x{calc_crc:08X}"
        )


def patch_b_crc(b_code: bytearray, cfg, log):
    crc_config = validate_crc_config("B", b_code, cfg)
    if crc_config is None:
        log.detail("B CRC", "not configured")
        return

    crc_offset, start, end = crc_config
    crc = crc32_mpeg2(b_code[start:end])
    dump_hex(log, "B CRC data", b_code[start:end], start)

    write_u32_le(b_code, crc_offset, crc)

    log.detail("B CRC range", format_range(start, end))
    log.detail("B CRC offset", format_hex(crc_offset))
    log.detail("B CRC write", f"0x{crc:08X}")


def copy_range(output, name, start, end, data):
    validate_range(name, start, end, len(output), len(data))
    output[start:end] = data


def read_range(buf, name, start, end):
    validate_range(name, start, end, len(buf))
    return buf[start:end]


def validate_combine_layout(cfg, dp_ab, a_code, b_code, output_size):
    dp_start = cfg.to_flash_from_dpa(0)
    dp_end = cfg.to_flash_from_dpa(len(dp_ab))

    tpa_flash_start, tpa_flash_end = cfg.get_tpa_flash_range()
    tpb_flash_start, tpb_flash_end = cfg.get_tpb_flash_range()
    tp_data_len = cfg.tp_end_offset - cfg.tp_start_offset

    validate_range("DP_AB output", dp_start, dp_end, output_size, len(dp_ab))
    validate_range("TPA input", cfg.tp_start_offset, cfg.tp_end_offset, len(a_code))
    validate_range("TPB input", cfg.tp_start_offset, cfg.tp_end_offset, len(b_code))
    validate_range("TPA output", tpa_flash_start, tpa_flash_end, output_size, tp_data_len)
    validate_range("TPB output", tpb_flash_start, tpb_flash_end, output_size, tp_data_len)

    return {
        "dp_start": dp_start,
        "dp_end": dp_end,
        "tpa_flash_start": tpa_flash_start,
        "tpa_flash_end": tpa_flash_end,
        "tpb_flash_start": tpb_flash_start,
        "tpb_flash_end": tpb_flash_end,
    }


def combine(cfg, dp_ab, a_code, b_code, debug=DEBUG_INFO):
    log = Reporter(debug)
    output_size = len(dp_ab)
    if output_size == 0:
        raise ValueError("DP_AB is empty")

    layout = validate_combine_layout(cfg, dp_ab, a_code, b_code, output_size)
    output = bytearray(output_size)

    log.banner("COMBINE")
    log.info(f"Output size: {format_hex(output_size)} (from DP_AB)")
    log.detail("Layout", "validated")

    # --------------------------------------------------------
    # 1. DP
    # --------------------------------------------------------
    log.step(1, 4, "Copy DP_AB")
    start = layout["dp_start"]
    end = layout["dp_end"]

    log.detail("Output range", format_range(start, end))
    log.detail("Data size", format_hex(len(dp_ab)))
    copy_range(output, "DP_AB output", start, end, dp_ab)

    # --------------------------------------------------------
    # 2. TPA
    # --------------------------------------------------------
    log.step(2, 4, "Copy TPA")
    check_a_crc(a_code, cfg, log)
    tp_flash_start = layout["tpa_flash_start"]
    tp_flash_end = layout["tpa_flash_end"]
    data = read_range(a_code, "TPA input", cfg.tp_start_offset, cfg.tp_end_offset)

    log.detail("Output range", format_range(tp_flash_start, tp_flash_end))
    log.detail("Input range", format_range(cfg.tp_start_offset, cfg.tp_end_offset))
    log.detail("Data size", format_hex(len(data)))

    copy_range(output, "TPA output", tp_flash_start, tp_flash_end, data)

    # --------------------------------------------------------
    # 3. Patch TPB
    # --------------------------------------------------------
    log.step(3, 4, "Patch TPB")
    patch_b_code(b_code, cfg, log)
    patch_b_crc(b_code, cfg, log)

    # --------------------------------------------------------
    # 4. TPB
    # --------------------------------------------------------
    log.step(4, 4, "Copy TPB")
    tpb_flash_start = layout["tpb_flash_start"]
    tpb_flash_end = layout["tpb_flash_end"]
    data = read_range(b_code, "TPB input", cfg.tp_start_offset, cfg.tp_end_offset)

    log.detail("Output range", format_range(tpb_flash_start, tpb_flash_end))
    log.detail("Input range", format_range(cfg.tp_start_offset, cfg.tp_end_offset))
    log.detail("Data size", format_hex(len(data)))

    copy_range(output, "TPB output", tpb_flash_start, tpb_flash_end, data)

    # --------------------------------------------------------
    # Final sanity
    # --------------------------------------------------------
    log.banner("FINAL CHECK")
    log.info(f"Final output size: {format_hex(len(output))}")

    if len(output) != output_size:
        raise RuntimeError(
            f"Output size corrupted: expect={format_hex(output_size)}, "
            f"actual={format_hex(len(output))}"
        )

    log.info("Output size correct")

    return output
