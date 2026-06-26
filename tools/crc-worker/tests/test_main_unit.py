import base64
import io
import json
from typing import cast

import pytest

from nfc_crc_worker import __main__ as worker_main
from nfc_crc_worker.errors import ExitCode


class FakeStdin:
    def __init__(self, payload: bytes) -> None:
        self.buffer = io.BytesIO(payload)


def request_bytes(**overrides: object) -> bytes:
    document: dict[str, object] = {
        "protocolVersion": "1.0",
        "requestId": "unit-main",
        "operation": "calculate",
        "algorithmId": "crc-32-mpeg-2",
        "payloadBase64": base64.b64encode(b"123456789").decode("ascii"),
    }
    document.update(overrides)
    return json.dumps(document).encode("utf-8")


def response_object(document: dict[str, object], field: str) -> dict[str, object]:
    value = document[field]
    assert isinstance(value, dict)
    return cast(dict[str, object], value)


def load_response(payload: str) -> dict[str, object]:
    document = json.loads(payload)
    assert isinstance(document, dict)
    return cast(dict[str, object], document)


def invoke_main(monkeypatch: pytest.MonkeyPatch, payload: bytes) -> tuple[int, dict[str, object]]:
    output = io.StringIO()
    monkeypatch.setattr(worker_main.sys, "stdin", FakeStdin(payload))
    monkeypatch.setattr(worker_main.sys, "stdout", output)
    result = worker_main.main()
    return result, load_response(output.getvalue())


def invoke_error(monkeypatch: pytest.MonkeyPatch, payload: bytes) -> tuple[int, dict[str, object]]:
    output = io.StringIO()
    monkeypatch.setattr(worker_main.sys, "stdin", FakeStdin(payload))
    monkeypatch.setattr(worker_main.sys, "stdout", output)
    with pytest.raises(SystemExit) as captured:
        worker_main.main()
    exit_code = captured.value.code
    assert isinstance(exit_code, int)
    return exit_code, load_response(output.getvalue())


def test_main_success(monkeypatch: pytest.MonkeyPatch) -> None:
    exit_code, response = invoke_main(monkeypatch, request_bytes())
    assert exit_code == 0
    assert response["ok"] is True
    result = response_object(response, "result")
    assert result["valueHex"] == "0x0376E6E7"


def test_main_invalid_utf8_json(monkeypatch: pytest.MonkeyPatch) -> None:
    exit_code, response = invoke_error(monkeypatch, b"\xff")
    assert exit_code == 2
    assert response_object(response, "error")["code"] == "CRC_PROTOCOL_INVALID_JSON"


def test_main_rejects_encoded_request_over_limit(monkeypatch: pytest.MonkeyPatch) -> None:
    oversized = b"x" * (worker_main.MAX_ENCODED_REQUEST_BYTES + 1)
    exit_code, response = invoke_error(monkeypatch, oversized)
    assert exit_code == 2
    assert response_object(response, "error")["code"] == "CRC_PROTOCOL_PAYLOAD_TOO_LARGE"


def test_main_propagates_contract_error(monkeypatch: pytest.MonkeyPatch) -> None:
    exit_code, response = invoke_error(monkeypatch, request_bytes(protocolVersion="2.0"))
    assert exit_code == 3
    assert response["requestId"] == "unit-main"
    assert response_object(response, "error")["code"] == "CRC_PROTOCOL_UNSUPPORTED_VERSION"


def test_main_maps_calculation_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    calls = 0

    def failing_after_self_test(payload: bytes) -> int:
        nonlocal calls
        calls += 1
        if calls == 1:
            return 0x0376E6E7
        raise RuntimeError("simulated")

    monkeypatch.setattr(worker_main, "crc32_mpeg2", failing_after_self_test)
    exit_code, response = invoke_error(monkeypatch, request_bytes())
    assert exit_code == 4
    assert response["requestId"] == "unit-main"
    assert response_object(response, "error")["code"] == "CRC_INTERNAL_CALCULATION_FAILED"


def test_self_test_failure_is_stable(monkeypatch: pytest.MonkeyPatch) -> None:
    def bad_crc(_payload: bytes) -> int:
        return 0

    monkeypatch.setattr(worker_main, "crc32_mpeg2", bad_crc)
    exit_code, response = invoke_error(monkeypatch, request_bytes())
    assert exit_code == int(ExitCode.SELF_TEST_ERROR)
    assert response_object(response, "error")["code"] == "CRC_WORKER_SELF_TEST_FAILED"
