[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Commit,

    [string]$VersionOnlyBasePackage,

    [string]$VersionOnlyBasePackageSha256,

    [switch]$AllowPrerelease,

    [switch]$ManualOnly,

    [switch]$ExternalToolPolicyDryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$InvocationRepoRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = $InvocationRepoRoot
$SourceSnapshotRoot = $null
$SourceSnapshotAttached = $false
$ReleaseManifestSchemaPath = Join-Path $RepoRoot 'docs/contracts/release-manifest-v1.schema.json'
$DistributionOwner = 'MSP/FW3'
$SourceIdentity = 'urn:msp-fw3:nvt-fw-combiner:source'
$ReleaseNamespace = 'urn:msp-fw3:nvt-fw-combiner:release'
$SourceTag = if ($Version.StartsWith('v', [StringComparison]::Ordinal)) { $Version } else { "v$Version" }
$SemanticVersion = $SourceTag.Substring(1)
$StableSemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+$'
$PackageSemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$'

if ($ManualOnly -and $AllowPrerelease) {
    throw 'ManualOnly cannot be combined with AllowPrerelease.'
}
if ($ManualOnly -and $ExternalToolPolicyDryRun) {
    throw 'ManualOnly cannot be combined with ExternalToolPolicyDryRun.'
}
if ($ManualOnly -and $SemanticVersion -cne '1.1.0') {
    throw 'ManualOnly is available only for v1.1.0.'
}
if (-not $ManualOnly -and $SemanticVersion -ceq '1.1.0') {
    throw 'v1.1.0 requires the ManualOnly package mode.'
}

function Assert-CanonicalJsonSchema {
    param(
        [Parameter(Mandatory = $true)]
        [string]$JsonPath,

        [Parameter(Mandatory = $true)]
        [string]$SchemaPath
    )

    if (-not (Test-Path -LiteralPath $SchemaPath -PathType Leaf)) {
        throw "Canonical JSON schema is missing: $SchemaPath"
    }
    $Json = Get-Content -LiteralPath $JsonPath -Raw
    if (-not ($Json | Test-Json -SchemaFile $SchemaPath -ErrorAction Stop)) {
        throw "JSON document does not satisfy canonical schema: $JsonPath"
    }
}
if ($AllowPrerelease) {
    if ($SemanticVersion -notmatch $PackageSemVerPattern) {
        throw "Package version must be SemVer without build metadata; received '$Version'."
    }
}
elseif ($SemanticVersion -notmatch $StableSemVerPattern) {
    throw "Stable release packaging requires vX.Y.Z; received '$Version'."
}
if ($Commit -notmatch '^[0-9a-f]{40}$') {
    throw "Commit must be a lowercase 40-character Git SHA; received '$Commit'."
}

$PolicyDryRunSentinel =
    $ExternalToolPolicyDryRun -and
    $Version -ceq '0.0.0' -and
    $Commit -ceq ('0' * 40)
$ResolvedVersionOnlyBasePackage = $null
if (-not [string]::IsNullOrWhiteSpace($VersionOnlyBasePackage)) {
    if ($SemanticVersion -cne '1.0.1') {
        throw 'A version-only base package may be used only for 1.0.1.'
    }
    $ResolvedVersionOnlyBasePackage = (
        Get-Item -LiteralPath $VersionOnlyBasePackage -ErrorAction Stop).FullName
    if ($VersionOnlyBasePackageSha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'A version-only base package requires its independently authenticated lowercase SHA-256.'
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($VersionOnlyBasePackageSha256)) {
    throw 'A version-only base package SHA-256 cannot be supplied without the package.'
}
if (-not $PolicyDryRunSentinel) {
    $InvocationVersionPath = Join-Path $RepoRoot 'VERSION'
    if (-not (Test-Path -LiteralPath $InvocationVersionPath -PathType Leaf)) {
        throw "Repository VERSION is missing: $InvocationVersionPath"
    }
    $InvocationVersion = (Get-Content -LiteralPath $InvocationVersionPath -Raw).Trim()
    if ($SemanticVersion -cne $InvocationVersion) {
        throw "Package version '$SemanticVersion' does not match repository VERSION '$InvocationVersion'."
    }
    $RepositoryHeadOutput = & git -C $RepoRoot rev-parse --verify HEAD 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Repository HEAD could not be resolved: $($RepositoryHeadOutput -join ' ')"
    }
    $RepositoryHead = ([string]($RepositoryHeadOutput -join '')).Trim()
    if ($RepositoryHead -notmatch '^[0-9a-f]{40}$') {
        throw "Repository HEAD is not a lowercase full Git SHA: '$RepositoryHead'."
    }
    if ($Commit -cne $RepositoryHead) {
        throw "Package commit does not match repository HEAD: requested '$Commit', actual '$RepositoryHead'."
    }

    $RepositoryStatus = & git -C $RepoRoot status --porcelain=v1 --untracked-files=all 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Repository status could not be read: $($RepositoryStatus -join ' ')"
    }
    if (@($RepositoryStatus).Count -ne 0) {
        throw "Release packaging requires a clean repository worktree and index: $($RepositoryStatus -join '; ')"
    }
}
if (
    -not $PolicyDryRunSentinel -and
    $SemanticVersion -ceq '1.0.1' -and
    $null -eq $ResolvedVersionOnlyBasePackage
) {
    throw 'Stable 1.0.1 packaging requires the published 1.0.0 base package.'
}

$DotNet = $null
$Python = $null
$ReleaseRoot = Join-Path $InvocationRepoRoot 'artifacts/release'
$WorkRoot = Join-Path $InvocationRepoRoot 'artifacts/package-work'
$PackageName = "NvtFwCombiner-$SourceTag-win-x64"
$PackageRoot = Join-Path $WorkRoot $PackageName
$AppPublish = Join-Path $WorkRoot 'app-publish'
$LauncherPublish = Join-Path $WorkRoot 'launcher-publish'
$WorkerBuild = Join-Path $WorkRoot 'worker-build'
$WorkerDist = Join-Path $WorkRoot 'worker-dist'
$IdleBuildWorkerStopper = Join-Path $PSScriptRoot 'stop-idle-build-workers.ps1'
$CanonicalGoldenReleaseAllowlistPath = Join-Path $RepoRoot 'testdata/golden/release-canonical-v1.json'
$ApprovedCanonicalGoldenReleaseAllowlistSha256 = '3fa22d263cee83a07ee30d2977dbf4afde2b5bcca685f9ded51f9d54c1f26ee7'

try {
if (-not $PolicyDryRunSentinel) {
    $SourceSnapshotParent = Split-Path -Parent $InvocationRepoRoot
    $SourceSnapshotId = [guid]::NewGuid().ToString('N').Substring(0, 12)
    $SourceSnapshotRoot = Join-Path $SourceSnapshotParent ".nfcps-$SourceSnapshotId"
    $SnapshotAddOutput = & git -C $InvocationRepoRoot worktree add --detach $SourceSnapshotRoot $Commit 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Exact source snapshot could not be materialized: $($SnapshotAddOutput -join ' ')"
    }
    $SourceSnapshotAttached = $true

    $SnapshotHeadOutput = & git -C $SourceSnapshotRoot rev-parse --verify HEAD 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Exact source snapshot HEAD could not be resolved: $($SnapshotHeadOutput -join ' ')"
    }
    $SnapshotHead = ([string]($SnapshotHeadOutput -join '')).Trim()
    if ($SnapshotHead -cne $Commit) {
        throw "Exact source snapshot resolved '$SnapshotHead' instead of requested commit '$Commit'."
    }
    $SnapshotStatus = & git -C $SourceSnapshotRoot status --porcelain=v1 --untracked-files=all 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Exact source snapshot status could not be read: $($SnapshotStatus -join ' ')"
    }
    if (@($SnapshotStatus).Count -ne 0) {
        throw 'Exact source snapshot is not clean.'
    }

    $SnapshotVersionPath = Join-Path $SourceSnapshotRoot 'VERSION'
    if (-not (Test-Path -LiteralPath $SnapshotVersionPath -PathType Leaf)) {
        throw "Exact source snapshot VERSION is missing: $SnapshotVersionPath"
    }
    $SnapshotVersion = (Get-Content -LiteralPath $SnapshotVersionPath -Raw).Trim()
    if ($SemanticVersion -cne $SnapshotVersion) {
        throw "Package version '$SemanticVersion' does not match exact source snapshot VERSION '$SnapshotVersion'."
    }

    $RepoRoot = $SourceSnapshotRoot
    $ReleaseManifestSchemaPath = Join-Path $RepoRoot 'docs/contracts/release-manifest-v1.schema.json'
    $IdleBuildWorkerStopper = Join-Path $RepoRoot 'scripts/stop-idle-build-workers.ps1'
    $CanonicalGoldenReleaseAllowlistPath = Join-Path $RepoRoot 'testdata/golden/release-canonical-v1.json'
}

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
    $Bytes = [Text.Encoding]::UTF8.GetBytes($Canonical)
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Get-TreeDigest {
    param([Parameter(Mandatory = $true)][string[]]$Paths)
    $Lines = foreach ($Path in ($Paths | Sort-Object)) {
        $Relative = [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
        "{0}:{1}" -f $Relative, (Get-LowerSha256 -Path $Path)
    }
    $Bytes = [Text.Encoding]::UTF8.GetBytes(($Lines -join "`n"))
    $Digest = [Security.Cryptography.SHA256]::HashData($Bytes)
    return [Convert]::ToHexString($Digest).ToLowerInvariant()
}

function Write-PackageHashList {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [Parameter(Mandatory = $true)][string[]]$RelativePaths,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $HashLines = foreach ($RelativePath in $RelativePaths) {
        $Path = Join-Path $PackageRoot $RelativePath
        "$(Get-LowerSha256 -Path $Path)  $RelativePath"
    }
    $HashLines | Set-Content -LiteralPath $DestinationPath -Encoding utf8NoBOM
}

function Save-SourcePackageLocks {
    $Snapshots = @{}
    Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'src') -Filter 'packages.lock.json' -File -Recurse |
        ForEach-Object { $Snapshots[$_.FullName] = [IO.File]::ReadAllBytes($_.FullName) }
    return $Snapshots
}

function Restore-SourcePackageLocks {
    param([Parameter(Mandatory = $true)][hashtable]$Snapshots)
    foreach ($Snapshot in $Snapshots.GetEnumerator()) {
        [IO.File]::WriteAllBytes($Snapshot.Key, [byte[]]$Snapshot.Value)
    }
}

