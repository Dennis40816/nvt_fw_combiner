[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PackagePath,

    [ValidateRange(1, 30)]
    [int]$StartupWaitSeconds = 3,

    [switch]$SkipUiLaunch,

    [switch]$KeepExtracted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePackagePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    return [IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

function Assert-FileHash {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)]$Entry
    )

    $path = Join-Path $Root ([string]$Entry.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Manifest file is missing: $($Entry.path)"
    }

    $actual = Get-LowerSha256 -Path $path
    if ($actual -ne [string]$Entry.sha256) {
        throw "Manifest hash mismatch: $($Entry.path)"
    }
}

$fullPackagePath = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $fullPackagePath -PathType Leaf)) {
    throw "Release package was not found: $fullPackagePath"
}
if (-not $fullPackagePath.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release smoke requires a .zip package.'
}

$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) "nvt-fw-combiner-release-smoke-$([guid]::NewGuid().ToString('N'))"
$extractRoot = Join-Path $smokeRoot 'extract'
$packageRoot = $null

try {
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    Expand-Archive -LiteralPath $fullPackagePath -DestinationPath $extractRoot -Force

    $topLevelDirectories = @(Get-ChildItem -LiteralPath $extractRoot -Directory)
    $topLevelFiles = @(Get-ChildItem -LiteralPath $extractRoot -File)
    if ($topLevelDirectories.Count -ne 1 -or $topLevelFiles.Count -ne 0) {
        throw 'Release ZIP must contain exactly one top-level package directory.'
    }

    $packageRoot = $topLevelDirectories[0].FullName
    foreach ($requiredPath in @(
        'NvtFwCombiner.exe',
        'Nfc.CrcWorker.exe',
        'RELEASE-MANIFEST.json',
        'SHA256SUMS.txt',
        'README.txt',
        'LICENSE.txt',
        'THIRD-PARTY-NOTICES.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $requiredPath) -PathType Leaf)) {
            throw "Release package is missing required file '$requiredPath'."
        }
    }

    $forbiddenFiles = @(
        Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object {
            $_.Extension -in @('.pdb', '.cs', '.csproj', '.sln', '.py', '.pyc') -or
            (Get-RelativePackagePath -Root $packageRoot -Path $_.FullName) -match '^(refcode|src|tests)/'
        }
    )
    if ($forbiddenFiles.Count -gt 0) {
        $paths = $forbiddenFiles | ForEach-Object { Get-RelativePackagePath -Root $packageRoot -Path $_.FullName }
        throw "Release package contains forbidden files: $($paths -join ', ')"
    }

    $manifestPath = Join-Path $packageRoot 'RELEASE-MANIFEST.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($null -eq $manifest.files -or $manifest.files.Count -eq 0) {
        throw 'Release manifest has no file entries.'
    }
    foreach ($entry in $manifest.files) {
        Assert-FileHash -Root $packageRoot -Entry $entry
    }

    $expectedPackagePaths = @(
        @($manifest.files | ForEach-Object { [string]$_.path }) +
        @('RELEASE-MANIFEST.json', 'SHA256SUMS.txt')
    ) | Sort-Object
    $actualPackagePaths = @(
        Get-ChildItem -LiteralPath $packageRoot -File -Recurse |
            ForEach-Object { Get-RelativePackagePath -Root $packageRoot -Path $_.FullName } |
            Sort-Object
    )
    if (Compare-Object -ReferenceObject $expectedPackagePaths -DifferenceObject $actualPackagePaths) {
        throw 'Release package files differ from the manifest closed allowlist.'
    }

    foreach ($line in Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt')) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line -split '  ', 2
        if ($parts.Count -ne 2) {
            throw "Invalid SHA256SUMS entry '$line'."
        }

        $path = Join-Path $packageRoot $parts[1]
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-LowerSha256 -Path $path) -ne $parts[0]) {
            throw "SHA256SUMS verification failed for '$($parts[1])'."
        }
    }

    $workerPath = Join-Path $packageRoot 'Nfc.CrcWorker.exe'
    $request = '{"protocolVersion":"1.0","requestId":"release-smoke","operation":"calculate","algorithmId":"crc-32-mpeg-2","payloadBase64":"MTIzNDU2Nzg5"}'
    $workerOutput = [string]::Join([Environment]::NewLine, @($request | & $workerPath))
    if ($LASTEXITCODE -ne 0) {
        throw 'Bundled CRC worker exited with a non-zero code.'
    }

    $workerResponse = $workerOutput | ConvertFrom-Json
    if ($workerResponse.result.valueHex -ne '0x0376E6E7') {
        throw "Bundled CRC worker returned '$($workerResponse.result.valueHex)', expected 0x0376E6E7."
    }

    if (-not $SkipUiLaunch) {
        $application = Start-Process -FilePath (Join-Path $packageRoot 'NvtFwCombiner.exe') -PassThru
        Start-Sleep -Seconds $StartupWaitSeconds
        if ($application.HasExited) {
            throw "Bundled application exited during startup with code $($application.ExitCode)."
        }

        Stop-Process -Id $application.Id -Force
        $application.WaitForExit()
    }

    Write-Host "Release smoke passed: $(Split-Path -Leaf $fullPackagePath)"
}
finally {
    if ($KeepExtracted) {
        Write-Host "Release smoke extraction retained: $smokeRoot"
    }
    elseif (Test-Path -LiteralPath $smokeRoot) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force
    }
}
