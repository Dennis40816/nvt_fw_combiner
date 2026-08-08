"""One-shot stdin/stdout entry point for the NFC CRC worker."""

import json
import sys

from . import __version__
from .crc import crc32_mpeg2
from .errors import ExitCode, WorkerError
from .protocol import error_response, parse_request, success_response

MAX_ENCODED_REQUEST_BYTES = 5_600_000
_CRC32_MPEG2_CHECK_VALUE = 0x0376E6E7


def _write_response(document: dict[str, object]) -> None:
    encoded = json.dumps(document, ensure_ascii=True, separators=(",", ":"))
    print(encoded, flush=True)


def _self_test() -> None:
    if crc32_mpeg2(b"123456789") != _CRC32_MPEG2_CHECK_VALUE:
        raise WorkerError(
            "CRC_WORKER_SELF_TEST_FAILED",
            "CRC-32/MPEG-2 self-test failed",
            ExitCode.SELF_TEST_ERROR,
        )


def main() -> int:
    """Read one request, write one response, and return the protocol exit code."""
    try:
        _self_test()
        raw_request = sys.stdin.buffer.read(MAX_ENCODED_REQUEST_BYTES + 1)
        if len(raw_request) > MAX_ENCODED_REQUEST_BYTES:
            raise WorkerError(
                "CRC_PROTOCOL_PAYLOAD_TOO_LARGE",
                "encoded request exceeds worker input limit",
                ExitCode.REQUEST_ERROR,
            )
        try:
            document = json.loads(raw_request.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise WorkerError(
                "CRC_PROTOCOL_INVALID_JSON",
                "stdin is not one valid UTF-8 JSON document",
                ExitCode.REQUEST_ERROR,
            ) from exc

        request = parse_request(document)
        try:
            value = crc32_mpeg2(request.payload)
        except Exception as exc:  # pragma: no cover - defensive process boundary
            raise WorkerError(
                "CRC_INTERNAL_CALCULATION_FAILED",
                "checksum calculation failed",
                ExitCode.CALCULATION_ERROR,
                request.request_id,
            ) from exc
        _write_response(success_response(request, value, __version__))
        return int(ExitCode.SUCCESS)
    except WorkerError as error:
        _write_response(error_response(error, __version__))
        raise SystemExit(int(error.exit_code)) from None


if __name__ == "__main__":  # pragma: no cover - exercised by process tests
    raise SystemExit(main())
