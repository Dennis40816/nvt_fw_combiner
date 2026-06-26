[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Owner = 'Dennis40816',
    [string]$Repository = 'nvt_fw_combiner',
    [switch]$SkipTagPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Version = (Get-Content -LiteralPath (Join-Path $RepoRoot 'VERSION') -Raw).Trim()
$Tag = "v$Version"
$FullName = "$Owner/$Repository"

foreach ($Command in @('git', 'gh')) {
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        throw "$Command is required and was not found on PATH."
    }
}

Push-Location $RepoRoot
try {
    & git rev-parse --is-inside-work-tree | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The repository is not initialized.' }
    if (& git status --porcelain) { throw 'Refusing to publish a dirty worktree.' }
    & gh auth status
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI authentication is required.' }

    $Head = (& git rev-parse HEAD).Trim()
    $TagCommit = (& git rev-list -n 1 $Tag 2>$null).Trim()
    if ($TagCommit -ne $Head) {
        throw "Annotated tag $Tag must exist at HEAD before publishing."
    }

    & gh repo view $FullName --json nameWithOwner 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        if ($PSCmdlet.ShouldProcess($FullName, 'Create private GitHub repository and push main')) {
            & gh repo create $FullName --private --source . --remote origin --push `
                --description 'Profile-driven firmware image composition desktop utility.'
            if ($LASTEXITCODE -ne 0) { throw 'GitHub repository creation failed.' }
        }
    }
    else {
        $RemoteUrl = "https://github.com/$FullName.git"
        & git remote get-url origin 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { & git remote set-url origin $RemoteUrl }
        else { & git remote add origin $RemoteUrl }
        if ($PSCmdlet.ShouldProcess($FullName, 'Push main')) {
            & git push --set-upstream origin main
            if ($LASTEXITCODE -ne 0) { throw 'Pushing main failed.' }
        }
    }

    if (-not $SkipTagPush -and $PSCmdlet.ShouldProcess($FullName, "Push $Tag")) {
        & git push origin $Tag
        if ($LASTEXITCODE -ne 0) { throw "Pushing $Tag failed." }
    }
}
finally {
    Pop-Location
}
