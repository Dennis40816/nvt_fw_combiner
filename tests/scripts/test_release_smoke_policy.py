import hashlib
import json
import os
import shutil
import subprocess
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
def test_packaged_combiner_executes_certified_crc_command_without_mutation(
    tmp_path,
) -> None:
    package = tmp_path / "package"
    tool_directory = package / COMBINER_DIRECTORY
    tool_directory.mkdir(parents=True)
    for name in ("Combiner.exe", "vcruntime140.dll"):
        shutil.copy2(ROOT / COMBINER_DIRECTORY / name, tool_directory)
    golden = package / "reference" / GOLDEN_PATH
    golden.parent.mkdir(parents=True)
    shutil.copy2(ROOT / GOLDEN_PATH, golden)
    package_arg = str(package).replace("'", "''")
    scratch_arg = str(tmp_path / "scratch").replace("'", "''")
    result = run_release_functions(
        "smoke-release.ps1",
        ("Get-LowerSha256", "Assert-CombinerRuntime", "Invoke-CombinerSmoke"),
        f"Invoke-CombinerSmoke -PackageRoot '{package_arg}' -SmokeRoot '{scratch_arg}'",
    )
    assert result.returncode == 0, result.stdout + result.stderr
    assert "Bundled Combiner CRC smoke passed" in result.stdout
    assert golden.read_bytes() == (ROOT / GOLDEN_PATH).read_bytes()


@pytest.mark.parametrize(
    "version,mutation,expected",
    [
        ("1.1.7", "valid", "has no materialized built-in profile files"),
        ("1.1.8", "valid", "has no materialized built-in profile files"),
        ("1.1.10", "valid", "has no materialized built-in profile files"),
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
    result = subprocess.run(
        [
            PWSH,
            "-NoProfile",
            "-File",
            str(ROOT / "scripts/smoke-release.ps1"),
            "-PackagePath",
            str(archive_path),
            "-SkipUiLaunch",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=50,
        check=False,
    )
    assert result.returncode != 0
    # Valid dependency inventory must advance to the intentionally absent profile gate.
    assert expected in result.stderr, result.stdout + result.stderr


def test_stable_package_couples_one_version_scoped_launcher() -> None:
    package = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")

    assert "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj" in package
    assert "launcher/NvtFwCombiner.Launcher.exe" in package
    assert (
        "$IncludeManagedLauncher = -not ($AllowPrerelease -or $ManualOnly)" in package
    )
    assert "if ($ManualOnly) { '1.3' }" in package
    assert "elseif ($IncludeManagedLauncher) { '1.2' }" in package
    assert "$Manifest.versionManagementProtocolVersion = 1" in package
    assert "role = 'launcher'" in package
    assert "NvtFwCombiner.Bootstrap.exe" not in package


def test_release_smoke_rejects_bootstrap_in_update_and_checks_launcher_identity() -> (
    None
):
    smoke = (ROOT / "scripts" / "smoke-release.ps1").read_text(encoding="utf-8-sig")

    assert (
        "Immutable Bootstrap must remain outside every version update package." in smoke
    )
    assert "Release manifest launcher identity is inconsistent." in smoke
    assert "Version 1.0.0 and newer require the managed launcher contract." in smoke


def test_manual_only_package_is_explicit_and_excludes_deployment_payloads() -> None:
    package = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")
    smoke = (ROOT / "scripts" / "smoke-release.ps1").read_text(encoding="utf-8-sig")

    assert "[switch]$ManualOnly" in package
    assert "$Manifest.distributionMode = 'manual-only'" in package
    assert "scripts/package.ps1 manual-only operator build" in package
    assert "scripts/package.ps1 manual-only operator build" in smoke
    assert "if (-not $ManualOnly) {" in package
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
