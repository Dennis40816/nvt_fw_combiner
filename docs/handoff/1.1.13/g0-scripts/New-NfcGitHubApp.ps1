#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Owner,
    [Parameter(Mandatory)][string]$Repo,
    [Parameter(Mandatory)][string]$AppName,
    [ValidateSet('Dpapi', 'Bitwarden', 'Both')][string]$KeyStore = 'Both',
    [string]$DpapiPath,
    [switch]$DryRun,
    [int]$TimeoutMinutes = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/NfcG0.Common.ps1"

function Invoke-NfcBitwarden {
    param([string[]]$Arguments, [string]$InputText, [int]$TimeoutSeconds = 30)
    $app = Get-Command bw -All -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandType -eq 'Application' -and $_.Source -match '\.(exe|cmd)$' } |
        Select-Object -First 1
    if (-not $app) { throw 'Bitwarden CLI executable is unavailable.' }
    $psi = [Diagnostics.ProcessStartInfo]::new()
    if ($app.Source -match '\.cmd$') {
        $psi.FileName = $env:ComSpec
        $psi.Arguments = '/d /s /c ""' + $app.Source.Replace('"', '') + '" ' + ($Arguments -join ' ') + '"'
    } else {
        $psi.FileName = $app.Source
        foreach ($arg in $Arguments) { [void]$psi.ArgumentList.Add($arg) }
    }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $proc = [Diagnostics.Process]::Start($psi)
    try {
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $cancel = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
        try {
            if ($null -ne $InputText) {
                $null = $proc.StandardInput.WriteAsync($InputText).WaitAsync($cancel.Token).GetAwaiter().GetResult()
            }
            $proc.StandardInput.Close()
            $null = $proc.WaitForExitAsync($cancel.Token).GetAwaiter().GetResult()
            $stdout = $outTask.WaitAsync($cancel.Token).GetAwaiter().GetResult()
            $null = $errTask.WaitAsync($cancel.Token).GetAwaiter().GetResult()
        }
        catch {
            if (-not $proc.HasExited) { $proc.Kill($true) }
            throw 'Bitwarden operation timed out or failed.'
        } finally { $cancel.Dispose() }
        if ($proc.ExitCode -ne 0) { throw 'Bitwarden operation failed.' }
        return $stdout
    } finally { $proc.Dispose() }
}

function Set-NfcPrivateFile {
    param([string]$Path, [byte[]]$Bytes)
    $full = [IO.Path]::GetFullPath($Path)
    if ([IO.File]::Exists($full)) { throw 'Protected key file already exists.' }
    $directory = [IO.Path]::GetDirectoryName($full)
    if (-not [IO.Directory]::Exists($directory)) { throw 'DPAPI destination directory must already exist.' }
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = New-Object Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($sid,
        [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.AccessControlType]::Allow)))
    $created = $false
    try {
        $stream = [IO.FileSystemAclExtensions]::Create([IO.FileInfo]::new($full),
            [IO.FileMode]::CreateNew, [Security.AccessControl.FileSystemRights]::FullControl,
            [IO.FileShare]::None, 4096, [IO.FileOptions]::None, $acl)
        $created = $true
        try { $stream.Write($Bytes, 0, $Bytes.Length) } finally { $stream.Dispose() }
    } catch {
        if ($created -and [IO.File]::Exists($full)) { [IO.File]::Delete($full) }
        throw 'Could not save protected key with the requested ACL.'
    }
}

function Save-NfcBitwardenNote {
    param([string]$Pem, [string]$Name)
    if (-not $env:BW_SESSION) { throw 'BW_SESSION is absent. Unlock Bitwarden yourself and rerun.' }
    $status = Invoke-NfcBitwarden -Arguments @('status') | ConvertFrom-Json
    if ($status.status -ne 'unlocked') {
        throw 'Bitwarden is locked. Unlock it yourself and rerun.'
    }
    $note = @{ type = 2; name = $Name; notes = $Pem; secureNote = @{ type = 0 } }
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($note | ConvertTo-Json -Compress -Depth 5)))
    try {
        $created = Invoke-NfcBitwarden -Arguments @('create', 'item') -InputText $encoded | ConvertFrom-Json
        if (-not $created.id -or $created.type -ne 2) { throw 'Bitwarden secure note creation was not confirmed.' }
    }
    finally { $encoded = $null; $note = $null }
}

