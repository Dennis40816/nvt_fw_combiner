[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Commit,

    [Parameter(Mandatory = $true)]
    [ValidateSet('unsigned-owner-approved')]
    [string]$ReleaseDisposition
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$InvocationRepoRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = $InvocationRepoRoot
$SourceSnapshotRoot = $null
$SourceSnapshotAttached = $false
$DotNet = $null
$SemanticVersion = if ($Version.StartsWith('v', [StringComparison]::Ordinal)) {
    $Version.Substring(1)
}
else {
    $Version
}
$StableSemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+$'
if ($SemanticVersion -notmatch $StableSemVerPattern) {
    throw "Distribution Launcher packaging requires stable SemVer; received '$Version'."
}
if ($Commit -cnotmatch '^[0-9a-f]{40}$') {
    throw "Commit must be a lowercase 40-character Git SHA; received '$Commit'."
}

$VersionPath = Join-Path $InvocationRepoRoot 'VERSION'
if (-not (Test-Path -LiteralPath $VersionPath -PathType Leaf) -or
    (Get-Content -LiteralPath $VersionPath -Raw).Trim() -cne $SemanticVersion) {
    throw "Distribution Launcher version '$SemanticVersion' does not match repository VERSION."
}
$Head = (& git -C $InvocationRepoRoot rev-parse --verify HEAD 2>&1) -join ''
if ($LASTEXITCODE -ne 0 -or $Head.Trim() -cne $Commit) {
    throw "Distribution Launcher commit '$Commit' does not match repository HEAD."
}
$Status = @(& git -C $InvocationRepoRoot status --porcelain=v1 --untracked-files=all 2>&1)
if ($LASTEXITCODE -ne 0 -or $Status.Count -ne 0) {
    throw 'Distribution Launcher packaging requires a clean repository worktree and index.'
}

$ReleaseRoot = Join-Path $InvocationRepoRoot 'artifacts/release'
$WorkRoot = Join-Path $InvocationRepoRoot 'artifacts/installer-work'
$Prefix = "NvtFwCombiner-Launcher-v$SemanticVersion-win-x64"
$LauncherName = "$Prefix.exe"
$ManifestName = "$Prefix.manifest.json"
$SbomName = "$Prefix.spdx.json"
$ProvenanceName = "$Prefix.intoto.jsonl"
$ChecksumName = "$Prefix.sha256"
$AssetNames = @($LauncherName, $ManifestName, $SbomName, $ProvenanceName, $ChecksumName)
$PreservedFiles = @{}
if (Test-Path -LiteralPath $ReleaseRoot -PathType Container) {
    Get-ChildItem -LiteralPath $ReleaseRoot -File | Where-Object {
        $_.Name -notin $AssetNames
    } | ForEach-Object {
        $PreservedFiles[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
}

function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-CanonicalJson {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$Depth = 10
    )
    $Value | ConvertTo-Json -Depth $Depth | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Assert-JsonSchema {
    param(
        [Parameter(Mandatory = $true)][string]$JsonPath,
        [Parameter(Mandatory = $true)][string]$SchemaPath
    )
    if (-not ((Get-Content -LiteralPath $JsonPath -Raw) |
        Test-Json -SchemaFile $SchemaPath -ErrorAction Stop)) {
        throw "JSON document does not satisfy canonical schema: $JsonPath"
    }
}

try {
    $SnapshotParent = Split-Path -Parent $InvocationRepoRoot
    $SnapshotId = [guid]::NewGuid().ToString('N').Substring(0, 12)
    $SourceSnapshotRoot = Join-Path $SnapshotParent ".nfcl-$SnapshotId"
    $SnapshotOutput = & git -C $InvocationRepoRoot worktree add --detach $SourceSnapshotRoot $Commit 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Exact Launcher source snapshot could not be created: $($SnapshotOutput -join ' ')"
    }
    $SourceSnapshotAttached = $true
    $RepoRoot = $SourceSnapshotRoot

    $SnapshotVersion = (Get-Content -LiteralPath (Join-Path $RepoRoot 'VERSION') -Raw).Trim()
    if ($SnapshotVersion -cne $SemanticVersion) {
        throw "Exact Launcher source VERSION '$SnapshotVersion' differs from '$SemanticVersion'."
    }
    $SnapshotStatus = @(& git -C $RepoRoot status --porcelain=v1 --untracked-files=all 2>&1)
    if ($LASTEXITCODE -ne 0 -or $SnapshotStatus.Count -ne 0) {
        throw 'Exact Launcher source snapshot is not clean.'
    }

    $RepositoryDotNet = Join-Path $InvocationRepoRoot '.dotnet/dotnet.exe'
    $DotNet = if (Test-Path -LiteralPath $RepositoryDotNet -PathType Leaf) {
        $RepositoryDotNet
    }
    else {
        (Get-Command dotnet -ErrorAction Stop).Source
    }

    if (Test-Path -LiteralPath $WorkRoot) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $ReleaseRoot, $WorkRoot | Out-Null
    foreach ($AssetName in $AssetNames) {
        Remove-Item -LiteralPath (Join-Path $ReleaseRoot $AssetName) -Force -ErrorAction SilentlyContinue
    }

    $BootstrapPublish = Join-Path $WorkRoot 'bootstrap-publish'
    $LauncherPublish = Join-Path $WorkRoot 'launcher-publish'
    $ExtractionRoot = Join-Path $WorkRoot 'extracted'
    $DescriptorPath = Join-Path $WorkRoot 'managed-setup-payload-admission.v1.json'
    $BootstrapProject = Join-Path $RepoRoot 'src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj'
    $LauncherProject = Join-Path $RepoRoot 'src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj'

    & $DotNet restore $BootstrapProject -r win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Distribution Bootstrap restore failed.' }
    & $DotNet publish $BootstrapProject -c Release -r win-x64 --self-contained true --no-restore `
        -p:Version=$SemanticVersion `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $BootstrapPublish
    if ($LASTEXITCODE -ne 0) { throw 'Distribution Bootstrap publish failed.' }
    $BootstrapPath = Join-Path $BootstrapPublish 'NvtFwCombiner.LauncherBootstrap.exe'
    if (-not (Test-Path -LiteralPath $BootstrapPath -PathType Leaf)) {
        throw 'Published Distribution Bootstrap executable is missing.'
    }
    $BootstrapSize = (Get-Item -LiteralPath $BootstrapPath).Length
    $BootstrapHash = Get-LowerSha256 -Path $BootstrapPath

    $Descriptor = [ordered]@{
        schemaVersion = '1.0'
        product = 'NVT FW Combiner'
        payloadKind = 'distribution-launcher-bootstrap'
        launcherSetupProtocolVersion = 1
        launcherVersion = $SemanticVersion
        runtimeIdentifier = 'win-x64'
        sourceCommit = $Commit
        bootstrap = [ordered]@{
            resourceName = 'NvtFwCombiner.DistributionLauncher.Payload.NvtFwCombiner.Bootstrap.exe'
            installedFileName = 'NvtFwCombiner.Bootstrap.exe'
            size = $BootstrapSize
            sha256 = $BootstrapHash
            versionManagementProtocolVersion = 1
            sourceCommit = $Commit
        }
    }
    Write-CanonicalJson -Value $Descriptor -Path $DescriptorPath
    $PayloadSchemaPath = Join-Path $RepoRoot 'docs/contracts/managed-setup-payload-admission-v1.schema.json'
    Assert-JsonSchema -JsonPath $DescriptorPath -SchemaPath $PayloadSchemaPath

    & $DotNet restore $LauncherProject -r win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Distribution Launcher restore failed.' }
    & $DotNet publish $LauncherProject -c Release -r win-x64 --self-contained true --no-restore `
        -p:Version=$SemanticVersion `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        "-p:ManagedSetupPayloadAdmissionPath=$DescriptorPath" `
        "-p:ManagedSetupBootstrapPath=$BootstrapPath" `
        -o $LauncherPublish
    if ($LASTEXITCODE -ne 0) { throw 'Distribution Launcher publish failed.' }
    $PublishedLauncher = Join-Path $LauncherPublish 'NvtFwCombiner.DistributionLauncher.exe'
    if (-not (Test-Path -LiteralPath $PublishedLauncher -PathType Leaf)) {
        throw 'Published Distribution Launcher executable is missing.'
    }
    $LauncherPath = Join-Path $ReleaseRoot $LauncherName
    Copy-Item -LiteralPath $PublishedLauncher -Destination $LauncherPath

    & $LauncherPath '--extract-release-payload' $ExtractionRoot
    if ($LASTEXITCODE -ne 0) { throw 'Final Distribution Launcher payload extraction failed.' }
    $ExtractedDescriptor = Join-Path $ExtractionRoot 'managed-setup-payload-admission.v1.json'
    $ExtractedBootstrap = Join-Path $ExtractionRoot 'NvtFwCombiner.Bootstrap.exe'
    if ((Get-LowerSha256 -Path $ExtractedDescriptor) -cne (Get-LowerSha256 -Path $DescriptorPath) -or
        (Get-LowerSha256 -Path $ExtractedBootstrap) -cne $BootstrapHash -or
        (Get-Item -LiteralPath $ExtractedBootstrap).Length -ne $BootstrapSize) {
        throw 'Final Distribution Launcher embedded payload differs from its reviewed inputs.'
    }
    Assert-JsonSchema -JsonPath $ExtractedDescriptor -SchemaPath $PayloadSchemaPath
    $ExtractedAdmission = Get-Content -LiteralPath $ExtractedDescriptor -Raw | ConvertFrom-Json
    if ($ExtractedAdmission.launcherVersion -cne $SemanticVersion -or
        $ExtractedAdmission.sourceCommit -cne $Commit -or
        $ExtractedAdmission.bootstrap.sha256 -cne $BootstrapHash -or
        $ExtractedAdmission.bootstrap.size -ne $BootstrapSize) {
        throw 'Extracted payload-admission identity differs from the release source.'
    }

    $LauncherSize = (Get-Item -LiteralPath $LauncherPath).Length
    $LauncherHash = Get-LowerSha256 -Path $LauncherPath
    $DescriptorSize = (Get-Item -LiteralPath $ExtractedDescriptor).Length
    $DescriptorHash = Get-LowerSha256 -Path $ExtractedDescriptor
    $ManifestPath = Join-Path $ReleaseRoot $ManifestName
    $Manifest = [ordered]@{
        schemaVersion = '1.0'
        product = 'NVT FW Combiner'
        version = $SemanticVersion
        sourceCommit = $Commit
        runtimeIdentifier = 'win-x64'
        launcherSetupProtocolVersion = 1
        selectionPolicy = 'latest-compatible-verified-registry-candidate'
        distributionEntry = [ordered]@{
            name = $LauncherName
            size = $LauncherSize
            sha256 = $LauncherHash
        }
        payloadAdmission = [ordered]@{
            resourceName = 'NvtFwCombiner.DistributionLauncher.Payload.managed-setup-payload-admission.v1.json'
            size = $DescriptorSize
            sha256 = $DescriptorHash
        }
        embeddedBootstrap = [ordered]@{
            installedFileName = 'NvtFwCombiner.Bootstrap.exe'
            size = $BootstrapSize
            sha256 = $BootstrapHash
            versionManagementProtocolVersion = 1
        }
        sbomAsset = $SbomName
        provenanceAsset = $ProvenanceName
        checksumAsset = $ChecksumName
        releaseDisposition = $ReleaseDisposition
    }
    Write-CanonicalJson -Value $Manifest -Path $ManifestPath
    Assert-JsonSchema `
        -JsonPath $ManifestPath `
        -SchemaPath (Join-Path $RepoRoot 'docs/contracts/installer-release-manifest-v1.schema.json')

    $CommitTimestampText = (& git -C $RepoRoot show -s --format=%cI $Commit 2>&1) -join ''
    if ($LASTEXITCODE -ne 0) { throw 'Release source timestamp could not be resolved.' }
    $CommitTimestamp = [DateTimeOffset]::Parse($CommitTimestampText.Trim()).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $SbomPath = Join-Path $ReleaseRoot $SbomName
    $Sbom = [ordered]@{
        spdxVersion = 'SPDX-2.3'
        dataLicense = 'CC0-1.0'
        SPDXID = 'SPDXRef-DOCUMENT'
        name = $Prefix
        documentNamespace = "urn:msp-fw3:nvt-fw-combiner:launcher:v${SemanticVersion}:$Commit"
        creationInfo = [ordered]@{
            created = $CommitTimestamp
            creators = @('Tool: NVT-FW-Combiner-distribution-launcher-packager')
        }
        packages = @([ordered]@{
            name = 'NVT FW Combiner Distribution Launcher'
            SPDXID = 'SPDXRef-Package-DistributionLauncher'
            versionInfo = $SemanticVersion
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $true
            licenseConcluded = 'MIT'
            licenseDeclared = 'MIT'
            copyrightText = 'NOASSERTION'
        })
        files = @([ordered]@{
            fileName = $LauncherName
            SPDXID = 'SPDXRef-File-DistributionLauncher'
            checksums = @([ordered]@{ algorithm = 'SHA256'; checksumValue = $LauncherHash })
            licenseConcluded = 'NOASSERTION'
            copyrightText = 'NOASSERTION'
        })
    }
    Write-CanonicalJson -Value $Sbom -Path $SbomPath

    $ProvenancePath = Join-Path $ReleaseRoot $ProvenanceName
    $Provenance = [ordered]@{
        _type = 'https://in-toto.io/Statement/v1'
        subject = @([ordered]@{
            name = $LauncherName
            digest = [ordered]@{ sha256 = $LauncherHash }
        })
        predicateType = 'https://slsa.dev/provenance/v1'
        predicate = [ordered]@{
            buildDefinition = [ordered]@{
                buildType = 'urn:msp-fw3:nvt-fw-combiner:distribution-launcher-packager:v1'
                externalParameters = [ordered]@{
                    version = $SemanticVersion
                    runtimeIdentifier = 'win-x64'
                    releaseDisposition = $ReleaseDisposition
                }
                internalParameters = [ordered]@{}
                resolvedDependencies = @([ordered]@{
                    uri = 'urn:msp-fw3:nvt-fw-combiner:source'
                    digest = [ordered]@{ gitCommit = $Commit }
                })
            }
            runDetails = [ordered]@{
                builder = [ordered]@{ id = 'GitHub Actions / scripts/package-distribution-launcher.ps1' }
            }
        }
    }
    ($Provenance | ConvertTo-Json -Depth 12 -Compress) |
        Set-Content -LiteralPath $ProvenancePath -Encoding utf8NoBOM

    $ChecksumPath = Join-Path $ReleaseRoot $ChecksumName
    @($LauncherName, $ManifestName, $SbomName, $ProvenanceName) | ForEach-Object {
        "$(Get-LowerSha256 -Path (Join-Path $ReleaseRoot $_))  $_"
    } | Set-Content -LiteralPath $ChecksumPath -Encoding utf8NoBOM

    $ActualLauncherAssets = @(
        Get-ChildItem -LiteralPath $ReleaseRoot -File |
            Where-Object { $_.Name -like "$Prefix*" } |
            ForEach-Object Name |
            Sort-Object
    )
    if (Compare-Object -ReferenceObject ($AssetNames | Sort-Object) -DifferenceObject $ActualLauncherAssets) {
        throw 'Distribution Launcher release assets differ from the exact five-file closed set.'
    }
    foreach ($PreservedFile in $PreservedFiles.GetEnumerator()) {
        $Path = Join-Path $ReleaseRoot $PreservedFile.Key
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $PreservedFile.Value) {
            throw "Existing release asset changed while packaging the Launcher: $($PreservedFile.Key)"
        }
    }

    Write-Host "Distribution Launcher: $LauncherPath"
    Write-Host "Distribution Launcher SHA-256: $LauncherHash"
}
finally {
    if ($null -ne $DotNet) {
        & $DotNet build-server shutdown
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE."
        }
    }
    if ($SourceSnapshotAttached) {
        $RemoveOutput = & git -C $InvocationRepoRoot worktree remove --force $SourceSnapshotRoot 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Exact Launcher source snapshot cleanup failed at '$SourceSnapshotRoot': $($RemoveOutput -join ' ')"
        }
        & git -C $InvocationRepoRoot worktree prune
        if ($LASTEXITCODE -ne 0) {
            Write-Warning 'Git worktree prune failed after Launcher packaging.'
        }
    }
}
