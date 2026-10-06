[CmdletBinding()]
param([switch]$SkipRestore)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'install-dotnet.ps1') -Scope Repository
$dotnet = Join-Path $repoRoot '.dotnet/dotnet.exe'
if (-not $SkipRestore) {
    $python = (Get-Command python -ErrorAction Stop).Source
    & $python -B (Join-Path $repoRoot 'scripts/fetch_core_packages.py') --manifest (Join-Path $repoRoot 'core-packages.json')
    if ($LASTEXITCODE -ne 0) { throw 'Core package download or verification failed before restore.' }
    & $dotnet restore (Join-Path $repoRoot 'NvtFwCombiner.slnx')
}
& $dotnet build (Join-Path $repoRoot 'NvtFwCombiner.slnx') -c Debug --no-restore
