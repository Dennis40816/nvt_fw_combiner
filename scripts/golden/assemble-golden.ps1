param(
    [string]$Mode = 'InTree',
    [string]$Source = '',
    [string]$Root = (Join-Path $PSScriptRoot '../..'),
    [string]$BaselinePath = (Join-Path $PSScriptRoot '../ci/test-baseline.json')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$failure = 'Golden assembly failed.'
$cloneDirectory = $null
$runnerRoot = $null
$keyFile = $null
$previousEnvironment = @{}
$assemblyFailed = $false

function Assert-RegularPath([string]$Path) {
    $current = [System.IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw 'Golden assembly failed.'
            }
        }
        $current = [System.IO.Path]::GetDirectoryName($current)
    }
}

function Assert-TrustedEvent {
    # Allow-list: a local run has no event name. Every other event refuses before a key or network use.
    $trustedEvents = @('', 'push', 'workflow_dispatch', 'schedule', 'merge_group', 'pull_request')
    if (([string]$env:GITHUB_EVENT_NAME) -cnotin $trustedEvents -or $env:GITHUB_ACTOR -like 'dependabot*') {
        $script:failure = 'unverified, cannot merge'
        throw $script:failure
    }
    if ($env:GITHUB_EVENT_NAME -eq 'pull_request') {
        $script:failure = 'unverified, cannot merge'
        if (-not $env:GITHUB_EVENT_PATH -or -not $env:GITHUB_REPOSITORY) { throw $script:failure }
        $event = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
        if ($event.pull_request.head.repo.full_name -cne $env:GITHUB_REPOSITORY -or
            $event.pull_request.user.login -eq 'dependabot[bot]') { throw $script:failure }
        $script:failure = 'Golden assembly failed.'
    }
}

function Invoke-QuietGit([string[]]$Arguments) {
    $null = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Golden assembly failed.' }
}

