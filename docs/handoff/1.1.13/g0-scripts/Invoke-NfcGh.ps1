#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Owner,
    [Parameter(Mandatory)][string]$TargetRepository,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][long]$InstallationId,
    [Parameter(Mandatory)][string]$DpapiPath,
    [Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$GhArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$token = $null
try {
    if (-not $IsWindows) { throw 'Windows is required.' }
    $helper = [Diagnostics.ProcessStartInfo]::new()
    $helper.FileName = (Get-Command pwsh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $helper.UseShellExecute = $false
    $helper.CreateNoWindow = $true
    $helper.RedirectStandardOutput = $true
    $helper.RedirectStandardError = $true
    foreach ($arg in @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'nfc-app-token-helper.ps1'),
        '-Mode', 'token', '-Owner', $Owner, '-Repo', $TargetRepository, '-ClientId', $ClientId,
        '-InstallationId', [string]$InstallationId, '-DpapiPath', $DpapiPath)) {
        [void]$helper.ArgumentList.Add($arg)
    }
    $proc = [Diagnostics.Process]::Start($helper)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $token = $outTask.GetAwaiter().GetResult()
        $null = $errTask.GetAwaiter().GetResult()
        if ($proc.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($token) -or
            $token -match '[\r\n]') { throw 'Could not obtain an installation token.' }
    } finally { $proc.Dispose() }

    $gh = [Diagnostics.ProcessStartInfo]::new()
    $gh.FileName = (Get-Command gh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $gh.UseShellExecute = $false
    $gh.CreateNoWindow = $true
    $gh.RedirectStandardOutput = $true
    $gh.RedirectStandardError = $true
    $gh.Environment['GH_TOKEN'] = $token
    foreach ($arg in $GhArguments) { [void]$gh.ArgumentList.Add($arg) }
    $proc = [Diagnostics.Process]::Start($gh)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $stdout = $outTask.GetAwaiter().GetResult().Replace($token, '[redacted]')
        $stderr = $errTask.GetAwaiter().GetResult().Replace($token, '[redacted]')
        [Console]::Out.Write($stdout)
        [Console]::Error.Write($stderr)
        exit $proc.ExitCode
    } finally { $proc.Dispose() }
} catch {
    [Console]::Error.WriteLine('NFC gh wrapper failed. Ask the owner to check the installed helper and gh configuration.')
    exit 1
} finally { $token = $null }
