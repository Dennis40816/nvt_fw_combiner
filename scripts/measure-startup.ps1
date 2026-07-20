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
    [string]$Page = 'home'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$TracePathEnvironmentVariable = 'NFC_STARTUP_TRACE_PATH'

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

    $matches = @($Trace.stages | Where-Object { $_.name -eq $Name })
    if ($matches.Count -ne 1) {
        throw "Startup trace must contain exactly one '$Name' stage."
    }

    return $matches[0]
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
    $milliseconds = [double]$end.elapsedMilliseconds - [double]$start.elapsedMilliseconds
    if ($milliseconds -lt 0) {
        throw "Startup trace UI-thread interval '$Name' has reversed stages."
    }

    return [ordered]@{
        name = $Name
        startStage = $StartStage
        endStage = $EndStage
        milliseconds = [Math]::Round($milliseconds, 3)
    }
}

function Get-UiThreadWorkSummary {
    param([Parameter(Mandatory = $true)]$Trace)

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

function Invoke-StartupSample {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$TracePath,
        [Parameter(Mandatory = $true)][string]$StartupPage,
        [Parameter(Mandatory = $true)][int]$Timeout
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
        while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $Timeout) {
            Start-Sleep -Milliseconds 10
            $process.Refresh()
            $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, $process.WorkingSet64)
            if ($windowMilliseconds -eq 0 -and $process.MainWindowHandle -ne 0) {
                $windowMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
                $workingSetBytesAtWindow = $process.WorkingSet64
            }
            if ($traceReadyMilliseconds -eq 0 -and (Test-Path -LiteralPath $TracePath -PathType Leaf)) {
                $traceReadyMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
                $workingSetBytesAtTrace = $process.WorkingSet64
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

        $trace = Get-Content -LiteralPath $TracePath -Raw | ConvertFrom-Json
        if ($trace.schemaVersion -ne 'nfc-startup-trace-v2' -or @($trace.stages).Count -eq 0) {
            throw "The application wrote an unsupported or empty startup trace at '$TracePath'."
        }

        return [ordered]@{
            processId = $process.Id
            processToWindowMilliseconds = [Math]::Round($windowMilliseconds, 3)
            processToTraceMilliseconds = [Math]::Round($traceReadyMilliseconds, 3)
            workingSetBytesAtWindow = $workingSetBytesAtWindow
            workingSetBytesAtTrace = $workingSetBytesAtTrace
            peakWorkingSetBytes = $peakWorkingSetBytes
            uiThreadWork = Get-UiThreadWorkSummary -Trace $trace
            trace = $trace
        }
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
        $warmups += Invoke-StartupSample $application $tracePath $Page $TimeoutSeconds
    }

    $samples = @()
    for ($index = 0; $index -lt $Runs; $index++) {
        $tracePath = Join-Path $traceRoot "run-$index.json"
        $samples += Invoke-StartupSample $application $tracePath $Page $TimeoutSeconds
    }

    $expectedStageNames = @($samples[0].trace.stages | ForEach-Object { $_.name })
    $expectedStageSequence = $expectedStageNames -join '|'
    foreach ($sample in $samples) {
        $actualStageSequence = @($sample.trace.stages | ForEach-Object { $_.name }) -join '|'
        if ($actualStageSequence -ne $expectedStageSequence) {
            throw 'Measured startup traces do not contain the same ordered stage sequence.'
        }
    }

    $stageSummaries = @(
        foreach ($stageName in $expectedStageNames) {
            $stagePoints = @(
                foreach ($sample in $samples) {
                    @($sample.trace.stages | Where-Object { $_.name -eq $stageName })[0]
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

    $openedStage = @($stageSummaries | Where-Object { $_.name -eq 'main-window.opened' })[0]
    $warmupStage = @($stageSummaries | Where-Object { $_.name -eq 'startup-warmup.completed' })[0]
    if ($null -eq $openedStage -or $null -eq $warmupStage) {
        throw 'Startup measurement did not observe both first-frame and completed background warm-up stages.'
    }

    $result = [ordered]@{
        schemaVersion = 'nfc-startup-measurement-v2'
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
            workingSetBytesAtWindow = Get-MetricSummary @($samples.workingSetBytesAtWindow)
            workingSetBytesAtTrace = Get-MetricSummary @($samples.workingSetBytesAtTrace)
            peakWorkingSetBytes = Get-MetricSummary @($samples.peakWorkingSetBytes)
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