try {
    if ($Mode -eq 'Clone' -and $env:NFC_ASSETS_KEY_FILE) {
        # Named before any refusal, so the cleanup below also removes the key after an early failure.
        $keyFile = [System.IO.Path]::GetFullPath($env:NFC_ASSETS_KEY_FILE)
    }
    if ($args.Count -ne 0 -or $Mode -notin @('InTree', 'FromDirectory', 'Clone') -or
        ($Mode -eq 'FromDirectory' -and -not $Source) -or
        ($Mode -ne 'FromDirectory' -and $Source)) {
        $failure = 'Golden assembly arguments are invalid.'
        throw $failure
    }
    $pin = (Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json).pin
    if ($pin.assetsRepo -cne 'Dennis40816/nvt-testdata' -or
        $pin.assetsPath -cne 'nfc' -or
        $pin.assetsCommit -cnotmatch '\A[0-9a-f]{40}\z') { throw $failure }
    if ($Mode -ne 'InTree') {
        Assert-TrustedEvent
        if ($pin.assetsCommit -ceq ('0' * 40)) {
            $failure = 'pin is not bound to a private commit'
            throw $failure
        }
    }
    if ($Mode -eq 'InTree') {
        & python (Join-Path $PSScriptRoot 'golden_pin.py') verify --root $Root --baseline $BaselinePath 2>$null
        if ($LASTEXITCODE -ne 0) { throw $failure }
    } else {
        if ($Mode -eq 'Clone') {
            if (-not $env:NFC_ASSETS_KEY_FILE) {
                $failure = 'Assets key variable is missing.'
                throw $failure
            }
            if (-not $env:RUNNER_TEMP -or -not [System.IO.Path]::IsPathRooted($env:RUNNER_TEMP)) {
                $failure = 'Runner temporary directory is invalid.'
                throw $failure
            }
            $runnerRoot = [System.IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\', '/')
            Assert-RegularPath $runnerRoot
            if (-not (Test-Path -LiteralPath $runnerRoot -PathType Container)) { throw $failure }
            $keyFile = [System.IO.Path]::GetFullPath($env:NFC_ASSETS_KEY_FILE)
            Assert-RegularPath $keyFile
            if ($keyFile -match '["\r\n$`]' -or -not (Test-Path -LiteralPath $keyFile -PathType Leaf)) { throw $failure }
            $cloneDirectory = Join-Path $runnerRoot ('nfc-assets-' + [guid]::NewGuid().ToString('N'))
            $traceNames = @(Get-ChildItem Env: | Where-Object { $_.Name -like 'GIT_TRACE*' } |
                ForEach-Object { $_.Name })
            foreach ($name in @($traceNames + @('GIT_CURL_VERBOSE', 'GIT_SSH_COMMAND', 'GIT_SSH',
                'GIT_TERMINAL_PROMPT', 'GIT_ASKPASS', 'SSH_ASKPASS', 'GIT_CONFIG_COUNT',
                'GIT_CONFIG_PARAMETERS', 'GIT_DIR', 'GIT_WORK_TREE'))) {
                $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
                [Environment]::SetEnvironmentVariable($name, $null)
            }
            $env:GIT_TERMINAL_PROMPT = '0'
            # Git runs this command through its own sh, which finds Git's bundled ssh first. Use the Windows OpenSSH
            # client when it exists: it reads the key file with Windows ACL rules.
            $openSsh = (Join-Path ([Environment]::GetFolderPath('System')) 'OpenSSH/ssh.exe').Replace('\', '/')
            $sshProgram = if (Test-Path -LiteralPath $openSsh -PathType Leaf) { '"' + $openSsh + '"' } else { 'ssh' }
            $env:GIT_SSH_COMMAND = $sshProgram + ' -i "' + $keyFile + '" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes'
            Invoke-QuietGit -Arguments @('clone', '--filter=blob:none', '--no-checkout', '--sparse',
                ('git@github.com:' + $pin.assetsRepo + '.git'), $cloneDirectory)
            Invoke-QuietGit -Arguments @('-C', $cloneDirectory, 'sparse-checkout', 'set', '--no-cone', ('/' + $pin.assetsPath + '/'))
            Invoke-QuietGit -Arguments @('-C', $cloneDirectory, 'fetch', '--depth=1', 'origin', $pin.assetsCommit)
            Invoke-QuietGit -Arguments @('-C', $cloneDirectory, 'checkout', '--detach', $pin.assetsCommit)
            $actualCommit = (& git -C $cloneDirectory rev-parse HEAD 2>$null)
            if ($LASTEXITCODE -ne 0 -or $actualCommit -cne $pin.assetsCommit) { throw $failure }
            $Source = $cloneDirectory
        }
        & python (Join-Path $PSScriptRoot 'golden_layout.py') assemble --root $Root `
            --baseline $BaselinePath --directory $Source 2>$null
        if ($LASTEXITCODE -ne 0) { throw $failure }
    }
} catch {
    [Console]::Error.WriteLine($failure)
    $assemblyFailed = $true
} finally {
    try {
        if ($cloneDirectory) {
            $target = [System.IO.Path]::GetFullPath($cloneDirectory)
            $prefix = $runnerRoot + [System.IO.Path]::DirectorySeparatorChar
            if (-not $target.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                [System.IO.Path]::GetFileName($target) -notmatch '^nfc-assets-[0-9a-f]{32}$') {
                throw 'Golden assembly cleanup failed.'
            }
            Assert-RegularPath $target
            if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
        }
    } catch {
        [Console]::Error.WriteLine('Golden assembly cleanup failed.')
        $assemblyFailed = $true
    } finally {
        try {
            if ($keyFile) {
                Assert-RegularPath $keyFile
                if (Test-Path -LiteralPath $keyFile -PathType Leaf) { Remove-Item -LiteralPath $keyFile -Force }
            }
        } catch {
            [Console]::Error.WriteLine('Assets key cleanup failed.')
            $assemblyFailed = $true
        }
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
        }
    }
}
if ($assemblyFailed) { exit 1 }
exit 0