$CrcWorkerPackagePath = 'external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe'
$ApprovedRepositoryExternalToolPackagePaths = @(
    'external-tools/README.md',
    'external-tools/legacy-combiner/README.md',
    'external-tools/legacy-combiner/1.13.0/Combiner.exe',
    'external-tools/legacy-combiner/1.13.0/manifest.json'
) | Sort-Object
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
$ApprovedRuntimeCatalogDirectories = @('ctrlram-postbuild-v2')
$PackageTrustIndexPackagePath = 'profiles/built-in/package-trust-index.json'
$ApprovedCanonicalCapabilityPolicyPackageContract = [pscustomobject]@{
    path = 'docs/contracts/canonical-capability-policy-v1.json'
    role = 'capabilityPolicy'
    sha256 = '6207923baf537c4031f2095942d363660c7a1c5cbd35e704ec14b28c509aef0f'
}

$ApprovedCanonicalCapabilityPolicyPackagePath =
    [string]$ApprovedCanonicalCapabilityPolicyPackageContract.path

function Copy-PackageFile {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$DestinationRoot
    )

    Copy-PackageFileFromRoot -SourceRoot $RepoRoot -RelativePath $RelativePath -DestinationRoot $DestinationRoot
}

function Copy-PackageFileFromRoot {
    param(
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$DestinationRoot
    )

    $NormalizedRelativePath = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $SourcePath = Join-Path $SourceRoot $NormalizedRelativePath
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Package file was not found at $SourcePath"
    }

    $DestinationPath = Join-Path $DestinationRoot $NormalizedRelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationPath) | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath
}

function Copy-ApprovedExternalToolPackageFiles {
    param(
        [Parameter(Mandatory = $true)][string]$DestinationRoot,
        [string]$SourceRoot = $RepoRoot
    )

    foreach ($ApprovedExternalToolPackagePath in $ApprovedRepositoryExternalToolPackagePaths) {
        Copy-PackageFileFromRoot `
            -SourceRoot $SourceRoot `
            -RelativePath $ApprovedExternalToolPackagePath `
            -DestinationRoot $DestinationRoot
    }
}

function Get-ExternalToolManifestEntries {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [Parameter(Mandatory = $true)][string]$ExternalToolsRoot
    )

    $ExternalToolFiles = @(Get-ChildItem -LiteralPath $ExternalToolsRoot -File -Recurse | ForEach-Object FullName)
    $PackagedExternalToolPaths = @(
        $ExternalToolFiles |
            ForEach-Object { [System.IO.Path]::GetRelativePath($PackageRoot, $_).Replace('\', '/') } |
            Sort-Object
    )
    if (Compare-Object -ReferenceObject $ApprovedExternalToolPackagePaths -DifferenceObject $PackagedExternalToolPaths) {
        throw 'Release package external-tool files differ from the approved allowlist.'
    }

    return @(
        $ExternalToolFiles | Sort-Object | ForEach-Object {
            $RelativePath = [System.IO.Path]::GetRelativePath($PackageRoot, $_).Replace('\', '/')
            [ordered]@{
                path = $RelativePath
                size = (Get-Item $_).Length
                sha256 = (Get-LowerSha256 $_)
                role = 'externalTool'
            }
        }
    )
}

function Get-BuiltInProfileBundleDirectories {
    $TrustIndexPath = Join-Path $RepoRoot $PackageTrustIndexPackagePath
    if (-not (Test-Path -LiteralPath $TrustIndexPath -PathType Leaf)) {
        throw 'Repository does not contain the built-in profile package trust index.'
    }
    $TrustIndex = Get-Content -LiteralPath $TrustIndexPath -Raw | ConvertFrom-Json -Depth 32
    $BundleDirectories = @(
        @($TrustIndex.bundles) |
            ForEach-Object { [string]$_.bundleDirectory } |
            Sort-Object
    )
    if ($BundleDirectories.Count -eq 0) {
        throw 'Package trust index does not declare any built-in profile bundles.'
    }

    $UniqueDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($BundleDirectory in $BundleDirectories) {
        if ([string]::IsNullOrWhiteSpace($BundleDirectory) -or
            $BundleDirectory.Contains('/') -or
            $BundleDirectory.Contains('\') -or
            $BundleDirectory -in @('.', '..')) {
            throw "Package trust index declares an unsafe built-in profile bundle directory '$BundleDirectory'."
        }
        if (-not $UniqueDirectories.Add($BundleDirectory)) {
            throw "Package trust index repeats built-in profile bundle directory '$BundleDirectory'."
        }
    }

    return $BundleDirectories
}

function Assert-SafeBuiltInProfileManifestPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $Segments = @($RelativePath.Split('/'))
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath.Contains('\') -or
        $RelativePath.Contains(':') -or
        $Segments.Count -eq 0 -or
        @($Segments | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -in @('.', '..') }).Count -ne 0) {
        throw "Built-in profile bundle manifest contains an unsafe path '$RelativePath'."
    }
}

function Get-BuiltInProfilePackagePaths {
    param([Parameter(Mandatory = $true)][string]$PublishedRoot)

    $BundleDirectories = @(Get-BuiltInProfileBundleDirectories)
    $BuiltInRoot = Join-Path $PublishedRoot 'profiles/built-in'
    if (-not (Test-Path -LiteralPath $BuiltInRoot -PathType Container)) {
        throw "Published application has no materialized built-in profile root at $BuiltInRoot"
    }

    $PublishedBundleDirectories = @(
        Get-ChildItem -LiteralPath $BuiltInRoot -Directory |
            ForEach-Object Name |
            Sort-Object
    )
    $ApprovedBuiltInDirectories = @($BundleDirectories + $ApprovedRuntimeCatalogDirectories | Sort-Object)
    if (Compare-Object -ReferenceObject $ApprovedBuiltInDirectories -DifferenceObject $PublishedBundleDirectories) {
        throw 'Published built-in profile directories differ from the bundle and runtime-catalog allowlists.'
    }

    $PackagePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $PublishedTrustIndexPath = Join-Path $PublishedRoot $PackageTrustIndexPackagePath
    $SourceTrustIndexPath = Join-Path $RepoRoot $PackageTrustIndexPackagePath
    if (-not (Test-Path -LiteralPath $PublishedTrustIndexPath -PathType Leaf) -or
        (Get-LowerSha256 -Path $PublishedTrustIndexPath) -ne
            (Get-LowerSha256 -Path $SourceTrustIndexPath)) {
        throw 'Published package trust index is missing or differs from reviewed source material.'
    }
    $PublishedTrustIndex = Get-Content -LiteralPath $PublishedTrustIndexPath -Raw |
        ConvertFrom-Json -Depth 32
    if ([string]$PublishedTrustIndex.schemaVersion -ne '1.1' -or
        [string]$PublishedTrustIndex.trustAnchorBindingId -ne 'built-in-profile-bundle-v2') {
        throw 'Published package trust index has an unsupported schema or trust anchor.'
    }
    [void]$PackagePaths.Add($PackageTrustIndexPackagePath)
    foreach ($BundleDirectory in $BundleDirectories) {
        $BundleRoot = Join-Path $BuiltInRoot $BundleDirectory
        $ManifestPath = Join-Path $BundleRoot 'profile-bundle.json'
        if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
            throw "Published built-in profile bundle manifest is missing: $BundleDirectory/profile-bundle.json"
        }

        $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
        $Entries = @($Manifest.entries)
        if ($Entries.Count -eq 0) {
            throw "Published built-in profile bundle '$BundleDirectory' has no manifest entries."
        }
        $TrustEntries = @($PublishedTrustIndex.bundles | Where-Object {
            [string]$_.bundleDirectory -eq $BundleDirectory
        })
        if ($TrustEntries.Count -ne 1) {
            throw "Published package trust index does not uniquely bind '$BundleDirectory'."
        }
        $TrustEntry = $TrustEntries[0]
        if ([string]$Manifest.schemaVersion -ne [string]$TrustEntry.bundleSchemaVersion -or
            [string]$Manifest.bundleVersion -ne [string]$TrustEntry.bundleVersion -or
            [string]$Manifest.contentHash -ne [string]$TrustEntry.contentHash -or
            [string]$Manifest.trustAnchorBindingId -ne [string]$PublishedTrustIndex.trustAnchorBindingId -or
            [string]$Manifest.hashAlgorithm -ne 'sha256-rfc8785-entry-array-v1' -or
            (Get-ProfileBundleEntryArrayHash -Entries $Entries) -ne [string]$Manifest.contentHash) {
            throw "Published built-in profile bundle '$BundleDirectory' differs from its trust-index identity."
        }

        $DeclaredBundlePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        [void]$DeclaredBundlePaths.Add('profile-bundle.json')
        [void]$PackagePaths.Add("profiles/built-in/$BundleDirectory/profile-bundle.json")
        foreach ($Entry in $Entries) {
            if ($null -eq $Entry -or $Entry.PSObject.Properties.Name -notcontains 'path') {
                throw "Published built-in profile bundle '$BundleDirectory' has an entry without a path."
            }

            $EntryPath = [string]$Entry.path
            Assert-SafeBuiltInProfileManifestPath -RelativePath $EntryPath
            if (-not $DeclaredBundlePaths.Add($EntryPath)) {
                throw "Published built-in profile bundle '$BundleDirectory' repeats path '$EntryPath'."
            }

            $PublishedPath = Join-Path $BundleRoot $EntryPath.Replace('/', [IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $PublishedPath -PathType Leaf)) {
                throw "Published built-in profile bundle file is missing: $BundleDirectory/$EntryPath"
            }
            if ([string]$Entry.contentHash -notmatch '^[0-9a-f]{64}$' -or
                (Get-LowerSha256 -Path $PublishedPath) -ne [string]$Entry.contentHash) {
                throw "Published built-in profile bundle file hash differs: $BundleDirectory/$EntryPath"
            }
            [void]$PackagePaths.Add("profiles/built-in/$BundleDirectory/$EntryPath")
        }

        $ActualBundlePaths = @(
            Get-ChildItem -LiteralPath $BundleRoot -File -Recurse |
                ForEach-Object { [IO.Path]::GetRelativePath($BundleRoot, $_.FullName).Replace('\', '/') } |
                Sort-Object
        )
        $ExpectedBundlePaths = @($DeclaredBundlePaths | Sort-Object)
        if (Compare-Object -ReferenceObject $ExpectedBundlePaths -DifferenceObject $ActualBundlePaths) {
            throw "Published built-in profile bundle '$BundleDirectory' differs from its manifest-pinned allowlist."
        }
    }

    $ActualRuntimeCatalogPaths = @(
        foreach ($RuntimeCatalogDirectory in $ApprovedRuntimeCatalogDirectories) {
            $RuntimeCatalogRoot = Join-Path $BuiltInRoot $RuntimeCatalogDirectory
            Get-ChildItem -LiteralPath $RuntimeCatalogRoot -File -Recurse |
                ForEach-Object { [IO.Path]::GetRelativePath($PublishedRoot, $_.FullName).Replace('\', '/') }
        }
    ) | Sort-Object
    if (Compare-Object -ReferenceObject $ApprovedRuntimeCatalogPackagePaths -DifferenceObject $ActualRuntimeCatalogPaths) {
        throw 'Published runtime catalog files differ from the approved allowlist.'
    }
    foreach ($RuntimeCatalogPath in $ApprovedRuntimeCatalogPackagePaths) {
        [void]$PackagePaths.Add($RuntimeCatalogPath)
    }

    return @($PackagePaths | Sort-Object)
}

function Copy-BuiltInProfilePackageFiles {
    param(
        [Parameter(Mandatory = $true)][string]$PublishedRoot,
        [Parameter(Mandatory = $true)][string]$DestinationRoot
    )

    $PackagePaths = @(Get-BuiltInProfilePackagePaths -PublishedRoot $PublishedRoot)
    foreach ($PackagePath in $PackagePaths) {
        Copy-PackageFileFromRoot `
            -SourceRoot $PublishedRoot `
            -RelativePath $PackagePath `
            -DestinationRoot $DestinationRoot
    }
    return $PackagePaths
}

