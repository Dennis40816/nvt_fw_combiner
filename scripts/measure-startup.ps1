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
        while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $Timeout) {
            Start-Sleep -Milliseconds 10
            $process.Refresh()
            if ($windowMilliseconds -eq 0 -and $process.MainWindowHandle -ne 0) {
                $windowMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }
            if ($traceReadyMilliseconds -eq 0 -and (Test-Path -LiteralPath $TracePath -PathType Leaf)) {
                $traceReadyMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
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
        if ($trace.schemaVersion -ne 'nfc-startup-trace-v1' -or @($trace.stages).Count -eq 0) {
            throw "The application wrote an unsupported or empty startup trace at '$TracePath'."
        }

        return [ordered]@{
            processId = $process.Id
            processToWindowMilliseconds = [Math]::Round($windowMilliseconds, 3)
            processToTraceMilliseconds = [Math]::Round($traceReadyMilliseconds, 3)
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
            }
        }
    )

    $result = [ordered]@{
        schemaVersion = 'nfc-startup-measurement-v1'
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
            stages = $stageSummaries
        }
    }

    $json = $result | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($measurementPath, $json, [Text.UTF8Encoding]::new($false))

    Write-Host "Startup measurement: $measurementPath"
    Write-Host "Process to window median: $($result.summary.processToWindowMilliseconds.median) ms"
    $openedStage = @($stageSummaries | Where-Object { $_.name -eq 'main-window.opened' })[0]
    $warmupStage = @($stageSummaries | Where-Object { $_.name -eq 'startup-warmup.completed' })[0]
    if ($null -eq $openedStage -or $null -eq $warmupStage) {
        throw 'Startup measurement did not observe both first-frame and completed background warm-up stages.'
    }
    Write-Host "Managed entry to opened median: $($openedStage.elapsedMilliseconds.median) ms"
    Write-Host "Managed entry to background warm-up median: $($warmupStage.elapsedMilliseconds.median) ms"
    $stageSummaries | ForEach-Object {
        [pscustomobject]@{
            Stage = $_.name
            MedianElapsedMs = $_.elapsedMilliseconds.median
            MedianDeltaMs = $_.deltaMilliseconds.median
        }
    } | Format-Table -AutoSize
}
finally {
    if (Test-Path -LiteralPath $traceRoot) {
        Remove-Item -LiteralPath $traceRoot -Recurse -Force
    }
}
