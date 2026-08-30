param(
    [Parameter(Mandatory = $true)]
    [string]$LauncherPath,
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,
    [ValidateRange(10, 300)]
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ExactFile([string]$Path, [string]$Label) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if (-not [System.IO.File]::Exists($resolved)) {
        throw "$Label must be an existing file: $resolved"
    }
    return [System.IO.Path]::GetFullPath($resolved)
}

function Assert-SmokeRoot([string]$Root) {
    $exactRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $temp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
    if (
        -not [string]::Equals(
            [System.IO.Path]::GetDirectoryName($exactRoot),
            $temp,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($exactRoot) -notmatch '^nvt-distribution-launcher-smoke-[0-9a-f]{32}$'
    ) {
        throw "Refusing to clean an unrecognized smoke root: $exactRoot"
    }
    return $exactRoot
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Wait-ForInstallButton([System.Diagnostics.Process]$Process, [int]$Timeout) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $Process.Id)
    $buttonCondition = [System.Windows.Automation.AndCondition]::new(
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            'Install'),
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button))
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Distribution Launcher exited before Install became available (exit $($Process.ExitCode))."
        }
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            $processCondition)
        if ($null -ne $window) {
            $button = $window.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants,
                $buttonCondition)
            if ($null -ne $button -and $button.Current.IsEnabled) {
                return $button
            }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Enabled Install button was not exposed through Windows UI Automation within $Timeout seconds."
}

function Start-DistributionLauncher(
    [string]$Executable,
    [bool]$ClickInstall,
    [int]$Timeout
) {
    $process = Start-Process -FilePath $Executable -PassThru
    try {
        if ($ClickInstall) {
            $button = Wait-ForInstallButton $process $Timeout
            $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
            ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
        }
        if (-not $process.WaitForExit($Timeout * 1000)) {
            throw "Distribution Launcher did not exit within $Timeout seconds."
        }
        return $process.ExitCode
    }
    finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $process.Dispose()
    }
}

function Stop-ExactManagedApplication([string]$Executable) {
    $expected = [System.IO.Path]::GetFullPath($Executable)
    foreach ($process in Get-Process) {
        try {
            $candidate = $process.Path
        }
        catch {
            $process.Dispose()
            continue
        }
        try {
            if ($null -ne $candidate -and [string]::Equals(
                [System.IO.Path]::GetFullPath($candidate),
                $expected,
                [System.StringComparison]::OrdinalIgnoreCase)) {
                if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) {
                    $process.Kill($true)
                    $process.WaitForExit()
                }
            }
        }
        finally {
            $process.Dispose()
        }
    }
}

