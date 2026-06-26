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
$dotnetExe = Join-Path $InstallDir 'dotnet.exe'
$InstallerArchitecture = if ($Architecture -eq 'auto') { '<auto>' } else { $Architecture }

function Test-RequiredSdk {
    param([string]$Executable)
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
        return $false
    }
    $installed = & $Executable --list-sdks 2>$null
    return [bool]($installed | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + '\s') })
}

if (-not $Force -and (Test-RequiredSdk -Executable $dotnetExe)) {
    Write-Host ".NET SDK $sdkVersion is already installed at $InstallDir"
} else {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("nfc-dotnet-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    try {
        $installer = Join-Path $tempRoot 'dotnet-install.ps1'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing $InstallerUri -OutFile $installer
        & $installer -Version $sdkVersion -InstallDir $InstallDir -Architecture $InstallerArchitecture -NoPath
        if ($LASTEXITCODE -ne 0) {
            throw "Microsoft dotnet-install.ps1 failed with exit code $LASTEXITCODE."
        }
    } finally {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-RequiredSdk -Executable $dotnetExe)) {
    throw ".NET SDK $sdkVersion was not found after installation."
}

$env:DOTNET_ROOT = $InstallDir
$env:PATH = "$InstallDir$([System.IO.Path]::PathSeparator)$env:PATH"

if ($PersistUserPath) {
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $InstallDir, 'User')
    $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
    $parts = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($parts -notcontains $InstallDir) {
        [Environment]::SetEnvironmentVariable('PATH', (($parts + $InstallDir) -join ';'), 'User')
    }
}

Write-Host "Installed .NET SDK: $(& $dotnetExe --version)"
Write-Host "DOTNET_ROOT: $InstallDir"
Write-Host "Current shell PATH has been updated."
