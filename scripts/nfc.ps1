[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Task = 'verify'
)

$AllowedTasks = @('bootstrap', 'structure', 'verify', 'package-policy', 'host-diagnostic')
if ($Task -notin $AllowedTasks) {
    throw "[toolchain:invocation] Unknown task '$Task'. Expected one of: $($AllowedTasks -join ', ')."
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    $PowerShell7 = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $PowerShell7) {
        throw '[toolchain:dependency] PowerShell 7 is required. Install the approved PowerShell 7 runtime, then run scripts/nfc.ps1 again from any PowerShell host.'
    }

    & $PowerShell7.Source -NoLogo -NoProfile -File $PSCommandPath -Task $Task
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ($Task -eq 'host-diagnostic') {
    Write-Output "toolchain-host=pwsh;version=$($PSVersionTable.PSVersion)"
    exit 0
}

$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$DotNet = Join-Path $RepositoryRoot '.dotnet/dotnet.exe'
$PythonCommand = Get-Command python -ErrorAction SilentlyContinue

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Step,
        [ValidateSet('assertion', 'dependency', 'evidence')]
        [string]$FailureClass = 'assertion'
    )

    Write-Host "> $Executable $($Arguments -join ' ')"
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "[toolchain:$FailureClass`:$Step] Command failed with exit code $LASTEXITCODE."
    }
}

function Initialize-Toolchain {
    if ($null -eq $PythonCommand) {
        throw '[toolchain:dependency] Python 3 was not found on PATH.'
    }

    & (Join-Path $PSScriptRoot 'install-dotnet.ps1') -Scope Repository
    if (-not (Test-Path -LiteralPath $DotNet -PathType Leaf)) {
        throw "[toolchain:environment] Repository .NET executable was not created at $DotNet."
    }

    $env:DOTNET_ROOT = Split-Path -Parent $DotNet
    $env:PATH = "$env:DOTNET_ROOT$([IO.Path]::PathSeparator)$env:PATH"
}

Push-Location $RepositoryRoot
try {
    switch ($Task) {
        'bootstrap' {
            Initialize-Toolchain
            Invoke-Checked -Executable $PythonCommand.Source `
                -Arguments @('-m', 'pip', 'install', '--disable-pip-version-check', '-e', './tools/crc-worker[dev,package]') `
                -Step 'bootstrap-python' `
                -FailureClass 'dependency'
        }
        'structure' {
            if ($null -eq $PythonCommand) {
                throw '[toolchain:dependency] Python 3 was not found on PATH.'
            }
            Invoke-Checked -Executable $PythonCommand.Source `
                -Arguments @('scripts/verify.py', '--structure-only') `
                -Step 'structure'
        }
        'verify' {
            Initialize-Toolchain
            & $PythonCommand.Source -c 'import coverage, pylint, pyright, pytest, ruff' 2>$null
            if ($LASTEXITCODE -ne 0) {
                Invoke-Checked -Executable $PythonCommand.Source `
                    -Arguments @('-m', 'pip', 'install', '--disable-pip-version-check', '-e', './tools/crc-worker[dev]') `
                    -Step 'verify-dependencies' `
                    -FailureClass 'dependency'
            }
            Invoke-Checked -Executable $PythonCommand.Source `
                -Arguments @('scripts/verify.py', '--all') `
                -Step 'verify'
        }
        'package-policy' {
            Invoke-Checked -Executable (Get-Command pwsh -ErrorAction Stop).Source `
                -Arguments @('-NoLogo', '-NoProfile', '-File', './scripts/package.ps1', '-Version', '0.0.0', '-Commit', ('0' * 40), '-ExternalToolPolicyDryRun') `
                -Step 'package-policy' `
                -FailureClass 'evidence'
        }
    }
}
catch {
    if ($_.Exception.Message.StartsWith('[toolchain:', [StringComparison]::Ordinal)) {
        Write-Error $_.Exception.Message
    }
    else {
        Write-Error "[toolchain:environment:$Task] $($_.Exception.Message)"
    }
    exit 1
}
finally {
    Pop-Location
}
