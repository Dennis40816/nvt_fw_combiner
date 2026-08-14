[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApplicationPath,

    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\startup-measurements\startup-measurement.json'),

    [ValidateRange(0, 10)]
    [int]$WarmupRuns = 1,

    [ValidateRange(1, 30)]
    [int]$Runs = 5,

    [ValidateRange(3, 60)]
    [int]$TimeoutSeconds = 15,

    [ValidateSet('home', 'settings', 'merge', 'replace', 'hex-editor')]
    [string]$Page = 'home',

    [switch]$RequirePreloadLifecycle
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$TracePathEnvironmentVariable = 'NFC_STARTUP_TRACE_PATH'

function Assert-ReleaseSampleCounts {
    param(
        [Parameter(Mandatory = $true)][bool]$Required,
        [Parameter(Mandatory = $true)][int]$Warmups,
        [Parameter(Mandatory = $true)][int]$ScoredRuns
    )

    if ($Required -and ($Warmups -lt 1 -or $ScoredRuns -lt 5)) {
        throw 'Release preload evidence requires at least one warm-up and five scored launches.'
    }
}

function Assert-ReleaseStartupPage {
    param(
        [Parameter(Mandatory = $true)][bool]$Required,
        [Parameter(Mandatory = $true)][string]$StartupPage
    )

    if ($Required -and -not [StringComparer]::Ordinal.Equals($StartupPage, 'home')) {
        throw "Release preload evidence requires the exact lowercase 'home' startup page."
    }
}

function New-StartupMeasurementValidation {
    param([Parameter(Mandatory = $true)][bool]$Required)

    return [pscustomobject][ordered]@{
        mode = if ($Required) { 'preload-release' } else { 'standard' }
        releaseAdmissionPassed = $Required
    }
}

function Test-OrdinalValue {
    param(
        [AllowNull()][string]$Value,
        [Parameter(Mandatory = $true)][string[]]$Allowed
    )

    foreach ($candidate in $Allowed) {
        if ([StringComparer]::Ordinal.Equals($Value, $candidate)) {
            return $true
        }
    }
    return $false
}

function Test-OrdinalSequence {
    param(
        [Parameter(Mandatory = $true)][object[]]$Actual,
        [Parameter(Mandatory = $true)][object[]]$Expected
    )

    if ($Actual.Count -ne $Expected.Count) {
        return $false
    }
    for ($index = 0; $index -lt $Actual.Count; $index++) {
        if (-not [StringComparer]::Ordinal.Equals(
                [string]$Actual[$index],
                [string]$Expected[$index])) {
            return $false
        }
    }
    return $true
}

function ConvertTo-ValidatedWorkCount {
    param(
        [AllowNull()]$Value,
        [Parameter(Mandatory = $true)][string]$StageId
    )

    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [string] -or $Value -is [bool] -or $Value -is [char] -or
        $Value -isnot [IConvertible]) {
        throw "Startup trace contains non-integral preload work for '$StageId'."
    }
    try {
        $number = [Convert]::ToDecimal($Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "Startup trace contains non-integral preload work for '$StageId'."
    }
    if ($number -ne [Math]::Truncate($number) -or
        $number -lt [long]::MinValue -or $number -gt [long]::MaxValue) {
        throw "Startup trace contains non-integral preload work for '$StageId'."
    }
    return [long]$number
}

function Get-MetricSummary {
    param([Parameter(Mandatory = $true)][double[]]$Values)

    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    $median = if (($sorted.Count % 2) -eq 0) {
        ($sorted[$middle - 1] + $sorted[$middle]) / 2
    }
    else {
        $sorted[$middle]
    }

    return [ordered]@{
        minimum = [Math]::Round($sorted[0], 3)
        median = [Math]::Round($median, 3)
        maximum = [Math]::Round($sorted[-1], 3)
    }
}

function Get-TraceStage {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $matches = @($Trace.stages | Where-Object {
            [StringComparer]::Ordinal.Equals([string]$_.name, $Name)
        })
    if ($matches.Count -ne 1) {
        throw "Startup trace must contain exactly one '$Name' stage."
    }

    return $matches[0]
}

function ConvertTo-ValidatedElapsedMilliseconds {
    param(
        [AllowNull()]$Value,
        [Parameter(Mandatory = $true)][string]$StageName
    )

    if ($null -eq $Value -or $Value -is [string] -or $Value -is [bool] -or
        $Value -is [char] -or $Value -isnot [IConvertible]) {
        throw "Startup trace contains invalid elapsed time for '$StageName'."
    }
    try {
        $number = [Convert]::ToDouble($Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "Startup trace contains invalid elapsed time for '$StageName'."
    }
    if (-not [double]::IsFinite($number) -or $number -lt 0) {
        throw "Startup trace contains invalid elapsed time for '$StageName'."
    }
    return $number
}

function Assert-ReleaseTraceProcessId {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][int]$ExpectedProcessId
    )

    $property = $Trace.PSObject.Properties['processId']
    try {
        $actualProcessId = if ($null -eq $property) {
            $null
        }
        else {
            ConvertTo-ValidatedWorkCount -Value $property.Value -StageId 'process-id'
        }
    }
    catch {
        throw 'Release startup trace contains an invalid process ID.'
    }
    if ($null -eq $actualProcessId -or $actualProcessId -ne $ExpectedProcessId) {
        throw 'Release startup trace does not belong to the measured process.'
    }
}