function Copy-CanonicalCapabilityPolicyPackageFile {
    param(
        [Parameter(Mandatory = $true)][string]$PublishedRoot,
        [Parameter(Mandatory = $true)][string]$DestinationRoot
    )

    $Contract = $ApprovedCanonicalCapabilityPolicyPackageContract
    $PackagePath = [string]$Contract.path
    $PublishedPath = Join-Path $PublishedRoot $PackagePath.Replace(
        '/',
        [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $PublishedPath -PathType Leaf)) {
        throw "Published canonical capability policy is missing: $PackagePath"
    }
    if ((Get-LowerSha256 -Path $PublishedPath) -ne [string]$Contract.sha256) {
        throw "Published canonical capability policy does not match the approved SHA-256: $PackagePath"
    }
    Copy-PackageFileFromRoot `
        -SourceRoot $PublishedRoot `
        -RelativePath $PackagePath `
        -DestinationRoot $DestinationRoot
}

function Get-CanonicalCapabilityPolicyManifestEntry {
    param([Parameter(Mandatory = $true)][string]$PackageRoot)

    $Contract = $ApprovedCanonicalCapabilityPolicyPackageContract
    $RelativePath = [string]$Contract.path
    $Path = Join-Path $PackageRoot $RelativePath.Replace(
        '/',
        [IO.Path]::DirectorySeparatorChar)
    $ActualSha256 = Get-LowerSha256 -Path $Path
    if ($ActualSha256 -ne [string]$Contract.sha256) {
        throw "Packaged canonical capability policy does not match the approved SHA-256: $RelativePath"
    }
    return [ordered]@{
        path = $RelativePath
        size = (Get-Item -LiteralPath $Path).Length
        sha256 = [string]$Contract.sha256
        role = [string]$Contract.role
    }
}

function Get-BuiltInProfileManifestEntries {
    param(
        [Parameter(Mandatory = $true)][string]$PackageRoot,
        [Parameter(Mandatory = $true)][string[]]$PackagePaths
    )

    return @(
        $PackagePaths | Sort-Object | ForEach-Object {
            $Path = Join-Path $PackageRoot $_.Replace('/', [IO.Path]::DirectorySeparatorChar)
            [ordered]@{
                path = $_
                size = (Get-Item -LiteralPath $Path).Length
                sha256 = (Get-LowerSha256 -Path $Path)
                role = 'builtInProfile'
            }
        }
    )
}

function New-BuiltInProfilePolicyDryRunFixture {
    param([Parameter(Mandatory = $true)][string]$PublishedRoot)

    $TrustIndex = Get-Content -LiteralPath (Join-Path $RepoRoot $PackageTrustIndexPackagePath) -Raw |
        ConvertFrom-Json -Depth 32
    Copy-PackageFileFromRoot `
        -SourceRoot $RepoRoot `
        -RelativePath $PackageTrustIndexPackagePath `
        -DestinationRoot $PublishedRoot

    foreach ($BundleDirectory in (Get-BuiltInProfileBundleDirectories)) {
        $TrustEntry = @($TrustIndex.bundles | Where-Object {
            [string]$_.bundleDirectory -eq $BundleDirectory
        })[0]
        $SourceManifestPath = Join-Path $RepoRoot "profiles/built-in/$BundleDirectory/profile-bundle.json"
        $FixtureBundleRoot = Join-Path $PublishedRoot "profiles/built-in/$BundleDirectory"
        New-Item -ItemType Directory -Force -Path $FixtureBundleRoot | Out-Null
        Copy-Item -LiteralPath $SourceManifestPath -Destination (Join-Path $FixtureBundleRoot 'profile-bundle.json')

        $Manifest = Get-Content -LiteralPath $SourceManifestPath -Raw | ConvertFrom-Json
        foreach ($Entry in @($Manifest.entries)) {
            $EntryPath = [string]$Entry.path
            Assert-SafeBuiltInProfileManifestPath -RelativePath $EntryPath
            $FixturePath = Join-Path $FixtureBundleRoot $EntryPath.Replace('/', [IO.Path]::DirectorySeparatorChar)
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $FixturePath) | Out-Null
            $SourcePath = Join-Path $RepoRoot "profiles/built-in/$BundleDirectory/$EntryPath"
            if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
                if ($EntryPath -eq 'schemas/composition-profile-v2.schema.json') {
                    $SourcePath = Join-Path $RepoRoot "docs/contracts/$($TrustEntry.materialization.compositionProfileSchemaFile)"
                }
                elseif ($EntryPath -eq 'schemas/firmware-family-v1.schema.json') {
                    $SourcePath = Join-Path $RepoRoot "docs/contracts/$($TrustEntry.materialization.firmwareFamilySchemaFile)"
                }
                elseif ($null -ne $TrustEntry.materialization.canonicalFirmwareFamily -and
                    $EntryPath -eq [string]$TrustEntry.materialization.canonicalFirmwareFamily.destination) {
                    $SourcePath = Join-Path $RepoRoot "profiles/built-in/$($TrustEntry.materialization.canonicalFirmwareFamily.source)"
                }
            }
            if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
                throw "Dry-run fixture cannot materialize '$BundleDirectory/$EntryPath'."
            }
            Copy-Item -LiteralPath $SourcePath -Destination $FixturePath
        }
    }

    foreach ($RuntimeCatalogPath in $ApprovedRuntimeCatalogPackagePaths) {
        Copy-PackageFileFromRoot `
            -SourceRoot $RepoRoot `
            -RelativePath $RuntimeCatalogPath `
            -DestinationRoot $PublishedRoot
    }
}

function Invoke-ExternalToolPolicyDryRun {
    $ProbeRelativePath = "external-tools/release-package-policy-probe-$([guid]::NewGuid().ToString('N')).txt"

    $DryRunRoot = Join-Path ([IO.Path]::GetTempPath()) "nvt-fw-combiner-package-policy-$([guid]::NewGuid().ToString('N'))"
    $DryRunSourceRoot = Join-Path $DryRunRoot 'source'
    $DryRunPackageRoot = Join-Path $DryRunRoot 'package'
    $DryRunPublishedRoot = Join-Path $DryRunRoot 'published'
    try {
        New-Item -ItemType Directory -Force -Path $DryRunSourceRoot, $DryRunPackageRoot, $DryRunPublishedRoot | Out-Null

        Copy-ApprovedExternalToolPackageFiles -DestinationRoot $DryRunSourceRoot
        $ProbeSourcePath = Join-Path $DryRunSourceRoot $ProbeRelativePath
        'negative release-policy probe' | Set-Content -LiteralPath $ProbeSourcePath -Encoding ascii

        Copy-ApprovedExternalToolPackageFiles `
            -SourceRoot $DryRunSourceRoot `
            -DestinationRoot $DryRunPackageRoot
        $DryRunWorkerPath = Join-Path $DryRunPackageRoot $CrcWorkerPackagePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DryRunWorkerPath) | Out-Null
        'generated CRC worker policy fixture' | Set-Content -LiteralPath $DryRunWorkerPath -Encoding ascii
        $DryRunExternalToolsRoot = Join-Path $DryRunPackageRoot 'external-tools'
        $DryRunEntries = @(Get-ExternalToolManifestEntries `
            -PackageRoot $DryRunPackageRoot `
            -ExternalToolsRoot $DryRunExternalToolsRoot)
        New-BuiltInProfilePolicyDryRunFixture -PublishedRoot $DryRunPublishedRoot
        Copy-PackageFileFromRoot `
            -SourceRoot $RepoRoot `
            -RelativePath $ApprovedCanonicalCapabilityPolicyPackagePath `
            -DestinationRoot $DryRunPublishedRoot
        $DryRunProfilePaths = @(Copy-BuiltInProfilePackageFiles `
            -PublishedRoot $DryRunPublishedRoot `
            -DestinationRoot $DryRunPackageRoot)
        $DryRunProfileEntries = @(Get-BuiltInProfileManifestEntries `
            -PackageRoot $DryRunPackageRoot `
            -PackagePaths $DryRunProfilePaths)
        if ($DryRunProfileEntries.Count -eq 0 -or
            @($DryRunProfileEntries | Where-Object { $_.role -ne 'builtInProfile' }).Count -ne 0) {
            throw 'Built-in profile policy dry-run did not produce role-pinned manifest entries.'
        }
        $DryRunRuntimeCatalogPaths = @(
            $DryRunProfileEntries |
                Where-Object { $_.path -in $ApprovedRuntimeCatalogPackagePaths } |
                ForEach-Object path |
                Sort-Object
        )
        if (Compare-Object -ReferenceObject $ApprovedRuntimeCatalogPackagePaths -DifferenceObject $DryRunRuntimeCatalogPaths) {
            throw 'Runtime catalog policy dry-run did not produce the approved manifest entries.'
        }
        Copy-CanonicalCapabilityPolicyPackageFile `
            -PublishedRoot $DryRunPublishedRoot `
            -DestinationRoot $DryRunPackageRoot
        $DryRunCapabilityPolicyEntry = Get-CanonicalCapabilityPolicyManifestEntry `
            -PackageRoot $DryRunPackageRoot

        $DryRunManifestPath = Join-Path $DryRunPackageRoot 'RELEASE-MANIFEST.json'
        [ordered]@{
            files =
                @($DryRunEntries) +
                @($DryRunProfileEntries) +
                @($DryRunCapabilityPolicyEntry)
        } |
            ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath $DryRunManifestPath -Encoding utf8NoBOM

        $StagedProbePath = Join-Path $DryRunPackageRoot $ProbeRelativePath
        if (Test-Path -LiteralPath $StagedProbePath) {
            throw 'External-tool policy probe entered package staging.'
        }

        $PersistedManifest = Get-Content -LiteralPath $DryRunManifestPath -Raw | ConvertFrom-Json
        $ManifestProbeEntries = @($PersistedManifest.files | Where-Object { $_.path -eq $ProbeRelativePath })
        if ($ManifestProbeEntries.Count -ne 0) {
            throw 'External-tool policy probe entered the release manifest.'
        }
        if (@($PersistedManifest.files |
                Where-Object { $_.role -eq 'publicationPolicy' }).Count -ne 0) {
            throw 'Persisted release manifest contains the retired support publication policy.'
        }
        $PersistedCapabilityPolicyEntries = @(
            $PersistedManifest.files | Where-Object {
                $_.path -eq $ApprovedCanonicalCapabilityPolicyPackageContract.path -or
                $_.role -eq 'capabilityPolicy'
            }
        )
        if ($PersistedCapabilityPolicyEntries.Count -ne 1 -or
            $PersistedCapabilityPolicyEntries[0].path -ne
                $ApprovedCanonicalCapabilityPolicyPackageContract.path -or
            $PersistedCapabilityPolicyEntries[0].role -ne
                $ApprovedCanonicalCapabilityPolicyPackageContract.role -or
            $PersistedCapabilityPolicyEntries[0].sha256 -ne
                $ApprovedCanonicalCapabilityPolicyPackageContract.sha256) {
            throw 'Persisted release manifest does not pin the approved canonical capability policy.'
        }

        $FirstBundleDirectory = @(Get-BuiltInProfileBundleDirectories)[0]
        $UnexpectedProfilePath = Join-Path $DryRunPublishedRoot "profiles/built-in/$FirstBundleDirectory/unexpected.json"
        '{}' | Set-Content -LiteralPath $UnexpectedProfilePath -Encoding ascii
        $UnexpectedProfileRejected = $false
        try {
            Get-BuiltInProfilePackagePaths -PublishedRoot $DryRunPublishedRoot | Out-Null
        }
        catch {
            if ($_.Exception.Message -notlike '*differs from its manifest-pinned allowlist*') {
                throw
            }
            $UnexpectedProfileRejected = $true
        }
        if (-not $UnexpectedProfileRejected) {
            throw 'Unexpected built-in profile file was not rejected by the package allowlist.'
        }
        Remove-Item -LiteralPath $UnexpectedProfilePath -Force

        $FirstManifestPath = Join-Path $DryRunPublishedRoot "profiles/built-in/$FirstBundleDirectory/profile-bundle.json"
        $FirstManifest = Get-Content -LiteralPath $FirstManifestPath -Raw | ConvertFrom-Json
        $FirstEntryPath = Join-Path `
            (Split-Path -Parent $FirstManifestPath) `
            ([string]$FirstManifest.entries[0].path).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $FirstEntryBytes = [IO.File]::ReadAllBytes($FirstEntryPath)
        $FirstEntryBytes[0] = $FirstEntryBytes[0] -bxor 1
        [IO.File]::WriteAllBytes($FirstEntryPath, $FirstEntryBytes)
        $ProfileHashDriftRejected = $false
        try {
            Get-BuiltInProfilePackagePaths -PublishedRoot $DryRunPublishedRoot | Out-Null
        }
        catch {
            if ($_.Exception.Message -notlike '*bundle file hash differs*') {
                throw
            }
            $ProfileHashDriftRejected = $true
        }
        if (-not $ProfileHashDriftRejected) {
            throw 'Built-in profile entry hash drift was not rejected.'
        }
        New-BuiltInProfilePolicyDryRunFixture -PublishedRoot $DryRunPublishedRoot

        $UnexpectedRuntimeCatalogPath = Join-Path $DryRunPublishedRoot 'profiles/built-in/ctrlram-postbuild-v2/unexpected.json'
        '{}' | Set-Content -LiteralPath $UnexpectedRuntimeCatalogPath -Encoding ascii
        $UnexpectedRuntimeCatalogRejected = $false
        try {
            Get-BuiltInProfilePackagePaths -PublishedRoot $DryRunPublishedRoot | Out-Null
        }
        catch {
            if ($_.Exception.Message -notlike '*runtime catalog files differ from the approved allowlist*') {
                throw
            }
            $UnexpectedRuntimeCatalogRejected = $true
        }
        if (-not $UnexpectedRuntimeCatalogRejected) {
            throw 'Unexpected runtime catalog file was not rejected by the package allowlist.'
        }

        $GoldenPaths = @(Get-DeclaredCanonicalGoldenPaths)
        $GoldenBinPaths = @($GoldenPaths | Where-Object { $_.EndsWith('.bin', [StringComparison]::OrdinalIgnoreCase) })
        if ($GoldenBinPaths.Count -ne 148 -or $script:CanonicalGoldenPackageManifest.cases.Count -ne 35) {
            throw 'Canonical package selection did not retain 148 unique BIN paths and 35 direct/input/alias cases.'
        }
        if (@($GoldenPaths | Where-Object {
            $_ -like 'testdata/diagnostics/*' -or
            $_ -like 'testdata/golden/owner-handoff/*' -or
            $_ -match '/(?:NT51920|NT51925|NT51930|NT51931)/'
        }).Count -ne 0) {
            throw 'Canonical package selection included diagnostics, owner-handoff, or retired-IC content.'
        }

        $SourceGoldenAllowlist = Get-Content -LiteralPath $CanonicalGoldenReleaseAllowlistPath -Raw |
            ConvertFrom-Json
        $DirectGoldenPolicyProbes = @(
            [pscustomobject]@{
                Name = 'boolean-flip'
                Value = -not [bool]$SourceGoldenAllowlist.cases[0].directGolden
                ExpectedMessage = '*directGolden differs from the explicit release allowlist*'
            },
            [pscustomobject]@{
                Name = 'numeric'
                Value = 1
                ExpectedMessage = '*directGolden must be a JSON boolean*'
            },
            [pscustomobject]@{
                Name = 'string'
                Value = 'false'
                ExpectedMessage = '*directGolden must be a JSON boolean*'
            }
        )
        foreach ($PolicyProbe in $DirectGoldenPolicyProbes) {
            $InvalidGoldenAllowlistPath = Join-Path $DryRunRoot "invalid-canonical-$($PolicyProbe.Name).json"
            $InvalidGoldenAllowlist = Get-Content -LiteralPath $CanonicalGoldenReleaseAllowlistPath -Raw |
                ConvertFrom-Json
            $InvalidGoldenAllowlist.cases[0].directGolden = $PolicyProbe.Value
            $InvalidGoldenAllowlist |
                ConvertTo-Json -Depth 8 |
                Set-Content -LiteralPath $InvalidGoldenAllowlistPath -Encoding utf8NoBOM
            $InvalidGoldenAllowlistRejected = $false
            try {
                Get-DeclaredCanonicalGoldenPaths `
                    -ReleaseAllowlistPath $InvalidGoldenAllowlistPath `
                    -ExpectedAllowlistSha256 (Get-LowerSha256 -Path $InvalidGoldenAllowlistPath) |
                    Out-Null
            }
            catch {
                if ($_.Exception.Message -notlike $PolicyProbe.ExpectedMessage) {
                    throw
                }
                $InvalidGoldenAllowlistRejected = $true
            }
            if (-not $InvalidGoldenAllowlistRejected) {
                throw "Canonical package selection accepted the $($PolicyProbe.Name) directGolden policy probe."
            }
        }

        $GoldenSelectorPolicyProbes = @(
            [pscustomobject]@{
                Name = 'direct-evidence-mismatch'
                ExpectedMessage = '*directEvidence differs from the explicit release allowlist*'
                Mutate = { param($Allowlist) $Allowlist.cases[0].directEvidence = $true }
            },
            [pscustomobject]@{
                Name = 'dependent-alias-without-source'
                ExpectedMessage = '*does not select its exact same-workflow direct Golden source*'
                Mutate = {
                    param($Allowlist)
                    $Alias = @($Allowlist.cases | Where-Object {
                        $_.directGolden -eq $false -and $_.directEvidence -eq $false
                    })[0]
                    $SourceCaseId = [string]$Alias.alias.sourceCaseId
                    $Allowlist.cases = @($Allowlist.cases | Where-Object { $_.caseId -cne $SourceCaseId })
                }
            },
            [pscustomobject]@{
                Name = 'alias-source-direct-input-evidence'
                ExpectedMessage = '*differs from the explicit release allowlist*'
                Mutate = {
                    param($Allowlist)
                    $Alias = @($Allowlist.cases | Where-Object {
                        $_.directGolden -eq $false -and $_.directEvidence -eq $false
                    })[0]
                    $InputEvidence = @($Allowlist.cases | Where-Object { $_.directEvidence -eq $true })[0]
                    $Alias.alias.sourceCaseId = $InputEvidence.caseId
                }
            },
            [pscustomobject]@{
                Name = 'direct-input-evidence-role-drift'
                ExpectedMessage = '*differs from the explicit release allowlist*'
                Mutate = {
                    param($Allowlist)
                    $InputEvidence = @($Allowlist.cases | Where-Object { $_.directEvidence -eq $true })[0]
                    $InputEvidence.artifacts[0].role = 'expected'
                }
            },
            [pscustomobject]@{
                Name = 'artifact-hash-drift'
                ExpectedMessage = '*differs from the explicit release allowlist*'
                Mutate = { param($Allowlist) $Allowlist.cases[0].artifacts[0].sha256 = '0' * 64 }
            },
            [pscustomobject]@{
                Name = 'artifact-id-omission'
                ExpectedMessage = '*artifacts differ from the explicit release allowlist*'
                Mutate = {
                    param($Allowlist)
                    $Allowlist.cases[0].artifacts = @($Allowlist.cases[0].artifacts | Select-Object -SkipLast 1)
                }
            },
            [pscustomobject]@{
                Name = 'artifact-id-extra'
                ExpectedMessage = '*artifacts differ from the explicit release allowlist*'
                Mutate = {
                    param($Allowlist)
                    $Extra = $Allowlist.cases[0].artifacts[0] | ConvertTo-Json -Depth 8 | ConvertFrom-Json
                    $Extra.artifactId = 'unapproved-extra-artifact'
                    $Allowlist.cases[0].artifacts = @($Allowlist.cases[0].artifacts) + @($Extra)
                }
            },
            [pscustomobject]@{
                Name = 'same-path-conflicting-declaration'
                ExpectedMessage = '*differs from the explicit release allowlist*'
                Mutate = {
                    param($Allowlist)
                    $Declarations = @($Allowlist.cases | ForEach-Object { @($_.artifacts) })
                    $DuplicatePath = @(
                        $Declarations |
                            Group-Object path |
                            Where-Object { $_.Count -gt 1 }
                    )[0]
                    $DuplicatePath.Group[1].sha256 = '0' * 64
                }
            }
        )
        foreach ($PolicyProbe in $GoldenSelectorPolicyProbes) {
            $InvalidGoldenAllowlistPath = Join-Path $DryRunRoot "invalid-canonical-$($PolicyProbe.Name).json"
            $InvalidGoldenAllowlist = Get-Content -LiteralPath $CanonicalGoldenReleaseAllowlistPath -Raw |
                ConvertFrom-Json -Depth 100
            & $PolicyProbe.Mutate $InvalidGoldenAllowlist
            $InvalidGoldenAllowlist |
                ConvertTo-Json -Depth 100 |
                Set-Content -LiteralPath $InvalidGoldenAllowlistPath -Encoding utf8NoBOM
            $InvalidGoldenAllowlistRejected = $false
            try {
                Get-DeclaredCanonicalGoldenPaths `
                    -ReleaseAllowlistPath $InvalidGoldenAllowlistPath `
                    -ExpectedAllowlistSha256 (Get-LowerSha256 -Path $InvalidGoldenAllowlistPath) |
                    Out-Null
            }
            catch {
                if ($_.Exception.Message -notlike $PolicyProbe.ExpectedMessage) {
                    throw
                }
                $InvalidGoldenAllowlistRejected = $true
            }
            if (-not $InvalidGoldenAllowlistRejected) {
                throw "Canonical package selection accepted the $($PolicyProbe.Name) selector policy probe."
            }
        }

        $RetiredGoldenAllowlistPath = Join-Path $DryRunRoot 'invalid-canonical-retired-ic.json'
        $RetiredGoldenAllowlist = Get-Content -LiteralPath $CanonicalGoldenReleaseAllowlistPath -Raw |
            ConvertFrom-Json
        $RetiredGoldenAllowlist.cases[0].caseId = 'nt51920-retired-publication-probe'
        $RetiredGoldenAllowlist |
            ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath $RetiredGoldenAllowlistPath -Encoding utf8NoBOM
        $RetiredGoldenAllowlistRejected = $false
        try {
            Get-DeclaredCanonicalGoldenPaths `
                -ReleaseAllowlistPath $RetiredGoldenAllowlistPath `
                -ExpectedAllowlistSha256 (Get-LowerSha256 -Path $RetiredGoldenAllowlistPath) |
                Out-Null
        }
        catch {
            if ($_.Exception.Message -notlike '*cannot publish retired IC NT51920*') {
                throw
            }
            $RetiredGoldenAllowlistRejected = $true
        }
        if (-not $RetiredGoldenAllowlistRejected) {
            throw 'Canonical package selection accepted a retired IC publication probe.'
        }

        $UnicodeRelativePath = 'reference/多語/請先看.md'
        $UnicodeFixturePath = Join-Path $DryRunPackageRoot $UnicodeRelativePath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $UnicodeFixturePath) | Out-Null
        '多語 release hash-list fixture' | Set-Content -LiteralPath $UnicodeFixturePath -Encoding utf8NoBOM
        $DryRunHashListPath = Join-Path $DryRunPackageRoot 'SHA256SUMS.txt'
        Write-PackageHashList `
            -PackageRoot $DryRunPackageRoot `
            -RelativePaths @($UnicodeRelativePath) `
            -DestinationPath $DryRunHashListPath
        $ExpectedHashLine = "$(Get-LowerSha256 -Path $UnicodeFixturePath)  $UnicodeRelativePath"
        $PersistedHashBytes = [IO.File]::ReadAllBytes($DryRunHashListPath)
        if ($PersistedHashBytes.Length -ge 3 -and
            $PersistedHashBytes[0] -eq 0xef -and
            $PersistedHashBytes[1] -eq 0xbb -and
            $PersistedHashBytes[2] -eq 0xbf) {
            throw 'Release hash list must be UTF-8 without a byte-order mark.'
        }
        $StrictUtf8 = [Text.UTF8Encoding]::new($false, $true)
        $PersistedHashText = $StrictUtf8.GetString($PersistedHashBytes)
        if ($PersistedHashText -cne "$ExpectedHashLine$([Environment]::NewLine)") {
            throw 'Unicode release hash-list path did not round-trip through UTF-8.'
        }

        Write-Host 'External-tool package policy dry-run passed: probe excluded from staging and manifest.'
        Write-Host 'Built-in profile package policy dry-run passed: manifest-pinned materialized files included, entry hashes closed, and unexpected file rejected.'
        Write-Host 'Runtime catalog package policy dry-run passed: approved files included and unexpected file rejected.'
        Write-Host 'Retired support publication policy package dry-run passed: no parallel publicationPolicy payload entered staging or manifest.'
        Write-Host 'Canonical golden package policy dry-run passed: 25 direct Goldens, one owner-certified input-only evidence case, nine self-contained aliases, 161 declarations, and 158 unique artifact paths selected.'
        Write-Host 'Canonical golden package policy identity, direct/input/alias drift, retired-IC, and strict-type rejection passed.'
        Write-Host 'Release hash-list policy dry-run passed: Unicode paths round-trip through UTF-8.'
    }
    finally {
        if (Test-Path -LiteralPath $DryRunRoot) {
            Remove-Item -LiteralPath $DryRunRoot -Recurse -Force
        }
    }
}

