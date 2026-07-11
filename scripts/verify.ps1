[CmdletBinding()]
param(
    [switch]$All,
    [switch]$StructureOnly,
    [switch]$SkipDotNet,
    [switch]$SkipPython,
    [switch]$SkipStructure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Arguments = @('scripts/verify.py')
if ($All) { $Arguments += '--all' }
if ($StructureOnly) { $Arguments += '--structure-only' }
if ($SkipDotNet) { $Arguments += '--skip-dotnet' }
if ($SkipPython) { $Arguments += '--skip-python' }
if ($SkipStructure) { $Arguments += '--skip-structure' }

$Root = Split-Path -Parent $PSScriptRoot
Push-Location $Root
try {
    & python @Arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
