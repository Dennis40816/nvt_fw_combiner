import base64

import pytest

from nfc_crc_worker.errors import ExitCode, WorkerError
from nfc_crc_worker.protocol import (
    MAX_DECODED_PAYLOAD_BYTES,
    error_response,
    parse_request,
    success_response,
)


def request_for(payload: bytes = b"123456789") -> dict[str, object]:
    return {
        "protocolVersion": "1.0",
        "requestId": "test-request",
        "operation": "calculate",
        "algorithmId": "crc-32-mpeg-2",
        "payloadBase64": base64.b64encode(payload).decode("ascii"),
    }


def assert_worker_error(document: object, code: str, exit_code: ExitCode) -> WorkerError:
    with pytest.raises(WorkerError) as captured:
        parse_request(document)
    assert captured.value.code == code
    assert captured.value.exit_code == exit_code
    return captured.value


def test_parse_valid_request() -> None:
    request = parse_request(request_for())
    assert request.request_id == "test-request"
    assert request.algorithm_id == "crc-32-mpeg-2"
    assert request.payload == b"123456789"


def test_empty_payload_is_valid() -> None:
    request = parse_request(request_for(b""))
    assert request.payload == b""


def test_non_object_is_rejected() -> None:
    assert_worker_error([], "CRC_PROTOCOL_INVALID_REQUEST", ExitCode.REQUEST_ERROR)


def test_missing_field_is_rejected() -> None:
    document = request_for()
    del document["operation"]
    error = assert_worker_error(document, "CRC_PROTOCOL_INVALID_REQUEST", ExitCode.REQUEST_ERROR)
    assert "missing fields: operation" in error.message


def test_unknown_field_is_rejected() -> None:
    document = request_for()
    document["extra"] = True
    error = assert_worker_error(document, "CRC_PROTOCOL_INVALID_REQUEST", ExitCode.REQUEST_ERROR)
    assert "unknown fields: extra" in error.message


@pytest.mark.parametrize(
    ("field", "value", "code"),
    [
        ("protocolVersion", "2.0", "CRC_PROTOCOL_UNSUPPORTED_VERSION"),
        ("operation", "write-file", "CRC_PROTOCOL_UNSUPPORTED_OPERATION"),
        ("algorithmId", "crc-32", "CRC_PROTOCOL_UNSUPPORTED_ALGORITHM"),
    ],
)
def test_unsupported_contract_values_are_rejected(field: str, value: str, code: str) -> None:
    document = request_for()
    document[field] = value
    assert_worker_error(document, code, ExitCode.COMPATIBILITY_ERROR)


def test_request_id_wrong_type_is_rejected_without_echo() -> None:
    document = request_for()
    document["requestId"] = 42
    error = assert_worker_error(document, "CRC_PROTOCOL_INVALID_REQUEST", ExitCode.REQUEST_ERROR)
    assert error.request_id == ""


def test_request_id_too_long_is_rejected_without_echo() -> None:
    document = request_for()
    document["requestId"] = "x" * 129
    error = assert_worker_error(document, "CRC_PROTOCOL_INVALID_REQUEST", ExitCode.REQUEST_ERROR)
    assert error.request_id == ""


def test_invalid_base64_is_rejected() -> None:
    document = request_for()
    document["payloadBase64"] = "***"
    assert_worker_error(document, "CRC_PROTOCOL_INVALID_BASE64", ExitCode.REQUEST_ERROR)


def test_noncanonical_base64_is_rejected() -> None:
    document = request_for()
    document["payloadBase64"] = "YR=="
    assert_worker_error(document, "CRC_PROTOCOL_INVALID_BASE64", ExitCode.REQUEST_ERROR)


def test_oversized_payload_is_rejected() -> None:
    document = request_for(b"x" * (MAX_DECODED_PAYLOAD_BYTES + 1))
    assert_worker_error(document, "CRC_PROTOCOL_PAYLOAD_TOO_LARGE", ExitCode.REQUEST_ERROR)


def test_success_response_has_correlated_fixed_width_values() -> None:
    request = parse_request(request_for())
    response = success_response(request, 0x0376E6E7, "0.1.0")
    assert response == {
        "protocolVersion": "1.0",
        "requestId": "test-request",
        "ok": True,
        "workerVersion": "0.1.0",
        "result": {
            "algorithmId": "crc-32-mpeg-2",
            "valueUnsigned": 58124007,
            "valueHex": "0x0376E6E7",
            "bytesLittleEndianHex": "E7E67603",
        },
    }


def test_error_response_has_stable_shape() -> None:
    error = WorkerError(
        "CRC_PROTOCOL_INVALID_REQUEST",
        "bad request",
        ExitCode.REQUEST_ERROR,
        "test-request",
    )
    assert error_response(error, "0.1.0") == {
        "protocolVersion": "1.0",
        "requestId": "test-request",
        "ok": False,
        "workerVersion": "0.1.0",
        "error": {
            "code": "CRC_PROTOCOL_INVALID_REQUEST",
            "message": "bad request",
        },
    }
