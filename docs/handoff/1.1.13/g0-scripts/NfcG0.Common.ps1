#Requires -Version 7.4
Set-StrictMode -Version Latest

function Assert-NfcRuntime {
    if (-not $IsWindows -or $PSVersionTable.PSVersion -lt [version]'7.4') {
        throw 'Windows and PowerShell 7.4 or later are required.'
    }
    foreach ($type in @('System.Security.Cryptography.ProtectedData',
        'System.IO.FileSystemAclExtensions', 'System.Threading.CancellationTokenSource')) {
        if (-not ($type -as [type])) { throw 'A required Windows or .NET API is unavailable.' }
    }
}

function Test-NfcRecordingPolicy {
    $roots = @(
        'HKLM:\SOFTWARE\Policies\Microsoft\Windows\PowerShell',
        'HKCU:\SOFTWARE\Policies\Microsoft\Windows\PowerShell',
        'HKLM:\SOFTWARE\Policies\Microsoft\PowerShellCore',
        'HKCU:\SOFTWARE\Policies\Microsoft\PowerShellCore'
    )
    $settings = @{
        ScriptBlockLogging = 'EnableScriptBlockLogging'
        ModuleLogging = 'EnableModuleLogging'
        Transcription = 'EnableTranscripting'
    }
    foreach ($root in $roots) {
        foreach ($section in $settings.Keys) {
            $value = Get-ItemProperty -LiteralPath (Join-Path $root $section) -ErrorAction SilentlyContinue
            if ($null -ne $value -and $value.($settings[$section]) -eq 1) { return $true }
        }
    }
    $configFiles = @((Join-Path $PSHOME 'powershell.config.json'),
        (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell/powershell.config.json'))
    foreach ($file in $configFiles) {
        if (-not [IO.File]::Exists($file)) { continue }
        try { $config = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json -AsHashtable }
        catch { throw 'PowerShell logging configuration could not be inspected.' }
        foreach ($section in $settings.Keys) {
            if ($config.ContainsKey("PowerShellPolicies") -and
                $config.PowerShellPolicies.ContainsKey($section) -and
                $config.PowerShellPolicies[$section][$settings[$section]] -eq $true) { return $true }
        }
    }
    return $false
}

function New-NfcManifest {
    param([Parameter(Mandatory)][string]$Owner, [Parameter(Mandatory)][string]$Repo,
          [Parameter(Mandatory)][string]$AppName, [Parameter(Mandatory)][int]$Port)
    if ($Owner -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$' -or
        $Repo -notmatch '^[A-Za-z0-9_.-]+$' -or $AppName.Length -gt 34 -or
        [string]::IsNullOrWhiteSpace($AppName) -or $Port -lt 1 -or $Port -gt 65535) {
        throw 'Invalid owner, repository, application name, or port.'
    }
    return [ordered]@{
        name = $AppName
        url = "https://github.com/$Owner/$Repo"
        description = "Agent identity for ${Repo}: pushes feature branches and opens pull requests. No administration, secrets or environment access."
        public = $false
        hook_attributes = [ordered]@{ url = 'https://example.com/nfc-agent-webhook-unused'; active = $false }
        redirect_url = "http://127.0.0.1:$Port/callback"
        request_oauth_on_install = $false
        default_permissions = [ordered]@{
            metadata = 'read'; contents = 'write'; pull_requests = 'write'
            checks = 'read'; statuses = 'read'; actions = 'read'
        }
        default_events = @()
    }
}

function ConvertTo-NfcBase64Url {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-NfcJwt {
    param([Parameter(Mandatory)][System.Security.Cryptography.RSA]$Rsa,
          [Parameter(Mandatory)][string]$ClientId,
          [datetimeoffset]$Now = [datetimeoffset]::UtcNow)
    $header = ConvertTo-NfcBase64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"RS256","typ":"JWT"}'))
    $claims = @{ iss = $ClientId; iat = $Now.ToUnixTimeSeconds() - 60; exp = $Now.ToUnixTimeSeconds() + 540 }
    $payload = ConvertTo-NfcBase64Url ([Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress)))
    $inputBytes = [Text.Encoding]::ASCII.GetBytes("$header.$payload")
    $sig = $Rsa.SignData($inputBytes, [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    return "$header.$payload.$(ConvertTo-NfcBase64Url $sig)"
}

function Test-NfcState {
    param([Parameter(Mandatory)][string]$Expected, [Parameter(Mandatory)][string]$Actual)
    $a = [Text.Encoding]::UTF8.GetBytes($Expected)
    $b = [Text.Encoding]::UTF8.GetBytes($Actual)
    if ($a.Length -ne $b.Length) { return $false }
    return [Security.Cryptography.CryptographicOperations]::FixedTimeEquals($a, $b)
}

function Read-NfcCallback {
    param([Parameter(Mandatory)][string]$Target, [Parameter(Mandatory)][string]$ExpectedState)
    if ($Target -notmatch '^/callback\?[^#\s]+$') { throw 'Unexpected callback target.' }
    if ($Target -match '%(?![0-9A-Fa-f]{2})') { throw 'Invalid callback encoding.' }
    $uri = [uri]("http://127.0.0.1$Target")
    if ($uri.AbsolutePath -ne '/callback') { throw 'Unexpected callback path.' }
    $pairs = @{}
    foreach ($part in $uri.Query.TrimStart('?').Split('&', [StringSplitOptions]::RemoveEmptyEntries)) {
        $kv = $part.Split('=', 2)
        $key = [uri]::UnescapeDataString($kv[0].Replace('+', ' '))
        if ($key.Contains([char]0xFFFD)) { throw 'Invalid callback encoding.' }
        if ($pairs.ContainsKey($key)) { throw 'Duplicate callback parameter.' }
        $pairs[$key] = if ($kv.Length -eq 2) { [uri]::UnescapeDataString($kv[1].Replace('+', ' ')) } else { '' }
        if ($pairs[$key].Contains([char]0xFFFD)) { throw 'Invalid callback encoding.' }
    }
    if (-not $pairs.ContainsKey('state') -or -not (Test-NfcState $ExpectedState $pairs.state)) {
        throw 'Invalid callback state.'
    }
    if ($pairs.ContainsKey('error') -or -not $pairs.ContainsKey('code') -or [string]::IsNullOrWhiteSpace($pairs.code)) {
        throw 'GitHub did not return a conversion code.'
    }
    return $pairs.code
}

function Read-NfcCallbackOnce {
    param([string]$Target, [string]$ExpectedState, [ref]$Consumed)
    if ($Consumed.Value) { throw 'Callback already consumed.' }
    $code = Read-NfcCallback -Target $Target -ExpectedState $ExpectedState
    $Consumed.Value = $true
    return $code
}

function Test-NfcCredentialScope {
    param([hashtable]$Request, [string]$Owner, [string]$Repo)
    return ($Request['protocol'] -ceq 'https' -and $Request['host'] -ceq 'github.com' -and
        $Request.ContainsKey('path') -and $Request['path'] -cin @("$Owner/$Repo", "$Owner/$Repo.git"))
}

function Read-NfcHttpRequest {
    param([Parameter(Mandatory)][IO.Stream]$Stream, [int]$Port,
          [datetimeoffset]$Deadline, [int]$MaximumBytes = 16384, [int]$MaximumHeaders = 64)
    $buffer = [byte[]]::new(1)
    $bytes = [Collections.Generic.List[byte]]::new()
    while ($bytes.Count -lt $MaximumBytes) {
        $remaining = $Deadline - [datetimeoffset]::UtcNow
        if ($remaining.TotalMilliseconds -le 0) { throw 'Request deadline expired.' }
        $cancel = [Threading.CancellationTokenSource]::new()
        try {
            $cancel.CancelAfter([TimeSpan]::FromMilliseconds([Math]::Max(1, $remaining.TotalMilliseconds)))
            $read = $Stream.ReadAsync($buffer, 0, 1, $cancel.Token).GetAwaiter().GetResult()
        } finally { $cancel.Dispose() }
        if ($read -ne 1) { throw 'Truncated HTTP request.' }
        $b = $buffer[0]
        if ($b -gt 127 -or ($b -lt 32 -and $b -notin @(10, 13))) { throw 'Invalid HTTP encoding.' }
        $bytes.Add($b)
        $n = $bytes.Count
        if ($n -ge 4 -and $bytes[$n - 4] -eq 13 -and $bytes[$n - 3] -eq 10 -and
            $bytes[$n - 2] -eq 13 -and $bytes[$n - 1] -eq 10) { break }
    }
    if ($bytes.Count -ge $MaximumBytes -and -not ($bytes[$bytes.Count - 4] -eq 13 -and
        $bytes[$bytes.Count - 3] -eq 10 -and $bytes[$bytes.Count - 2] -eq 13 -and $bytes[$bytes.Count - 1] -eq 10)) {
        throw 'HTTP request exceeds byte limit.'
    }
    $raw = [Text.Encoding]::ASCII.GetString($bytes.ToArray())
    if ($raw -match '(?<!\r)\n|\r(?!\n)') { throw 'Invalid HTTP line endings.' }
    $lines = $raw.Substring(0, $raw.Length - 4).Split([string[]]@("`r`n"), [StringSplitOptions]::None)
    if ($lines.Count -lt 2 -or $lines.Count -gt ($MaximumHeaders + 1) -or
        $lines[0] -notmatch '^GET (/[^ ]*) HTTP/1\.1$') { throw 'Invalid HTTP request line.' }
    $target = $Matches[1]
    $hostCount = 0
    foreach ($line in $lines[1..($lines.Count - 1)]) {
        if ($line -notmatch '^([!#$%&''*+.^_`|~0-9A-Za-z-]+):[ \t]*([\x20-\x7E]*)$') {
            throw 'Invalid HTTP header.'
        }
        if ($Matches[1] -ieq 'Host') {
            $hostCount++
            if ($Matches[2].Trim() -cne "127.0.0.1:$Port") { throw 'Unexpected HTTP host.' }
        }
    }
    if ($hostCount -ne 1) { throw 'Missing or duplicate HTTP host.' }
    return $target
}

function Read-NfcCredentialInput {
    param([Parameter(Mandatory)][string]$InputText)
    $values = @{}
    foreach ($line in ($InputText -split "`r?`n")) {
        if ($line -eq '') { break }
        $kv = $line.Split('=', 2)
        if ($kv.Length -ne 2) { throw 'Invalid credential request.' }
        # Git may repeat these advisory arrays; neither selects an identity or scope.
        if ($kv[0] -cin @('capability[]', 'wwwauth[]')) { continue }
        if ($values.ContainsKey($kv[0])) { throw 'Invalid credential request.' }
        $values[$kv[0]] = $kv[1]
    }
    return $values
}

function Format-NfcAppSummary {
    param([Parameter(Mandatory)][hashtable]$Conversion,
          [Parameter(Mandatory)][string]$Owner,
          [Parameter(Mandatory)][string]$Repo)
    return @(
        "App ID: $($Conversion.id)"
        "Client ID: $($Conversion.client_id)"
        "Slug: $($Conversion.slug)"
        "App URL: $($Conversion.html_url)"
        "Installation URL: https://github.com/apps/$($Conversion.slug)/installations/new"
        "Install with Only select repositories and choose $Owner/$Repo. Record the installation ID yourself."
    )
}
