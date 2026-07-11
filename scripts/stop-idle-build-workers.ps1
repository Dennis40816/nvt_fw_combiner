[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedRoot = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$escapedRoot = [regex]::Escape($resolvedRoot)
$collectors = @(
    Get-CimInstance Win32_Process | Where-Object {
        $_.Name -ieq 'dotnet.exe' -and
        $_.CommandLine -match $escapedRoot -and
        $_.CommandLine -match 'Avalonia\.BuildServices\.Collector\.dll'
    }
)

foreach ($collector in $collectors) {
    if ($PSCmdlet.ShouldProcess(
            "Avalonia BuildServices collector PID $($collector.ProcessId)",
            'Stop idle repository build worker')) {
        Stop-Process -Id $collector.ProcessId -Force
    }
}

Write-Verbose "Stopped $($collectors.Count) idle Avalonia build worker(s) for $resolvedRoot."