function Copy-PackageReferenceTree {
    param(
        [Parameter(Mandatory = $true)][string]$RelativeRoot,
        [Parameter(Mandatory = $true)][string[]]$AllowedExtensions
    )

    $NormalizedRelativeRoot = $RelativeRoot.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $SourceRoot = Join-Path $RepoRoot $NormalizedRelativeRoot
    if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) {
        throw "Reference directory was not found at $SourceRoot"
    }

    $Allowed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($Extension in $AllowedExtensions) {
        [void]$Allowed.Add($Extension)
    }

    Get-ChildItem -LiteralPath $SourceRoot -File -Recurse |
        Where-Object { $Allowed.Contains($_.Extension) -and $_.Length -gt 0 } |
        Sort-Object FullName |
        ForEach-Object {
            $RelativePath = [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/')
            Copy-PackageFile -RelativePath $RelativePath -DestinationRoot $ReferenceDestination
        }
}

function Assert-SafeCanonicalGoldenPath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.Contains('\') -or
        ($RelativePath.Split('/') -contains '..') -or
        $RelativePath.Split('/')[0].Contains(':') -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Unsafe canonical golden manifest path: '$RelativePath'"
    }
}

function Add-GoldenManifestEntryPath {
    param(
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$Paths,
        [Parameter(Mandatory = $true)][string]$GoldenRootRelative,
        [Parameter(Mandatory = $true)]$Entry
    )

    if ($null -eq $Entry -or $Entry.PSObject.Properties.Name -notcontains 'path') {
        throw "Canonical golden manifest has an entry without a path."
    }

    $ManifestRelativePath = [string]$Entry.path
    Assert-SafeCanonicalGoldenPath -RelativePath $ManifestRelativePath

    [void]$Paths.Add("$GoldenRootRelative/$ManifestRelativePath")
}

