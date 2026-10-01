#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateSet('git', 'token')][string]$Mode = 'git',
    [Parameter(Mandatory)][string]$Owner,
    [Parameter(Mandatory)][string]$Repo,
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][long]$InstallationId,
    [Parameter(Mandatory)][string]$DpapiPath,
    # Adds workflows: write to the fixed A1 set for this one token; never implied.
    [switch]$IncludeWorkflowsWrite,
    [Parameter(ValueFromRemainingArguments)][string[]]$GitArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/NfcG0.Common.ps1"

function New-NfcTokenPermissionSet {
    param([switch]$IncludeWorkflowsWrite)
    $permissions = @{ metadata = 'read'; contents = 'write'; pull_requests = 'write'; checks = 'read'; statuses = 'read'; actions = 'read' }
    if ($IncludeWorkflowsWrite) { $permissions['workflows'] = 'write' }
    return $permissions
}

function Request-NfcInstallationToken {
    param([string]$Owner, [string]$Repo, [long]$InstallationId, [string]$Jwt, [hashtable]$Permissions)
    $reply = $null
    try {
        $body = @{ repositories = @($Repo); permissions = $Permissions } | ConvertTo-Json -Compress -Depth 5
        $reply = Invoke-RestMethod -Method Post -Uri "https://api.github.com/app/installations/$InstallationId/access_tokens" `
            -Headers @{ Authorization = "Bearer $Jwt"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'nfc-g0-token-helper' } `
            -ContentType 'application/json' -Body $body -MaximumRedirection 0 -TimeoutSec 30
        if ($reply.token -isnot [string] -or $reply.token -match '[\r\n\0]') {
            throw 'Installation token has an unexpected form.'
        }
        if (-not $reply.token -or $reply.repositories.Count -ne 1 -or
            $reply.repositories[0].full_name -cne "$Owner/$Repo") {
            throw 'Installation token was not restricted to the configured repository.'
        }
        foreach ($key in $reply.permissions.PSObject.Properties.Name) {
            if (-not $Permissions.ContainsKey($key) -or $Permissions[$key] -cne $reply.permissions.$key) {
                throw 'Installation token permissions differ from the requested set.'
            }
        }
        if ($Permissions.ContainsKey('workflows')) {
            $granted = $reply.permissions.PSObject.Properties['workflows']
            if (-not $granted -or $granted.Value -cne 'write') {
                throw 'Installation token lacks the requested workflows permission.'
            }
        }
        return $reply.token
    } finally {
        $reply = $null
    }
}

function Get-NfcInstallationToken {
    param([string]$Owner, [string]$Repo, [string]$ClientId, [long]$InstallationId, [string]$DpapiPath,
          [switch]$IncludeWorkflowsWrite)
    $cipher = $null; $plain = $null; $pem = $null; $rsa = $null; $jwt = $null
    try {
        if ($Owner -notmatch '^[A-Za-z0-9-]+\z' -or $Repo -notmatch '^[A-Za-z0-9_.-]+\z' -or
            $InstallationId -lt 1) { throw 'Invalid helper configuration.' }
        $cipher = [IO.File]::ReadAllBytes($DpapiPath)
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($cipher, $null,
            [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $pem = [Text.Encoding]::UTF8.GetString($plain)
        $rsa = [Security.Cryptography.RSA]::Create()
        $rsa.ImportFromPem($pem)
        $jwt = New-NfcJwt -Rsa $rsa -ClientId $ClientId
        return Request-NfcInstallationToken -Owner $Owner -Repo $Repo -InstallationId $InstallationId -Jwt $jwt `
            -Permissions (New-NfcTokenPermissionSet -IncludeWorkflowsWrite:$IncludeWorkflowsWrite)
    } finally {
        if ($rsa) { $rsa.Dispose() }
        if ($cipher) { [Array]::Clear($cipher, 0, $cipher.Length) }
        if ($plain) { [Array]::Clear($plain, 0, $plain.Length) }
        $pem = $null; $jwt = $null
    }
}

try {
    Assert-NfcRuntime
    if ($Mode -eq 'git') {
        if ($GitArguments.Count -ne 1 -or $GitArguments[0] -ne 'get') { exit 0 }
        $lines = [Collections.Generic.List[string]]::new()
        while ($null -ne ($line = [Console]::In.ReadLine()) -and $line -ne '') { $lines.Add($line) }
        $request = Read-NfcCredentialInput (($lines -join "`n") + "`n`n")
        if (-not (Test-NfcCredentialScope -Request $request -Owner $Owner -Repo $Repo)) { exit 0 }
    }
    if (Test-NfcRecordingPolicy) { throw 'Recording policy is enabled.' }
    $token = Get-NfcInstallationToken -Owner $Owner -Repo $Repo -ClientId $ClientId `
        -InstallationId $InstallationId -DpapiPath $DpapiPath -IncludeWorkflowsWrite:$IncludeWorkflowsWrite
    if ($Mode -eq 'git') {
        [Console]::Out.Write("username=x-access-token`npassword=$token`n`n")
    } else {
        [Console]::Out.Write($token)
    }
} catch {
    [Console]::Error.WriteLine('NFC token helper failed. Ask the owner to check its local configuration and installation.')
    exit 1
} finally {
    $token = $null
}
