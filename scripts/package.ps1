[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Commit,

    [switch]$AllowPrerelease
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$SourceTag = if ($Version.StartsWith('v', [StringComparison]::Ordinal)) { $Version } else { "v$Version" }
$SemanticVersion = $SourceTag.Substring(1)
$StableSemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+$'
$PackageSemVerPattern = '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$'
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

$RepositoryDotNet = Join-Path $RepoRoot '.dotnet/dotnet.exe'
$DotNet = if (Test-Path -LiteralPath $RepositoryDotNet -PathType Leaf) {
    $RepositoryDotNet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}
$Python = (Get-Command python -ErrorAction Stop).Source
$ReleaseRoot = Join-Path $RepoRoot 'artifacts/release'
$WorkRoot = Join-Path $RepoRoot 'artifacts/package-work'
$PackageName = "NvtFwCombiner-$SourceTag-win-x64"
$PackageRoot = Join-Path $WorkRoot $PackageName
$AppPublish = Join-Path $WorkRoot 'app-publish'
$WorkerBuild = Join-Path $WorkRoot 'worker-build'
$WorkerDist = Join-Path $WorkRoot 'worker-dist'

Remove-Item -LiteralPath $ReleaseRoot, $WorkRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $ReleaseRoot, $PackageRoot, $AppPublish, $WorkerBuild, $WorkerDist | Out-Null

function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
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

$ApprovedExternalToolPackagePaths = @(
    'external-tools/README.md',
    'external-tools/legacy-combiner/README.md',
    'external-tools/legacy-combiner/1.13.0/Combiner.exe',
    'external-tools/legacy-combiner/1.13.0/manifest.json'
) | Sort-Object

function Copy-PackageReferenceFile {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $NormalizedRelativePath = $RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $SourcePath = Join-Path $RepoRoot $NormalizedRelativePath
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Reference file was not found at $SourcePath"
    }

    $DestinationPath = Join-Path $ReferenceDestination $NormalizedRelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationPath) | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath
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
            Copy-PackageReferenceFile -RelativePath $RelativePath
        }
}

function Add-GoldenManifestEntryPath {
    param(
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$Paths,
        [Parameter(Mandatory = $true)][string]$GoldenRootRelative,
        [Parameter(Mandatory = $true)]$Entry
    )

    if ($null -eq $Entry -or $Entry.PSObject.Properties.Name -notcontains 'path') {
        throw "Standard Merge golden manifest has an entry without a path."
    }

    $ManifestRelativePath = [string]$Entry.path
    if ([string]::IsNullOrWhiteSpace($ManifestRelativePath) -or $ManifestRelativePath.Contains('..') -or $ManifestRelativePath.StartsWith('/')) {
        throw "Unsafe Standard Merge golden manifest path: '$ManifestRelativePath'"
    }

    [void]$Paths.Add("$GoldenRootRelative/$ManifestRelativePath")
}