function Get-DeclaredCanonicalGoldenPaths {
    param(
        [string]$ReleaseAllowlistPath = $CanonicalGoldenReleaseAllowlistPath,
        [string]$ExpectedAllowlistSha256 = $ApprovedCanonicalGoldenReleaseAllowlistSha256
    )

    $GoldenRootRelative = 'testdata/golden/canonical'
    $GoldenRoot = Join-Path $RepoRoot $GoldenRootRelative
    $ManifestPath = Join-Path $GoldenRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Canonical golden manifest was not found at $ManifestPath"
    }
    if (-not (Test-Path -LiteralPath $ReleaseAllowlistPath -PathType Leaf)) {
        throw "Canonical golden release allowlist was not found at $ReleaseAllowlistPath"
    }
    if ((Get-LowerSha256 -Path $ReleaseAllowlistPath) -cne $ExpectedAllowlistSha256) {
        throw 'Canonical golden release allowlist identity differs from the exact approved authority.'
    }

    $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -Depth 100
    if ($Manifest.schemaVersion -ne '1.1' -or
        $Manifest.payloadClass -ne 'owner-approved-golden' -or
        $Manifest.binaryPayloadsIncluded -ne $true) {
        throw 'Canonical golden inventory must declare schemaVersion=1.1, owner-approved-golden, and binaryPayloadsIncluded=true.'
    }
    $ReleaseAllowlist = Get-Content -LiteralPath $ReleaseAllowlistPath -Raw | ConvertFrom-Json -Depth 100
    if ($ReleaseAllowlist.schemaVersion -ne '1.1' -or
        $ReleaseAllowlist.policyId -ne 'canonical-reference-v1' -or
        $ReleaseAllowlist.authorizedForVersion -ne '1.1.2' -or
        $ReleaseAllowlist.releaseStatus -ne 'human-gated-allowlist' -or
        $ReleaseAllowlist.redistributionAuthorization.authorizedOn -ne '2026-09-04' -or
        $ReleaseAllowlist.redistributionAuthorization.authorizedBy -ne 'repository owner' -or
        $ReleaseAllowlist.redistributionAuthorization.scope -ne 'reference-payload-only' -or
        $ReleaseAllowlist.redistributionAuthorization.supersedesHistoricalCaseRestrictions -ne $true -or
        $ReleaseAllowlist.authorityLimits.runtimeSupportPromotion -ne $false -or
        $ReleaseAllowlist.authorityLimits.fullByteParityClaim -ne $false) {
        throw 'Canonical golden release allowlist has invalid identity, authorization, or authority limits.'
    }
    if ([int]$ReleaseAllowlist.selectionSummary.caseCount -ne 35 -or
        [int]$ReleaseAllowlist.selectionSummary.directGoldenCount -ne 25 -or
        [int]$ReleaseAllowlist.selectionSummary.directInputEvidenceCount -ne 1 -or
        [int]$ReleaseAllowlist.selectionSummary.factScopedAliasCount -ne 9 -or
        [int]$ReleaseAllowlist.selectionSummary.artifactDeclarationCount -ne 161 -or
        [int]$ReleaseAllowlist.selectionSummary.uniqueArtifactPathCount -ne 158) {
        throw 'Canonical golden release allowlist selection summary differs from the exact approved scope.'
    }
    $CanonicalReadmePath = Join-Path $GoldenRoot 'README.md'
    if ([string]$ReleaseAllowlist.canonicalReadmeSha256 -cnotmatch '^[0-9a-f]{64}$' -or
        -not (Test-Path -LiteralPath $CanonicalReadmePath -PathType Leaf) -or
        (Get-LowerSha256 -Path $CanonicalReadmePath) -cne [string]$ReleaseAllowlist.canonicalReadmeSha256) {
        throw 'Canonical golden README exact bytes differ from the explicit release allowlist.'
    }

    $ApprovedCases = @{}
    $RetiredIcTokens = @('51920', '51925', '51930', '51931')
    foreach ($ApprovedCase in $ReleaseAllowlist.cases) {
        $ApprovedCaseId = [string]$ApprovedCase.caseId
        if ([string]::IsNullOrWhiteSpace($ApprovedCaseId) -or $ApprovedCases.ContainsKey($ApprovedCaseId)) {
            throw "Canonical golden release allowlist contains an invalid or duplicate case id: '$ApprovedCaseId'"
        }
        $PublicationFields = @($ApprovedCaseId, [string]$ApprovedCase.manifestPath) +
            @($ApprovedCase.artifacts | ForEach-Object { [string]$_.path })
        foreach ($RetiredIcToken in $RetiredIcTokens) {
            if (@($PublicationFields | Where-Object {
                $_.IndexOf($RetiredIcToken, [StringComparison]::OrdinalIgnoreCase) -ge 0
            }).Count -ne 0) {
                throw "Canonical golden release allowlist cannot publish retired IC NT$RetiredIcToken."
            }
        }
        $ApprovedCases[$ApprovedCaseId] = $ApprovedCase
    }

    $Paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$Paths.Add("$GoldenRootRelative/README.md")
    $SelectedCases = [System.Collections.Generic.List[object]]::new()
    $SelectedCaseIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $SelectedCaseFacts = @{}
    $ArtifactDeclarationCount = 0
    $DirectInputEvidenceCount = 0

    foreach ($CaseEntry in $Manifest.cases) {
        $CaseId = [string]$CaseEntry.caseId
        if (-not $ApprovedCases.ContainsKey($CaseId)) {
            continue
        }
        $ApprovedCase = $ApprovedCases[$CaseId]
        $ManifestEntry = [pscustomobject]@{ path = [string]$CaseEntry.manifestPath }
        if ($ManifestEntry.path -cne [string]$ApprovedCase.manifestPath) {
            throw "Release-approved canonical case '$CaseId' manifest path differs from the explicit release allowlist."
        }
        Assert-SafeCanonicalGoldenPath -RelativePath $ManifestEntry.path
        $CaseManifestPath = Join-Path $GoldenRoot $ManifestEntry.path
        if (-not (Test-Path -LiteralPath $CaseManifestPath -PathType Leaf)) {
            throw "Canonical golden case manifest was not found: $($ManifestEntry.path)"
        }
        if ([string]$ApprovedCase.manifestSha256 -cnotmatch '^[0-9a-f]{64}$' -or
            (Get-LowerSha256 -Path $CaseManifestPath) -cne [string]$ApprovedCase.manifestSha256) {
            throw "Canonical golden case manifest exact bytes drifted: $($ManifestEntry.path)"
        }
        $Case = Get-Content -LiteralPath $CaseManifestPath -Raw | ConvertFrom-Json -Depth 100
        if ($Case.caseId -cne $CaseId -or
            [string]$ApprovedCase.workflow -cne [string]$Case.workflow -or
            [string]$ApprovedCase.testDispositionKind -cne [string]$Case.testDisposition.kind) {
            throw "Release-approved canonical case '$CaseId' identity, workflow, or disposition differs from the allowlist."
        }
        $CanonicalDirectEvidence = if ($Case.PSObject.Properties.Name -contains 'directEvidence') {
            $Case.directEvidence
        }
        else {
            $false
        }
        if ($ApprovedCase.directEvidence -isnot [bool] -or $CanonicalDirectEvidence -isnot [bool] -or
            $ApprovedCase.directEvidence -ne $CanonicalDirectEvidence) {
            throw "Release-approved canonical case '$CaseId' directEvidence differs from the explicit release allowlist."
        }
        if ($ApprovedCase.directGolden -isnot [bool] -or $Case.directGolden -isnot [bool]) {
            throw "Release-approved canonical case '$CaseId' directGolden must be a JSON boolean."
        }
        if ($ApprovedCase.directGolden -ne $Case.directGolden) {
            throw "Release-approved canonical case '$CaseId' directGolden differs from the explicit release allowlist."
        }

        Add-GoldenManifestEntryPath -Paths $Paths -GoldenRootRelative $GoldenRootRelative -Entry $ManifestEntry
        $SelectedCases.Add($CaseEntry)
        [void]$SelectedCaseIds.Add($CaseId)
        $SelectedCaseFacts[$CaseId] = $Case
        $ApprovedArtifactIds = @($ApprovedCase.artifacts | ForEach-Object { [string]$_.artifactId } | Sort-Object)
        if ($Case.directGolden -eq $true -or $CanonicalDirectEvidence -eq $true) {
            $Roles = @($Case.artifacts | ForEach-Object { [string]$_.role })
            if ($Case.directGolden -eq $true -and ($Roles -notcontains 'input' -or $Roles -notcontains 'expected')) {
                throw "Direct canonical case '$CaseId' must declare input and expected artifacts."
            }
            if ($CanonicalDirectEvidence -eq $true -and
                ($Case.testDisposition.kind -ne 'input-only-evidence' -or
                 $Roles.Count -eq 0 -or
                 @($Roles | Where-Object { $_ -ne 'input' }).Count -ne 0)) {
                throw "Direct input evidence case '$CaseId' must declare only input artifacts."
            }
            if ($CanonicalDirectEvidence -eq $true) { $DirectInputEvidenceCount++ }
            $ActualArtifactIds = @($Case.artifacts | ForEach-Object { [string]$_.artifactId } | Sort-Object)
            if (Compare-Object -ReferenceObject $ApprovedArtifactIds -DifferenceObject $ActualArtifactIds) {
                throw "Canonical case '$CaseId' artifacts differ from the explicit release allowlist."
            }
            foreach ($Artifact in $Case.artifacts) {
                $ApprovedArtifact = @($ApprovedCase.artifacts | Where-Object { $_.artifactId -ceq $Artifact.artifactId })
                if ($ApprovedArtifact.Count -ne 1 -or
                    [string]$ApprovedArtifact[0].role -cne [string]$Artifact.role -or
                    [string]$ApprovedArtifact[0].path -cne [string]$Artifact.path -or
                    [long]$ApprovedArtifact[0].size -ne [long]$Artifact.size -or
                    [string]$ApprovedArtifact[0].sha256 -cne [string]$Artifact.sha256) {
                    throw "Canonical artifact '$CaseId/$($Artifact.artifactId)' differs from the explicit release allowlist."
                }
                $ArtifactDeclarationCount++
                Add-GoldenManifestEntryPath -Paths $Paths -GoldenRootRelative $GoldenRootRelative -Entry $Artifact
                $ArtifactPath = Join-Path $GoldenRoot ([string]$Artifact.path)
                if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf) -or
                    (Get-Item -LiteralPath $ArtifactPath).Length -ne [long]$Artifact.size -or
                    (Get-LowerSha256 -Path $ArtifactPath) -cne [string]$Artifact.sha256) {
                    throw "Canonical golden artifact bytes drifted: $($Artifact.path)"
                }
            }
        }
        else {
            $CanonicalAlias = $Case.alias | ConvertTo-Json -Compress -Depth 20
            $ApprovedAlias = $ApprovedCase.alias | ConvertTo-Json -Compress -Depth 20
            if ($null -eq $Case.alias -or $ApprovedAlias -cne $CanonicalAlias -or $ApprovedArtifactIds.Count -ne 0) {
                throw "Canonical alias case '$CaseId' differs from the explicit release allowlist."
            }
        }
    }

    $MissingApprovedCases = @($ApprovedCases.Keys | Where-Object { -not $SelectedCaseIds.Contains($_) } | Sort-Object)
    if ($MissingApprovedCases.Count -ne 0) {
        throw "Canonical inventory is missing release-approved cases: $($MissingApprovedCases -join ', ')"
    }
    foreach ($ApprovedCase in $ReleaseAllowlist.cases) {
        if ($ApprovedCase.directGolden -eq $true -or $ApprovedCase.directEvidence -eq $true) {
            continue
        }
        $SourceCaseId = [string]$ApprovedCase.alias.sourceCaseId
        if (-not $SelectedCaseFacts.ContainsKey($SourceCaseId) -or
            $SelectedCaseFacts[$SourceCaseId].directGolden -ne $true -or
            [string]$SelectedCaseFacts[$SourceCaseId].workflow -cne [string]$ApprovedCase.workflow) {
            throw "Canonical alias '$($ApprovedCase.caseId)' does not select its exact same-workflow direct Golden source '$SourceCaseId'."
        }
    }
    if ($SelectedCases.Count -ne 35 -or $DirectInputEvidenceCount -ne 1 -or $ArtifactDeclarationCount -ne 161 -or $Paths.Count -ne 194) {
        throw 'Canonical golden package projection differs from 35 cases, one input-evidence case, 161 declarations, or 158 unique artifacts.'
    }

    $script:CanonicalGoldenPackageManifest = [ordered]@{
        schemaVersion = '1.0'
        payloadClass = 'owner-approved-golden'
        binaryPayloadsIncluded = $true
        diagnosticsRoot = 'testdata/diagnostics/golden-evidence'
        inventoryScope = 'release-canonical-v1'
        sourceManifest = 'testdata/golden/canonical/manifest.json'
        cases = @($SelectedCases)
    }

    return @($Paths | Sort-Object)
}

