#Requires -Version 7.4
# Usage: pwsh -NoProfile -File <this file> -Owner OWNER -Repo REPO -ClientId CLIENT_ID -InstallationId ID
#            -DpapiPath PATH [-IncludeWorkflowsWrite] [--] <gh arguments>
# There is deliberately no param block. PowerShell would bind gh options such as --repo to the
# wrapper's own parameters and split arguments such as --head=owner:branch, so the wrapper reads
# its own process command line and passes the gh arguments to gh exactly as given.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Split-NfcGhCommandLine {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$CommandLine,
          [Parameter(Mandatory)][string]$ScriptPath)
    # Only the PowerShell start-up options are read, and the first execution mode decides: -NoProfile is
    # required, and the mode must be -File with the fully qualified path of this script, so no profile runs
    # and no working directory is consulted. -Command, another script, a relative path or any other
    # start-up option is refused, and nothing after the first mode is searched for a second -File.
    $launch = 'Start the wrapper as its own process: pwsh -NoProfile [-NonInteractive] [-NoLogo] -File <fully qualified wrapper path> <wrapper options> -- <gh arguments>.'
    $i = 1
    $noProfile = $false
    while ($i -lt $CommandLine.Count -and $CommandLine[$i] -in @('-NoProfile', '-NonInteractive', '-NoLogo')) {
        if ($CommandLine[$i] -eq '-NoProfile') { $noProfile = $true }
        $i++
    }
    if (-not $noProfile -or $i + 1 -ge $CommandLine.Count -or $CommandLine[$i] -notin @('-File', '-f') -or
        -not [IO.Path]::IsPathFullyQualified($CommandLine[$i + 1])) { throw $launch }
    $full = $null
    try { $full = [IO.Path]::GetFullPath($CommandLine[$i + 1]) } catch { throw $launch }
    if (-not [string]::Equals($full, $ScriptPath, [StringComparison]::OrdinalIgnoreCase)) { throw $launch }
    $start = $i + 2
    $valueOptions = @('Owner', 'Repo', 'ClientId', 'InstallationId', 'DpapiPath')
    $parsed = @{ IncludeWorkflowsWrite = $false }
    $i = $start
    while ($i -lt $CommandLine.Count) {
        $arg = $CommandLine[$i]
        if ($arg -ceq '--') { $i++; break }
        if (-not $arg.StartsWith([char]'-')) { break }
        $name = $arg.Substring(1)
        if ($name -eq 'IncludeWorkflowsWrite') {
            if ($parsed.IncludeWorkflowsWrite) { throw 'Wrapper option -IncludeWorkflowsWrite is given more than once.' }
            $parsed.IncludeWorkflowsWrite = $true
            $i++
            continue
        }
        $option = @($valueOptions | Where-Object { $_ -eq $name })
        if ($option.Count -ne 1) {
            throw "Unknown wrapper option '$arg'. Wrapper options come first; put -- before gh arguments that begin with an option."
        }
        $key = $option[0]
        if ($parsed.ContainsKey($key)) { throw "Wrapper option -$key is given more than once." }
        if ($i + 1 -ge $CommandLine.Count -or [string]::IsNullOrEmpty($CommandLine[$i + 1]) -or
            $CommandLine[$i + 1].StartsWith([char]'-')) { throw "Wrapper option -$key needs a value." }
        $parsed[$key] = $CommandLine[$i + 1]
        $i += 2
    }
    foreach ($key in $valueOptions) {
        if (-not $parsed.ContainsKey($key)) { throw "Wrapper option -$key is required." }
    }
    if ($parsed.Owner -notmatch '^[A-Za-z0-9-]+\z' -or $parsed.Repo -notmatch '^[A-Za-z0-9_.-]+\z') {
        throw 'Wrapper options -Owner and -Repo must name one GitHub repository.'
    }
    if ($parsed.InstallationId -notmatch '^[1-9][0-9]{0,17}\z') {
        throw 'Wrapper option -InstallationId must be a positive number.'
    }
    if ($i -ge $CommandLine.Count) { throw 'No gh arguments were given.' }
    $parsed.GhArguments = [string[]]@($CommandLine[$i..($CommandLine.Count - 1)])
    return $parsed
}

function Assert-NfcStandaloneInvocation {
    # The process must have run this script itself: a call from another script, a profile, a command or
    # dot-sourcing has another origin or another frame on the call stack.
    param([Parameter(Mandatory)][string]$CommandOrigin,
          [Parameter(Mandatory)][AllowEmptyString()][AllowEmptyCollection()][string[]]$CallStackScripts,
          [Parameter(Mandatory)][string]$ScriptPath)
    if ($CommandOrigin -cne 'Runspace' -or $CallStackScripts.Count -ne 1 -or
        -not [string]::Equals($CallStackScripts[0], $ScriptPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Start the wrapper as its own process: pwsh -NoProfile [-NonInteractive] [-NoLogo] -File <fully qualified wrapper path> <wrapper options> -- <gh arguments>.'
    }
}

function Assert-NfcGhRepository {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$GhArguments,
          [Parameter(Mandatory)][string]$Owner, [Parameter(Mandatory)][string]$Repo,
          [AllowNull()][AllowEmptyString()][string]$EnvironmentRepo)
    $expected = "$Owner/$Repo"
    $hint = "The wrapper's token is limited to $expected. Omit the option (gh then uses the repository " +
        "of the working directory) or pass exactly '--repo $expected'. Pass a value that begins with -R " +
        'as --option=value.'
    if (-not [string]::IsNullOrEmpty($EnvironmentRepo) -and $EnvironmentRepo -cne $expected) {
        throw "GH_REPO names another repository. The wrapper's token is limited to $expected; unset GH_REPO or set it to exactly $expected."
    }
    for ($i = 0; $i -lt $GhArguments.Count; $i++) {
        $arg = $GhArguments[$i]
        $value = $null
        if ($arg -ceq '--repo' -or $arg -ceq '-R') {
            if ($i + 1 -ge $GhArguments.Count) { throw "gh option $arg has no value. $hint" }
            $i++
            $value = $GhArguments[$i]
        } elseif ($arg.StartsWith('--repo=', [StringComparison]::Ordinal)) {
            $value = $arg.Substring(7)
        } elseif ($arg.StartsWith('-R', [StringComparison]::Ordinal)) {
            $value = $arg.Substring(2)
            if ($value.StartsWith([char]'=')) { $value = $value.Substring(1) }
        } elseif ($arg -cmatch '^-[^-]' -and $arg.Contains('R')) {
            throw "gh argument '$arg' may select a repository inside a group of short options. $hint"
        }
        if ($null -ne $value -and $value -cne $expected) {
            throw "gh argument '$arg' selects the repository '$value'. $hint"
        }
    }
}

