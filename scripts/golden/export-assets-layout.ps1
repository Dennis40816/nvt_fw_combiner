param(
    [string]$Destination = '',
    [string]$Root = (Join-Path $PSScriptRoot '../..'),
    [string]$BaselinePath = (Join-Path $PSScriptRoot '../ci/test-baseline.json')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
try {
    if (-not $Destination) { throw 'Golden export failed.' }
    & python (Join-Path $PSScriptRoot 'golden_layout.py') export --root $Root `
        --baseline $BaselinePath --directory $Destination 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Golden export failed.' }
    exit 0
} catch {
    [Console]::Error.WriteLine('Golden export failed.')
    exit 1
}
