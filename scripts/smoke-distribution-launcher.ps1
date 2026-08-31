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
    [int]$TimeoutSeconds = 120,
    [switch]$KeepSmokeRootOnFailure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script:UiAutomationInstallInvoked = $false

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

function Remove-SmokeRoot([string]$Root) {
    $exactRoot = Assert-SmokeRoot $Root
    for ($attempt = 1; $attempt -le 40; $attempt++) {
        try {
            if ([System.IO.Directory]::Exists($exactRoot)) {
                [System.IO.Directory]::Delete($exactRoot, $true)
            }
            if (-not [System.IO.Directory]::Exists($exactRoot)) {
                return
            }
        }
        catch {
            $cause = $_.Exception
            while ($null -ne $cause.InnerException) {
                $cause = $cause.InnerException
            }
            if (
                $cause -isnot [System.IO.IOException] -and
                $cause -isnot [System.UnauthorizedAccessException]
            ) {
                throw
            }
            if ($attempt -eq 40) {
                throw
            }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Smoke root remained after bounded cleanup: $exactRoot"
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
            $script:UiAutomationInstallInvoked = $true
        }
        $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
        while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) {
            Add-Type -AssemblyName UIAutomationClient
            Add-Type -AssemblyName UIAutomationTypes
            $processCondition = [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
                $process.Id)
            $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                [System.Windows.Automation.TreeScope]::Children,
                $processCondition)
            $diagnostics = @{}
            if ($null -ne $window) {
                foreach ($automationId in @(
                    'OutcomeText',
                    'OperationProgressText',
                    'SourceStatusText',
                    'PrimaryButton')) {
                    $condition = [System.Windows.Automation.PropertyCondition]::new(
                        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                        $automationId)
                    $element = $window.FindFirst(
                        [System.Windows.Automation.TreeScope]::Descendants,
                        $condition)
                    if ($null -ne $element -and -not [string]::IsNullOrWhiteSpace($element.Current.Name)) {
                        $diagnostics[$automationId] = $element.Current.Name
                    }
                }
            }
            if ($ClickInstall -and -not [string]::IsNullOrWhiteSpace($diagnostics['OutcomeText'])) {
                $detail = $diagnostics.GetEnumerator() |
                    Sort-Object Key |
                    ForEach-Object { "$($_.Key)=$($_.Value)" }
                throw "Setup reported a terminal failure. UI: $($detail -join '; ')"
            }
            Start-Sleep -Milliseconds 100
        }
        if (-not $process.HasExited) {
            $suffix = if ($diagnostics.Count -eq 0) {
                ' No visible diagnostic text was exposed.'
            }
            else {
                $detail = $diagnostics.GetEnumerator() |
                    Sort-Object Key |
                    ForEach-Object { "$($_.Key)=$($_.Value)" }
                " UI: $($detail -join '; ')"
            }
            throw "Distribution Launcher did not exit within $Timeout seconds.$suffix"
        }
        return $process.ExitCode
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
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
                    Stop-Process -Id $process.Id -Force -ErrorAction Stop
                    $process.WaitForExit()
                }
            }
        }
        finally {
            $process.Dispose()
        }
    }
}

function Wait-ExactManagedProcessSetExit(
    [string[]]$Executables,
    [int]$TimeoutMilliseconds,
    [scriptblock]$ProcessSnapshot = { Get-Process }) {
    $expected = @($Executables | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $consecutiveEmptySnapshots = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        $found = $false
        foreach ($process in (& $ProcessSnapshot)) {
            try {
                $candidate = $process.Path
                if ($null -ne $candidate) {
                    $fullCandidate = [System.IO.Path]::GetFullPath($candidate)
                    foreach ($path in $expected) {
                        if ([string]::Equals(
                            $fullCandidate,
                            $path,
                            [System.StringComparison]::OrdinalIgnoreCase)) {
                            $found = $true
                            break
                        }
                    }
                }
            }
            catch {
            }
            finally {
                $process.Dispose()
            }
        }
        if (-not $found) {
            $consecutiveEmptySnapshots++
            if ($consecutiveEmptySnapshots -ge 2) {
                return
            }
        }
        else {
            $consecutiveEmptySnapshots = 0
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Managed process set did not exit within $TimeoutMilliseconds ms: $($expected -join '; ')"
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
        [System.IO.File]::Exists("$ManagedRoot.managed-setup-transaction.v1.json") -or
        [System.IO.Directory]::Exists("$ManagedRoot.managed-setup-staging")
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
$InstalledVersionLauncher = Join-Path $ManagedRoot (
    "versions\$Version\launcher\NvtFwCombiner.Launcher.exe")
$InstalledBootstrap = Join-Path $ManagedRoot 'NvtFwCombiner.Bootstrap.exe'
$ManagedProcessPaths = @(
    $InstalledApplication,
    $InstalledVersionLauncher,
    $InstalledBootstrap)
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
    cleanupSucceeded = $false
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
    Wait-ExactManagedProcessSetExit $ManagedProcessPaths 10000

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
        Wait-ExactManagedProcessSetExit $ManagedProcessPaths 10000
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
        $Evidence.uiAutomationInstallInvoked = $script:UiAutomationInstallInvoked
        $Evidence.stateExists = [System.IO.File]::Exists($StatePath)
        $Evidence.launcherStateExists = [System.IO.File]::Exists(
            "$StatePath.launcher-bootstrap.v1.json")
        $transactionPath = "$ManagedRoot.managed-setup-transaction.v1.json"
        $Evidence.transactionExists = [System.IO.File]::Exists($transactionPath)
        if ($Evidence.transactionExists) {
            try {
                $transaction = Get-Content -LiteralPath $transactionPath -Raw | ConvertFrom-Json
                $Evidence.transactionPhase = [string]$transaction.phase
            }
            catch {
                $Evidence.transactionPhase = '<unreadable>'
            }
        }

        $SmokeRoot = Assert-SmokeRoot $SmokeRoot
        if ($KeepSmokeRootOnFailure -and $null -ne $Failure) {
            $Evidence.cleanupRetainedForDiagnosis = $true
            $Evidence.diagnosticSmokeRoot = $SmokeRoot
        }
        else {
            try {
                if ([System.IO.Directory]::Exists($SmokeRoot)) {
                    Remove-SmokeRoot $SmokeRoot
                }
                $Evidence.cleanupSucceeded = -not [System.IO.Directory]::Exists($SmokeRoot)
            }
            catch {
                if ($null -eq $Failure) {
                    $Failure = $_
                    $Evidence.error = $_.Exception.Message
                }
            }
        }

        $Evidence.success = $null -eq $Failure -and $Evidence.cleanupSucceeded
        $evidenceParent = [System.IO.Path]::GetDirectoryName($ExactEvidence)
        if (-not [string]::IsNullOrWhiteSpace($evidenceParent)) {
            New-Item -ItemType Directory -Path $evidenceParent -Force | Out-Null
        }
        [System.IO.File]::WriteAllText(
            $ExactEvidence,
            ($Evidence | ConvertTo-Json -Depth 5),
            [System.Text.UTF8Encoding]::new($false))
    }
}

if ($null -ne $Failure) {
    throw $Failure
}

Write-Host "Distribution Launcher local E2E smoke passed. Evidence: $ExactEvidence"
