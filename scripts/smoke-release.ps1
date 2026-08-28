[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PackagePath,

    [ValidateRange(1, 30)]
    [int]$StartupWaitSeconds = 15,

    [switch]$SkipUiLaunch,

    [switch]$KeepExtracted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MaximumLegacyPackageBytes = 80000000
$MaximumManagedUpgradePairPackageBytes = 134217728
$MaximumApplicationBytes = 80000000
$ApprovedExternalToolPackagePaths = @(
    'external-tools/README.md',
    'external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe',
    'external-tools/legacy-combiner/README.md',
    'external-tools/legacy-combiner/1.13.0/Combiner.exe',
    'external-tools/legacy-combiner/1.13.0/manifest.json'
) | Sort-Object

$ApprovedRuntimeCatalogPackagePaths = @(
    'profiles/built-in/ctrlram-postbuild-v2/catalog.json',
    'profiles/built-in/ctrlram-postbuild-v2/flash-map.json'
) | Sort-Object
$PackageTrustIndexPackagePath = 'profiles/built-in/package-trust-index.json'
$ApprovedPackageTrustIndexSha256 = 'e365b73e53aff65faa107347400aac82546a3dc700160914b1412f6858fe276d'
$ApprovedCanonicalCapabilityPolicyPackageContract = [pscustomobject]@{
    path = 'docs/contracts/canonical-capability-policy-v1.json'
    role = 'capabilityPolicy'
    sha256 = 'bf818a4c9aa4d539882e4bc4a0a662ef70ece67a44e78ae83356430365828f50'
}
$RetiredSupportPublicationPolicyPackagePaths = @(
    'docs/contracts/support-publication-policy-v1.0.0.json',
    'docs/contracts/support-publication-policy-v1.json'
)
function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ProfileBundleEntryArrayHash {
    param([Parameter(Mandatory = $true)][object[]]$Entries)

    $CanonicalEntries = @(
        $Entries |
            Sort-Object entryId, kind, path, schemaId, contentHash |
            ForEach-Object {
                [ordered]@{
                    contentHash = [string]$_.contentHash
                    entryId = [string]$_.entryId
                    kind = [string]$_.kind
                    path = [string]$_.path
                    schemaId = [string]$_.schemaId
                }
            }
    )
    $Canonical = ConvertTo-Json -InputObject $CanonicalEntries -Compress -Depth 4
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData(
            [Text.Encoding]::UTF8.GetBytes($Canonical))).ToLowerInvariant()
}

