from nfc_crc_worker.crc import crc32_mpeg2


def test_empty_payload_keeps_initial_value() -> None:
    assert crc32_mpeg2(b"") == 0xFFFFFFFF


def test_standard_check_vector() -> None:
    assert crc32_mpeg2(b"123456789") == 0x0376E6E7


def test_all_byte_values_vector() -> None:
    assert crc32_mpeg2(bytes(range(256))) == 0x494A116A


def test_result_is_deterministic() -> None:
    payload = bytes(range(256)) * 4
    values = {crc32_mpeg2(payload) for _ in range(100)}
    assert values == {0x1A5C3E13}
