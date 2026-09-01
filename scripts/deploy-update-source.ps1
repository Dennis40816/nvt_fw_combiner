[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$CatalogPublishedAtUtc,

    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,

    [ValidateSet(1, 2)]
    [int]$CatalogSchemaVersion = 1,

    [string[]]$NotificationPolicy = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PinnedRepository = 'Dennis40816/nvt_fw_combiner'
$Tag = "v$Version"
$PackageName = "NvtFwCombiner-v$Version-win-x64.zip"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$CatalogPublisher = Join-Path $PSScriptRoot 'create_update_catalog.py'

if ($CatalogSchemaVersion -eq 1 -and $NotificationPolicy.Count -ne 0) {
    throw 'Catalog schema version 1 does not accept notification-policy assignments.'
}
if (-not (Test-Path -LiteralPath $CatalogPublisher -PathType Leaf)) {
    throw "The repository Catalog publisher is missing: $CatalogPublisher"
}

foreach ($TypeName in @('NfcDeployPathIdentity')) {
    if (-not ($TypeName -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class NfcDeployPathIdentity
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out BY_HANDLE_FILE_INFORMATION information);

    private const uint FileFlagBackupSemantics = 0x02000000;

    public static string FromPath(string path, bool directory)
    {
        uint flags = directory ? FileFlagBackupSemantics : 0;
        using (SafeFileHandle handle = CreateFileW(
            path,
            0,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            flags,
            IntPtr.Zero))
        {
            if (handle.IsInvalid)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Unable to open a stable identity handle for " + path);
            }
            return FromHandle(handle);
        }
    }

    public static string FromHandle(SafeFileHandle handle)
    {
        BY_HANDLE_FILE_INFORMATION information;
        if (!GetFileInformationByHandle(handle, out information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to read a stable Windows file identity");
        }
        return String.Format(
            "{0:x8}:{1:x8}{2:x8}",
            information.VolumeSerialNumber,
            information.FileIndexHigh,
            information.FileIndexLow);
    }
}
'@
    }
}