function Assert-SafeProfileBundlePath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $Segments = @($RelativePath.Split('/'))
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains('\') -or
        $RelativePath.Contains(':') -or
        @($Segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -in @('.', '..') }).Count -ne 0) {
        throw "Profile bundle contains an unsafe path '$RelativePath'."
    }
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
$packageFileName = Split-Path -Leaf $fullPackagePath
$ManagedUpgradePairPackageNames = @(
    'NvtFwCombiner-v1.0.0-win-x64.zip',
    'NvtFwCombiner-v1.0.1-win-x64.zip'
)
$MaximumPackageBytes = if ($packageFileName -cin $ManagedUpgradePairPackageNames) {
    $MaximumManagedUpgradePairPackageBytes
}
else {
    $MaximumLegacyPackageBytes
}
if ($packageBytes -gt $MaximumPackageBytes) {
    throw "Release package size $packageBytes exceeds the owner-approved maximum $MaximumPackageBytes bytes."
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
    $RequiredPackagePaths = @(
        @(
            'NvtFwCombiner.exe',
            'external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe',
            'RELEASE-MANIFEST.json',
            'SHA256SUMS.txt',
            'README.txt',
            'LICENSE.txt',
            'THIRD-PARTY-NOTICES.txt'
        ) + @([string]$ApprovedCanonicalCapabilityPolicyPackageContract.path)
    )
    foreach ($requiredPath in $RequiredPackagePaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $requiredPath) -PathType Leaf)) {
            throw "Release package is missing required file '$requiredPath'."
        }
    }

    $applicationPath = Join-Path $packageRoot 'NvtFwCombiner.exe'
    $applicationBytes = (Get-Item -LiteralPath $applicationPath).Length
    if ($applicationBytes -gt $MaximumApplicationBytes) {
        throw "Release application size $applicationBytes exceeds the owner-approved maximum $MaximumApplicationBytes bytes."
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

    if (Test-Path -LiteralPath (Join-Path $packageRoot 'NvtFwCombiner.Bootstrap.exe') -PathType Leaf) {
        throw 'Immutable Bootstrap must remain outside every version update package.'
    }
    $LauncherEntries = @(
        $manifest.files | Where-Object {
            $_.path -eq 'launcher/NvtFwCombiner.Launcher.exe' -or $_.role -eq 'launcher'
        }
    )
    $ManifestSchemaVersion = if ($manifest.PSObject.Properties.Name -contains 'schemaVersion') {
        [string]$manifest.schemaVersion
    }
    else { $null }
    $ManifestLauncher = if ($manifest.PSObject.Properties.Name -contains 'launcher') {
        $manifest.launcher
    }
    else { $null }
    $ManifestVersion = if ($manifest.PSObject.Properties.Name -contains 'version') {
        [string]$manifest.version
    }
    else { $null }
    $ManifestProtocolVersion = if ($manifest.PSObject.Properties.Name -contains 'versionManagementProtocolVersion') {
        [int]$manifest.versionManagementProtocolVersion
    }
    else { 0 }
    if ($ManifestSchemaVersion -eq '1.2') {
        if ($ManifestProtocolVersion -ne 1 -or
            $null -eq $ManifestLauncher -or
            [int]$ManifestLauncher.protocolVersion -ne 1 -or
            [string]$ManifestLauncher.launcherVersion -notmatch '^\d+\.\d+\.\d+$' -or
            [string]$ManifestLauncher.executableRelativePath -ne 'launcher/NvtFwCombiner.Launcher.exe' -or
            $LauncherEntries.Count -ne 1 -or
            [string]$LauncherEntries[0].path -ne 'launcher/NvtFwCombiner.Launcher.exe' -or
            [string]$LauncherEntries[0].role -ne 'launcher' -or
            [long]$LauncherEntries[0].size -ne [long]$ManifestLauncher.size -or
            [string]$LauncherEntries[0].sha256 -ne [string]$ManifestLauncher.sha256) {
            throw 'Release manifest launcher identity is inconsistent.'
        }
    }
    elseif ($null -ne $ManifestSchemaVersion -and
        ($LauncherEntries.Count -ne 0 -or $null -ne $ManifestLauncher)) {
        throw 'Legacy release manifest must not declare managed launcher identity.'
    }
    if ($ManifestVersion -match '^(\d+)\.\d+\.\d+$' -and
        [int]$Matches[1] -ge 1 -and
        $ManifestSchemaVersion -ne '1.2') {
        throw 'Version 1.0.0 and newer require the managed launcher contract.'
    }

    if (@($manifest.files |
            Where-Object {
                $_.path -in $RetiredSupportPublicationPolicyPackagePaths -or
                $_.role -eq 'publicationPolicy'
            }).Count -ne 0) {
        throw 'Release manifest contains the retired support publication policy payload.'
    }

    $DeclaredCapabilityPolicyEntries = @(
        $manifest.files | Where-Object {
            $_.path -eq $ApprovedCanonicalCapabilityPolicyPackageContract.path -or
            $_.role -eq 'capabilityPolicy'
        }
    )
    if ($DeclaredCapabilityPolicyEntries.Count -ne 1 -or
        $DeclaredCapabilityPolicyEntries[0].path -ne
            $ApprovedCanonicalCapabilityPolicyPackageContract.path -or
        $DeclaredCapabilityPolicyEntries[0].role -ne
            $ApprovedCanonicalCapabilityPolicyPackageContract.role -or
        $DeclaredCapabilityPolicyEntries[0].sha256 -ne
            $ApprovedCanonicalCapabilityPolicyPackageContract.sha256) {
        throw 'Release manifest canonical capability policy identity is inconsistent.'
    }
    $CapabilityPolicyPath = Join-Path `
        $packageRoot `
        ([string]$ApprovedCanonicalCapabilityPolicyPackageContract.path).Replace(
            '/',
            [IO.Path]::DirectorySeparatorChar)
    if ((Get-LowerSha256 -Path $CapabilityPolicyPath) -ne
        [string]$ApprovedCanonicalCapabilityPolicyPackageContract.sha256) {
        throw 'Release package canonical capability policy does not match the approved SHA-256.'
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
    $PackageTrustIndexEntries = @(
        $DeclaredBuiltInProfileEntries | Where-Object {
            ([string]$_.path) -eq $PackageTrustIndexPackagePath
        }
    )
    if ($PackageTrustIndexEntries.Count -ne 1) {
        throw 'Release manifest must contain exactly one package trust index.'
    }
    $PackageTrustIndexPath = Join-Path $packageRoot $PackageTrustIndexPackagePath
    if ((Get-LowerSha256 -Path $PackageTrustIndexPath) -ne $ApprovedPackageTrustIndexSha256) {
        throw 'Release package trust index does not match the exact reviewed identity.'
    }
    $PackageTrustIndex = Get-Content -LiteralPath $PackageTrustIndexPath -Raw |
        ConvertFrom-Json -Depth 32
    if ([string]$PackageTrustIndex.schemaVersion -ne '1.1' -or
        [string]$PackageTrustIndex.trustAnchorBindingId -ne 'built-in-profile-bundle-v2') {
        throw 'Release package trust index has an unsupported schema or trust anchor.'
    }
    $BuiltInProfileBundleManifests = @(
        $DeclaredBuiltInProfileEntries | Where-Object {
            ([string]$_.path) -match '^profiles/built-in/[^/]+/profile-bundle\.json$'
        }
    )
    if ($BuiltInProfileBundleManifests.Count -eq 0) {
        throw 'Release manifest has no built-in profile bundle manifest.'
    }
    $IndexedManifestPaths = @(
        @($PackageTrustIndex.bundles) | ForEach-Object {
            "profiles/built-in/$([string]$_.bundleDirectory)/profile-bundle.json"
        } | Sort-Object
    )
    $DeclaredManifestPaths = @(
        $BuiltInProfileBundleManifests | ForEach-Object { [string]$_.path } | Sort-Object
    )
    if (Compare-Object -ReferenceObject $IndexedManifestPaths -DifferenceObject $DeclaredManifestPaths) {
        throw 'Release package trust index and bundle manifest inventory differ.'
    }
    foreach ($TrustEntry in @($PackageTrustIndex.bundles)) {
        $BundleDirectory = [string]$TrustEntry.bundleDirectory
        if ($BundleDirectory -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
            throw "Release package trust index has an unsafe bundle directory '$BundleDirectory'."
        }
        $BundleRoot = Join-Path $packageRoot "profiles/built-in/$BundleDirectory"
        $BundleManifestPath = Join-Path $BundleRoot 'profile-bundle.json'
        $BundleManifest = Get-Content -LiteralPath $BundleManifestPath -Raw | ConvertFrom-Json
        $BundleEntries = @($BundleManifest.entries)
        if ($BundleEntries.Count -eq 0 -or
            [string]$BundleManifest.schemaVersion -ne [string]$TrustEntry.bundleSchemaVersion -or
            [string]$BundleManifest.bundleVersion -ne [string]$TrustEntry.bundleVersion -or
            [string]$BundleManifest.contentHash -ne [string]$TrustEntry.contentHash -or
            [string]$BundleManifest.trustAnchorBindingId -ne [string]$PackageTrustIndex.trustAnchorBindingId -or
            [string]$BundleManifest.hashAlgorithm -ne 'sha256-rfc8785-entry-array-v1' -or
            (Get-ProfileBundleEntryArrayHash -Entries $BundleEntries) -ne [string]$BundleManifest.contentHash) {
            throw "Release built-in profile bundle '$BundleDirectory' differs from its trust-index identity."
        }
        foreach ($BundleEntry in $BundleEntries) {
            $EntryPath = [string]$BundleEntry.path
            Assert-SafeProfileBundlePath -RelativePath $EntryPath
            $EntryFile = Join-Path $BundleRoot $EntryPath.Replace('/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $EntryFile -PathType Leaf) -or
                [string]$BundleEntry.contentHash -notmatch '^[0-9a-f]{64}$' -or
                (Get-LowerSha256 -Path $EntryFile) -ne [string]$BundleEntry.contentHash) {
                throw "Release built-in profile bundle file hash differs: $BundleDirectory/$EntryPath"
            }
        }
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

    foreach ($line in Get-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding utf8) {
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

    $workerPath = Join-Path $packageRoot 'external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe'
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
        $application = $null
        try {
            $application = Start-Process `
                -FilePath (Join-Path $packageRoot 'NvtFwCombiner.exe') `
                -WorkingDirectory $packageRoot `
                -PassThru
            $startupDeadline = [DateTime]::UtcNow.AddSeconds($StartupWaitSeconds)
            do {
                Start-Sleep -Milliseconds 100
                $application.Refresh()
            }
            while (
                -not $application.HasExited -and
                $application.MainWindowHandle -eq 0 -and
                [DateTime]::UtcNow -lt $startupDeadline
            )

            if ($application.HasExited) {
                throw "Bundled application exited during startup with code $($application.ExitCode)."
            }
            if ($application.MainWindowHandle -eq 0) {
                throw "Bundled application did not create a main window within $StartupWaitSeconds seconds."
            }
            if (-not $application.Responding) {
                throw 'Bundled application main window is not responding.'
            }
        }
        finally {
            if ($null -ne $application) {
                $application.Refresh()
                if (-not $application.HasExited) {
                    Stop-Process -Id $application.Id -Force
                    $application.WaitForExit()
                }
                $application.Dispose()
            }
        }
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