function Assert-ReleaseTraceTerminal {
    param([Parameter(Mandatory = $true)]$Trace)

    $terminalNames = @(
        'startup-warmup.completed'
        'startup-warmup.failed'
        'startup-warmup.cancelled'
    )
    $terminals = @($Trace.stages | Where-Object {
            Test-OrdinalValue -Value ([string]$_.name) -Allowed $terminalNames
        })
    $stages = @($Trace.stages)
    if ($terminals.Count -ne 1 -or
        -not [StringComparer]::Ordinal.Equals(
            [string]$terminals[0].name,
            'startup-warmup.completed') -or
        -not [StringComparer]::Ordinal.Equals(
            [string]$stages[-1].name,
            'startup-warmup.completed')) {
        throw "Release startup trace must end with exactly one 'startup-warmup.completed' terminal."
    }
}

function New-UiThreadWorkInterval {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$StartStage,
        [Parameter(Mandatory = $true)][string]$EndStage
    )

    $start = Get-TraceStage -Trace $Trace -Name $StartStage
    $end = Get-TraceStage -Trace $Trace -Name $EndStage
    $startElapsed = ConvertTo-ValidatedElapsedMilliseconds `
        -Value $start.elapsedMilliseconds -StageName $StartStage
    $endElapsed = ConvertTo-ValidatedElapsedMilliseconds `
        -Value $end.elapsedMilliseconds -StageName $EndStage
    $milliseconds = $endElapsed - $startElapsed
    if ($milliseconds -lt 0) {
        throw "Startup trace UI-thread interval '$Name' has reversed stages."
    }

    return [pscustomobject][ordered]@{
        name = $Name
        startStage = $StartStage
        endStage = $EndStage
        milliseconds = [Math]::Round($milliseconds, 3)
    }
}

