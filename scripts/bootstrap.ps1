[CmdletBinding()]
param([switch]$SkipRestore)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'install-dotnet.ps1') -Scope Repository
$dotnet = Join-Path $repoRoot '.dotnet/dotnet.exe'
if (-not $SkipRestore) {
    & $dotnet restore (Join-Path $repoRoot 'NvtFwCombiner.slnx')
}
& $dotnet build (Join-Path $repoRoot 'NvtFwCombiner.slnx') -c Debug --no-restore
