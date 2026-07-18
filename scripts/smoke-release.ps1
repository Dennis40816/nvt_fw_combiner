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

$ApprovedPackageBaselineBytes = 57501699
$MaximumPackageBytes = 58076715
$ApprovedExternalToolPackagePaths = @(
    'external-tools/README.md',
    'external-tools/legacy-combiner/README.md',
    'external-tools/legacy-combiner/1.13.0/Combiner.exe',
    'external-tools/legacy-combiner/1.13.0/manifest.json'
) | Sort-Object

$ApprovedRuntimeCatalogPackagePaths = @(
    'profiles/built-in/ctrlram-postbuild-v2/catalog.json',
    'profiles/built-in/ctrlram-postbuild-v2/flash-map.json'
) | Sort-Object

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

function Assert-AssetFileName {
    param(
        [Parameter(Mandatory = $true)][string]$AssetName,
        [Parameter(Mandatory = $true)][string]$AssetKind
    )

    if ([string]::IsNullOrWhiteSpace($AssetName) -or
        -not [string]::Equals([IO.Path]::GetFileName($AssetName), $AssetName, [StringComparison]::Ordinal)) {
        throw "Release manifest has an unsafe $AssetKind asset name '$AssetName'."
    }
}

function Assert-DeclaredSubjectHashes {
    param(
        [Parameter(Mandatory = $true)][object[]]$ManifestEntries,
        [Parameter(Mandatory = $true)][object[]]$Subjects,
        [Parameter(Mandatory = $true)][string]$ArtifactKind
    )

    $expected = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $ManifestEntries) {
        $path = [string]$entry.path
        if (-not $expected.TryAdd($path, [string]$entry.sha256)) {
            throw "Release manifest repeats file '$path'."
        }
    }

    $actual = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($subject in $Subjects) {
        $path = [string]$subject.name
        if (-not $actual.TryAdd($path, [string]$subject.sha256)) {
            throw "$ArtifactKind repeats subject '$path'."
        }
    }

    if ($expected.Count -ne $actual.Count) {
        throw "$ArtifactKind subjects do not match the release manifest."
    }

    foreach ($path in $expected.Keys) {
        $actualHash = $null
        if (-not $actual.TryGetValue($path, [ref]$actualHash) -or $actualHash -ne $expected[$path]) {
            throw "$ArtifactKind subject hash mismatch: $path"
        }
    }
}

function Assert-ReleaseSidecars {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [Parameter(Mandatory = $true)][string]$PackageDirectory,
        [Parameter(Mandatory = $true)]$Manifest
    )

    Assert-AssetFileName -AssetName ([string]$Manifest.sbomAsset) -AssetKind 'SBOM'
    Assert-AssetFileName -AssetName ([string]$Manifest.provenanceAsset) -AssetKind 'provenance'
    if (-not ([string]$Manifest.sbomAsset).EndsWith('.spdx.json', [StringComparison]::Ordinal) -or
        -not ([string]$Manifest.provenanceAsset).EndsWith('.provenance.json', [StringComparison]::Ordinal)) {
        throw 'Release manifest sidecar names do not match the required SBOM/provenance suffixes.'
    }

    $expectedPackageName = "NvtFwCombiner-$($Manifest.sourceTag)-win-x64"
    if (-not [string]::Equals((Split-Path -Leaf $PackageRoot), $expectedPackageName, [StringComparison]::Ordinal)) {
        throw "Release package root does not match manifest source tag '$($Manifest.sourceTag)'."
    }

    $sbomPath = Join-Path $PackageDirectory ([string]$Manifest.sbomAsset)
    $provenancePath = Join-Path $PackageDirectory ([string]$Manifest.provenanceAsset)
    if (-not (Test-Path -LiteralPath $sbomPath -PathType Leaf)) {
        throw "Release SBOM sidecar is missing: $($Manifest.sbomAsset)"
    }
    if (-not (Test-Path -LiteralPath $provenancePath -PathType Leaf)) {
        throw "Release provenance sidecar is missing: $($Manifest.provenanceAsset)"
    }

    $sbom = Get-Content -LiteralPath $sbomPath -Raw | ConvertFrom-Json
    if ($sbom.spdxVersion -ne 'SPDX-2.3' -or
        $sbom.name -ne $expectedPackageName -or
        -not ([string]$sbom.documentNamespace).EndsWith("/$($Manifest.sourceTag)/$($Manifest.sbomAsset)", [StringComparison]::Ordinal)) {
        throw 'Release SBOM identity does not match the package manifest.'
    }
    $sbomPackages = @($sbom.packages)
    if ($sbomPackages.Count -ne 1 -or
        $sbomPackages[0].name -ne $Manifest.product -or
        $sbomPackages[0].versionInfo -ne $Manifest.version) {
        throw 'Release SBOM package metadata does not match the package manifest.'
    }

    $sbomSubjects = @($sbom.files | ForEach-Object {
        $sha256 = @($_.checksums | Where-Object { $_.algorithm -eq 'SHA256' })
        if ($sha256.Count -ne 1) {
            throw "Release SBOM has an invalid SHA-256 declaration for '$($_.fileName)'."
        }
        [pscustomobject]@{ name = [string]$_.fileName; sha256 = [string]$sha256[0].checksumValue }
    })
    Assert-DeclaredSubjectHashes -ManifestEntries @($Manifest.files) -Subjects $sbomSubjects -ArtifactKind 'Release SBOM'

    $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
    if ($provenance.schemaVersion -ne '1.0' -or
        $provenance.product -ne $Manifest.product -or
        $provenance.version -ne $Manifest.version -or
        $provenance.sourceCommit -ne $Manifest.sourceCommit -or
        $provenance.sourceTag -ne $Manifest.sourceTag -or
        $provenance.runtimeIdentifier -ne $Manifest.runtimeIdentifier) {
        throw 'Release provenance identity does not match the package manifest.'
    }
    Assert-DeclaredSubjectHashes -ManifestEntries @($Manifest.files) -Subjects @($provenance.subjects) -ArtifactKind 'Release provenance'
}

