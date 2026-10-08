import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import struct
import zipfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[2]
PWSH = shutil.which("pwsh")
COMBINER_DIRECTORY = Path("external-tools/legacy-combiner/1.13.0")
RUNTIME_PATH = COMBINER_DIRECTORY / "vcruntime140.dll"
GOLDEN_PATH = Path(
    "testdata/golden/canonical/NT51927/standard-merge/gen-flash/"
    "topology-unscoped/nt51927-gen-flash/expected/nt51927-expected-output.bin"
)
GOLDEN_PACKAGE_PATH = Path("reference/golden/c021/expected/nt51927-expected-output.bin")

# A captured pwsh error view can truncate a line with an ellipsis, encoded in
# whatever code page the host console uses (e.g. Big5 on a zh-TW Windows
# host). Force UTF-8 so this subprocess's captured output decodes the same
# way on every host, including CI.
PWSH_FORCE_UTF8_OUTPUT = (
    "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)"
)


def run_release_functions(script_name: str, names: tuple[str, ...], command: str):
    """Execute the real PowerShell owners without launching the package entrypoint."""
    if not PWSH:
        pytest.skip("PowerShell 7 is required")
    script = str(ROOT / "scripts" / script_name).replace("'", "''")
    functions = ",".join(f"'{name}'" for name in names)
    return subprocess.run(
        [
            PWSH,
            "-NoProfile",
            "-Command",
            f"""
{PWSH_FORCE_UTF8_OUTPUT}
$ErrorActionPreference = 'Stop'
$PSStyle.OutputRendering = 'PlainText'
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    '{script}', [ref]$tokens, [ref]$errors)
if ($errors.Count) {{ throw ($errors | Out-String) }}
foreach ($name in @({functions})) {{
    $definitions = @($ast.FindAll({{ param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq $name
    }}, $true))
    if ($definitions.Count -ne 1) {{ throw "Missing or duplicate owner: $name" }}
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}}
{command}
""",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=50,
        check=False,
    )


def catalog_fixture(root: Path) -> tuple[Path, Path, Path, str]:
    """Make a tiny independently encoded pack and its reviewed JSON peers."""
    built_in = root / "profiles" / "built-in"
    bundle = built_in / "example"
    bundle.mkdir(parents=True)
    document = b'{"familyId":"sample"}\n'
    manifest = {
        "entries": [
            {
                "entryId": "family",
                "kind": "firmware-family",
                "path": "families/sample.json",
                "schemaId": "family-schema",
                "contentHash": hashlib.sha256(document).hexdigest(),
            }
        ]
    }
    manifest_bytes = json.dumps(manifest, separators=(",", ":")).encode()
    document_path = bundle / "families" / "sample.json"
    document_path.parent.mkdir()
    document_path.write_bytes(document)
    (bundle / "profile-bundle.json").write_bytes(manifest_bytes)
    index = {
        "trustIndexId": "test-index",
        "trustIndexVersion": "1",
        "trustAnchorBindingId": "test-anchor",
        "bundles": [
            {"bundleDirectory": "example", "bundleVersion": "1", "contentHash": "a" * 64}
        ],
    }
    index_path = built_in / "package-trust-index.json"
    index_path.write_text(json.dumps(index, separators=(",", ":")), encoding="utf-8")
    index_hash = hashlib.sha256(index_path.read_bytes()).hexdigest()
    body = document + manifest_bytes
    header = {
        "formatVersion": 1,
        "trustIndex": {
            "sha256": index_hash,
            "trustIndexId": index["trustIndexId"],
            "trustIndexVersion": index["trustIndexVersion"],
            "trustAnchorBindingId": index["trustAnchorBindingId"],
        },
        "bundles": [
            {
                "bundleDirectory": "example",
                "bundleVersion": "1",
                "contentHash": "a" * 64,
                "documents": [
                    {**manifest["entries"][0], "offset": 0, "length": len(document)}
                ],
                "manifest": {
                    "offset": len(document),
                    "length": len(manifest_bytes),
                    "sha256": hashlib.sha256(manifest_bytes).hexdigest(),
                },
            }
        ],
        "body": {"length": len(body), "sha256": hashlib.sha256(body).hexdigest()},
    }
    header_bytes = json.dumps(header, sort_keys=True, separators=(",", ":")).encode()
    pack = built_in / "prebuilt-profile-catalog.pack"
    pack.write_bytes(b"NFCPBCAT" + struct.pack("<I", len(header_bytes)) + header_bytes + body)
    return index_path, pack, document_path, index_hash


@pytest.mark.parametrize("mutation", ["valid", "missing", "damaged", "oversized", "stale"])
def test_prebuilt_pack_corresponds_to_packaged_json(tmp_path: Path, mutation: str) -> None:
    index, pack, document, index_hash = catalog_fixture(tmp_path)
    if mutation == "missing":
        pack.unlink()
    elif mutation == "damaged":
        pack.write_bytes(pack.read_bytes()[:-1] + b"X")
    elif mutation == "oversized":
        pack.write_bytes(pack.read_bytes() + b"x" * 4_194_304)
    elif mutation == "stale":
        document.write_bytes(b'{"familyId":"changed"}\n')
    root_arg = str(tmp_path).replace("'", "''")
    hash_arg = index_hash.replace("'", "''")
    result = run_release_functions(
        "smoke-release.ps1",
        ("Assert-SafeProfileBundlePath", "Assert-PackagedPrebuiltCatalog"),
        f"Assert-PackagedPrebuiltCatalog -PackageRoot '{root_arg}' "
        f"-ApprovedTrustIndexSha256 '{hash_arg}'",
    )
    output = result.stdout + result.stderr
    if mutation == "valid":
        assert result.returncode == 0, output
    else:
        assert result.returncode != 0
        assert "Prebuilt catalog" in output, output


@pytest.mark.parametrize("mutation", ["valid", "missing", "damaged", "oversized"])
def test_packager_requires_regenerated_pack_bytes(tmp_path: Path, mutation: str) -> None:
    _, pack, _, _ = catalog_fixture(tmp_path)
    regenerated = tmp_path / "regenerated.pack"
    regenerated.write_bytes(pack.read_bytes())
    if mutation == "missing":
        pack.unlink()
    elif mutation == "damaged":
        pack.write_bytes(pack.read_bytes()[:-1] + b"X")
    elif mutation == "oversized":
        pack.write_bytes(pack.read_bytes() + b"x" * 4_194_304)
    root_arg = str(tmp_path).replace("'", "''")
    generated_arg = str(regenerated).replace("'", "''")
    result = run_release_functions(
        "package.ps1",
        ("Assert-MatchingPrebuiltCatalog",),
        f"Assert-MatchingPrebuiltCatalog -PublishedRoot '{root_arg}' "
        f"-RegeneratedPack '{generated_arg}'",
    )
    output = result.stdout + result.stderr
    if mutation == "valid":
        assert result.returncode == 0, output
    else:
        assert result.returncode != 0
        assert "Prebuilt catalog" in output, output


@pytest.mark.parametrize("mutation", ["valid", "missing", "wrong-role", "extra", "oversized"])
def test_smoke_requires_one_bounded_pack_manifest_entry(mutation: str) -> None:
    entries = [
        {
            "path": "profiles/built-in/prebuilt-profile-catalog.pack",
            "role": "builtInProfile",
            "size": 100,
        }
    ]
    if mutation == "missing":
        entries.clear()
    elif mutation == "wrong-role":
        entries[0]["role"] = "reference"
    elif mutation == "extra":
        entries.append(
            {"path": "profiles/built-in/other.pack", "role": "builtInProfile", "size": 10}
        )
    elif mutation == "oversized":
        entries[0]["size"] = 4_194_305
    payload = json.dumps(entries, separators=(",", ":")).replace("'", "''")
    result = run_release_functions(
        "smoke-release.ps1",
        ("Assert-PrebuiltCatalogManifestEntry",),
        f"$entries = '{payload}' | ConvertFrom-Json; "
        "Assert-PrebuiltCatalogManifestEntry -Entries @($entries)",
    )
    output = result.stdout + result.stderr
    if mutation == "valid":
        assert result.returncode == 0, output
    else:
        assert result.returncode != 0
        assert "Prebuilt catalog" in output, output


@pytest.mark.parametrize("version", ["1.1.7", "1.1.8", "1.1.10"])
def test_combiner_runtime_version_boundary(version: str) -> None:
    result = run_release_functions(
        "smoke-release.ps1",
        ("Get-ReleaseProductVersion",),
        f"$v = Get-ReleaseProductVersion ([pscustomobject]@{{version='{version}';sourceTag='v{version}'}}); "
        "Write-Output ($v -ge [version]'1.1.8')",
    )
    assert result.returncode == 0, result.stdout + result.stderr
    assert result.stdout.strip() == ("False" if version == "1.1.7" else "True")


@pytest.mark.parametrize(
    "version,tag",
    [
        ("invalid", "vinvalid"),
        ("1.1.8-preview", "v1.1.8-preview"),
        ("1.1.7", "v1.1.8"),
        ("", "v1.1.8"),
    ],
)
def test_combiner_runtime_rejects_invalid_or_mismatched_version(version, tag) -> None:
    result = run_release_functions(
        "smoke-release.ps1",
        ("Get-ReleaseProductVersion",),
        f"Get-ReleaseProductVersion ([pscustomobject]@{{version='{version}';sourceTag='{tag}'}})",
    )
    assert result.returncode != 0
    assert "Release product version and source tag are inconsistent" in result.stderr


def test_combiner_runtime_legacy_manifest_uses_source_tag() -> None:
    result = run_release_functions(
        "smoke-release.ps1",
        ("Get-ReleaseProductVersion",),
        "$v = Get-ReleaseProductVersion ([pscustomobject]@{sourceTag='v0.9.19'}); Write-Output $v.ToString()",
    )
    assert result.returncode == 0, result.stdout + result.stderr
    assert result.stdout.strip() == "0.9.19"


@pytest.mark.parametrize(
    "script_name,owner",
    [
        ("package.ps1", "Assert-ApprovedCombinerRuntime"),
        ("smoke-release.ps1", "Assert-CombinerRuntime"),
    ],
)
@pytest.mark.parametrize("mutation", ["valid", "missing", "tampered"])
def test_combiner_runtime_is_independently_pinned(
    tmp_path, script_name, owner, mutation
) -> None:
    tools = tmp_path / COMBINER_DIRECTORY
    tools.mkdir(parents=True)
    shutil.copy2(ROOT / COMBINER_DIRECTORY / "Combiner.exe", tools)
    if mutation == "valid":
        shutil.copy2(ROOT / RUNTIME_PATH, tools)
    elif mutation == "tampered":
        (tools / "vcruntime140.dll").write_bytes(b"substituted runtime")
    path = str(tmp_path).replace("'", "''")
    result = run_release_functions(
        script_name,
        ("Get-LowerSha256", owner),
        f"{owner} '{path}'",
    )
    if mutation == "valid":
        assert result.returncode == 0, result.stdout + result.stderr
    else:
        assert result.returncode != 0
        assert (
            "Combiner runtime is missing or does not match the approved SHA-256"
            in result.stderr
        )


@pytest.mark.skipif(os.name != "nt", reason="The bundled Combiner is Windows x64")
def test_packaged_combiner_executes_certified_crc_command_without_mutation() -> None:
    # Keep the real package layout, but avoid pytest's additional long test-name
    # directory beneath the verifier's already nested session scratch.
    with tempfile.TemporaryDirectory(prefix="crc-") as directory:
        scratch = Path(directory)
        package = scratch / "package"
        tool_directory = package / COMBINER_DIRECTORY
        tool_directory.mkdir(parents=True)
        for name in ("Combiner.exe", "vcruntime140.dll"):
            shutil.copy2(ROOT / COMBINER_DIRECTORY / name, tool_directory)
        golden = package / GOLDEN_PACKAGE_PATH
        golden.parent.mkdir(parents=True)
        shutil.copy2(ROOT / GOLDEN_PATH, golden)
        package_arg = str(package).replace("'", "''")
        scratch_arg = str(scratch / "scratch").replace("'", "''")
        result = run_release_functions(
            "smoke-release.ps1",
            ("Get-LowerSha256", "Assert-CombinerRuntime", "Invoke-CombinerSmoke"),
            f"Invoke-CombinerSmoke -PackageRoot '{package_arg}' -SmokeRoot '{scratch_arg}'",
        )
        assert result.returncode == 0, result.stdout + result.stderr
        assert "Bundled Combiner CRC smoke passed" in result.stdout
        assert golden.read_bytes() == (ROOT / GOLDEN_PATH).read_bytes()


CURRENT_ALLOWLIST_SHA256 = "8cf5e2c610f7012ea53db83da7f8a41aa3191b81774ae3689165ad1ceb28e718"
LEGACY_ALLOWLIST_SHA256 = "4496e7a6379e05877f0f372e5ec056938f6b279b2508400f96b52bb213219a87"


@pytest.mark.parametrize(
    "sha256,version,accepted",
    [
        (CURRENT_ALLOWLIST_SHA256, "1.2.1", "current"),
        (CURRENT_ALLOWLIST_SHA256, "1.2.2", "current"),
        (LEGACY_ALLOWLIST_SHA256, "1.1.4", "legacy"),
        (LEGACY_ALLOWLIST_SHA256, "1.2.1", "legacy"),
        (LEGACY_ALLOWLIST_SHA256, "1.2.2", None),
        (LEGACY_ALLOWLIST_SHA256, None, None),
        ("0" * 64, "1.2.1", None),
        ("", "1.2.1", None),
    ],
)
def test_canonical_golden_shape_follows_the_package_version(
    sha256: str, version: str | None, accepted: str | None
) -> None:
    """A published package keeps its own allowlist shape; a new release must use the current one."""
    version_arg = "$null" if version is None else f"([version]'{version}')"
    result = run_release_functions(
        "smoke-release.ps1",
        ("Get-CanonicalGoldenShape",),
        f"$ApprovedCanonicalGoldenAllowlistSha256 = '{CURRENT_ALLOWLIST_SHA256}'; "
        f"$LegacyCanonicalGoldenAllowlistSha256 = '{LEGACY_ALLOWLIST_SHA256}'; "
        f"$shape = Get-CanonicalGoldenShape -PackagedAllowlistSha256 '{sha256}' "
        f"-ProductVersion {version_arg}; "
        "'{0} {1} {2} {3}' -f $shape.Legacy, $shape.ProjectionFiles, "
        "$shape.PrivateReferences, $shape.PublicArtifacts",
    )
    if accepted is None:
        assert result.returncode != 0, result.stdout + result.stderr
        assert "allowlist identity" in result.stderr + result.stdout
    elif accepted == "current":
        assert result.returncode == 0, result.stdout + result.stderr
        assert result.stdout.strip().endswith("False 215 1 173"), result.stdout
    else:
        assert result.returncode == 0, result.stdout + result.stderr
        assert result.stdout.strip().endswith("True 216 0 174"), result.stdout


def test_smoke_pins_match_the_packager_and_the_published_legacy_allowlist() -> None:
    """The smoke pins stay equal to the packager pin and to the allowlist that 1.2.1 shipped."""
    smoke = (ROOT / "scripts/smoke-release.ps1").read_text(encoding="utf-8")
    package = (ROOT / "scripts/package.ps1").read_text(encoding="utf-8")
    assert f"$ApprovedCanonicalGoldenAllowlistSha256 = '{CURRENT_ALLOWLIST_SHA256}'" in smoke
    assert f"$LegacyCanonicalGoldenAllowlistSha256 = '{LEGACY_ALLOWLIST_SHA256}'" in smoke
    assert f"$ApprovedCanonicalGoldenReleaseAllowlistSha256 = '{CURRENT_ALLOWLIST_SHA256}'" in package
    current = (ROOT / "testdata/golden/release-canonical-v1.json").read_bytes()
    assert hashlib.sha256(current).hexdigest() == CURRENT_ALLOWLIST_SHA256


@pytest.mark.parametrize(
    "version,mutation,expected",
    [
        ("1.1.7", "valid", "has no materialized built-in profile files"),
        ("1.1.8", "valid", "has no materialized built-in profile files"),
        ("1.1.10", "valid", "has no materialized built-in profile files"),
        ("1.2.1", "valid", "has no materialized built-in profile files"),
        ("1.2.2", "valid", "public confidential-reference manifest"),
        ("1.2.5", "valid", "public confidential-reference manifest"),
        ("1.1.8", "missing", "external-tool files differ from the approved allowlist"),
        (
            "1.1.8",
            "tampered",
            "runtime is missing or does not match the approved SHA-256",
        ),
        ("1.1.8", "extra", "external-tool files differ from the approved allowlist"),
        ("1.1.8", "fake-exe", "Combiner does not match the approved SHA-256"),
    ],
)
def test_release_entrypoint_enforces_runtime_before_later_package_gates(
    tmp_path,
    version,
    mutation,
    expected,
) -> None:
    """Use a real ZIP and self-consistent file hashes; no executable is launched."""
    if not PWSH:
        pytest.skip("PowerShell 7 is required")
    payloads = {
        name: b"non-executable package fixture\n"
        for name in (
            "NvtFwCombiner.exe",
            "README.txt",
            "LICENSE.txt",
            "THIRD-PARTY-NOTICES.txt",
            "SHA256SUMS.txt",
            "launcher/NvtFwCombiner.Launcher.exe",
            "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
        )
    }
    catalog = json.loads((ROOT / "external-tools/catalog.json").read_text())
    for path in catalog["releasePackagePaths"]:
        if (ROOT / path).is_file():
            payloads[path] = (ROOT / path).read_bytes()
    if version == "1.1.7" or mutation == "missing":
        del payloads[RUNTIME_PATH.as_posix()]
    elif mutation == "tampered":
        payloads[RUNTIME_PATH.as_posix()] = b"self-consistent substituted runtime"
    elif mutation == "extra":
        payloads[(COMBINER_DIRECTORY / "unexpected.dll").as_posix()] = b"extra"
    elif mutation == "fake-exe":
        payloads[(COMBINER_DIRECTORY / "Combiner.exe").as_posix()] = b"exit-only fake"
    policy = "docs/contracts/canonical-capability-policy-v1.json"
    payloads[policy] = (ROOT / policy).read_bytes()
    entries = [
        {
            "path": path,
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
            "role": (
                "externalTool"
                if path.startswith("external-tools/")
                else "capabilityPolicy"
                if path == policy
                else "launcher"
                if path.startswith("launcher/")
                else "application"
                if path == "NvtFwCombiner.exe"
                else "documentation"
            ),
        }
        for path, payload in payloads.items()
        if path != "SHA256SUMS.txt"
    ]
    launcher = next(entry for entry in entries if entry["role"] == "launcher")
    manifest = {
        "schemaVersion": "1.2",
        "version": version,
        "sourceTag": f"v{version}",
        "versionManagementProtocolVersion": 1,
        "files": entries,
        "launcher": {
            "protocolVersion": 1,
            "launcherVersion": "1.0.0",
            "executableRelativePath": launcher["path"],
            "size": launcher["size"],
            "sha256": launcher["sha256"],
        },
    }
    payloads["RELEASE-MANIFEST.json"] = json.dumps(manifest).encode()
    package_name = f"NvtFwCombiner-v{version}-win-x64"
    archive_path = tmp_path / f"{package_name}.zip"
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for path, payload in payloads.items():
            archive.writestr(f"{package_name}/{path}", payload)
    script = str(ROOT / "scripts/smoke-release.ps1").replace("'", "''")
    package_path = str(archive_path).replace("'", "''")
    result = subprocess.run(
        [
            PWSH,
            "-NoProfile",
            "-Command",
            f"{PWSH_FORCE_UTF8_OUTPUT}; "
            f"& '{script}' -PackagePath '{package_path}' -SkipUiLaunch",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=50,
        check=False,
    )
    assert result.returncode != 0
    # Each fixture must reach its declared gate before UI launch.
    assert expected in result.stderr, result.stdout + result.stderr


def test_stable_package_couples_one_version_scoped_launcher() -> None:
    package = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")

    assert "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj" in package
    assert "launcher/NvtFwCombiner.Launcher.exe" in package
    assert "$IncludeManagedLauncher = -not $AllowPrerelease" in package
    assert "schemaVersion = if ($IncludeManagedLauncher) { '1.2' }" in package
    assert "$Manifest.versionManagementProtocolVersion = 1" in package
    assert "role = 'launcher'" in package
    assert "NvtFwCombiner.Bootstrap.exe" not in package


REVIEWED_PUBLIC_MANIFEST_SHA256 = hashlib.sha256(
    (ROOT / "docs/references/confidential-references.json").read_bytes()
).hexdigest()


def run_public_reference_inventory(
    files: list[dict[str, str]], version: str | None = None, published: dict[str, str] | None = None
):
    entries = json.dumps(files, separators=(",", ":")).replace("'", "''")
    manifest_path = str(ROOT / "docs/references/confidential-references.json").replace("'", "''")
    table = "".join(f"'{key}' = '{value}'; " for key, value in (published or {}).items())
    version_arg = "" if version is None else f" -ProductVersion ([version]'{version}')"
    return run_release_functions(
        "smoke-release.ps1",
        ("Get-LowerSha256", "Assert-PublicReferenceInventory"),
        f"$ApprovedPublicReferenceManifestPath = '{manifest_path}'; "
        f"$PublishedPublicReferenceManifestSha256 = @{{ {table}}}; "
        f"$manifest = [pscustomobject]@{{ files = ('{entries}' | ConvertFrom-Json) }}; "
        f"Assert-PublicReferenceInventory -Manifest $manifest{version_arg}",
    )


@pytest.mark.parametrize(
    ("paths", "valid"),
    [
        (["reference/docs/references/ic-flashmap/README.md"], True),
        (["reference/docs/references/ic-flashmap/data.json"], True),
        (["reference/docs/references/ic-flashmap/sheet.xlsx"], False),
        (["reference/docs/references/ic-flashmap/script.bat"], False),
        (["reference/docs/references/ic-flashmap/memory_mmap.h"], False),
        (["reference/golden/c001/provenance/script.bat"], False),
        (["reference/docs/References/ic-flashmap/script.bat"], False),
        (["REFERENCE/docs/references/source.c"], False),
        (["reference/docs/references/sheet.xls"], False),
        (["reference/docs/references/source.cpp"], False),
        (["REFERENCE/GOLDEN/c001/provenance/script.BAT"], False),
        (["reference/docs/architecture/ic-workflow-flowcharts.md"], True),
        (["reference/docs/architecture/private.xlsx"], False),
        (["reference/docs/architecture/postbuild.bat"], False),
        (["reference/docs/architecture/notes.txt"], False),
        (["reference/golden/c001/expected/output.bin"], True),
        (["reference/golden/c001/provenance/source.cpp"], False),
        (["reference/golden/c001/provenance/cmd.ps1"], False),
        ([r"reference\docs\references\ic-flashmap\script.bat"], False),
    ],
)
def test_public_reference_inventory_rejects_confidential_content(
    paths: list[str], valid: bool
) -> None:
    files = [
        {
            "path": "reference/docs/references/confidential-references.json",
            "role": "reference",
            "sha256": REVIEWED_PUBLIC_MANIFEST_SHA256,
        },
        *({"path": path, "role": "reference"} for path in paths),
    ]
    result = run_public_reference_inventory(files)
    assert (result.returncode == 0) is valid, result.stdout + result.stderr


@pytest.mark.parametrize("sha256", ["0" * 64, "", "A" * 64])
def test_public_reference_inventory_requires_the_reviewed_manifest(sha256: str) -> None:
    """Replacing the packaged inventory with any other JSON fails the smoke."""
    files = [
        {
            "path": "reference/docs/references/confidential-references.json",
            "role": "reference",
            "sha256": sha256,
        }
    ]
    result = run_public_reference_inventory(files)
    assert result.returncode != 0
    assert "differs from the reviewed manifest of its version" in result.stdout + result.stderr


def manifest_entry(sha256: str) -> list[dict[str, str]]:
    return [{
        "path": "reference/docs/references/confidential-references.json",
        "role": "reference",
        "sha256": sha256,
    }]


def test_a_published_version_keeps_its_own_reviewed_manifest() -> None:
    """A newer checkout still smokes an older published package with the manifest that package shipped."""
    older = "b" * 64
    assert older != REVIEWED_PUBLIC_MANIFEST_SHA256
    result = run_public_reference_inventory(manifest_entry(older), "1.2.2", {"1.2.2": older})
    assert result.returncode == 0, result.stdout + result.stderr


def test_a_published_version_rejects_the_current_manifest_when_it_shipped_another() -> None:
    older = "b" * 64
    result = run_public_reference_inventory(
        manifest_entry(REVIEWED_PUBLIC_MANIFEST_SHA256), "1.2.2", {"1.2.2": older}
    )
    assert result.returncode != 0
    assert "differs from the reviewed manifest of its version" in result.stdout + result.stderr


def test_an_unpublished_version_must_carry_the_manifest_of_this_checkout() -> None:
    result = run_public_reference_inventory(manifest_entry("b" * 64), "1.2.3", {"1.2.2": "b" * 64})
    assert result.returncode != 0
    ok = run_public_reference_inventory(manifest_entry(REVIEWED_PUBLIC_MANIFEST_SHA256), "1.2.3", {"1.2.2": "b" * 64})
    assert ok.returncode == 0, ok.stdout + ok.stderr


def test_release_smoke_rejects_bootstrap_in_update_and_checks_launcher_identity() -> (
    None
):
    smoke = (ROOT / "scripts" / "smoke-release.ps1").read_text(encoding="utf-8-sig")

    assert (
        "Immutable Bootstrap must remain outside every version update package." in smoke
    )
    assert "Release manifest launcher identity is inconsistent." in smoke
    assert "Version 1.0.0 and newer require the managed launcher contract." in smoke


def test_manual_only_package_mode_is_retired_while_historical_smoke_remains() -> None:
    package = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")
    smoke = (ROOT / "scripts" / "smoke-release.ps1").read_text(encoding="utf-8-sig")

    assert "[switch]$ManualOnly" not in package
    assert "$Manifest.distributionMode = 'manual-only'" not in package
    assert "scripts/package.ps1 manual-only operator build" in smoke
    assert (
        "Manual-only release package contains forbidden deployment or reference content."
        in smoke
    )
    assert "Release manifest manual-only identity is inconsistent." in smoke


def test_managed_lab_publishes_only_the_immutable_bootstrap_at_root() -> None:
    lab = (ROOT / "scripts" / "create-managed-installation-lab.ps1").read_text(
        encoding="utf-8-sig"
    )

    assert (
        "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj"
        in lab
    )
    assert "Move-Item -LiteralPath $PublishedBootstrap -Destination $Bootstrap" in lab
    assert "--bootstrap $Bootstrap" in lab
    assert "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj" not in lab


def test_process_smoke_runs_published_nested_ready_and_exact_rollback() -> None:
    smoke = (ROOT / "scripts" / "smoke-launcher-bootstrap.ps1").read_text(
        encoding="utf-8-sig"
    )

    assert "NvtFwCombiner.LauncherBootstrap.csproj" in smoke
    assert "NvtFwCombiner.Launcher.csproj" in smoke
    assert "NvtFwCombiner.ReadyProbe.csproj" in smoke
    assert "$env:LOCALAPPDATA = $LocalAppData" in smoke
    assert "cleanZeroArgumentExit" in smoke
    assert "--failing-launcher $Probe" in smoke
    assert "$env:NVT_READY_PROBE_BEHAVIOR = 'exit-outer-candidate'" in smoke
    assert "candidateFailureKind = 'exited-before-ready'" in smoke
    assert "missingOuterReadyExit = $MissingOuterReadyExit" in smoke
    assert "$MissingOuterReadyExit -ne 16" in smoke
    assert "active.ownerAppVersion -ne '0.10.5'" in smoke
    assert "failed.ownerAppVersion -ne '0.10.6'" in smoke
