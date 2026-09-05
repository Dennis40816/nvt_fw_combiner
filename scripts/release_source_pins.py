"""Mechanical projections of already-reviewed source payloads, never their approval."""

from __future__ import annotations

import hashlib
import json
import re
from typing import Mapping


POLICY_PATH = "docs/contracts/canonical-capability-policy-v1.json"
INDEX_PATH = "profiles/built-in/package-trust-index.json"
GOLDEN_ALLOWLIST_PATH = "testdata/golden/release-canonical-v1.json"
SOURCE_PIN_SOURCES = (
    POLICY_PATH,
    INDEX_PATH,
    GOLDEN_ALLOWLIST_PATH,
)
SOURCE_PIN_OUTPUTS = (
    "src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs",
    "scripts/package.ps1",
    "scripts/smoke-release.ps1",
    "tests/scripts/test_release_package_policy.py",
)
SOURCE_PIN_INPUTS = SOURCE_PIN_SOURCES + SOURCE_PIN_OUTPUTS


def _replace_named_pin(payload: bytes, pattern: bytes, digest: str) -> bytes:
    if len(re.findall(pattern, payload)) != 1:
        raise RuntimeError(
            "missing or ambiguous reviewed-source pin; inspect its named binding"
        )
    return re.sub(
        pattern, lambda match: match[1] + digest.encode("ascii") + match[3], payload
    )


def plan_reviewed_source_pins(before: Mapping[str, bytes]) -> dict[str, bytes]:
    if set(before) != set(SOURCE_PIN_INPUTS):
        raise RuntimeError("reviewed-source pin input inventory differs")
    for source in SOURCE_PIN_SOURCES:
        json.loads(before[source])
    policy_digest = hashlib.sha256(before[POLICY_PATH]).hexdigest()
    index_digest = hashlib.sha256(before[INDEX_PATH]).hexdigest()
    golden_digest = hashlib.sha256(before[GOLDEN_ALLOWLIST_PATH]).hexdigest()
    loader_path, package_path, smoke_path, fixture_path = SOURCE_PIN_OUTPUTS
    loader = before[loader_path]
    if (
        len(
            re.findall(
                rb'RelativePath\s*=\s*"docs/contracts/canonical-capability-policy-v1\.json"\s*;',
                loader,
            )
        )
        != 1
    ):
        raise RuntimeError("canonical policy loader source binding differs")
    expected = {
        loader_path: _replace_named_pin(
            loader, rb'(ExpectedSha256\s*=\s*")([0-9a-f]{64})("\s*;)', policy_digest
        ),
        fixture_path: _replace_named_pin(
            before[fixture_path],
            rb'(CAPABILITY_POLICY_SHA256\s*=\s*\(\s*")([0-9a-f]{64})("\s*\))',
            policy_digest,
        ),
    }
    contract_pattern = (
        rb"(\$ApprovedCanonicalCapabilityPolicyPackageContract\s*=\s*\[pscustomobject\]@\{\s*"
        rb"path\s*=\s*'docs/contracts/canonical-capability-policy-v1\.json'\s*"
        rb"role\s*=\s*'capabilityPolicy'\s*sha256\s*=\s*')([0-9a-f]{64})('\s*\})"
    )
    for path in (package_path, smoke_path):
        expected[path] = _replace_named_pin(
            before[path], contract_pattern, policy_digest
        )
    expected[smoke_path] = _replace_named_pin(
        expected[smoke_path],
        rb"(\$PackageTrustIndexPackagePath\s*=\s*'profiles/built-in/package-trust-index\.json'\s*"
        rb"\$ApprovedPackageTrustIndexSha256\s*=\s*')([0-9a-f]{64})(')",
        index_digest,
    )
    for path, source_binding, scalar in (
        (
            package_path,
            rb"\$CanonicalGoldenReleaseAllowlistPath\s*=\s*Join-Path\s+\$RepoRoot\s+'testdata/golden/release-canonical-v1\.json'",
            b"ApprovedCanonicalGoldenReleaseAllowlistSha256",
        ),
        (
            smoke_path,
            rb"\$ApprovedCanonicalGoldenAllowlistPath\s*=\s*Join-Path\s+\$PSScriptRoot\s+'\.\./testdata/golden/release-canonical-v1\.json'",
            b"ApprovedCanonicalGoldenAllowlistSha256",
        ),
    ):
        if len(re.findall(rb"(?im)^[ \t]*\$" + scalar + rb"\s*=", before[path])) != 1:
            raise RuntimeError("missing or ambiguous Golden allowlist scalar binding")
        expected[path] = _replace_named_pin(
            expected[path],
            rb"("
            + source_binding
            + rb"\s*\$"
            + scalar
            + rb"\s*=\s*')([0-9a-f]{64})(')",
            golden_digest,
        )
    return expected