function Send-NfcHttpPage {
    param([Net.Sockets.TcpClient]$Client, [string]$Body, [string]$Status = '200 OK')
    $stream = $Client.GetStream()
    $content = [Text.Encoding]::UTF8.GetBytes($Body)
    $head = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 $Status`r`nContent-Type: text/html; charset=utf-8`r`nContent-Length: $($content.Length)`r`nConnection: close`r`nCache-Control: no-store`r`n`r`n")
    $stream.Write($head, 0, $head.Length)
    $stream.Write($content, 0, $content.Length)
}

$listener = $null
$conversion = $null
$pemBytes = $null
$protected = $null
$dpapiSaved = $false
$bwSaved = $false
$conversionAttempted = $false
$conversionConfirmed = $false
try {
    Assert-NfcRuntime
    if ($TimeoutMinutes -lt 1 -or $TimeoutMinutes -gt 60) { throw 'TimeoutMinutes must be between 1 and 60.' }
    if (-not $DryRun) {
        if (Test-NfcRecordingPolicy) { throw 'PowerShell recording policy is enabled. Use an unrecorded owner process.' }
        Write-Warning 'Manual transcript and external screen recording may be undetectable. Use an unrecorded owner process.'
        if ($KeyStore -ne 'Both') { throw 'Decision 82 requires both DPAPI and Bitwarden storage.' }
        if (-not $DpapiPath) { throw 'DpapiPath is required.' }
        $fullDpapiPath = [IO.Path]::GetFullPath($DpapiPath)
        if ([IO.File]::Exists($fullDpapiPath) -or
            -not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($fullDpapiPath))) {
            throw 'DPAPI destination must be a new file in an existing directory.'
        }
        if (-not $env:BW_SESSION) { throw 'BW_SESSION is absent. Unlock Bitwarden yourself and rerun.' }
        $status = Invoke-NfcBitwarden -Arguments @('status') | ConvertFrom-Json
        if ($status.status -ne 'unlocked') { throw 'Bitwarden is locked. Unlock it yourself and rerun.' }
        if ((Read-Host 'Continue? Type YES') -cne 'YES') { throw 'Stopped before conversion.' }
    }
    if ($DryRun) { $port = 49152 }
    else {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    $manifest = New-NfcManifest -Owner $Owner -Repo $Repo -AppName $AppName -Port $port
    $manifestJson = $manifest | ConvertTo-Json -Depth 8 -Compress
    $stateBytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($stateBytes)
    $state = ConvertTo-NfcBase64Url $stateBytes
    $formBytes = [byte[]]::new(16)
    [Security.Cryptography.RandomNumberGenerator]::Fill($formBytes)
    $formPath = '/form/' + (ConvertTo-NfcBase64Url $formBytes)
    $action = 'https://github.com/settings/apps/new?state=' + [uri]::EscapeDataString($state)
    $escapedAction = [Net.WebUtility]::HtmlEncode($action)
    $escapedManifest = [Net.WebUtility]::HtmlEncode($manifestJson)
    $page = '<!doctype html><meta charset="utf-8"><title>NFC GitHub App</title>' +
        '<form method="post" action="' + $escapedAction + '"><input type="hidden" name="manifest" value="' +
        $escapedManifest + '"><button type="submit">Create the agent GitHub App</button></form>'
    if ($DryRun) {
        [IO.File]::WriteAllText((Join-Path (Get-Location) 'nfc-manifest.dry-run.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.Encoding]::UTF8)
        [IO.File]::WriteAllText((Join-Path (Get-Location) 'nfc-manifest.dry-run.html'), $page, [Text.Encoding]::UTF8)
        Write-Output 'Dry run files written. No callback listener was opened.'
        return
    }
    Start-Process "http://127.0.0.1:$port$formPath"
    $deadline = [datetimeoffset]::UtcNow.AddMinutes($TimeoutMinutes)
    $code = $null
    $callbackConsumed = $false
    while (-not $code -and [datetimeoffset]::UtcNow -lt $deadline) {
        $remaining = [int][Math]::Max(1, ($deadline - [datetimeoffset]::UtcNow).TotalMilliseconds)
        try { $client = $listener.AcceptTcpClientAsync().WaitAsync([TimeSpan]::FromMilliseconds($remaining)).GetAwaiter().GetResult() }
        catch [TimeoutException] { break }
        try {
            if (-not $client.Client.RemoteEndPoint.Address.Equals([Net.IPAddress]::Loopback)) { continue }
            $connectionDeadline = [datetimeoffset]::UtcNow.AddSeconds(5)
            if ($connectionDeadline -gt $deadline) { $connectionDeadline = $deadline }
            $target = Read-NfcHttpRequest -Stream $client.GetStream() -Port $port -Deadline $connectionDeadline
            if ($target -ceq $formPath) { Send-NfcHttpPage $client $page; continue }
            $code = Read-NfcCallbackOnce -Target $target -ExpectedState $state -Consumed ([ref]$callbackConsumed)
            Send-NfcHttpPage $client 'Callback received. You may close this tab.'
        } catch {
            try { Send-NfcHttpPage $client 'Invalid request or callback.' '400 Bad Request' } catch { }
        } finally { $client.Dispose() }
    }
    if (-not $code) { throw 'Timed out waiting for a valid callback.' }
    $listener.Stop(); $listener = $null
    $uri = 'https://api.github.com/app-manifests/' + [uri]::EscapeDataString($code) + '/conversions'
    $conversionAttempted = $true
    $conversion = Invoke-RestMethod -Method Post -Uri $uri -Headers @{
        Accept = 'application/vnd.github+json'; 'User-Agent' = 'nfc-g0-owner-script'
    } -MaximumRedirection 0 -TimeoutSec 30
    if (-not $conversion.pem -or -not $conversion.id -or -not $conversion.slug -or
        -not $conversion.client_id) { throw 'Conversion response is incomplete.' }
    $conversionConfirmed = $true
    if ($KeyStore -eq 'Both') {
        $pemBytes = [Text.Encoding]::UTF8.GetBytes($conversion.pem)
        $protected = [Security.Cryptography.ProtectedData]::Protect($pemBytes, $null,
            [Security.Cryptography.DataProtectionScope]::CurrentUser)
        Set-NfcPrivateFile -Path $DpapiPath -Bytes $protected
        $dpapiSaved = $true
    }
    if ($KeyStore -eq 'Both') {
        Save-NfcBitwardenNote -Pem $conversion.pem -Name "NFC GitHub App $Owner/$Repo"
        $bwSaved = $true
    }
    Format-NfcAppSummary -Conversion @{
        id = $conversion.id; client_id = $conversion.client_id
        slug = $conversion.slug; html_url = $conversion.html_url
    } -Owner $Owner -Repo $Repo
} catch {
    if ($conversionAttempted -and -not $conversionConfirmed) {
        throw 'App conversion outcome is unknown. Owner must check GitHub for an App and key, revoke or clean up any created key and App, then decide whether to create a new App. Do not retry conversion directly.'
    }
    if ($dpapiSaved -and -not $bwSaved) {
        throw "App setup stopped after DPAPI save. DPAPI file exists at $fullDpapiPath. Bitwarden creation may have an unknown outcome; owner must inspect the vault. If absent, use an owner-reviewed local recovery step to copy this DPAPI key into Bitwarden, or remove the DPAPI file and revoke/delete the App key before restarting."
    }
    if ($conversion -and -not $dpapiSaved) {
        throw 'App conversion succeeded, but no protected key copy was confirmed. Owner must revoke or delete the App key before restarting.'
    }
    throw 'App setup did not complete. Check the local requirements and retry; do not disclose conversion details.'
} finally {
    if ($listener) { $listener.Stop() }
    if ($pemBytes) { [Array]::Clear($pemBytes, 0, $pemBytes.Length) }
    if ($protected) { [Array]::Clear($protected, 0, $protected.Length) }
    foreach ($name in @('conversion','code','state','stateBytes','formBytes','manifestJson','page','pemBytes','protected')) {
        Remove-Variable -Name $name -ErrorAction SilentlyContinue
    }
}
