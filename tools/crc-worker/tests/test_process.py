import base64
import json
import os
import subprocess
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
SRC = PROJECT_ROOT / "src"


def run_worker(document: object | bytes) -> subprocess.CompletedProcess[bytes]:
    payload = document if isinstance(document, bytes) else json.dumps(document).encode()
    environment = os.environ.copy()
    environment["PYTHONPATH"] = str(SRC)
    return subprocess.run(
        [sys.executable, "-m", "nfc_crc_worker"],
        input=payload,
        capture_output=True,
        check=False,
        timeout=5,
        env=environment,
    )


def valid_request() -> dict[str, object]:
    return {
        "protocolVersion": "1.0",
        "requestId": "process-test",
        "operation": "calculate",
        "algorithmId": "crc-32-mpeg-2",
        "payloadBase64": base64.b64encode(b"123456789").decode("ascii"),
    }


def test_process_success_writes_only_one_json_response() -> None:
    completed = run_worker(valid_request())
    assert completed.returncode == 0
    assert completed.stderr == b""
    assert completed.stdout.count(b"\n") == 1
    response = json.loads(completed.stdout)
    assert response["requestId"] == "process-test"
    assert response["result"]["valueHex"] == "0x0376E6E7"


def test_process_invalid_json_has_stable_exit_and_no_traceback() -> None:
    completed = run_worker(b"not-json")
    assert completed.returncode == 2
    assert completed.stderr == b""
    assert b"Traceback" not in completed.stdout
    response = json.loads(completed.stdout)
    assert response["ok"] is False
    assert response["error"]["code"] == "CRC_PROTOCOL_INVALID_JSON"


def test_process_bounds_error_message_for_long_unsupported_value() -> None:
    request = valid_request()
    request["protocolVersion"] = "x" * 4096

    completed = run_worker(request)

    assert completed.returncode == 3
    assert completed.stderr == b""
    assert completed.stdout.count(b"\n") == 1
    assert len(completed.stdout) <= 64 * 1024
    response = json.loads(completed.stdout)
    assert response["requestId"] == "process-test"
    assert response["error"]["code"] == "CRC_PROTOCOL_UNSUPPORTED_VERSION"
    assert len(response["error"]["message"]) == 512


def test_process_replays_identically_except_no_nondeterministic_fields() -> None:
    first = run_worker(valid_request())
    second = run_worker(valid_request())
    assert first.stdout == second.stdout