function Get-UiThreadWorkSummary {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][bool]$RequireLifecycle
    )

    $firstFrameIntervals = @(
        New-UiThreadWorkInterval `
            -Trace $Trace `
            -Name 'application-xaml' `
            -StartStage 'application-xaml.started' `
            -EndStage 'application-xaml.ready'
        New-UiThreadWorkInterval `
            -Trace $Trace `
            -Name 'framework-initialization-to-window-assignment' `
            -StartStage 'framework-initialization.started' `
            -EndStage 'main-window.assigned'
    )

    $backgroundStartStages = @(
        $Trace.stages |
            Where-Object {
                ([string]$_.name).StartsWith('startup-warmup.', [StringComparison]::Ordinal) -and
                ([string]$_.name).EndsWith('.started', [StringComparison]::Ordinal)
            }
    )
    if ($backgroundStartStages.Count -eq 0) {
        throw 'Startup trace has no background UI materialization intervals.'
    }

    if ($RequireLifecycle) {
        $expectedStarts = @(
            'startup-warmup.device-context.started'
            'startup-warmup.replace-view.started'
            'startup-warmup.merge-view.started'
            'startup-warmup.settings-view.started'
            'startup-warmup.hex-editor-view.started'
        )
        $actualStarts = @($backgroundStartStages | ForEach-Object { [string]$_.name })
        if (-not (Test-OrdinalSequence -Actual $actualStarts -Expected $expectedStarts)) {
            throw 'Release startup trace does not contain all five ordered deferred-view intervals.'
        }

        $expectedMarkers = @(
            foreach ($startName in $expectedStarts) {
                $startName
                "$($startName.Substring(0, $startName.Length - '.started'.Length)).ready"
            }
        )
        $actualMarkers = @($Trace.stages | Where-Object {
                Test-OrdinalValue -Value ([string]$_.name) -Allowed $expectedMarkers
            } | ForEach-Object { [string]$_.name })
        if (-not (Test-OrdinalSequence -Actual $actualMarkers -Expected $expectedMarkers)) {
            throw 'Release startup trace does not contain serial deferred-view markers.'
        }

        $previousReadyElapsed = $null
        foreach ($startName in $expectedStarts) {
            $readyName = "$($startName.Substring(0, $startName.Length - '.started'.Length)).ready"
            $startElapsed = ConvertTo-ValidatedElapsedMilliseconds `
                -Value (Get-TraceStage -Trace $Trace -Name $startName).elapsedMilliseconds `
                -StageName $startName
            $readyElapsed = ConvertTo-ValidatedElapsedMilliseconds `
                -Value (Get-TraceStage -Trace $Trace -Name $readyName).elapsedMilliseconds `
                -StageName $readyName
            if ($null -ne $previousReadyElapsed -and $startElapsed -lt $previousReadyElapsed) {
                throw 'Release startup trace contains overlapping deferred-view intervals.'
            }
            $previousReadyElapsed = $readyElapsed
        }
    }

    $backgroundIntervals = @(
        foreach ($start in $backgroundStartStages) {
            $startName = [string]$start.name
            $intervalName = $startName.Substring(
                'startup-warmup.'.Length,
                $startName.Length - 'startup-warmup.'.Length - '.started'.Length)
            New-UiThreadWorkInterval `
                -Trace $Trace `
                -Name $intervalName `
                -StartStage $startName `
                -EndStage "$($startName.Substring(0, $startName.Length - '.started'.Length)).ready"
        }
    )

    $firstFrameTotal = ($firstFrameIntervals | Measure-Object -Property milliseconds -Sum).Sum
    $firstFrameMaximum = ($firstFrameIntervals | Measure-Object -Property milliseconds -Maximum).Maximum
    $backgroundTotal = ($backgroundIntervals | Measure-Object -Property milliseconds -Sum).Sum
    $backgroundMaximum = ($backgroundIntervals | Measure-Object -Property milliseconds -Maximum).Maximum
    return [ordered]@{
        firstFrame = [ordered]@{
            totalMilliseconds = [Math]::Round($firstFrameTotal, 3)
            maximumIntervalMilliseconds = [Math]::Round($firstFrameMaximum, 3)
            intervals = $firstFrameIntervals
        }
        background = [ordered]@{
            totalMilliseconds = [Math]::Round($backgroundTotal, 3)
            maximumIntervalMilliseconds = [Math]::Round($backgroundMaximum, 3)
            intervals = $backgroundIntervals
        }
    }
}