if ($ExternalToolPolicyDryRun) {
    Invoke-ExternalToolPolicyDryRun
    return
}

$RepositoryDotNet = Join-Path $RepoRoot '.dotnet/dotnet.exe'
$DotNet = if (Test-Path -LiteralPath $RepositoryDotNet -PathType Leaf) {
    $RepositoryDotNet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}
$Python = (Get-Command python -ErrorAction Stop).Source

Remove-Item -LiteralPath $ReleaseRoot, $WorkRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $ReleaseRoot, $PackageRoot, $AppPublish, $LauncherPublish, $WorkerBuild, $WorkerDist | Out-Null

$AppProject = Join-Path $RepoRoot 'src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj'
$LauncherProject = Join-Path $RepoRoot 'src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj'
$IncludeManagedLauncher = -not ($AllowPrerelease -or $ManualOnly)
$SourcePackageLockSnapshots = Save-SourcePackageLocks
try {
    & $DotNet restore $AppProject -r win-x64 -p:PublishReadyToRun=true
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed before clean publish.' }

    & $DotNet clean $AppProject -c Release -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'dotnet clean failed before publish.' }

    & $DotNet publish $AppProject -c Release -r win-x64 --self-contained true --no-restore `
        -p:Version=$SemanticVersion `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:PublishReadyToRunComposite=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $AppPublish
    $PublishExitCode = $LASTEXITCODE
    if ($PublishExitCode -eq 0 -and $IncludeManagedLauncher) {
        & $DotNet restore $LauncherProject -r win-x64 --locked-mode --disable-parallel
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed before launcher publish.' }
        & $DotNet publish $LauncherProject -c Release -r win-x64 --self-contained true --no-restore `
            -p:Version=$SemanticVersion `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -p:PublishTrimmed=false `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -o $LauncherPublish
        $LauncherPublishExitCode = $LASTEXITCODE
    }
}
finally {
    Restore-SourcePackageLocks -Snapshots $SourcePackageLockSnapshots
}
if ($PublishExitCode -ne 0) { throw 'dotnet publish failed.' }
if ($IncludeManagedLauncher -and $LauncherPublishExitCode -ne 0) { throw 'managed launcher publish failed.' }

$PublishedApp = Join-Path $AppPublish 'NvtFwCombiner.Desktop.exe'
if (-not (Test-Path -LiteralPath $PublishedApp -PathType Leaf)) {
    throw "Published application was not found at $PublishedApp"
}
$AppExe = Join-Path $PackageRoot 'NvtFwCombiner.exe'
Copy-Item -LiteralPath $PublishedApp -Destination $AppExe
$LauncherExe = $null
if ($IncludeManagedLauncher) {
    $PublishedLauncher = Join-Path $LauncherPublish 'NvtFwCombiner.Launcher.exe'
    if (-not (Test-Path -LiteralPath $PublishedLauncher -PathType Leaf)) {
        throw "Published managed launcher was not found at $PublishedLauncher"
    }
    $LauncherExe = Join-Path $PackageRoot 'launcher/NvtFwCombiner.Launcher.exe'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LauncherExe) | Out-Null
    Copy-Item -LiteralPath $PublishedLauncher -Destination $LauncherExe
}
$BuiltInProfilePackagePaths = @(Copy-BuiltInProfilePackageFiles `
    -PublishedRoot $AppPublish `
    -DestinationRoot $PackageRoot)
Copy-CanonicalCapabilityPolicyPackageFile `
    -PublishedRoot $AppPublish `
    -DestinationRoot $PackageRoot

$WorkerExe = Join-Path $PackageRoot $CrcWorkerPackagePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
if ($null -ne $ResolvedVersionOnlyBasePackage) {
    $ReleasePolicy = Join-Path $RepoRoot 'scripts/release_promotion_policy.py'
    & $Python $ReleasePolicy extract-version-only-stable-payload `
        --repository $RepoRoot `
        --base-package $ResolvedVersionOnlyBasePackage `
        --base-package-sha256 $VersionOnlyBasePackageSha256 `
        --destination $WorkerExe `
        --path $CrcWorkerPackagePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Published 1.0.0 CRC worker could not be reused for 1.0.1.'
    }
}
else {
    $WorkerEntry = Join-Path $WorkRoot 'crc_worker_entry.py'
    @'
from nfc_crc_worker.__main__ import main

raise SystemExit(main())
'@ | Set-Content -LiteralPath $WorkerEntry -Encoding utf8NoBOM

    $WorkerSource = Join-Path $RepoRoot 'tools/crc-worker/src'
    & $Python -m PyInstaller --onefile --clean --noconfirm --noupx `
        --name Nfc.CrcWorker `
        --paths $WorkerSource `
        --workpath $WorkerBuild `
        --distpath $WorkerDist `
        --specpath $WorkRoot `
        $WorkerEntry
    if ($LASTEXITCODE -ne 0) { throw 'PyInstaller worker packaging failed.' }

    $BuiltWorker = Join-Path $WorkerDist 'Nfc.CrcWorker.exe'
    if (-not (Test-Path -LiteralPath $BuiltWorker -PathType Leaf)) {
        throw "Packaged CRC worker was not found at $BuiltWorker"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $WorkerExe) | Out-Null
    Copy-Item -LiteralPath $BuiltWorker -Destination $WorkerExe
}