function Assert-NfcGhSubcommand {
    # Alias shell commands and extensions run as children of gh with GH_TOKEN, beyond the exact-string redaction.
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$GhArguments)
    if ($GhArguments.Count -gt 0 -and $GhArguments[0] -cin @('alias', 'extension', 'extensions', 'ext')) {
        throw "gh $($GhArguments[0]) is refused: its commands would run with the wrapper's token."
    }
}

$callStackScripts = @(Get-PSCallStack | ForEach-Object { [string]$_.ScriptName })
try {
    Assert-NfcStandaloneInvocation -CommandOrigin ([string]$MyInvocation.CommandOrigin) `
        -CallStackScripts $callStackScripts -ScriptPath $PSCommandPath
    $parsed = Split-NfcGhCommandLine -CommandLine ([Environment]::GetCommandLineArgs()) -ScriptPath $PSCommandPath
    Assert-NfcGhRepository -GhArguments $parsed.GhArguments -Owner $parsed.Owner -Repo $parsed.Repo `
        -EnvironmentRepo $env:GH_REPO
    Assert-NfcGhSubcommand -GhArguments $parsed.GhArguments
} catch {
    [Console]::Error.WriteLine("NFC gh wrapper usage error: $($_.Exception.Message)")
    exit 64
}
$Owner = $parsed.Owner
$Repo = $parsed.Repo
$ClientId = $parsed.ClientId
$InstallationId = $parsed.InstallationId
$DpapiPath = $parsed.DpapiPath
$GhArguments = $parsed.GhArguments

$token = $null
try {
    if (-not $IsWindows) { throw 'Windows is required.' }
    $helper = [Diagnostics.ProcessStartInfo]::new()
    $helper.FileName = (Get-Command pwsh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $helper.UseShellExecute = $false
    $helper.CreateNoWindow = $true
    $helper.RedirectStandardOutput = $true
    $helper.RedirectStandardError = $true
    $helperArguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'nfc-app-token-helper.ps1'),
        '-Mode', 'token', '-Owner', $Owner, '-Repo', $Repo, '-ClientId', $ClientId,
        '-InstallationId', $InstallationId, '-DpapiPath', $DpapiPath)
    if ($parsed.IncludeWorkflowsWrite) { $helperArguments += '-IncludeWorkflowsWrite' }
    foreach ($arg in $helperArguments) {
        [void]$helper.ArgumentList.Add($arg)
    }
    $proc = [Diagnostics.Process]::Start($helper)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $token = $outTask.GetAwaiter().GetResult()
        $null = $errTask.GetAwaiter().GetResult()
        if ($proc.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($token) -or
            $token -match '[\r\n]') { throw 'Could not obtain an installation token.' }
    } finally { $proc.Dispose() }

    $gh = [Diagnostics.ProcessStartInfo]::new()
    $gh.FileName = (Get-Command gh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $gh.UseShellExecute = $false
    $gh.CreateNoWindow = $true
    $gh.RedirectStandardOutput = $true
    $gh.RedirectStandardError = $true
    # gh writes UTF-8; the console code page would corrupt non-ASCII text and could hide the token from redaction.
    $utf8 = [Text.UTF8Encoding]::new($false)
    $gh.StandardOutputEncoding = $utf8
    $gh.StandardErrorEncoding = $utf8
    $gh.Environment['GH_TOKEN'] = $token
    foreach ($arg in $GhArguments) { [void]$gh.ArgumentList.Add($arg) }
    $proc = [Diagnostics.Process]::Start($gh)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        $stdout = $outTask.GetAwaiter().GetResult().Replace($token, '[redacted]')
        $stderr = $errTask.GetAwaiter().GetResult().Replace($token, '[redacted]')
        foreach ($pair in @(@([Console]::OpenStandardOutput(), $stdout), @([Console]::OpenStandardError(), $stderr))) {
            $bytes = $utf8.GetBytes($pair[1])
            $pair[0].Write($bytes, 0, $bytes.Length)
            $pair[0].Flush()
        }
        exit $proc.ExitCode
    } finally { $proc.Dispose() }
} catch {
    [Console]::Error.WriteLine('NFC gh wrapper failed. Ask the owner to check the installed helper and gh configuration.')
    exit 1
} finally { $token = $null }
