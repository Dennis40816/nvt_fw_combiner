[CmdletBinding()]
param(
    [ValidateSet('Repository', 'User')]
    [string]$Scope = 'Repository',

    [string]$InstallDir,

    [ValidateSet('auto', 'x64', 'x86', 'arm64')]
    [string]$Architecture = 'auto',

    [switch]$Force,
    [switch]$PersistUserPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$InstallerCommit = 'cbd31355adcf0c63eaeff601fb2eaa5fd0778f2b'
$InstallerUri = "https://raw.githubusercontent.com/dotnet/install-scripts/$InstallerCommit/src/dotnet-install.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
$globalJsonPath = Join-Path $repoRoot 'global.json'
if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    throw "global.json was not found at $globalJsonPath"
}

$globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
$sdkVersion = [string]$globalJson.sdk.version
if ($sdkVersion -notmatch '^10\.0\.[0-9]+$') {
    throw "global.json must pin a stable .NET 10 SDK; found '$sdkVersion'."
}

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = if ($Scope -eq 'Repository') {
        Join-Path $repoRoot '.dotnet'
    } else {
        Join-Path $HOME '.dotnet'
    }
}
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
$repositoryDotnetExe = Join-Path $InstallDir 'dotnet.exe'
$AutoArchitectureToken = '<auto>'

function Test-RequiredSdk {
    param([string]$Executable)
    if ([string]::IsNullOrWhiteSpace($Executable) -or -not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        return $false
    }
    $installed = & $Executable --list-sdks 2>$null
    return [bool]($installed | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + '\s') })
}

function Get-SystemDotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $null
    }
    return [string]$command.Source
}

$systemDotnet = Get-SystemDotnet
$selectedDotnet = $null

if (-not $Force -and (Test-RequiredSdk -Executable $repositoryDotnetExe)) {
    $selectedDotnet = $repositoryDotnetExe
    Write-Host ".NET SDK $sdkVersion is already installed at $InstallDir"
} elseif (-not $Force -and $Scope -ne 'Repository' -and (Test-RequiredSdk -Executable $systemDotnet)) {
    $selectedDotnet = $systemDotnet
    Write-Host ".NET SDK $sdkVersion is already available from system dotnet: $selectedDotnet"
} else {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("nfc-dotnet-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    try {
        $installer = Join-Path $tempRoot 'dotnet-install.ps1'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing $InstallerUri -OutFile $installer
        $installerArgs = @('-Version', $sdkVersion, '-InstallDir', $InstallDir, '-NoPath')
        if ($Architecture -ne 'auto') {
            $installerArgs += @('-Architecture', $Architecture)
        }
        # <auto> is wrapper-only; omit -Architecture so dotnet-install auto-detects.
        & $installer @installerArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Microsoft dotnet-install.ps1 failed with exit code $LASTEXITCODE."
        }
    } finally {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-RequiredSdk -Executable $repositoryDotnetExe) {
        $selectedDotnet = $repositoryDotnetExe
    } elseif ($Scope -ne 'Repository' -and (Test-RequiredSdk -Executable $systemDotnet)) {
        $selectedDotnet = $systemDotnet
    }
}

if (-not (Test-RequiredSdk -Executable $selectedDotnet)) {
    throw ".NET SDK $sdkVersion was not found after installation or system fallback."
}

$selectedDotnetDir = Split-Path -Parent $selectedDotnet
$env:DOTNET_ROOT = $selectedDotnetDir
$env:PATH = "$selectedDotnetDir$([System.IO.Path]::PathSeparator)$env:PATH"

if ($PersistUserPath -and $selectedDotnet -eq $repositoryDotnetExe) {
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $selectedDotnetDir, 'User')
    $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
    $parts = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($parts -notcontains $selectedDotnetDir) {
        [Environment]::SetEnvironmentVariable('PATH', (($parts + $selectedDotnetDir) -join ';'), 'User')
    }
}

Write-Host "Installed .NET SDK: $(& $selectedDotnet --version)"
Write-Host "DOTNET_ROOT: $selectedDotnetDir"
Write-Host "Current shell PATH has been updated."