function Get-PreloadLifecycleEvidence {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][bool]$Required
    )

    $stages = @(
        if ([StringComparer]::Ordinal.Equals(
                [string]$Trace.schemaVersion,
                'nfc-startup-trace-v3')) {
            $Trace.preloadStages
        }
    )
    if ($Required -and $stages.Count -eq 0) {
        throw 'Startup trace does not contain the required preload lifecycle evidence.'
    }
    if ($stages.Count -eq 0) {
        return $null
    }

    if ($Required) {
        $actualIds = @($stages | ForEach-Object { [string]$_.id })
        $expectedIds = @(
            'canonical-catalog'
            'report-history'
            'system-diagnostics'
            'external-environment'
            'deferred-views'
        )
        if (-not (Test-OrdinalSequence -Actual $actualIds -Expected $expectedIds)) {
            throw 'Startup trace does not contain the complete ordered preload stage set.'
        }
    }

    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $terminalStates = @('Succeeded', 'Failed', 'Skipped', 'Cancelled', 'DependencyBlocked')
    $normalized = @(
        for ($index = 0; $index -lt $stages.Count; $index++) {
            $stage = $stages[$index]
            $id = [string]$stage.id
            $state = [string]$stage.state
            $completed = ConvertTo-ValidatedWorkCount -Value $stage.completedWork -StageId $id
            $total = ConvertTo-ValidatedWorkCount -Value $stage.totalWork -StageId $id
            if ([string]::IsNullOrWhiteSpace($id) -or -not $ids.Add($id) -or
                -not (Test-OrdinalValue -Value $state -Allowed $terminalStates) -or
                ($Required -and -not [StringComparer]::Ordinal.Equals($state, 'Succeeded')) -or
                (($null -eq $completed) -ne ($null -eq $total)) -or
                ($null -ne $completed -and ($completed -lt 0 -or $total -lt 0 -or
                    $completed -gt $total -or
                    ([StringComparer]::Ordinal.Equals($state, 'Succeeded') -and
                        $completed -ne $total))) -or
                ($Required -and [StringComparer]::Ordinal.Equals($id, 'deferred-views') -and
                    ($completed -ne 5 -or $total -ne 5))) {
                throw "Startup trace contains invalid preload lifecycle evidence for '$id'."
            }
            [pscustomobject][ordered]@{
                id = $id
                state = $state
                completedWork = $completed
                totalWork = $total
            }
        }
    )
    return [pscustomobject][ordered]@{
        stageCount = $stages.Count
        stages = $normalized
    }
}

function New-StartupSampleEvidence {
    param(
        [Parameter(Mandatory = $true)]$Trace,
        [Parameter(Mandatory = $true)][bool]$RequireLifecycle,
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][double]$WindowMilliseconds,
        [Parameter(Mandatory = $true)][double]$TraceReadyMilliseconds,
        [Parameter(Mandatory = $true)][long]$WorkingSetBytesAtWindow,
        [Parameter(Mandatory = $true)][long]$WorkingSetBytesAtTrace,
        [Parameter(Mandatory = $true)][long]$PeakWorkingSetBytes,
        [Parameter(Mandatory = $true)][long]$PrivateBytesAtWindow,
        [Parameter(Mandatory = $true)][long]$PrivateBytesAtTrace,
        [Parameter(Mandatory = $true)][long]$PeakPrivateBytes
    )

    if (-not (Test-OrdinalValue `
            -Value ([string]$Trace.schemaVersion) `
            -Allowed @('nfc-startup-trace-v2', 'nfc-startup-trace-v3')) -or
        @($Trace.stages).Count -eq 0) {
        throw 'The application wrote an unsupported or empty startup trace.'
    }
    $preloadLifecycle = Get-PreloadLifecycleEvidence -Trace $Trace -Required $RequireLifecycle
    $catalogReadyAfterWindow = $null
    if ($RequireLifecycle) {
        Assert-ReleaseTraceProcessId -Trace $Trace -ExpectedProcessId $ProcessId
        Assert-ReleaseTraceTerminal -Trace $Trace
        $opened = Get-TraceStage -Trace $Trace -Name 'main-window.opened'
        $catalogReady = Get-TraceStage -Trace $Trace -Name 'startup-warmup.catalog-state.applied'
        $completed = Get-TraceStage -Trace $Trace -Name 'startup-warmup.completed'
        $openedElapsed = ConvertTo-ValidatedElapsedMilliseconds `
            -Value $opened.elapsedMilliseconds -StageName 'main-window.opened'
        $catalogElapsed = ConvertTo-ValidatedElapsedMilliseconds `
            -Value $catalogReady.elapsedMilliseconds `
            -StageName 'startup-warmup.catalog-state.applied'
        $completedElapsed = ConvertTo-ValidatedElapsedMilliseconds `
            -Value $completed.elapsedMilliseconds -StageName 'startup-warmup.completed'
        if ($catalogElapsed -lt $openedElapsed -or $completedElapsed -lt $catalogElapsed) {
            throw 'Release startup trace contains reversed catalog-ready lifecycle milestones.'
        }
        $catalogReadyAfterWindow = [Math]::Round($catalogElapsed - $openedElapsed, 3)
    }

    return [ordered]@{
        processId = $ProcessId
        processToWindowMilliseconds = [Math]::Round($WindowMilliseconds, 3)
        processToTraceMilliseconds = [Math]::Round($TraceReadyMilliseconds, 3)
        catalogReadyAfterWindowMilliseconds = $catalogReadyAfterWindow
        workingSetBytesAtWindow = $WorkingSetBytesAtWindow
        workingSetBytesAtTrace = $WorkingSetBytesAtTrace
        peakWorkingSetBytes = $PeakWorkingSetBytes
        privateBytesAtWindow = $PrivateBytesAtWindow
        privateBytesAtTrace = $PrivateBytesAtTrace
        peakPrivateBytes = $PeakPrivateBytes
        uiThreadWork = Get-UiThreadWorkSummary -Trace $Trace -RequireLifecycle $RequireLifecycle
        preloadLifecycle = $preloadLifecycle
        trace = $Trace
    }
}

