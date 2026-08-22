[CmdletBinding()]
param(
    [string]$UpdateSource,
    [string]$Output,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($UpdateSource)) {
    $UpdateSource = Join-Path $RepositoryRoot 'artifacts/version-update-source-lab'
}
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $RepositoryRoot 'artifacts/managed-installation-lab'
}
$PublishRoot = Join-Path $RepositoryRoot "artifacts/managed-launcher-publish-$PID"
$Launcher = Join-Path $PublishRoot 'NvtFwCombiner.Launcher.exe'
try {
    & dotnet publish `
        (Join-Path $RepositoryRoot 'src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj') `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=true `
        -p:TrimMode=partial `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $PublishRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Stable launcher publish failed with exit code $LASTEXITCODE."
    }
    & python `
        (Join-Path $RepositoryRoot 'scripts/create_managed_installation_lab.py') `
        --source $UpdateSource `
        --launcher $Launcher `
        --output $Output
    if ($LASTEXITCODE -ne 0) {
        throw "Managed installation lab creation failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $PublishRoot -PathType Container) {
        Remove-Item -LiteralPath $PublishRoot -Recurse -Force
    }
}
