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
$PublishRoot = Join-Path $RepositoryRoot "artifacts/immutable-bootstrap-publish-$PID"
$Bootstrap = Join-Path $PublishRoot 'NvtFwCombiner.Bootstrap.exe'
$PublishedBootstrap = Join-Path $PublishRoot 'NvtFwCombiner.LauncherBootstrap.exe'
$SourcePackageLockSnapshots = @{}
Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'src') -Filter 'packages.lock.json' -File -Recurse |
    ForEach-Object { $SourcePackageLockSnapshots[$_.FullName] = [IO.File]::ReadAllBytes($_.FullName) }
try {
    & dotnet publish `
        (Join-Path $RepositoryRoot 'src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj') `
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
        throw "Immutable Bootstrap publish failed with exit code $LASTEXITCODE."
    }
    Move-Item -LiteralPath $PublishedBootstrap -Destination $Bootstrap
    & python `
        (Join-Path $RepositoryRoot 'scripts/create_managed_installation_lab.py') `
        --source $UpdateSource `
        --bootstrap $Bootstrap `
        --output $Output
    if ($LASTEXITCODE -ne 0) {
        throw "Managed installation lab creation failed with exit code $LASTEXITCODE."
    }
}
finally {
    foreach ($Snapshot in $SourcePackageLockSnapshots.GetEnumerator()) {
        [IO.File]::WriteAllBytes($Snapshot.Key, [byte[]]$Snapshot.Value)
    }
    if (Test-Path -LiteralPath $PublishRoot -PathType Container) {
        Remove-Item -LiteralPath $PublishRoot -Recurse -Force
    }
}
