#Requires -Version 7.4
# Runs the offline G0 suite in its own pwsh process. That process inherits no GitHub host, repository
# or credential variable, no Git credential prompt variable and no Bitwarden session from this one,
# and its TEMP, TMP and TMPDIR point to the temp folder of the user-level NFC_TEST_AREA_ROOT.
# Only Pester 3.4.0 is supported: its FailedCount includes setup, cleanup and block failures. Newer
# versions report those separately, so any other requested version exits with 2.
# Exit code: 0 when Pester 3.4.0 ran at least one test and none failed; 1 when a test failed; 2 when the
# run itself failed (another or missing Pester version, no valid result, no executed test).
[CmdletBinding()]
param([string]$PesterVersion = '3.4.0', [string]$TestPath = (Join-Path $PSScriptRoot 'NfcG0.Tests.ps1'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    $supported = [version]'3.4.0'
    $requested = $null
    if (-not [version]::TryParse($PesterVersion, [ref]$requested) -or $requested -ne $supported) {
        throw "Only Pester $supported is supported; '$PesterVersion' was requested."
    }
    $root = [Environment]::GetEnvironmentVariable('NFC_TEST_AREA_ROOT', 'User')
    if ([string]::IsNullOrWhiteSpace($root) -or -not [IO.Path]::IsPathRooted($root)) {
        throw 'Set the user-level NFC_TEST_AREA_ROOT first (CONTRIBUTING.md).'
    }
    $temp = Join-Path $root 'temp'
    if (-not [IO.Directory]::Exists($temp)) { throw "The test-area temp folder does not exist: $temp" }
    if (-not [IO.File]::Exists($TestPath)) { throw "The test script does not exist: $TestPath" }

    $psi = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh -CommandType Application | Select-Object -First 1).Source)
    $psi.UseShellExecute = $false
    foreach ($name in @($psi.Environment.Keys)) {
        if ($name -match '^(GH_|GITHUB_|GCM_|BW_|GIT_CONFIG_)' -or $name -in @('GIT_ASKPASS', 'SSH_ASKPASS', 'GIT_TERMINAL_PROMPT')) {
            [void]$psi.Environment.Remove($name)
        }
    }
    foreach ($name in @('TEMP', 'TMP', 'TMPDIR')) { $psi.Environment[$name] = $temp }
    $psi.Environment['NFC_G0_PESTER_VERSION'] = $supported.ToString()
    $psi.Environment['NFC_G0_TESTS'] = [IO.Path]::GetFullPath($TestPath)
    $command = @'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
try {
    $expected = [version]$env:NFC_G0_PESTER_VERSION
    Import-Module -Name Pester -RequiredVersion $expected -Force
    $loaded = @(Get-Module -Name Pester)
    if ($loaded.Count -ne 1 -or $loaded[0].Version -ne $expected) { throw "Pester $expected is not the loaded version." }
    $result = Invoke-Pester -Script $env:NFC_G0_TESTS -PassThru
    if ($null -eq $result) { throw 'Pester returned no result.' }
    $total = $result.TotalCount -as [int]
    $passed = $result.PassedCount -as [int]
    $failed = $result.FailedCount -as [int]
    if ($null -eq $total -or $null -eq $passed -or $null -eq $failed -or ($passed + $failed) -lt 1 -or
        ($passed + $failed) -gt $total) { throw 'Pester returned no valid result with at least one executed test.' }
    if ($failed -gt 0) {
        [Console]::Error.WriteLine("NFC G0 tests: $failed of $total failed.")
        exit 1
    }
    exit 0
} catch {
    [Console]::Error.WriteLine("NFC G0 test runner failed: $($_.Exception.Message)")
    exit 2
}
'@
    foreach ($arg in @('-NoProfile', '-NonInteractive', '-Command', $command)) { [void]$psi.ArgumentList.Add($arg) }
    $proc = [Diagnostics.Process]::Start($psi)
    try {
        $proc.WaitForExit()
        $code = $proc.ExitCode
    } finally { $proc.Dispose() }
    if ($code -notin @(0, 1)) { $code = 2 }
    exit $code
} catch {
    [Console]::Error.WriteLine("NFC G0 test runner failed: $($_.Exception.Message)")
    exit 2
}
