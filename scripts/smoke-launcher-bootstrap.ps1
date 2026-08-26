[CmdletBinding()]
param(
    [string]$EvidencePath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$SmokeRoot = Join-Path ([IO.Path]::GetTempPath()) "nvt-launcher-process-smoke-$([guid]::NewGuid().ToString('N'))"
$PublishRoot = Join-Path $SmokeRoot 'publish'
$SourceRoot = Join-Path $SmokeRoot 'source'
$ManagedRoot = Join-Path $SmokeRoot 'managed'
$LocalAppData = Join-Path $SmokeRoot 'local-app-data'
$StatePath = Join-Path $LocalAppData 'NvtFwCombiner/version-manager.v1.json'
$LauncherStatePath = "$StatePath.launcher-bootstrap.v1.json"
$SourcePackageLockSnapshots = @{}
$PreviousLocalAppData = $env:LOCALAPPDATA
$PreviousBehavior = $env:NVT_READY_PROBE_BEHAVIOR
$PreviousLauncherReadyHandle = $env:NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE
$PreviousLauncherReadyExpected = $env:NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY

function Invoke-PublishSingleFile {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [Parameter(Mandatory = $true)][string]$Output
    )
    & dotnet publish $Project `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=true `
        -p:TrimMode=partial `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $Output
    if ($LASTEXITCODE -ne 0) {
        throw "Published process smoke dependency failed: $Project ($LASTEXITCODE)."
    }
}

try {
    New-Item -ItemType Directory -Force -Path $PublishRoot, $LocalAppData | Out-Null
    foreach ($LockRoot in @('src', 'tests')) {
        Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot $LockRoot) -Filter 'packages.lock.json' -File -Recurse |
            ForEach-Object { $SourcePackageLockSnapshots[$_.FullName] = [IO.File]::ReadAllBytes($_.FullName) }
    }

    $BootstrapPublish = Join-Path $PublishRoot 'bootstrap'
    $LauncherPublish = Join-Path $PublishRoot 'launcher'
    $ProbePublish = Join-Path $PublishRoot 'probe'
    Invoke-PublishSingleFile `
        -Project (Join-Path $RepositoryRoot 'src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj') `
        -Output $BootstrapPublish
    Invoke-PublishSingleFile `
        -Project (Join-Path $RepositoryRoot 'src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj') `
        -Output $LauncherPublish
    Invoke-PublishSingleFile `
        -Project (Join-Path $RepositoryRoot 'tests/NvtFwCombiner.ReadyProbe/NvtFwCombiner.ReadyProbe.csproj') `
        -Output $ProbePublish

    $PublishedBootstrap = Join-Path $BootstrapPublish 'NvtFwCombiner.LauncherBootstrap.exe'
    $Bootstrap = Join-Path $BootstrapPublish 'NvtFwCombiner.Bootstrap.exe'
    Move-Item -LiteralPath $PublishedBootstrap -Destination $Bootstrap
    $Launcher = Join-Path $LauncherPublish 'NvtFwCombiner.Launcher.exe'
    $Probe = Join-Path $ProbePublish 'NvtFwCombiner.ReadyProbe.exe'

    & python (Join-Path $RepositoryRoot 'scripts/create_launcher_process_smoke_source.py') create `
        --output $SourceRoot `
        --app $Probe `
        --stable-launcher $Launcher `
        --failing-launcher $Probe
    if ($LASTEXITCODE -ne 0) { throw 'Launcher process smoke source creation failed.' }
    & python (Join-Path $RepositoryRoot 'scripts/create_managed_installation_lab.py') `
        --source $SourceRoot `
        --bootstrap $Bootstrap `
        --output $ManagedRoot `
        --seed-version 0.10.5
    if ($LASTEXITCODE -ne 0) { throw 'Launcher process managed-root creation failed.' }

    $env:LOCALAPPDATA = $LocalAppData
    Remove-Item Env:NVT_READY_PROBE_BEHAVIOR -ErrorAction SilentlyContinue
    $BootstrapProcess = Start-Process `
        -FilePath (Join-Path $ManagedRoot 'NvtFwCombiner.Bootstrap.exe') `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    $CleanExit = $BootstrapProcess.ExitCode
    if ($CleanExit -ne 0 -or -not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        throw "Zero-argument Bootstrap chain failed with exit $CleanExit."
    }
    $CleanLauncherState = Get-Content -LiteralPath $LauncherStatePath -Raw | ConvertFrom-Json
    if ($null -ne $CleanLauncherState.pending -or $CleanLauncherState.active.ownerAppVersion -ne '0.10.5') {
        throw 'Clean Bootstrap chain did not commit the exact version-scoped launcher.'
    }

    $OwnerAdmissionHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $OwnerAdmissionDigestBytes = $OwnerAdmissionHasher.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes([string]$CleanLauncherState.active.ownerAdmissionIdentity))
        $OwnerAdmissionDigest = ([BitConverter]::ToString($OwnerAdmissionDigestBytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $OwnerAdmissionHasher.Dispose()
    }
    $env:NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE = 'not-a-pipe-handle'
    $env:NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY = [string]::Join(':', @(
        'READY-LAUNCHER',
        [string]$CleanLauncherState.active.protocolVersion,
        [string]$CleanLauncherState.active.ownerAppVersion,
        $OwnerAdmissionDigest,
        [string]$CleanLauncherState.active.ownerReleaseManifestSha256,
        [string]$CleanLauncherState.active.sha256))
    $LauncherProcess = Start-Process `
        -FilePath (Join-Path $ManagedRoot 'versions/0.10.5/launcher/NvtFwCombiner.Launcher.exe') `
        -ArgumentList @('--managed-root', $ManagedRoot, '--state-path', $StatePath) `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    $MissingOuterReadyExit = $LauncherProcess.ExitCode
    if ($MissingOuterReadyExit -ne 16) {
        throw "Launcher did not preserve the failed outer READY outcome: $MissingOuterReadyExit."
    }
    $env:NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE = $null
    $env:NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY = $null

    & python (Join-Path $RepositoryRoot 'scripts/create_launcher_process_smoke_source.py') install-candidate `
        --repository $RepositoryRoot `
        --source $SourceRoot `
        --managed-root $ManagedRoot `
        --state-path $StatePath
    if ($LASTEXITCODE -ne 0) { throw 'Launcher process candidate installation failed.' }
    $env:NVT_READY_PROBE_BEHAVIOR = 'exit-outer-candidate'
    $BootstrapProcess = Start-Process `
        -FilePath (Join-Path $ManagedRoot 'NvtFwCombiner.Bootstrap.exe') `
        -ArgumentList @('--managed-root', $ManagedRoot, '--state-path', $StatePath) `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    Remove-Item Env:NVT_READY_PROBE_BEHAVIOR -ErrorAction SilentlyContinue
    $RollbackExit = $BootstrapProcess.ExitCode
    if ($RollbackExit -ne 1) {
        throw "Candidate failure did not return exact rollback outcome: $RollbackExit."
    }
    $RollbackLauncherState = Get-Content -LiteralPath $LauncherStatePath -Raw | ConvertFrom-Json
    if ($null -ne $RollbackLauncherState.pending -or
        $RollbackLauncherState.active.ownerAppVersion -ne '0.10.5' -or
        $RollbackLauncherState.failed.ownerAppVersion -ne '0.10.6') {
        throw 'Candidate failure did not restore and commit the exact prior launcher.'
    }

    $Evidence = [ordered]@{
        schemaVersion = 1
        cleanZeroArgumentExit = $CleanExit
        rollbackExit = $RollbackExit
        missingOuterReadyExit = $MissingOuterReadyExit
        candidateFailureKind = 'exited-before-ready'
        activeLauncherOwner = [string]$RollbackLauncherState.active.ownerAppVersion
        failedLauncherOwner = [string]$RollbackLauncherState.failed.ownerAppVersion
        bootstrapSha256 = (Get-FileHash -LiteralPath (Join-Path $ManagedRoot 'NvtFwCombiner.Bootstrap.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
        stableLauncherSha256 = (Get-FileHash -LiteralPath $Launcher -Algorithm SHA256).Hash.ToLowerInvariant()
        statePathWasCanonicalDefault = [string]::Equals(
            $StatePath,
            (Join-Path $LocalAppData 'NvtFwCombiner/version-manager.v1.json'),
            [StringComparison]::OrdinalIgnoreCase)
    }
    $EvidenceJson = ConvertTo-Json -InputObject $Evidence -Depth 3
    if (-not [string]::IsNullOrWhiteSpace($EvidencePath)) {
        $EvidenceFullPath = [IO.Path]::GetFullPath($EvidencePath)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $EvidenceFullPath) | Out-Null
        [IO.File]::WriteAllText($EvidenceFullPath, $EvidenceJson + [Environment]::NewLine)
    }
    $EvidenceJson
}
finally {
    $env:LOCALAPPDATA = $PreviousLocalAppData
    $env:NVT_READY_PROBE_BEHAVIOR = $PreviousBehavior
    $env:NVT_FW_COMBINER_LAUNCHER_READY_PIPE_HANDLE = $PreviousLauncherReadyHandle
    $env:NVT_FW_COMBINER_EXPECTED_LAUNCHER_READY = $PreviousLauncherReadyExpected
    foreach ($Snapshot in $SourcePackageLockSnapshots.GetEnumerator()) {
        [IO.File]::WriteAllBytes($Snapshot.Key, [byte[]]$Snapshot.Value)
    }
    if (Test-Path -LiteralPath $SmokeRoot -PathType Container) {
        for ($Attempt = 0; $Attempt -lt 5 -and (Test-Path -LiteralPath $SmokeRoot -PathType Container); $Attempt++) {
            Start-Sleep -Milliseconds 200
            Remove-Item -LiteralPath $SmokeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