$fullPackagePath = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $fullPackagePath -PathType Leaf)) {
    throw "Release package was not found: $fullPackagePath"
}
if (-not $fullPackagePath.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release smoke requires a .zip package.'
}
$packageBytes = (Get-Item -LiteralPath $fullPackagePath).Length
if ($packageBytes -gt $MaximumPackageBytes) {
    throw "Release package size $packageBytes exceeds the owner-approved maximum $MaximumPackageBytes bytes (v0.9.7 baseline $ApprovedPackageBaselineBytes bytes plus 1%)."
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

    $DeclaredExternalToolEntries = @(
        $manifest.files | Where-Object {
            ([string]$_.path).StartsWith('external-tools/', [StringComparison]::Ordinal) -or
            $_.role -eq 'externalTool'
        }
    )
    $InvalidExternalToolEntries = @(
        $DeclaredExternalToolEntries | Where-Object {
            -not ([string]$_.path).StartsWith('external-tools/', [StringComparison]::Ordinal) -or
            $_.role -ne 'externalTool'
        }
    )
    if ($InvalidExternalToolEntries.Count -ne 0) {
        throw 'Release manifest external-tool paths and roles are inconsistent.'
    }

    $DeclaredExternalToolPaths = @($DeclaredExternalToolEntries | ForEach-Object { [string]$_.path } | Sort-Object)
    if (Compare-Object -ReferenceObject $ApprovedExternalToolPackagePaths -DifferenceObject $DeclaredExternalToolPaths) {
        throw 'Release manifest external-tool files differ from the approved allowlist.'
    }

    $DeclaredBuiltInProfileEntries = @(
        $manifest.files | Where-Object {
            ([string]$_.path).StartsWith('profiles/built-in/', [StringComparison]::Ordinal) -or
            $_.role -eq 'builtInProfile'
        }
    )
    if ($DeclaredBuiltInProfileEntries.Count -eq 0) {
        throw 'Release manifest has no materialized built-in profile files.'
    }
    $InvalidBuiltInProfileEntries = @(
        $DeclaredBuiltInProfileEntries | Where-Object {
            -not ([string]$_.path).StartsWith('profiles/built-in/', [StringComparison]::Ordinal) -or
            $_.role -ne 'builtInProfile'
        }
    )
    if ($InvalidBuiltInProfileEntries.Count -ne 0) {
        throw 'Release manifest built-in profile paths and roles are inconsistent.'
    }
    $BuiltInProfileBundleManifests = @(
        $DeclaredBuiltInProfileEntries | Where-Object {
            ([string]$_.path) -match '^profiles/built-in/[^/]+/profile-bundle\.json$'
        }
    )
    if ($BuiltInProfileBundleManifests.Count -eq 0) {
        throw 'Release manifest has no built-in profile bundle manifest.'
    }
    $DeclaredRuntimeCatalogPaths = @(
        $DeclaredBuiltInProfileEntries |
            Where-Object {
                ([string]$_.path).StartsWith('profiles/built-in/ctrlram-postbuild-v2/', [StringComparison]::Ordinal)
            } |
            ForEach-Object { [string]$_.path } |
            Sort-Object
    )
    if (Compare-Object -ReferenceObject $ApprovedRuntimeCatalogPackagePaths -DifferenceObject $DeclaredRuntimeCatalogPaths) {
        throw 'Release manifest runtime catalog files differ from the approved allowlist.'
    }

    foreach ($entry in $manifest.files) {
        Assert-FileHash -Root $packageRoot -Entry $entry
    }
    Assert-ReleaseSidecars -PackageRoot $packageRoot -PackageDirectory (Split-Path -Parent $fullPackagePath) -Manifest $manifest

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
