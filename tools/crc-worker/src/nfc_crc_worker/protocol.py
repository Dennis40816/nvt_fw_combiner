"""Strict protocol 1.0 parsing and response construction."""

from __future__ import annotations

import base64
import binascii
from dataclasses import dataclass
from typing import cast

from .errors import ExitCode, WorkerError

PROTOCOL_VERSION = "1.0"
OPERATION_CALCULATE = "calculate"
ALGORITHM_CRC32_MPEG2 = "crc-32-mpeg-2"
MAX_DECODED_PAYLOAD_BYTES = 4 * 1024 * 1024
MAX_REQUEST_ID_LENGTH = 128
_REQUEST_FIELDS = frozenset(
    {"protocolVersion", "requestId", "operation", "algorithmId", "payloadBase64"}
)


@dataclass(frozen=True, slots=True)
class CalculationRequest:
    """Validated request handed to the pure calculation boundary."""

    request_id: str
    algorithm_id: str
    payload: bytes


def _request_id_if_usable(document: object) -> str:
    if not isinstance(document, dict):
        return ""
    value = cast(dict[object, object], document).get("requestId")
    if isinstance(value, str) and len(value) <= MAX_REQUEST_ID_LENGTH:
        return value
    return ""


def _require_string(
    document: dict[str, object], field: str, request_id: str, *, allow_empty: bool = False
) -> str:
    value = document.get(field)
    if not isinstance(value, str) or (not allow_empty and not value):
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_REQUEST",
            f"{field} must be a non-empty string",
            ExitCode.REQUEST_ERROR,
            request_id,
        )
    return value


def parse_request(document: object) -> CalculationRequest:
    """Validate a decoded JSON object and return a typed calculation request."""
    request_id = _request_id_if_usable(document)
    if not isinstance(document, dict):
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_REQUEST",
            "request must be a JSON object",
            ExitCode.REQUEST_ERROR,
            request_id,
        )
    raw_document = cast(dict[object, object], document)
    if not all(isinstance(field, str) for field in raw_document):
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_REQUEST",
            "request fields must be strings",
            ExitCode.REQUEST_ERROR,
            request_id,
        )

    document = cast(dict[str, object], raw_document)
    actual_fields = set(document)
    missing = sorted(_REQUEST_FIELDS - actual_fields)
    unknown = sorted(actual_fields - _REQUEST_FIELDS)
    if missing or unknown:
        details: list[str] = []
        if missing:
            details.append(f"missing fields: {', '.join(missing)}")
        if unknown:
            details.append(f"unknown fields: {', '.join(unknown)}")
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_REQUEST",
            "; ".join(details),
            ExitCode.REQUEST_ERROR,
            request_id,
        )

    protocol_version = _require_string(document, "protocolVersion", request_id)
    request_id = _require_string(document, "requestId", request_id)
    if len(request_id) > MAX_REQUEST_ID_LENGTH:
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_REQUEST",
            f"requestId exceeds {MAX_REQUEST_ID_LENGTH} characters",
            ExitCode.REQUEST_ERROR,
        )

    operation = _require_string(document, "operation", request_id)
    algorithm_id = _require_string(document, "algorithmId", request_id)
    payload_base64 = _require_string(document, "payloadBase64", request_id, allow_empty=True)

    if protocol_version != PROTOCOL_VERSION:
        raise WorkerError(
            "CRC_PROTOCOL_UNSUPPORTED_VERSION",
            f"unsupported protocolVersion: {protocol_version}",
            ExitCode.COMPATIBILITY_ERROR,
            request_id,
        )
    if operation != OPERATION_CALCULATE:
        raise WorkerError(
            "CRC_PROTOCOL_UNSUPPORTED_OPERATION",
            f"unsupported operation: {operation}",
            ExitCode.COMPATIBILITY_ERROR,
            request_id,
        )
    if algorithm_id != ALGORITHM_CRC32_MPEG2:
        raise WorkerError(
            "CRC_PROTOCOL_UNSUPPORTED_ALGORITHM",
            f"unsupported algorithmId: {algorithm_id}",
            ExitCode.COMPATIBILITY_ERROR,
            request_id,
        )

    try:
        payload = base64.b64decode(payload_base64, validate=True)
    except (binascii.Error, ValueError) as exc:
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_BASE64",
            "payloadBase64 is not valid base64",
            ExitCode.REQUEST_ERROR,
            request_id,
        ) from exc

    if base64.b64encode(payload).decode("ascii") != payload_base64:
        raise WorkerError(
            "CRC_PROTOCOL_INVALID_BASE64",
            "payloadBase64 is not canonical base64",
            ExitCode.REQUEST_ERROR,
            request_id,
        )
    if len(payload) > MAX_DECODED_PAYLOAD_BYTES:
        raise WorkerError(
            "CRC_PROTOCOL_PAYLOAD_TOO_LARGE",
            f"decoded payload exceeds {MAX_DECODED_PAYLOAD_BYTES} bytes",
            ExitCode.REQUEST_ERROR,
            request_id,
        )

    return CalculationRequest(request_id=request_id, algorithm_id=algorithm_id, payload=payload)


def success_response(
    request: CalculationRequest, value: int, worker_version: str
) -> dict[str, object]:
    """Build the exact protocol 1.0 success envelope."""
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "requestId": request.request_id,
        "ok": True,
        "workerVersion": worker_version,
        "result": {
            "algorithmId": request.algorithm_id,
            "valueUnsigned": value,
            "valueHex": f"0x{value:08X}",
            "bytesLittleEndianHex": value.to_bytes(4, "little").hex().upper(),
        },
    }


def error_response(error: WorkerError, worker_version: str) -> dict[str, object]:
    """Build the exact protocol 1.0 error envelope."""
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "requestId": error.request_id,
        "ok": False,
        "workerVersion": worker_version,
        "error": {"code": error.code, "message": error.message},
    }