function Get-DeclaredStandardMergeGoldenPaths {
    $GoldenRootRelative = 'testdata/golden/standard-merge-gen-flash'
    $GoldenRoot = Join-Path $RepoRoot $GoldenRootRelative
    $ManifestPath = Join-Path $GoldenRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Standard Merge golden manifest was not found at $ManifestPath"
    }

    $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($Manifest.payloadClass -ne 'owner-approved-golden-firmware' -or $Manifest.binaryPayloadsIncluded -ne $true) {
        throw 'Standard Merge golden fixtures must declare owner-approved-golden-firmware with binaryPayloadsIncluded=true.'
    }

    $Paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($StaticFile in @('README.md', 'manifest.json')) {
        [void]$Paths.Add("$GoldenRootRelative/$StaticFile")
    }

    if ($Manifest.PSObject.Properties.Name -contains 'supportingFiles' -and $null -ne $Manifest.supportingFiles) {
        foreach ($Property in $Manifest.supportingFiles.PSObject.Properties) {
            Add-GoldenManifestEntryPath -Paths $Paths -GoldenRootRelative $GoldenRootRelative -Entry $Property.Value
        }
    }

    if ($Manifest.PSObject.Properties.Name -notcontains 'cases' -or $null -eq $Manifest.cases) {
        throw 'Standard Merge golden manifest does not contain cases.'
    }

    foreach ($Case in $Manifest.cases) {
        if ($Case.PSObject.Properties.Name -notcontains 'inputs' -or $null -eq $Case.inputs) {
            throw 'Standard Merge golden manifest case has no inputs.'
        }

        foreach ($Input in $Case.inputs.PSObject.Properties) {
            Add-GoldenManifestEntryPath -Paths $Paths -GoldenRootRelative $GoldenRootRelative -Entry $Input.Value
        }

        if ($Case.PSObject.Properties.Name -notcontains 'expectedOutput') {
            throw 'Standard Merge golden manifest case has no expectedOutput.'
        }
        Add-GoldenManifestEntryPath -Paths $Paths -GoldenRootRelative $GoldenRootRelative -Entry $Case.expectedOutput
    }

    $ActualBins = @(
        Get-ChildItem -LiteralPath $GoldenRoot -Filter '*.bin' -File -Recurse |
            ForEach-Object { [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName).Replace('\', '/') } |
            Sort-Object
    )
    $DeclaredBins = @($Paths | Where-Object { $_.EndsWith('.bin', [StringComparison]::OrdinalIgnoreCase) } | Sort-Object)
    if (Compare-Object -ReferenceObject $DeclaredBins -DifferenceObject $ActualBins) {
        throw 'Standard Merge golden BIN files do not exactly match manifest declarations.'
    }

    return @($Paths | Sort-Object)
}

$AppProject = Join-Path $RepoRoot 'src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj'
$SourcePackageLockSnapshots = Save-SourcePackageLocks
& $DotNet publish $AppProject -c Release -r win-x64 --self-contained true `
    -p:Version=$SemanticVersion `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $AppPublish
$PublishExitCode = $LASTEXITCODE
Restore-SourcePackageLocks -Snapshots $SourcePackageLockSnapshots
if ($PublishExitCode -ne 0) { throw 'dotnet publish failed.' }

$PublishedApp = Join-Path $AppPublish 'NvtFwCombiner.Presentation.Avalonia.exe'
if (-not (Test-Path -LiteralPath $PublishedApp -PathType Leaf)) {
    throw "Published application was not found at $PublishedApp"
}
$AppExe = Join-Path $PackageRoot 'NvtFwCombiner.exe'
Copy-Item -LiteralPath $PublishedApp -Destination $AppExe

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
$WorkerExe = Join-Path $PackageRoot 'Nfc.CrcWorker.exe'
Copy-Item -LiteralPath $BuiltWorker -Destination $WorkerExe

$ExternalToolsSource = Join-Path $RepoRoot 'external-tools'
$ExternalToolsDestination = Join-Path $PackageRoot 'external-tools'
if (-not (Test-Path -LiteralPath $ExternalToolsSource -PathType Container)) {
    throw "External tools directory was not found at $ExternalToolsSource"
}
Copy-Item -LiteralPath $ExternalToolsSource -Destination $ExternalToolsDestination -Recurse

$ReferenceDestination = Join-Path $PackageRoot 'reference'
New-Item -ItemType Directory -Force -Path $ReferenceDestination | Out-Null
@"
NVT FW Combiner reference payload

This directory contains human-review reference evidence and owner-approved golden fixtures shipped with the release package.

Included:
- docs/references/: flash-map, postbuild, flash-header, and provenance references.
- docs/architecture/: CtrlRAM postbuild investigation and IC workflow references.
- testdata/golden/standard-merge-gen-flash/: owner-approved Standard Merge golden BIN fixtures declared by manifest for future packaged self-tests.
- testdata/golden/ctrlram-replace/ and testdata/golden/owner-handoff/: non-BIN fixture notes and manifests.

Private golden inputs, unmanifested BIN files, generated firmware outputs, refcode, source trees, and test projects are not shipped here.
"@ | Set-Content -LiteralPath (Join-Path $ReferenceDestination 'README.txt') -Encoding utf8NoBOM

$ReferenceFiles = @(
    'docs/references/verification-report.md',
    'docs/references/combiner-info-2026-07-03.md',
    'docs/references/combiner-info-2026-07-03/TDDI_Flash_Header .xlsx',
    'docs/architecture/ctrlram-postbuild-command-matrix.md',
    'docs/architecture/ctrlram-postbuild-investigation-reference.md',
    'docs/architecture/ctrlram-postbuild-original-pasteback.md',
    'docs/architecture/ic-workflow-flowcharts.md',
    'docs/architecture/supported-ic-matrix.md'
)
foreach ($ReferenceFile in $ReferenceFiles) {
    Copy-PackageReferenceFile -RelativePath $ReferenceFile
}

Copy-PackageReferenceTree -RelativeRoot 'docs/references/ic-flashmap' -AllowedExtensions @('.bat', '.h', '.json', '.md', '.xlsx')

foreach ($GoldenPath in (Get-DeclaredStandardMergeGoldenPaths)) {
    Copy-PackageReferenceFile -RelativePath $GoldenPath
}
Copy-PackageReferenceTree -RelativeRoot 'testdata/golden/ctrlram-replace' -AllowedExtensions @('.json', '.md')
Copy-PackageReferenceTree -RelativeRoot 'testdata/golden/owner-handoff' -AllowedExtensions @('.json', '.md')

$SelfTestRequest = '{"protocolVersion":"1.0","requestId":"package-self-test","operation":"calculate","algorithmId":"crc-32-mpeg-2","payloadBase64":"MTIzNDU2Nzg5"}'
$SelfTestRaw = $SelfTestRequest | & $WorkerExe
if ($LASTEXITCODE -ne 0) { throw 'Packaged CRC worker self-test process failed.' }
$SelfTest = $SelfTestRaw | ConvertFrom-Json
if ($SelfTest.result.valueHex -ne '0x0376E6E7') {
    throw "Packaged CRC worker self-test returned '$($SelfTest.result.valueHex)'."
}

Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination (Join-Path $PackageRoot 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $RepoRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $PackageRoot 'THIRD-PARTY-NOTICES.txt')
@"
NVT FW Combiner $SemanticVersion

Contents:
- NvtFwCombiner.exe: self-contained Windows x64 desktop application
- Nfc.CrcWorker.exe: constrained external checksum/header worker
- external-tools/: approved legacy Combiner runtime packages
- reference/: owner-approved flash-map, postbuild, flash-header, and golden fixture evidence
- RELEASE-MANIFEST.json: source and file integrity metadata
- SHA256SUMS.txt: package file hashes

This package includes owner-approved golden firmware fixtures under reference/testdata/golden/standard-merge-gen-flash for future packaged self-tests. It contains no private golden inputs, unmanifested BIN files, generated firmware outputs, refcode, production source tree, test projects, editable profiles, Python runtime installation, or .NET installation requirement. External tools and reference files are pinned by manifest and SHA-256.
"@ | Set-Content -LiteralPath (Join-Path $PackageRoot 'README.txt') -Encoding utf8NoBOM

$AppHash = Get-LowerSha256 -Path $AppExe
$WorkerHash = Get-LowerSha256 -Path $WorkerExe
$NoticePath = Join-Path $PackageRoot 'THIRD-PARTY-NOTICES.txt'
$LicensePath = Join-Path $PackageRoot 'LICENSE.txt'
$ReadmePath = Join-Path $PackageRoot 'README.txt'

$ProfileFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'profiles') -File -Recurse | ForEach-Object FullName)
$SchemaFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'docs/contracts') -Filter '*.schema.json' -File | ForEach-Object FullName)
$ProfileDigest = Get-TreeDigest -Paths $ProfileFiles
$SchemaDigest = Get-TreeDigest -Paths $SchemaFiles
$ExternalToolFiles = @(Get-ChildItem -LiteralPath $ExternalToolsDestination -File -Recurse | ForEach-Object FullName)
$ExternalToolEntries = @(
    $ExternalToolFiles | Sort-Object | ForEach-Object {
        $RelativePath = [System.IO.Path]::GetRelativePath($PackageRoot, $_).Replace('\', '/')
        [ordered]@{ path = $RelativePath; size = (Get-Item $_).Length; sha256 = (Get-LowerSha256 $_); role = 'externalTool' }
    }
)
$ReferencePayloadFiles = @(Get-ChildItem -LiteralPath $ReferenceDestination -File -Recurse | ForEach-Object FullName)
$ReferencePayloadEntries = @(
    $ReferencePayloadFiles | Sort-Object | ForEach-Object {
        $RelativePath = [System.IO.Path]::GetRelativePath($PackageRoot, $_).Replace('\', '/')
        $Role = if ($_.EndsWith('.bin', [StringComparison]::OrdinalIgnoreCase)) { 'goldenFixture' } else { 'reference' }
        [ordered]@{ path = $RelativePath; size = (Get-Item $_).Length; sha256 = (Get-LowerSha256 $_); role = $Role }
    }
)
$ApprovedProcessorIds = @(
    'nfc.crc32-mpeg2.calculate-v1',
    'nfc.nt51917.ctrlram-postbuild-v1',
    'nfc.nt51919.ctrlram-postbuild-v1',
    'nfc.nt51920.ctrlram-postbuild-v1',
    'nfc.nt51923.ctrlram-postbuild-v1',
    'nfc.nt51926.ctrlram-postbuild-v1',
    'nfc.nt51927.ctrlram-postbuild-v1',
    'nfc.nt51928.ctrlram-postbuild-v1',
    'nfc.nt51929.ctrlram-postbuild-v1',
    'nfc.nt51930.ctrlram-postbuild-v1',
    'nfc.nt51931.ctrlram-postbuild-v1',
    'nfc.nt51932.ctrlram-postbuild-v1',
    'nfc.nt51950.ctrlram-postbuild-v1',
    'nfc.nt51951.ctrlram-postbuild-v1'
)

$SbomName = "$PackageName.spdx.json"
$ProvenanceName = "$PackageName.provenance.json"
$FileEntries = @(
    [ordered]@{ path = 'NvtFwCombiner.exe'; size = (Get-Item $AppExe).Length; sha256 = $AppHash; role = 'application' },
    [ordered]@{ path = 'Nfc.CrcWorker.exe'; size = (Get-Item $WorkerExe).Length; sha256 = $WorkerHash; role = 'crcWorker' },
    [ordered]@{ path = 'THIRD-PARTY-NOTICES.txt'; size = (Get-Item $NoticePath).Length; sha256 = (Get-LowerSha256 $NoticePath); role = 'notices' },
    [ordered]@{ path = 'LICENSE.txt'; size = (Get-Item $LicensePath).Length; sha256 = (Get-LowerSha256 $LicensePath); role = 'license' },
    [ordered]@{ path = 'README.txt'; size = (Get-Item $ReadmePath).Length; sha256 = (Get-LowerSha256 $ReadmePath); role = 'readme' }
) + $ExternalToolEntries + $ReferencePayloadEntries

$Manifest = [ordered]@{
    schemaVersion = '1.1'
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
$ManifestPath = Join-Path $PackageRoot 'RELEASE-MANIFEST.json'
$Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ManifestPath -Encoding utf8NoBOM

$Sbom = [ordered]@{
    spdxVersion = 'SPDX-2.3'
    dataLicense = 'CC0-1.0'
    SPDXID = 'SPDXRef-DOCUMENT'
    name = $PackageName
    documentNamespace = "https://github.com/Dennis40816/nvt_fw_combiner/releases/download/$SourceTag/$SbomName"
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
            [ordered]@{
                fileName = $_.path
                SPDXID = "SPDXRef-File-$($_.path.Replace('.', '-'))"
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
    sourceRepository = 'https://github.com/Dennis40816/nvt_fw_combiner'
    sourceCommit = $Commit
    sourceTag = $SourceTag
    builder = 'GitHub Actions / scripts/package.ps1'
    runtimeIdentifier = 'win-x64'
    subjects = $FileEntries | ForEach-Object { [ordered]@{ name = $_.path; sha256 = $_.sha256 } }
}
$Provenance | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ReleaseRoot $ProvenanceName) -Encoding utf8NoBOM

$HashTargets = @($FileEntries.path) + @('RELEASE-MANIFEST.json')
$HashLines = foreach ($Name in $HashTargets) {
    $Path = Join-Path $PackageRoot $Name
    "$(Get-LowerSha256 -Path $Path)  $Name"
}
$HashLines | Set-Content -LiteralPath (Join-Path $PackageRoot 'SHA256SUMS.txt') -Encoding ascii

$Expected = (@(
    'LICENSE.txt',
    'Nfc.CrcWorker.exe',
    'NvtFwCombiner.exe',
    'README.txt',
    'RELEASE-MANIFEST.json',
    'SHA256SUMS.txt',
    'THIRD-PARTY-NOTICES.txt'
) + @($ExternalToolEntries.path) + @($ReferencePayloadEntries.path)) | Sort-Object
$Actual = @(
    Get-ChildItem -LiteralPath $PackageRoot -File -Recurse |
        ForEach-Object { [System.IO.Path]::GetRelativePath($PackageRoot, $_.FullName).Replace('\', '/') } |
        Sort-Object
)
if (Compare-Object -ReferenceObject $Expected -DifferenceObject $Actual) {
    throw "Release package contents differ from the closed allowlist: $($Actual -join ', ')"
}

$ZipPath = Join-Path $ReleaseRoot "$PackageName.zip"
Compress-Archive -LiteralPath $PackageRoot -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Release package: $ZipPath"
Write-Host "Application SHA-256: $AppHash"
Write-Host "Worker SHA-256: $WorkerHash"