function Invoke-StartupSample {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$TracePath,
        [Parameter(Mandatory = $true)][string]$StartupPage,
        [Parameter(Mandatory = $true)][int]$Timeout,
        [Parameter(Mandatory = $true)][bool]$RequireLifecycle
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = Split-Path -Parent $Executable
    $startInfo.UseShellExecute = $false
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.Arguments = "--page $StartupPage"
    $startInfo.EnvironmentVariables[$TracePathEnvironmentVariable] = $TracePath

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw 'The startup measurement process could not be created.'
    }

    try {
        [double]$windowMilliseconds = 0
        [double]$traceReadyMilliseconds = 0
        [long]$workingSetBytesAtWindow = 0
        [long]$workingSetBytesAtTrace = 0
        [long]$peakWorkingSetBytes = 0
        [long]$privateBytesAtWindow = 0
        [long]$privateBytesAtTrace = 0
        [long]$peakPrivateBytes = 0
        $trace = $null
        while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $Timeout) {
            Start-Sleep -Milliseconds 10
            $process.Refresh()
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, $process.WorkingSet64)
            $peakPrivateBytes = [Math]::Max($peakPrivateBytes, $process.PrivateMemorySize64)
            if ($windowMilliseconds -eq 0 -and $process.MainWindowHandle -ne 0) {
                $windowMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
                $workingSetBytesAtWindow = $process.WorkingSet64
                $privateBytesAtWindow = $process.PrivateMemorySize64
            }
            if ($traceReadyMilliseconds -eq 0 -and (Test-Path -LiteralPath $TracePath -PathType Leaf)) {
                try {
                    $parsedTrace = Get-Content -LiteralPath $TracePath -Raw | ConvertFrom-Json -ErrorAction Stop
                    if ($null -ne $parsedTrace) {
                        $trace = $parsedTrace
                        $traceReadyMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
                        $workingSetBytesAtTrace = $process.WorkingSet64
                        $privateBytesAtTrace = $process.PrivateMemorySize64
                    }
                }
                catch {
                    $trace = $null
                }
            }
            if ($windowMilliseconds -ne 0 -and $traceReadyMilliseconds -ne 0) {
                break
            }
        }

        if ($process.HasExited) {
            throw "The application exited during startup with code $($process.ExitCode)."
        }
        if ($windowMilliseconds -eq 0) {
            throw "The application did not expose a main window within $Timeout seconds."
        }
        if ($traceReadyMilliseconds -eq 0) {
            throw "The application did not write its startup trace within $Timeout seconds."
        }

        return New-StartupSampleEvidence `
            -Trace $trace `
            -RequireLifecycle $RequireLifecycle `
            -ProcessId $process.Id `
            -WindowMilliseconds $windowMilliseconds `
            -TraceReadyMilliseconds $traceReadyMilliseconds `
            -WorkingSetBytesAtWindow $workingSetBytesAtWindow `
            -WorkingSetBytesAtTrace $workingSetBytesAtTrace `
            -PeakWorkingSetBytes $peakWorkingSetBytes `
            -PrivateBytesAtWindow $privateBytesAtWindow `
            -PrivateBytesAtTrace $privateBytesAtTrace `
            -PeakPrivateBytes $peakPrivateBytes
    }
    finally {
        if (-not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(2000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
        $process.Dispose()
    }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

Assert-ReleaseSampleCounts `
    -Required $RequirePreloadLifecycle.IsPresent `
    -Warmups $WarmupRuns `
    -ScoredRuns $Runs