function Assert-InstalledPackage([string]$VersionRoot, [string]$ExpectedVersion) {
    $manifestPath = Join-Path $VersionRoot 'RELEASE-MANIFEST.json'
    if (-not [System.IO.File]::Exists($manifestPath)) {
        throw "Installed RELEASE-MANIFEST.json is missing."
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.version -ne $ExpectedVersion) {
        throw "Installed manifest version does not match $ExpectedVersion."
    }
    foreach ($entry in $manifest.files) {
        $relative = [string]$entry.path
        if ($relative.Contains('\') -or $relative.StartsWith('/') -or $relative.Split('/') -contains '..') {
            throw "Installed manifest contains an unsafe path: $relative"
        }
        $file = Join-Path $VersionRoot ($relative.Replace('/', '\'))
        if (-not [System.IO.File]::Exists($file)) {
            throw "Installed manifest file is missing: $relative"
        }
        $info = [System.IO.FileInfo]::new($file)
        if ($info.Length -ne [long]$entry.size -or (Get-Sha256 $file) -ne [string]$entry.sha256) {
            throw "Installed file identity differs from RELEASE-MANIFEST.json: $relative"
        }
    }
    return Get-Sha256 $manifestPath
}

function Assert-ReadyInstallation(
    [string]$StatePath,
    [string]$ManagedRoot,
    [string]$ExpectedVersion
) {
    if (-not [System.IO.File]::Exists($StatePath)) {
        throw "Version manager state is missing."
    }
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    $pendingMutationProperty = $state.PSObject.Properties['pendingMutation']
    $pendingMutation = if ($null -eq $pendingMutationProperty) {
        $null
    } else {
        $pendingMutationProperty.Value
    }
    if (
        $state.activeVersion -ne $ExpectedVersion -or
        $state.lastKnownGoodVersion -ne $ExpectedVersion -or
        $null -ne $state.pendingActivation -or
        $null -ne $pendingMutation
    ) {
        throw "Version manager state is not terminal READY for $ExpectedVersion."
    }
    $admission = @($state.admissions | Where-Object { $_.version -eq $ExpectedVersion })
    if ($admission.Count -ne 1 -or [string]::IsNullOrWhiteSpace($state.managedRootIdentity)) {
        throw "Version manager state does not carry one admitted READY version and root identity."
    }

    $launcherStatePath = "$StatePath.launcher-bootstrap.v1.json"
    if (-not [System.IO.File]::Exists($launcherStatePath)) {
        throw "Launcher bootstrap state is missing."
    }
    $launcherState = Get-Content -LiteralPath $launcherStatePath -Raw | ConvertFrom-Json
    if (
        $launcherState.active.ownerAppVersion -ne $ExpectedVersion -or
        $launcherState.lastKnownGood.ownerAppVersion -ne $ExpectedVersion -or
        $null -ne $launcherState.pending -or
        $launcherState.managedRootIdentity -ne $state.managedRootIdentity
    ) {
        throw "Launcher bootstrap state is not terminal READY for $ExpectedVersion."
    }
    if (
        [System.IO.File]::Exists("$ManagedRoot.setup-transaction.v1.json") -or
        [System.IO.Directory]::Exists("$ManagedRoot.setup-staging")
    ) {
        throw "Setup residue remains after READY."
    }
    return @{
        stateSha256 = Get-Sha256 $StatePath
        launcherStateSha256 = Get-Sha256 $launcherStatePath
        managedRootIdentity = [string]$state.managedRootIdentity
    }
}

if ($env:OS -ne 'Windows_NT') {
    throw 'The Distribution Launcher UI Automation smoke requires Windows.'
}

$ExactLauncher = Resolve-ExactFile $LauncherPath 'LauncherPath'
$ExactPackage = Resolve-ExactFile $PackagePath 'PackagePath'
$ExactEvidence = [System.IO.Path]::GetFullPath($EvidencePath)
$SmokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'nvt-distribution-launcher-smoke-' + [Guid]::NewGuid().ToString('N'))
$SmokeRoot = Assert-SmokeRoot $SmokeRoot
$DeliveryRoot = Join-Path $SmokeRoot 'delivery'
$DeliveryLauncher = Join-Path $DeliveryRoot 'NvtFwCombiner.DistributionLauncher.exe'
$ManagedRoot = Join-Path $DeliveryRoot 'NvtFwCombiner'
$LocalAppData = Join-Path $SmokeRoot 'local-app-data'
$StatePath = Join-Path $LocalAppData 'NvtFwCombiner\version-manager.v1.json'
$SourceRoot = Join-Path $SmokeRoot 'source'
$OfflineSourceRoot = Join-Path $SmokeRoot 'source.offline'
$RegistryPath = Join-Path $SourceRoot 'update-source-registry.json'
$InstalledApplication = Join-Path $ManagedRoot "versions\$Version\NvtFwCombiner.exe"
$PreviousLocalAppData = $env:LOCALAPPDATA
$PreviousRegistry = $env:NFC_UPDATE_SOURCE_REGISTRY_PATH
$Failure = $null
$Evidence = [ordered]@{
    schemaVersion = 1
    version = $Version
    launcherSha256 = Get-Sha256 $ExactLauncher
    packageSha256 = Get-Sha256 $ExactPackage
    firstInstallExitCode = $null
    offlineExitCode = $null
    uiAutomationInstallInvoked = $false
    sourceRenamedOffline = $false
    ready = $false
}

try {
    New-Item -ItemType Directory -Path $DeliveryRoot, $LocalAppData -Force | Out-Null
    Copy-Item -LiteralPath $ExactLauncher -Destination $DeliveryLauncher
    if ((Get-Sha256 $DeliveryLauncher) -ne $Evidence.launcherSha256) {
        throw 'Delivery Launcher copy differs from the supplied final executable.'
    }
    & python (Join-Path $PSScriptRoot 'create_launcher_process_smoke_source.py') `
        create-single --output $SourceRoot --package $ExactPackage --version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Local single-version update source creation failed with exit $LASTEXITCODE."
    }
    $sourcePackage = Join-Path $SourceRoot "packages\NvtFwCombiner-v$Version-win-x64.zip"
    if ((Get-Sha256 $sourcePackage) -ne $Evidence.packageSha256) {
        throw 'Local update source did not preserve the exact supplied package.'
    }
    $catalog = Get-Content -LiteralPath (Join-Path $SourceRoot 'update-catalog.v1.json') -Raw |
        ConvertFrom-Json
    $catalogEntry = @($catalog.versions | Where-Object { $_.version -eq $Version })
    if ($catalogEntry.Count -ne 1) {
        throw "Local update source does not contain exactly one $Version Catalog entry."
    }
    $Evidence.expectedReleaseManifestSha256 = [string]$catalogEntry[0].releaseManifestSha256

    $env:LOCALAPPDATA = $LocalAppData
    $env:NFC_UPDATE_SOURCE_REGISTRY_PATH = $RegistryPath
    $Evidence.firstInstallExitCode = Start-DistributionLauncher $DeliveryLauncher $true $TimeoutSeconds
    $Evidence.uiAutomationInstallInvoked = $true
    if ($Evidence.firstInstallExitCode -ne 0) {
        throw "First-install Launcher returned $($Evidence.firstInstallExitCode), expected 0."
    }

    $versionRoot = Join-Path $ManagedRoot "versions\$Version"
    $Evidence.releaseManifestSha256 = Assert-InstalledPackage $versionRoot $Version
    if ($Evidence.releaseManifestSha256 -ne $Evidence.expectedReleaseManifestSha256) {
        throw 'Installed release manifest differs from the exact supplied package.'
    }
    $ready = Assert-ReadyInstallation $StatePath $ManagedRoot $Version
    $Evidence.stateSha256 = $ready.stateSha256
    $Evidence.launcherStateSha256 = $ready.launcherStateSha256
    $Evidence.managedRootIdentity = $ready.managedRootIdentity
    $stableCopy = Join-Path $ManagedRoot 'NvtFwCombiner.DistributionLauncher.exe'
    if (-not [System.IO.File]::Exists($stableCopy) -or (Get-Sha256 $stableCopy) -ne $Evidence.launcherSha256) {
        throw 'Managed stable Launcher copy is missing or differs from the delivery executable.'
    }
    $Evidence.stableLauncherSha256 = Get-Sha256 $stableCopy
    $Evidence.ready = $true
    Stop-ExactManagedApplication $InstalledApplication

    Move-Item -LiteralPath $SourceRoot -Destination $OfflineSourceRoot
    $Evidence.sourceRenamedOffline = $true
    $Evidence.offlineExitCode = Start-DistributionLauncher $DeliveryLauncher $false $TimeoutSeconds
    if ($Evidence.offlineExitCode -ne 0) {
        throw "Offline Launcher returned $($Evidence.offlineExitCode), expected 0."
    }
    $offlineReady = Assert-ReadyInstallation $StatePath $ManagedRoot $Version
    if ($offlineReady.managedRootIdentity -ne $Evidence.managedRootIdentity) {
        throw 'Offline launch changed the managed-root identity.'
    }
}
catch {
    $Failure = $_
    $Evidence.error = $_.Exception.Message
}
finally {
    try {
        Stop-ExactManagedApplication $InstalledApplication
    }
    catch {
        if ($null -eq $Failure) {
            $Failure = $_
            $Evidence.error = $_.Exception.Message
        }
    }
    finally {
        $env:LOCALAPPDATA = $PreviousLocalAppData
        $env:NFC_UPDATE_SOURCE_REGISTRY_PATH = $PreviousRegistry
        try {
            $evidenceParent = [System.IO.Path]::GetDirectoryName($ExactEvidence)
            if (-not [string]::IsNullOrWhiteSpace($evidenceParent)) {
                New-Item -ItemType Directory -Path $evidenceParent -Force | Out-Null
            }
            $Evidence.success = $null -eq $Failure
            [System.IO.File]::WriteAllText(
                $ExactEvidence,
                ($Evidence | ConvertTo-Json -Depth 5),
                [System.Text.UTF8Encoding]::new($false))
        }
        finally {
            $SmokeRoot = Assert-SmokeRoot $SmokeRoot
            if ([System.IO.Directory]::Exists($SmokeRoot)) {
                Remove-Item -LiteralPath $SmokeRoot -Recurse -Force
            }
        }
    }
}

if ($null -ne $Failure) {
    throw $Failure
}

Write-Host "Distribution Launcher local E2E smoke passed. Evidence: $ExactEvidence"
