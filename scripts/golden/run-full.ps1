param([string]$Shard = 'all', [string]$Source = '', [switch]$Keep)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
try {
    if (-not $env:NFC_TEST_AREA_ROOT) {
        [Console]::Error.WriteLine('NFC_TEST_AREA_ROOT is not set.')
        exit 1
    }
    $area = [System.IO.Path]::GetFullPath($env:NFC_TEST_AREA_ROOT)
    $scratch = Join-Path $area 'temp'
    if (-not [System.IO.Path]::IsPathRooted($env:NFC_TEST_AREA_ROOT) -or
        -not (Test-Path -LiteralPath $scratch -PathType Container) -or
        $Shard -notin @('bootstrap', 'ui', 'core', 'all')) { throw 'Golden full run failed.' }
    $env:TEMP = $scratch
    $env:TMP = $scratch
    $env:TMPDIR = $scratch
    $arguments = @((Join-Path $PSScriptRoot 'run_full.py'), '--shard', $Shard)
    if ($Source) { $arguments += @('--source', $Source) }
    if ($Keep) { $arguments += '--keep' }
    & python @arguments
    exit $LASTEXITCODE
} catch {
    [Console]::Error.WriteLine('Golden full run failed.')
    exit 1
}