Assert-ReleaseStartupPage `
    -Required $RequirePreloadLifecycle.IsPresent `
    -StartupPage $Page

$application = (Resolve-Path -LiteralPath $ApplicationPath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
    throw "Application was not found at '$application'."
}

$measurementPath = [IO.Path]::GetFullPath($OutputPath)
$measurementDirectory = Split-Path -Parent $measurementPath
if ([string]::IsNullOrWhiteSpace($measurementDirectory)) {
    throw 'OutputPath must include a parent directory.'
}
$null = New-Item -ItemType Directory -Force -Path $measurementDirectory
if (Test-Path -LiteralPath $measurementPath) {
    throw "Startup measurement output already exists at '$measurementPath'."
}

$traceRoot = Join-Path ([IO.Path]::GetTempPath()) "nfc-startup-measurement-$([Guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $traceRoot
try {
    $warmups = @()
    for ($index = 0; $index -lt $WarmupRuns; $index++) {
        $tracePath = Join-Path $traceRoot "warmup-$index.json"
        $warmups += Invoke-StartupSample $application $tracePath $Page $TimeoutSeconds $RequirePreloadLifecycle.IsPresent
    }

    $samples = @()
    for ($index = 0; $index -lt $Runs; $index++) {
        $tracePath = Join-Path $traceRoot "run-$index.json"
        $samples += Invoke-StartupSample $application $tracePath $Page $TimeoutSeconds $RequirePreloadLifecycle.IsPresent
    }

    $expectedStageNames = @($samples[0].trace.stages | ForEach-Object { $_.name })
    foreach ($sample in $samples) {
        $actualStageNames = @($sample.trace.stages | ForEach-Object { $_.name })
        if (-not (Test-OrdinalSequence -Actual $actualStageNames -Expected $expectedStageNames)) {
            throw 'Measured startup traces do not contain the same ordered stage sequence.'
        }
    }
    $expectedPreloadLifecycle = $samples[0].preloadLifecycle | ConvertTo-Json -Compress -Depth 8
    foreach ($sample in $samples) {
        $actualPreloadLifecycle = $sample.preloadLifecycle | ConvertTo-Json -Compress -Depth 8
        if (-not [StringComparer]::Ordinal.Equals(
                $actualPreloadLifecycle,
                $expectedPreloadLifecycle)) {
            throw 'Measured startup traces do not contain the same preload lifecycle evidence.'
        }
    }

    $stageSummaries = @(
        foreach ($stageName in $expectedStageNames) {
            $stagePoints = @(
                foreach ($sample in $samples) {
                    @($sample.trace.stages | Where-Object {
                            [StringComparer]::Ordinal.Equals([string]$_.name, $stageName)
                        })[0]
                }
            )
            [ordered]@{
                name = $stageName
                elapsedMilliseconds = Get-MetricSummary @($stagePoints.elapsedMilliseconds)
                deltaMilliseconds = Get-MetricSummary @($stagePoints.deltaMilliseconds)
                allocatedBytesSinceManagedEntry = Get-MetricSummary @($stagePoints.allocatedBytesSinceManagedEntry)
                allocationDeltaBytes = Get-MetricSummary @($stagePoints.allocationDeltaBytes)
            }
        }
    )

    $openedStage = @($stageSummaries | Where-Object {
            [StringComparer]::Ordinal.Equals([string]$_.name, 'main-window.opened')
        })[0]
    $warmupStage = @($stageSummaries | Where-Object {
            [StringComparer]::Ordinal.Equals([string]$_.name, 'startup-warmup.completed')
        })[0]
    if ($null -eq $openedStage -or $null -eq $warmupStage) {
        throw 'Startup measurement did not observe both first-frame and completed background warm-up stages.'
    }

    $result = [ordered]@{
        schemaVersion = 'nfc-startup-measurement-v3'
        validation = New-StartupMeasurementValidation -Required $RequirePreloadLifecycle.IsPresent
        capturedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        applicationPath = $application
        page = $Page
        warmupRuns = $WarmupRuns
        measuredRuns = $Runs
        timeoutSeconds = $TimeoutSeconds
        warmups = $warmups
        samples = $samples
        summary = [ordered]@{
            processToWindowMilliseconds = Get-MetricSummary @($samples.processToWindowMilliseconds)
            processToTraceMilliseconds = Get-MetricSummary @($samples.processToTraceMilliseconds)
            firstWindowToCatalogReadyMilliseconds = if ($RequirePreloadLifecycle.IsPresent) {
                Get-MetricSummary @($samples.catalogReadyAfterWindowMilliseconds)
            }
            else {
                $null
            }
            workingSetBytesAtWindow = Get-MetricSummary @($samples.workingSetBytesAtWindow)
            workingSetBytesAtTrace = Get-MetricSummary @($samples.workingSetBytesAtTrace)
            peakWorkingSetBytes = Get-MetricSummary @($samples.peakWorkingSetBytes)
            privateBytesAtWindow = Get-MetricSummary @($samples.privateBytesAtWindow)
            privateBytesAtTrace = Get-MetricSummary @($samples.privateBytesAtTrace)
            peakPrivateBytes = Get-MetricSummary @($samples.peakPrivateBytes)
            preloadLifecycle = $samples[0].preloadLifecycle
            allocatedBytesAtWindow = $openedStage.allocatedBytesSinceManagedEntry
            allocatedBytesAfterWarmup = $warmupStage.allocatedBytesSinceManagedEntry
            firstFrameUiSynchronousWorkMilliseconds = Get-MetricSummary @(
                $samples | ForEach-Object { $_.uiThreadWork.firstFrame.totalMilliseconds }
            )
            backgroundUiMaterializationMilliseconds = Get-MetricSummary @(
                $samples | ForEach-Object { $_.uiThreadWork.background.totalMilliseconds }
            )
            maximumBackgroundUiMaterializationIntervalMilliseconds = Get-MetricSummary @(
                $samples | ForEach-Object { $_.uiThreadWork.background.maximumIntervalMilliseconds }
            )
            stages = $stageSummaries
        }
    }

    $json = $result | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($measurementPath, $json, [Text.UTF8Encoding]::new($false))

    Write-Host "Startup measurement: $measurementPath"
    Write-Host "Process to window median: $($result.summary.processToWindowMilliseconds.median) ms"
    Write-Host "Managed entry to opened median: $($openedStage.elapsedMilliseconds.median) ms"
    Write-Host "Managed entry to background warm-up median: $($warmupStage.elapsedMilliseconds.median) ms"
    Write-Host "Allocated bytes at first window median: $($result.summary.allocatedBytesAtWindow.median) bytes"
    Write-Host "Allocated bytes after background warm-up median: $($result.summary.allocatedBytesAfterWarmup.median) bytes"
    Write-Host "First-frame synchronous UI work median: $($result.summary.firstFrameUiSynchronousWorkMilliseconds.median) ms"
    Write-Host "Background UI materialization work median: $($result.summary.backgroundUiMaterializationMilliseconds.median) ms"
    Write-Host "Longest background UI materialization interval median: $($result.summary.maximumBackgroundUiMaterializationIntervalMilliseconds.median) ms"
    Write-Host "Working set at window median: $($result.summary.workingSetBytesAtWindow.median) bytes"
    Write-Host "Working set after background warm-up median: $($result.summary.workingSetBytesAtTrace.median) bytes"
    Write-Host "Peak working set during startup median: $($result.summary.peakWorkingSetBytes.median) bytes"
    Write-Host "Private bytes at window median: $($result.summary.privateBytesAtWindow.median) bytes"
    Write-Host "Private bytes after background warm-up median: $($result.summary.privateBytesAtTrace.median) bytes"
    Write-Host "Peak private bytes during startup median: $($result.summary.peakPrivateBytes.median) bytes"
    $stageSummaries | ForEach-Object {
        [pscustomobject]@{
            Stage = $_.name
            MedianElapsedMs = $_.elapsedMilliseconds.median
            MedianDeltaMs = $_.deltaMilliseconds.median
            MedianAllocatedBytes = $_.allocatedBytesSinceManagedEntry.median
            MedianAllocationDeltaBytes = $_.allocationDeltaBytes.median
        }
    } | Format-Table -AutoSize
}
finally {
    if (Test-Path -LiteralPath $traceRoot) {
        Remove-Item -LiteralPath $traceRoot -Recurse -Force
    }
}