function Assert-NormalizedOrdinaryDirectoryChain {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -ne $Path.Trim()) {
        throw "$Description must be a non-empty normalized absolute path."
    }
    if ($Path.StartsWith('\\?\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\\.\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\??\', [StringComparison]::Ordinal) -or
        $Path.Contains('::') -or
        $Path -match '[*?\[]') {
        throw "$Description uses a forbidden provider, device, extended, or wildcard path."
    }
    if (-not [IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Description must be an absolute local-drive or UNC path."
    }

    $IsLocalDrive = $Path -match '^[A-Za-z]:\\'
    $IsUnc = $Path -match '^\\\\[^\\]+\\[^\\]+(?:\\|$)'
    if (-not $IsLocalDrive -and -not $IsUnc) {
        throw "$Description must be an absolute local-drive or UNC path."
    }
    $ColonSearchStart = if ($IsLocalDrive) { 2 } else { 0 }
    if ($Path.IndexOf(':', $ColonSearchStart) -ge 0) {
        throw "$Description cannot contain an alternate data stream."
    }

    $Root = [IO.Path]::GetPathRoot($Path)
    if ([string]::IsNullOrEmpty($Root)) {
        throw "$Description has no filesystem root."
    }
    $Remainder = $Path.Substring($Root.Length)
    $Components = if ($Remainder.Length -eq 0) { @() } else { @($Remainder.Split('\')) }
    foreach ($Component in $Components) {
        if ([string]::IsNullOrEmpty($Component) -or
            $Component -eq '.' -or
            $Component -eq '..' -or
            $Component.EndsWith('.', [StringComparison]::Ordinal) -or
            $Component.EndsWith(' ', [StringComparison]::Ordinal)) {
            throw "$Description is not normalized or contains a forbidden path component."
        }
    }

    $FullPath = [IO.Path]::GetFullPath($Path)
    if (-not [string]::Equals($FullPath, $Path, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must already be normalized: $FullPath"
    }

    $Current = $Root
    $Chain = [Collections.Generic.List[string]]::new()
    $Chain.Add($Root)
    foreach ($Component in $Components) {
        $Current = [IO.Path]::Combine($Current, $Component)
        $Chain.Add($Current)
    }
    foreach ($Candidate in $Chain) {
        if (-not [IO.Directory]::Exists($Candidate)) {
            throw "$Description directory chain is missing: $Candidate"
        }
        $Item = Get-Item -LiteralPath $Candidate -Force
        if (-not $Item.PSIsContainer -or
            (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
            $null -ne $Item.LinkType) {
            throw "$Description directory chain contains a reparse point or non-directory: $Candidate"
        }
    }
    return $FullPath
}

function Get-DirectoryState {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    if ([IO.Path]::IsPathFullyQualified($RootPath)) {
        $SuppliedFilesystemRoot = [IO.Path]::GetPathRoot($RootPath)
        if (-not [string]::IsNullOrEmpty($SuppliedFilesystemRoot) -and
            [string]::Equals(
                $RootPath.TrimEnd('\'),
                $SuppliedFilesystemRoot.TrimEnd('\'),
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'SourceRoot must be below a local drive root or UNC share root.'
        }
    }
    $ResolvedRoot = Assert-NormalizedOrdinaryDirectoryChain `
        -Path $RootPath -Description 'SourceRoot'
    $FilesystemRoot = [IO.Path]::GetPathRoot($ResolvedRoot)
    if ([string]::Equals(
        $ResolvedRoot.TrimEnd('\'),
        $FilesystemRoot.TrimEnd('\'),
        [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SourceRoot must be below a local drive root or UNC share root.'
    }
    $PackagesPath = Join-Path $ResolvedRoot 'packages'
    $ResolvedPackages = Assert-NormalizedOrdinaryDirectoryChain `
        -Path $PackagesPath -Description 'SourceRoot packages'
    [pscustomobject]@{
        Root = $ResolvedRoot
        Packages = $ResolvedPackages
        RootIdentity = [NfcDeployPathIdentity]::FromPath($ResolvedRoot, $true)
        PackagesIdentity = [NfcDeployPathIdentity]::FromPath($ResolvedPackages, $true)
    }
}

function Assert-SameDirectoryState {
    param(
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)]$Actual
    )

    if (-not [string]::Equals($Expected.Root, $Actual.Root, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($Expected.Packages, $Actual.Packages, [StringComparison]::OrdinalIgnoreCase) -or
        $Expected.RootIdentity -ne $Actual.RootIdentity -or
        $Expected.PackagesIdentity -ne $Actual.PackagesIdentity) {
        throw 'SourceRoot or packages identity changed during deployment.'
    }
}

function Get-OpenFileEvidence {
    param([Parameter(Mandatory = $true)][IO.FileStream]$Stream)

    $Stream.Position = 0
    $Sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $Digest = [Convert]::ToHexString($Sha256.ComputeHash($Stream)).ToLowerInvariant()
    }
    finally {
        $Sha256.Dispose()
    }
    [pscustomobject]@{
        Length = $Stream.Length
        Sha256 = $Digest
        Identity = [NfcDeployPathIdentity]::FromHandle($Stream.SafeFileHandle)
    }
}

function Open-StablePackage {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][long]$ExpectedLength,
        [Parameter(Mandatory = $true)][string]$ExpectedSha256,
        [string]$ExpectedIdentity,
        [string]$Description = 'canonical destination package'
    )

    if (-not [IO.File]::Exists($Path)) {
        throw "The $Description is missing: $Path"
    }
    $Item = Get-Item -LiteralPath $Path -Force
    if ($Item.PSIsContainer -or
        (($Item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
        $null -ne $Item.LinkType) {
        throw "The $Description is not an ordinary file: $Path"
    }

    $Stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read,
        1MB,
        [IO.FileOptions]::SequentialScan)
    try {
        $Evidence = Get-OpenFileEvidence -Stream $Stream
        $PathIdentity = [NfcDeployPathIdentity]::FromPath($Path, $false)
        if ($Evidence.Identity -ne $PathIdentity -or
            (-not [string]::IsNullOrEmpty($ExpectedIdentity) -and
                $Evidence.Identity -ne $ExpectedIdentity) -or
            $Evidence.Length -ne $ExpectedLength -or
            $Evidence.Sha256 -ne $ExpectedSha256) {
            throw "The $Description identity or bytes do not match the published Release: $Path"
        }
        return $Stream
    }
    catch {
        $Stream.Dispose()
        throw
    }
}

function New-DirectoryCustodyFile {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $Name = ".nfc-update-deploy-custody-$([guid]::NewGuid().ToString('N')).tmp"
    $Path = Join-Path $Directory $Name
    return [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::Read,
        1,
        [IO.FileOptions]::DeleteOnClose -bor [IO.FileOptions]::WriteThrough)
}

$Gh = Get-Command 'gh' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
$Python = Get-Command 'python' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $Gh) { throw 'gh is required and was not found on PATH.' }
if ($null -eq $Python) { throw 'python is required and was not found on PATH.' }

$InitialState = Get-DirectoryState -RootPath $SourceRoot
$TempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$TempRoot = Join-Path $TempParent "nfc-update-deploy-$([guid]::NewGuid().ToString('N'))"
$TempCustody = $null
$TempCustodyPath = $null
$TempCustodyIdentity = $null
$TempRootIdentity = $null
$DownloadedPath = $null
$DownloadedIdentity = $null
$NotesPath = $null
$NotesIdentity = $null
$AdmittedNewPackage = $false

try {
    [IO.Directory]::CreateDirectory($TempRoot) | Out-Null
    $TempItem = Get-Item -LiteralPath $TempRoot -Force
    if (-not $TempItem.PSIsContainer -or
        (($TempItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
        $null -ne $TempItem.LinkType) {
        throw 'Invocation temporary root is not an ordinary directory.'
    }
    $TempRootIdentity = [NfcDeployPathIdentity]::FromPath($TempRoot, $true)
    $TempCustodyPath = Join-Path $TempRoot `
        ".nfc-update-deploy-invocation-$([guid]::NewGuid().ToString('N')).tmp"
    $TempCustody = [IO.FileStream]::new(
        $TempCustodyPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::Read,
        1,
        [IO.FileOptions]::DeleteOnClose -bor [IO.FileOptions]::WriteThrough)
    $TempCustodyIdentity = [NfcDeployPathIdentity]::FromHandle(
        $TempCustody.SafeFileHandle)

    & $Gh.Source auth status 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI authentication is required.'
    }

    $ReleaseJsonLines = @(& $Gh.Source release view $Tag --repo $PinnedRepository `
        --json 'tagName,isDraft,isPrerelease,body,assets' 2>$null)
    if ($LASTEXITCODE -ne 0) {
        throw 'Published Release metadata could not be read.'
    }
    try {
        $Release = ($ReleaseJsonLines -join "`n") | ConvertFrom-Json -Depth 20
    }
    catch {
        throw 'Published Release metadata is not valid JSON.'
    }
    if ($Release.tagName -cne $Tag -or
        $Release.isDraft -isnot [bool] -or $Release.isDraft -or
        $Release.isPrerelease -isnot [bool] -or $Release.isPrerelease -or
        $Release.body -isnot [string] -or $Release.body.Length -eq 0) {
        throw 'Published Release identity, stability, or body is invalid.'
    }

    $Assets = @($Release.assets)
    $PackageAssets = @($Assets | Where-Object { $_.name -ceq $PackageName })
    if ($PackageAssets.Count -ne 1) {
        throw "Published Release must contain exactly one canonical package asset: $PackageName"
    }
    $Asset = $PackageAssets[0]
    $IntegralTypes = @('Byte', 'SByte', 'Int16', 'UInt16', 'Int32', 'UInt32', 'Int64', 'UInt64')
    if ($null -eq $Asset.size -or
        $IntegralTypes -notcontains $Asset.size.GetType().Name -or
        [decimal]$Asset.size -le 0 -or
        [decimal]$Asset.size -gt [long]::MaxValue -or
        $Asset.digest -isnot [string] -or
        $Asset.digest -cnotmatch '^sha256:[0-9a-f]{64}$') {
        throw 'Published package asset size or SHA-256 digest is invalid.'
    }
    $ExpectedLength = [long]$Asset.size
    $ExpectedSha256 = $Asset.digest.Substring(7)

    & $Gh.Source release download $Tag --repo $PinnedRepository `
        --pattern $PackageName --dir $TempRoot 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'Published package asset could not be downloaded.'
    }
    if (-not [IO.File]::Exists($TempCustodyPath) -or
        [NfcDeployPathIdentity]::FromPath($TempCustodyPath, $false) -ne
            $TempCustodyIdentity) {
        throw 'Invocation temporary-root custody changed during download.'
    }
    $DownloadedItems = @(Get-ChildItem -LiteralPath $TempRoot -Force |
        Where-Object {
            -not [string]::Equals(
                $_.FullName,
                $TempCustodyPath,
                [StringComparison]::OrdinalIgnoreCase)
        })
    if ($DownloadedItems.Count -ne 1 -or
        $DownloadedItems[0].PSIsContainer -or
        $DownloadedItems[0].Name -cne $PackageName -or
        (($DownloadedItems[0].Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
        $null -ne $DownloadedItems[0].LinkType) {
        throw 'GitHub CLI did not produce exactly one ordinary canonical package file.'
    }
    $DownloadedPath = $DownloadedItems[0].FullName
    $DownloadedStream = [IO.FileStream]::new(
        $DownloadedPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read,
        1MB,
        [IO.FileOptions]::SequentialScan)
    try {
        $DownloadedEvidence = Get-OpenFileEvidence -Stream $DownloadedStream
    }
    finally {
        $DownloadedStream.Dispose()
    }
    $DownloadedIdentity = $DownloadedEvidence.Identity
    if ($DownloadedEvidence.Length -ne $ExpectedLength -or
        $DownloadedEvidence.Sha256 -ne $ExpectedSha256) {
        throw 'Downloaded package bytes do not match published Release metadata.'
    }

    $ValidatedState = Get-DirectoryState -RootPath $InitialState.Root
    Assert-SameDirectoryState -Expected $InitialState -Actual $ValidatedState
    $DestinationPath = Join-Path $ValidatedState.Packages $PackageName
    if (Test-Path -LiteralPath $DestinationPath) {
        $ExistingStream = Open-StablePackage -Path $DestinationPath `
            -ExpectedLength $ExpectedLength -ExpectedSha256 $ExpectedSha256
        $ExistingStream.Dispose()
    }

    $NotesPath = Join-Path $TempRoot 'release-notes.md'
    [IO.File]::WriteAllText($NotesPath, $Release.body, [Text.UTF8Encoding]::new($false))
    $NotesIdentity = [NfcDeployPathIdentity]::FromPath($NotesPath, $false)

    $Action = "admit immutable $PackageName and publish Catalog schema $CatalogSchemaVersion"
    if (-not $PSCmdlet.ShouldProcess($ValidatedState.Root, $Action)) {
        Write-Information "Validated deployment plan for $PackageName; SourceRoot was not changed."
        return
    }

    $RootCustody = $null
    $PackagesCustody = $null
    $StablePackage = $null
    $StagingPath = $null
    $OperationFailure = $null
    try {
        $ApprovedState = Get-DirectoryState -RootPath $ValidatedState.Root
        Assert-SameDirectoryState -Expected $ValidatedState -Actual $ApprovedState

        $RootCustody = New-DirectoryCustodyFile -Directory $ApprovedState.Root
        $PackagesCustody = New-DirectoryCustodyFile -Directory $ApprovedState.Packages
        $CustodiedState = Get-DirectoryState -RootPath $ApprovedState.Root
        Assert-SameDirectoryState -Expected $ApprovedState -Actual $CustodiedState

        if (-not (Test-Path -LiteralPath $DestinationPath)) {
            $StagingPath = Join-Path $ApprovedState.Packages `
                ".$PackageName.$([guid]::NewGuid().ToString('N')).staging"
            $Input = Open-StablePackage -Path $DownloadedPath `
                -ExpectedLength $ExpectedLength -ExpectedSha256 $ExpectedSha256 `
                -ExpectedIdentity $DownloadedIdentity `
                -Description 'verified downloaded package'
            try {
                $Input.Position = 0
                $Output = [IO.FileStream]::new(
                    $StagingPath,
                    [IO.FileMode]::CreateNew,
                    [IO.FileAccess]::Write,
                    [IO.FileShare]::Read,
                    1MB,
                    [IO.FileOptions]::WriteThrough)
                try {
                    $Input.CopyTo($Output)
                    $Output.Flush($true)
                }
                finally {
                    $Output.Dispose()
                }
            }
            finally {
                $Input.Dispose()
            }
            [IO.File]::Move($StagingPath, $DestinationPath)
            $AdmittedNewPackage = $true
            $StagingPath = $null
        }

        $StablePackage = Open-StablePackage -Path $DestinationPath `
            -ExpectedLength $ExpectedLength -ExpectedSha256 $ExpectedSha256

        $PublisherArguments = @(
            $CatalogPublisher,
            '--source-root', $ApprovedState.Root,
            '--catalog-schema-version', $CatalogSchemaVersion.ToString([Globalization.CultureInfo]::InvariantCulture),
            '--published-at', "$Version=$CatalogPublishedAtUtc",
            '--release-notes-file', "$Version=$NotesPath"
        )
        foreach ($Assignment in $NotificationPolicy) {
            $PublisherArguments += @('--notification-policy', $Assignment)
        }
        & $Python.Source @PublisherArguments
        if ($LASTEXITCODE -ne 0) {
            throw "The repository Catalog publisher failed with exit code $LASTEXITCODE."
        }
    }
    catch {
        $OperationFailure = $_.Exception
    }
    finally {
        try {
            if ($null -ne $RootCustody -and $null -ne $PackagesCustody) {
                $FinalState = Get-DirectoryState -RootPath $ValidatedState.Root
                Assert-SameDirectoryState -Expected $ValidatedState -Actual $FinalState
            }
            if ($null -ne $StagingPath -and [IO.File]::Exists($StagingPath)) {
                [IO.File]::Delete($StagingPath)
            }
            if ($null -ne $RootCustody -and $null -ne $PackagesCustody) {
                $ReleasedState = Get-DirectoryState -RootPath $ValidatedState.Root
                Assert-SameDirectoryState -Expected $ValidatedState -Actual $ReleasedState
            }
        }
        catch {
            if ($null -eq $OperationFailure) {
                $OperationFailure = $_.Exception
            }
            else {
                $OperationFailure = [AggregateException]::new(
                    'Deployment and final directory-identity validation both failed.',
                    @($OperationFailure, $_.Exception))
            }
        }
        if ($null -ne $StablePackage) { $StablePackage.Dispose() }
        if ($null -ne $PackagesCustody) { $PackagesCustody.Dispose() }
        if ($null -ne $RootCustody) { $RootCustody.Dispose() }
    }

    if ($null -ne $OperationFailure) {
        if ($AdmittedNewPackage) {
            throw [InvalidOperationException]::new(
                "Catalog publication failed after immutable package admission. Retain and report the unreferenced package; do not delete it until the repository Catalog publisher proves it is unreferenced under its lock.",
                $OperationFailure)
        }
        throw $OperationFailure
    }
}
finally {
    $TempCleanupFailure = $null
    try {
        if ([IO.Directory]::Exists($TempRoot)) {
            $TempItem = Get-Item -LiteralPath $TempRoot -Force
            if (-not $TempItem.PSIsContainer -or
                (($TempItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
                $null -ne $TempItem.LinkType -or
                [NfcDeployPathIdentity]::FromPath($TempRoot, $true) -ne $TempRootIdentity) {
                throw 'Invocation temporary-root identity changed.'
            }

            $AllowedPaths = [Collections.Generic.Dictionary[string, string]]::new(
                [StringComparer]::OrdinalIgnoreCase)
            if ($null -ne $TempCustodyPath) {
                $AllowedPaths.Add($TempCustodyPath, $TempCustodyIdentity)
            }
            if ($null -ne $DownloadedPath -and $null -ne $DownloadedIdentity) {
                $AllowedPaths.Add($DownloadedPath, $DownloadedIdentity)
            }
            if ($null -ne $NotesPath -and $null -ne $NotesIdentity) {
                $AllowedPaths.Add($NotesPath, $NotesIdentity)
            }

            $Entries = @(Get-ChildItem -LiteralPath $TempRoot -Force)
            $UnknownEntries = @($Entries | Where-Object {
                -not $AllowedPaths.ContainsKey($_.FullName)
            })
            if ($UnknownEntries.Count -ne 0) {
                throw "Invocation temporary root contains an unknown entry: $($UnknownEntries[0].Name)"
            }
            foreach ($Entry in $Entries) {
                if ($Entry.PSIsContainer -or
                    (($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
                    $null -ne $Entry.LinkType -or
                    [NfcDeployPathIdentity]::FromPath($Entry.FullName, $false) -ne
                        $AllowedPaths[$Entry.FullName]) {
                    throw 'Invocation temporary root contains a changed or non-ordinary entry.'
                }
            }

            foreach ($KnownPath in @($DownloadedPath, $NotesPath)) {
                if ($null -ne $KnownPath -and [IO.File]::Exists($KnownPath)) {
                    [IO.File]::Delete($KnownPath)
                }
            }
            if ([NfcDeployPathIdentity]::FromPath($TempRoot, $true) -ne
                $TempRootIdentity) {
                throw 'Invocation temporary-root identity changed during cleanup.'
            }
        }
    }
    catch {
        $TempCleanupFailure = $_.Exception
    }
    finally {
        if ($null -ne $TempCustody) { $TempCustody.Dispose() }
    }

    if ($null -eq $TempCleanupFailure -and [IO.Directory]::Exists($TempRoot)) {
        try {
            if ([NfcDeployPathIdentity]::FromPath($TempRoot, $true) -ne
                $TempRootIdentity -or
                @(Get-ChildItem -LiteralPath $TempRoot -Force).Count -ne 0) {
                throw 'Invocation temporary root is not the same empty directory.'
            }
            [IO.Directory]::Delete($TempRoot, $false)
        }
        catch {
            $TempCleanupFailure = $_.Exception
        }
    }
    if ($null -ne $TempCleanupFailure) {
        throw [InvalidOperationException]::new(
            "Invocation temporary root was preserved for inspection: $TempRoot. $($TempCleanupFailure.Message)",
            $TempCleanupFailure)
    }
}
