"""Pure checksum algorithms supported by the NFC CRC worker."""

CRC32_MPEG2_POLYNOMIAL = 0x04C11DB7
CRC32_MPEG2_INITIAL = 0xFFFFFFFF
_U32_MASK = 0xFFFFFFFF


def crc32_mpeg2(data: bytes) -> int:
    """Return CRC-32/MPEG-2 for *data* using the approved fixed parameters."""
    crc = CRC32_MPEG2_INITIAL
    for value in data:
        crc ^= value << 24
        for _ in range(8):
            if crc & 0x80000000:
                crc = ((crc << 1) ^ CRC32_MPEG2_POLYNOMIAL) & _U32_MASK
            else:
                crc = (crc << 1) & _U32_MASK
    return crc
