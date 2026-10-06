#Requires -Version 7.4
# Usage: pwsh -NoProfile -File <this file> -Repo OWNER/REPO -Base BASE -Head HEAD -Title TITLE
#        -BodyFile FILE -ClientId CLIENT_ID -InstallationId ID -DpapiPath PATH [-Draft] [-ExpectedAuthor LOGIN]
# Parse options ourselves so every usage error, including missing or unknown options, exits with 64.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Split-NfcPrArguments {
    param([AllowEmptyString()][AllowEmptyCollection()][string[]]$Arguments)
    $required = @('Repo', 'Base', 'Head', 'Title', 'BodyFile', 'ClientId', 'InstallationId', 'DpapiPath')
    $options = @{}
    for ($i = 0; $i -lt $Arguments.Count; $i++) {
        $name = $Arguments[$i] -replace '^-', ''
        if (-not $Arguments[$i].StartsWith('-') -or $name -notin ($required + @('Draft', 'ExpectedAuthor'))) {
            throw "Unknown option '$($Arguments[$i])'."
        }
        if ($options.ContainsKey($name)) { throw "Option -$name is given more than once." }
        if ($name -eq 'Draft') { $options[$name] = $true; continue }
        if (++$i -ge $Arguments.Count -or [string]::IsNullOrWhiteSpace($Arguments[$i]) -or
            $Arguments[$i].StartsWith('-') -or $Arguments[$i] -match '[\x00-\x1F\x7F]') {
            throw "Option -$name needs a nonblank value without control characters or a leading hyphen."
        }
        $options[$name] = $Arguments[$i]
    }
    foreach ($name in $required) {
        if (-not $options.ContainsKey($name)) { throw "Option -$name is required." }
    }
    # These are the exact owner, repository and installation ID rules of Invoke-NfcGh.ps1.
    if ($options.Repo -cnotmatch '^[A-Za-z0-9-]+/[A-Za-z0-9_.-]+\z') {
        throw 'Option -Repo must name one GitHub repository as owner/name.'
    }
    if ($options.InstallationId -cnotmatch '^[1-9][0-9]{0,17}\z') {
        throw 'Option -InstallationId must be a positive number.'
    }
    if ($options.Base -match '\s' -or $options.Head -match '\s') { throw 'Base and Head cannot contain whitespace.' }
    if (-not [IO.File]::Exists($options.BodyFile)) { throw 'Option -BodyFile must name an existing file.' }
    $options.BodyFile = [IO.Path]::GetFullPath($options.BodyFile)
    if (-not $options.ContainsKey('Draft')) { $options.Draft = $false }
    if (-not $options.ContainsKey('ExpectedAuthor')) { $options.ExpectedAuthor = 'app/nfc-agent-dennis40816' }
    if ($options.ExpectedAuthor -cnotmatch '^(?:app/)?[A-Za-z0-9-]+(?:\[bot\])?\z') {
        throw 'Option -ExpectedAuthor must be a GitHub author login.'
    }
    return $options
}

function Invoke-NfcPrWrapper {
    # Offline tests replace this function; production always starts the sibling wrapper as its own process.
    param([string[]]$Arguments)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Get-Command pwsh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void]$psi.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($psi)
    try {
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return @{ ExitCode = $process.ExitCode; Output = $outputTask.GetAwaiter().GetResult(); Error = $errorTask.GetAwaiter().GetResult() }
    } finally { $process.Dispose() }
}

try { $options = Split-NfcPrArguments -Arguments $args }
catch {
    [Console]::Error.WriteLine("NFC open-pr usage error: $($_.Exception.Message)")
    exit 64
}

try {
    $owner, $repository = $options.Repo.Split('/')
    $prefix = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'Invoke-NfcGh.ps1'),
        '-Owner', $owner, '-Repo', $repository, '-ClientId', $options.ClientId,
        '-InstallationId', $options.InstallationId, '-DpapiPath', $options.DpapiPath, '--')
    $createArguments = @('pr', 'create', '--repo', $options.Repo, '--base', $options.Base,
        '--head', $options.Head, '--title', $options.Title, '--body-file', $options.BodyFile)
    if ($options.Draft) { $createArguments += '--draft' }
    $created = Invoke-NfcPrWrapper -Arguments ($prefix + $createArguments)
    if ($created.ExitCode -ne 0) {
        [Console]::Error.WriteLine($created.Output)
        [Console]::Error.WriteLine($created.Error)
        exit 1
    }
    $urls = [regex]::Matches($created.Output,
        '(?m)^https://github\.com/' + [regex]::Escape($options.Repo) + '/pull/([1-9][0-9]*)\r?$')
    if ($urls.Count -ne 1) { throw 'pr create did not return one identifiable pull request URL; inspect its result before retrying.' }
    $url = $urls[0].Value.TrimEnd("`r")
    $number = $urls[0].Groups[1].Value
    $actual = '<unreadable>'
    try {
        $view = Invoke-NfcPrWrapper -Arguments ($prefix + @('pr', 'view', $number, '--repo', $options.Repo, '--json', 'author,url,number'))
        if ($view.ExitCode -eq 0) {
            $pr = ConvertFrom-Json -InputObject $view.Output -AsHashtable
            if ($pr -is [Collections.IDictionary] -and $pr['author'] -is [Collections.IDictionary] -and
                $pr['author']['login'] -is [string] -and -not [string]::IsNullOrWhiteSpace($pr['author']['login']) -and
                [string]::Equals([string]$pr['number'], $number, [StringComparison]::Ordinal) -and
                [string]::Equals([string]$pr['url'], $url, [StringComparison]::Ordinal)) {
                $actual = $pr['author']['login']
            }
        }
    } catch { $actual = '<unreadable>' }
    if ([string]::Equals($actual, $options.ExpectedAuthor, [StringComparison]::Ordinal)) {
        [Console]::Out.WriteLine($url)
        exit 0
    }
    $reason = "PR author verification failed: expected '$($options.ExpectedAuthor)', actual '$actual'; $url. Closing this pull request."
    [Console]::Error.WriteLine($reason)
    $closed = Invoke-NfcPrWrapper -Arguments ($prefix + @('pr', 'close', $number, '--repo', $options.Repo, '--comment', $reason))
    if ($closed.ExitCode -ne 0) {
        [Console]::Error.WriteLine('Could not close the pull request; close it manually before retrying.')
        [Console]::Error.WriteLine($closed.Output)
        [Console]::Error.WriteLine($closed.Error)
    }
    exit 1
} catch {
    [Console]::Error.WriteLine("NFC open-pr failed: $($_.Exception.Message)")
    exit 1
}