$ExternalToolsDestination = Join-Path $PackageRoot 'external-tools'
Copy-ApprovedExternalToolPackageFiles -DestinationRoot $PackageRoot

$ReferenceDestination = Join-Path $PackageRoot 'reference'
if (-not $ManualOnly) {
    New-Item -ItemType Directory -Force -Path $ReferenceDestination | Out-Null
    @"
NVT FW Combiner reference payload

This directory contains human-review reference evidence and owner-approved golden fixtures shipped with the release package.

Included:
- docs/references/: flash-map, postbuild, flash-header, and provenance references.
- docs/architecture/: CtrlRAM postbuild investigation and IC workflow references.
- testdata/golden/canonical/: 25 Direct Golden cases, one owner-certified input-only evidence case, and nine self-contained fact-scoped alias manifests.

Non-allowlisted private firmware, diagnostics, owner-handoff records, unmanifested BIN files, generated firmware outputs, refcode, source trees, and test projects are not shipped here.
"@ | Set-Content -LiteralPath (Join-Path $ReferenceDestination 'README.txt') -Encoding utf8NoBOM

    $ReferenceFiles = @(
        'docs/references/verification-report.md',
        'docs/references/tddi-flash-header.md',
        'docs/references/nvt-fwconfig-copy-validation.md',
        'docs/references/tddi-flash-header/TDDI_Flash_Header.xlsx',
        'docs/architecture/ctrlram-postbuild-command-matrix.md',
        'docs/architecture/ctrlram-postbuild-investigation-reference.md',
        'docs/architecture/ctrlram-postbuild-original-pasteback.md',
        'docs/architecture/ic-workflow-flowcharts.md',
        'docs/architecture/supported-ic-matrix.md'
    )
    foreach ($ReferenceFile in $ReferenceFiles) {
        Copy-PackageFile -RelativePath $ReferenceFile -DestinationRoot $ReferenceDestination
    }

    Copy-PackageReferenceTree -RelativeRoot 'docs/references/ic-flashmap' -AllowedExtensions @('.bat', '.h', '.json', '.md', '.xlsx')

    $CanonicalGoldenPaths = Get-DeclaredCanonicalGoldenPaths
    foreach ($GoldenPath in $CanonicalGoldenPaths) {
        Copy-PackageFile -RelativePath $GoldenPath -DestinationRoot $ReferenceDestination
    }
    Copy-PackageFile `
        -RelativePath 'testdata/golden/release-canonical-v1.json' `
        -DestinationRoot $ReferenceDestination
    $PackagedGoldenManifestPath = Join-Path $ReferenceDestination 'testdata/golden/canonical/manifest.json'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $PackagedGoldenManifestPath) | Out-Null
    $script:CanonicalGoldenPackageManifest |
        ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath $PackagedGoldenManifestPath -Encoding utf8NoBOM
}

$SelfTestRequest = '{"protocolVersion":"1.0","requestId":"package-self-test","operation":"calculate","algorithmId":"crc-32-mpeg-2","payloadBase64":"MTIzNDU2Nzg5"}'
$SelfTestRaw = $SelfTestRequest | & $WorkerExe
if ($LASTEXITCODE -ne 0) { throw 'Packaged CRC worker self-test process failed.' }
$SelfTest = $SelfTestRaw | ConvertFrom-Json
if ($SelfTest.result.valueHex -ne '0x0376E6E7') {
    throw "Packaged CRC worker self-test returned '$($SelfTest.result.valueHex)'."
}

Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination (Join-Path $PackageRoot 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $RepoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $PackageRoot 'THIRD-PARTY-NOTICES.txt')
if ($ManualOnly) {
    @"
NVT FW Combiner $SemanticVersion
Distribution owner: $DistributionOwner

This is a Windows x64 manual-download package. Run NvtFwCombiner.exe directly.

Contents:
- NvtFwCombiner.exe: self-contained Windows x64 desktop application
- external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe: constrained external checksum/header worker
- profiles/built-in/: exact package trust index, materialized bundles, and runtime catalogs
- external-tools/: generated CRC Worker and approved legacy Combiner runtime packages
- RELEASE-MANIFEST.json: source and file integrity metadata
- SHA256SUMS.txt: package file hashes

Launcher, Setup, Bootstrap, Catalog/Registry deployment, automatic update, Version deployment, and reference/Golden evidence are intentionally absent. This package is not a managed-install or update candidate.
"@ | Set-Content -LiteralPath (Join-Path $PackageRoot 'README.txt') -Encoding utf8NoBOM
}
else {
    @"
NVT FW Combiner $SemanticVersion
Distribution owner: $DistributionOwner

Contents:
- NvtFwCombiner.exe: self-contained Windows x64 desktop application
- launcher/NvtFwCombiner.Launcher.exe: release-coupled managed launcher (stable packages only)
- external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe: constrained external checksum/header worker
- profiles/built-in/: exact package trust index plus its manifest-pinned materialized bundles; profile stage and support publication remain independently authoritative
- external-tools/: generated CRC Worker and approved legacy Combiner runtime packages
- reference/: owner-approved flash-map, postbuild, flash-header, and golden fixture evidence
- RELEASE-MANIFEST.json: source and file integrity metadata
- SHA256SUMS.txt: package file hashes

This exact release selection includes 25 Direct Golden cases, one selected owner-certified input-only evidence case, and nine self-contained evidence aliases across Standard Merge, AB Merge, and CtrlRAM Replace under reference/testdata/golden/canonical. The selected input-only case contains two neutral-named raw input BIN entries; it is not an expected output, Direct Golden, parity claim, runtime path, or support promotion. Eleven Direct Goldens use full-output comparison; fourteen retain their reviewed allowed-byte-difference scope. Two older input-only evidence cases and their three dependent aliases remain repository-only. Diagnostics, owner handoff records, CJK14/HackMD transfer material, archives, private or quarantine evidence, unmanifested BIN files, generated firmware outputs, refcode, production source tree, test projects, editable source profiles, Python runtime installation, and .NET installation requirements are excluded. The packaged BAT and CONFIG provenance are inert reference bytes only and are never tools, processors, or commands. Packaging reference evidence does not promote runtime support.
"@ | Set-Content -LiteralPath (Join-Path $PackageRoot 'README.txt') -Encoding utf8NoBOM
}

$AppHash = Get-LowerSha256 -Path $AppExe
$WorkerHash = Get-LowerSha256 -Path $WorkerExe
$NoticePath = Join-Path $PackageRoot 'THIRD-PARTY-NOTICES.txt'
$LicensePath = Join-Path $PackageRoot 'LICENSE.txt'
$ReadmePath = Join-Path $PackageRoot 'README.txt'

$ProfileFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'profiles') -File -Recurse | ForEach-Object FullName)
$SchemaFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs/contracts') -Filter '*.schema.json' -File | ForEach-Object FullName)
$ProfileDigest = Get-TreeDigest -Paths $ProfileFiles
$SchemaDigest = Get-TreeDigest -Paths $SchemaFiles
$ExternalToolEntries = @(Get-ExternalToolManifestEntries `
    -PackageRoot $PackageRoot `
    -ExternalToolsRoot $ExternalToolsDestination)
$BuiltInProfileEntries = @(Get-BuiltInProfileManifestEntries `
    -PackageRoot $PackageRoot `
    -PackagePaths $BuiltInProfilePackagePaths)
$CanonicalCapabilityPolicyEntry = Get-CanonicalCapabilityPolicyManifestEntry `
    -PackageRoot $PackageRoot
$ReferencePayloadEntries = @()
if (-not $ManualOnly) {
    $ReferencePayloadFiles = @(Get-ChildItem -LiteralPath $ReferenceDestination -File -Recurse | ForEach-Object FullName)
    $ReferencePayloadEntries = @(
        $ReferencePayloadFiles | Sort-Object | ForEach-Object {
            $RelativePath = [System.IO.Path]::GetRelativePath($PackageRoot, $_).Replace('\', '/')
            $Role = if ($_.EndsWith('.bin', [StringComparison]::OrdinalIgnoreCase)) { 'goldenFixture' } else { 'reference' }
            [ordered]@{ path = $RelativePath; size = (Get-Item $_).Length; sha256 = (Get-LowerSha256 $_); role = $Role }
        }
    )
}
$ApprovedProcessorIds = @(
    'nfc.crc32-mpeg2.calculate-v1',
    'nfc.nt51917.ctrlram-postbuild-v1',
    'nfc.nt51919.ctrlram-postbuild-v1',
    'nfc.nt51923.ctrlram-postbuild-v1',
    'nfc.nt51926.ctrlram-postbuild-fw1.4.1',
    'nfc.nt51926.ctrlram-postbuild-v1',
    'nfc.nt51927.ctrlram-postbuild-v1',
    'nfc.nt51928.ctrlram-postbuild-v1',
    'nfc.nt51929.ctrlram-postbuild-v1',
    'nfc.nt51932.ctrlram-postbuild-v1',
    'nfc.nt51950.ctrlram-postbuild-v1',
    'nfc.nt51951.ctrlram-postbuild-v1'
)

$SbomName = "$PackageName.spdx.json"
$ProvenanceName = "$PackageName.provenance.json"
$FileEntries = @(
    [ordered]@{ path = 'NvtFwCombiner.exe'; size = (Get-Item $AppExe).Length; sha256 = $AppHash; role = 'application' },
    [ordered]@{ path = 'THIRD-PARTY-NOTICES.txt'; size = (Get-Item $NoticePath).Length; sha256 = (Get-LowerSha256 $NoticePath); role = 'notices' },
    [ordered]@{ path = 'LICENSE.txt'; size = (Get-Item $LicensePath).Length; sha256 = (Get-LowerSha256 $LicensePath); role = 'license' },
    [ordered]@{ path = 'README.txt'; size = (Get-Item $ReadmePath).Length; sha256 = (Get-LowerSha256 $ReadmePath); role = 'readme' }
) + $BuiltInProfileEntries + @($CanonicalCapabilityPolicyEntry) +
    $ExternalToolEntries + $ReferencePayloadEntries
if ($IncludeManagedLauncher) {
    $LauncherEntry = [ordered]@{
        path = 'launcher/NvtFwCombiner.Launcher.exe'
        size = (Get-Item $LauncherExe).Length
        sha256 = (Get-LowerSha256 $LauncherExe)
        role = 'launcher'
    }
    $FileEntries = @($FileEntries) + @($LauncherEntry)
}

$Manifest = [ordered]@{
    schemaVersion = if ($ManualOnly) { '1.3' } elseif ($IncludeManagedLauncher) { '1.2' } else { '1.1' }
    product = 'NVT FW Combiner'
    version = $SemanticVersion
    sourceCommit = $Commit
    sourceTag = $SourceTag
    runtimeIdentifier = 'win-x64'
    licenseSpdx = 'MIT'
    workerProtocolVersions = @('1.0')
    approvedProcessorIds = $ApprovedProcessorIds
    processorBundleSha256 = $WorkerHash
    embeddedProfileCatalogSha256 = $ProfileDigest
    embeddedSchemaBundleSha256 = $SchemaDigest
    files = $FileEntries
    sbomAsset = $SbomName
    provenanceAsset = $ProvenanceName
}
if ($ManualOnly) {
    $Manifest.distributionMode = 'manual-only'
}
elseif ($IncludeManagedLauncher) {
    $Manifest.versionManagementProtocolVersion = 1
    $Manifest.launcher = [ordered]@{
        launcherVersion = $SemanticVersion
        protocolVersion = 1
        executableRelativePath = 'launcher/NvtFwCombiner.Launcher.exe'
        size = $LauncherEntry.size
        sha256 = $LauncherEntry.sha256
    }
}
$ManifestPath = Join-Path $PackageRoot 'RELEASE-MANIFEST.json'
$Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ManifestPath -Encoding utf8NoBOM
Assert-CanonicalJsonSchema -JsonPath $ManifestPath -SchemaPath $ReleaseManifestSchemaPath

$Sbom = [ordered]@{
    spdxVersion = 'SPDX-2.3'
    dataLicense = 'CC0-1.0'
    SPDXID = 'SPDXRef-DOCUMENT'
    name = $PackageName
    documentNamespace = "$ReleaseNamespace/$SourceTag/$SbomName"
    creationInfo = [ordered]@{
        created = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        creators = @('Tool: NVT-FW-Combiner-release-script')
    }
    packages = @(
        [ordered]@{
            name = 'NVT FW Combiner'
            SPDXID = 'SPDXRef-Package-NvtFwCombiner'
            versionInfo = $SemanticVersion
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $true
            licenseConcluded = 'MIT'
            licenseDeclared = 'MIT'
            copyrightText = 'NOASSERTION'
        }
    )
    files = @(
        $FileEntries | ForEach-Object {
            $SpdxPathId = [Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($_.path))
            [ordered]@{
                fileName = $_.path
                SPDXID = "SPDXRef-File-$SpdxPathId"
                checksums = @([ordered]@{ algorithm = 'SHA256'; checksumValue = $_.sha256 })
                licenseConcluded = 'NOASSERTION'
                copyrightText = 'NOASSERTION'
            }
        }
    )
}
$Sbom | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $ReleaseRoot $SbomName) -Encoding utf8NoBOM

$Provenance = [ordered]@{
    schemaVersion = '1.0'
    product = 'NVT FW Combiner'
    version = $SemanticVersion
    sourceRepository = $SourceIdentity
    sourceCommit = $Commit
    sourceTag = $SourceTag
    builder = if ($ManualOnly) { 'scripts/package.ps1 manual-only operator build' } else { 'GitHub Actions / scripts/package.ps1' }
    runtimeIdentifier = 'win-x64'
    subjects = $FileEntries | ForEach-Object { [ordered]@{ name = $_.path; sha256 = $_.sha256 } }
}
$Provenance | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ReleaseRoot $ProvenanceName) -Encoding utf8NoBOM

$HashTargets = @($FileEntries.path) + @('RELEASE-MANIFEST.json')
Write-PackageHashList `
    -PackageRoot $PackageRoot `
    -RelativePaths $HashTargets `
    -DestinationPath (Join-Path $PackageRoot 'SHA256SUMS.txt')

$Expected = (@(
    'LICENSE.txt',
    'NvtFwCombiner.exe',
    'README.txt',
    'RELEASE-MANIFEST.json',
    'SHA256SUMS.txt',
    'THIRD-PARTY-NOTICES.txt'
) + $(if ($IncludeManagedLauncher) { @('launcher/NvtFwCombiner.Launcher.exe') } else { @() }) +
    @($BuiltInProfileEntries.path) + @($CanonicalCapabilityPolicyEntry.path) +
    @($ExternalToolEntries.path) +
    @($ReferencePayloadEntries | ForEach-Object { $_.path })) | Sort-Object
$Actual = @(
    Get-ChildItem -LiteralPath $PackageRoot -File -Recurse |
        ForEach-Object { [System.IO.Path]::GetRelativePath($PackageRoot, $_.FullName).Replace('\', '/') } |
        Sort-Object
)
if (Compare-Object -ReferenceObject $Expected -DifferenceObject $Actual) {
    throw "Release package contents differ from the closed allowlist: $($Actual -join ', ')"
}
Assert-CanonicalJsonSchema -JsonPath $ManifestPath -SchemaPath $ReleaseManifestSchemaPath

$ZipPath = Join-Path $ReleaseRoot "$PackageName.zip"
Compress-Archive -LiteralPath $PackageRoot -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Release package: $ZipPath"
Write-Host "Application SHA-256: $AppHash"
Write-Host "Worker SHA-256: $WorkerHash"
}
finally {
    # Publishing can leave MSBuild/Roslyn servers alive after a successful or failed package run.
    # They are build helpers, not package runtime dependencies, so always stop the repository SDK servers.
    if ($null -ne $DotNet) {
        & $DotNet build-server shutdown
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE."
        }

        if (Test-Path -LiteralPath $IdleBuildWorkerStopper -PathType Leaf) {
            & $IdleBuildWorkerStopper -RepositoryRoot $RepoRoot
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "idle Avalonia build worker cleanup returned exit code $LASTEXITCODE."
            }
        }
    }

    if ($SourceSnapshotAttached) {
        $SnapshotRemoveOutput = & git -C $InvocationRepoRoot worktree remove --force $SourceSnapshotRoot 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Exact source snapshot cleanup failed and was preserved for inspection at '$SourceSnapshotRoot': $($SnapshotRemoveOutput -join ' ')"
        }
        else {
            & git -C $InvocationRepoRoot worktree prune
            if ($LASTEXITCODE -ne 0) {
                Write-Warning 'Git worktree prune failed after exact source snapshot cleanup.'
            }
        }
    }
}

if (-not $ManualOnly -and -not $AllowPrerelease -and [version]$SemanticVersion -ge [version]'1.0.6') {
    $DistributionLauncherPackager = Join-Path `
        $InvocationRepoRoot 'scripts/package-distribution-launcher.ps1'
    & $DistributionLauncherPackager `
        -Version $Version `
        -Commit $Commit `
        -ReleaseDisposition unsigned-owner-approved
    if ($LASTEXITCODE -ne 0) {
        throw 'Distribution Launcher packaging failed.'
    }
}
