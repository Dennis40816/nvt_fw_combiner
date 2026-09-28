. "$PSScriptRoot/../NfcG0.Common.ps1"

function Test-NfcThrows {
    param([scriptblock]$Action)
    try { $null = & $Action; return $false } catch { return $true }
}

Describe 'NFC G0 pure functions' {
    It 'creates the exact manifest permission set and inactive webhook' {
        $m = New-NfcManifest -Owner 'example-owner' -Repo 'example-repo' -AppName 'nfc-agent-example' -Port 49152
        $m.public | Should Be $false
        $m.hook_attributes.active | Should Be $false
        $m.redirect_url | Should Be 'http://127.0.0.1:49152/callback'
        $m.default_events.Count | Should Be 0
        (($m.default_permissions.Keys | Sort-Object) -join ',') | Should Be 'actions,checks,contents,metadata,pull_requests,statuses'
        $m.default_permissions.contents | Should Be 'write'
        $m.default_permissions.actions | Should Be 'read'
        $m.default_permissions.Contains('workflows') | Should Be $false
    }

    It 'validates state and parses only a valid callback' {
        (Test-NfcState -Expected 'abc' -Actual 'abc') | Should Be $true
        (Test-NfcState -Expected 'abc' -Actual 'abd') | Should Be $false
        (Read-NfcCallback -Target '/callback?code=xyz%2F1&state=abc' -ExpectedState 'abc') | Should Be 'xyz/1'
        foreach ($target in @('/callback?code=xyz&state=wrong',
            '/callback?code=xyz&state=abc&state=abc', '/callback?error=denied&state=abc')) {
            $rejected = $false
            try { $null = Read-NfcCallback -Target $target -ExpectedState 'abc' } catch { $rejected = $true }
            $rejected | Should Be $true
        }
    }

    It 'signs a JWT with RS256 and the required claims' {
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            $at = [datetimeoffset]::Parse('2026-09-26T00:00:00Z')
            $jwt = New-NfcJwt -Rsa $rsa -ClientId 'Iv1.example' -Now $at
            $parts = $jwt.Split('.')
            $parts.Count | Should Be 3
            $decode = {
                param($Part)
                $base64 = $Part.Replace('-', '+').Replace('_', '/')
                $base64 += '=' * ((4 - $base64.Length % 4) % 4)
                [Convert]::FromBase64String($base64)
            }
            $head = [Text.Encoding]::UTF8.GetString((& $decode $parts[0])) | ConvertFrom-Json
            $body = [Text.Encoding]::UTF8.GetString((& $decode $parts[1])) | ConvertFrom-Json
            $head.alg | Should Be 'RS256'
            $head.typ | Should Be 'JWT'
            $body.iss | Should Be 'Iv1.example'
            $body.iat | Should Be ($at.ToUnixTimeSeconds() - 60)
            $body.exp | Should Be ($at.ToUnixTimeSeconds() + 540)
            $rsa.VerifyData([Text.Encoding]::ASCII.GetBytes("$($parts[0]).$($parts[1])"),
                (& $decode $parts[2]), [Security.Cryptography.HashAlgorithmName]::SHA256,
                [Security.Cryptography.RSASignaturePadding]::Pkcs1) | Should Be $true
        } finally { $rsa.Dispose() }
    }

    It 'parses credential helper input without touching a store' {
        $request = Read-NfcCredentialInput "protocol=https`nhost=github.com`npath=example-owner/example-repo.git`n`n"
        $request.protocol | Should Be 'https'
        $request.host | Should Be 'github.com'
        $request.path | Should Be 'example-owner/example-repo.git'
        $rejected = $false
        try { $null = Read-NfcCredentialInput "host=github.com`nhost=evil`n`n" } catch { $rejected = $true }
        $rejected | Should Be $true
    }

    It 'keeps ruleset templates structurally aligned with the checklist' {
        $main = Get-Content "$PSScriptRoot/../rulesets/RS-1a-to-1k.json" -Raw | ConvertFrom-Json -AsHashtable
        $trunk = Get-Content "$PSScriptRoot/../rulesets/RS-2.json" -Raw | ConvertFrom-Json -AsHashtable
        $release = Get-Content "$PSScriptRoot/../rulesets/RS-3.json" -Raw | ConvertFrom-Json -AsHashtable
        $tag = Get-Content "$PSScriptRoot/../rulesets/RS-4.json" -Raw | ConvertFrom-Json -AsHashtable
        $main.rules[0].parameters.required_review_thread_resolution | Should Be $true
        $trunk.conditions.ref_name.include[0] | Should Be 'refs/heads/*.*.x'
        $release.conditions.ref_name.exclude[0] | Should Be 'refs/heads/*.*.x'
        ('deletion' -in @($release.rules.type)) | Should Be $false
        $tag.action | Should Be 'confirm_only'
        $tag.bypass_actors.Count | Should Be 0
    }

    It 'formats the app summary without conversion secrets' {
        $fake = @{ id = 42; client_id = 'Iv1.fake'; slug = 'nfc-agent'; html_url = 'https://github.com/apps/nfc-agent'
            pem = 'private-pem'; client_secret = 'client-secret'; webhook_secret = 'webhook-secret' }
        $output = (Format-NfcAppSummary -Conversion $fake -Owner 'owner' -Repo 'repo') -join "`n"
        $output | Should Match 'App ID: 42'
        $output | Should Match 'Client ID: Iv1.fake'
        foreach ($secret in @('private-pem', 'client-secret', 'webhook-secret')) {
            $output.Contains($secret) | Should Be $false
        }
    }
}

Describe 'NFC G0 Git advisory credential metadata' {
    It 'accepts repeated advisory arrays without treating their contents as identity' {
        $inputText = "capability[]=authtype`ncapability[]=state`nprotocol=https`nhost=github.com`npath=owner/repo.git`nwwwauth[]=Basic realm=GitHub`nwwwauth[]=host=evil.example`n`n"
        $request = Read-NfcCredentialInput $inputText
        $request.Count | Should Be 3
        $request.protocol | Should Be 'https'
        $request.host | Should Be 'github.com'
        $request.path | Should Be 'owner/repo.git'
        (Test-NfcCredentialScope -Request $request -Owner owner -Repo repo) | Should Be $true
    }

    It 'still rejects repeated scalar identity and unknown fields' {
        foreach ($field in @('protocol', 'host', 'path', 'unknown')) {
            foreach ($second in @('first', 'different')) {
                $inputText = "capability[]=authtype`n${field}=first`nwwwauth[]=Basic realm=GitHub`n${field}=$second`n`n"
                (Test-NfcThrows { Read-NfcCredentialInput $inputText }) | Should Be $true
            }
        }
        (Test-NfcThrows { Read-NfcCredentialInput "capability[]=one`nCapability[]=two`nCapability[]=three`n`n" }) | Should Be $true
    }

    It 'rejects malformed advisory records and respects the blank-line terminator' {
        foreach ($field in @('capability[]', 'wwwauth[]')) {
            (Test-NfcThrows { Read-NfcCredentialInput "$field`n`n" }) | Should Be $true
        }
        $request = Read-NfcCredentialInput "protocol=https`nhost=github.com`npath=owner/repo`n`nhost=evil.example`n"
        (Test-NfcCredentialScope -Request $request -Owner owner -Repo repo) | Should Be $true
    }

    It 'does not let advisory metadata repair an invalid repository scope' {
        foreach ($identity in @("protocol=http`nhost=github.com`npath=owner/repo",
            "protocol=https`nhost=evil.example`npath=owner/repo",
            "protocol=https`nhost=github.com`npath=other/repo")) {
            $request = Read-NfcCredentialInput "capability[]=authtype`ncapability[]=state`n$identity`nwwwauth[]=host=github.com`nwwwauth[]=path=owner/repo`n`n"
            (Test-NfcCredentialScope -Request $request -Owner owner -Repo repo) | Should Be $false
        }
    }
}

Describe 'NFC G0 scope, callback, and recording guards' {
    It 'rejects missing, blank, and unrelated credential paths before any store call' {
        foreach ($request in @(
            @{ protocol = 'https'; host = 'github.com' },
            @{ protocol = 'https'; host = 'github.com'; path = '' },
            @{ protocol = 'https'; host = 'github.com'; path = 'other/repo' }
        )) {
            (Test-NfcCredentialScope -Request $request -Owner 'owner' -Repo 'repo') | Should Be $false
        }
        (Test-NfcCredentialScope -Request @{ protocol = 'https'; host = 'github.com'; path = 'owner/repo.git' } -Owner 'owner' -Repo 'repo') | Should Be $true
    }

    It 'rejects oversized and truncated callback requests' {
        $port = 49152
        $oversized = "GET /callback?code=x&state=y HTTP/1.1`r`nHost: 127.0.0.1:$port`r`nX-Fill: " + ('a' * 200) + "`r`n`r`n"
        $stream = [IO.MemoryStream]::new([Text.Encoding]::ASCII.GetBytes($oversized))
        (Test-NfcThrows { Read-NfcHttpRequest -Stream $stream -Port $port -Deadline ([datetimeoffset]::UtcNow.AddSeconds(2)) -MaximumBytes 100 }) | Should Be $true
        $stream = [IO.MemoryStream]::new([Text.Encoding]::ASCII.GetBytes("GET / HTTP/1.1`r`nHost: 127.0.0.1:$port`r`n"))
        (Test-NfcThrows { Read-NfcHttpRequest -Stream $stream -Port $port -Deadline ([datetimeoffset]::UtcNow.AddSeconds(2)) }) | Should Be $true
    }

    It 'bounds callbacks by an absolute deadline' {
        $stream = [IO.MemoryStream]::new([Text.Encoding]::ASCII.GetBytes('GET /'))
        (Test-NfcThrows { Read-NfcHttpRequest -Stream $stream -Port 49152 -Deadline ([datetimeoffset]::UtcNow.AddMilliseconds(-1)) }) | Should Be $true
    }

    It 'cancels a blocked callback read and accepts a later valid connection' {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try {
            $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
            $slow = [Net.Sockets.TcpClient]::new('127.0.0.1', $port)
            $server = $listener.AcceptTcpClient()
            try {
                $watch = [Diagnostics.Stopwatch]::StartNew()
                (Test-NfcThrows { Read-NfcHttpRequest -Stream $server.GetStream() -Port $port -Deadline ([datetimeoffset]::UtcNow.AddMilliseconds(150)) }) | Should Be $true
                $watch.Elapsed.TotalSeconds | Should BeLessThan 2
            } finally { $server.Dispose(); $slow.Dispose() }
            $valid = [Net.Sockets.TcpClient]::new('127.0.0.1', $port)
            $server = $listener.AcceptTcpClient()
            try {
                $request = [Text.Encoding]::ASCII.GetBytes("GET /callback?code=fake&state=right HTTP/1.1`r`nHost: 127.0.0.1:$port`r`n`r`n")
                $valid.GetStream().Write($request, 0, $request.Length)
                $target = Read-NfcHttpRequest -Stream $server.GetStream() -Port $port -Deadline ([datetimeoffset]::UtcNow.AddSeconds(2))
                (Read-NfcCallback -Target $target -ExpectedState 'right') | Should Be 'fake'
            } finally { $server.Dispose(); $valid.Dispose() }
        } finally { $listener.Stop() }
    }

    It 'rejects wrong methods, hosts, targets, state, and repeated callbacks' {
        $requests = @(
            "POST /callback?code=x&state=y HTTP/1.1`r`nHost: 127.0.0.1:49152`r`n`r`n",
            "GET /callback?code=x&state=y HTTP/1.1`r`nHost: evil:49152`r`n`r`n",
            "GET http://evil/callback?code=x&state=y HTTP/1.1`r`nHost: 127.0.0.1:49152`r`n`r`n"
        )
        foreach ($request in $requests) {
            $stream = [IO.MemoryStream]::new([Text.Encoding]::ASCII.GetBytes($request))
            (Test-NfcThrows { Read-NfcHttpRequest -Stream $stream -Port 49152 -Deadline ([datetimeoffset]::UtcNow.AddSeconds(2)) }) | Should Be $true
        }
        (Test-NfcThrows { Read-NfcCallback -Target '/callback?code=x&state=wrong' -ExpectedState 'right' }) | Should Be $true
        (Test-NfcThrows { Read-NfcCallback -Target '/wrong?code=x&state=y' -ExpectedState 'y' }) | Should Be $true
        (Test-NfcThrows { Read-NfcCallback -Target '/callback?code=x&state=y&state=y' -ExpectedState 'y' }) | Should Be $true
        (Test-NfcThrows { Read-NfcCallback -Target '/callback?code=%ZZ&state=y' -ExpectedState 'y' }) | Should Be $true
        $consumed = $false
        (Read-NfcCallbackOnce -Target '/callback?code=x&state=y' -ExpectedState 'y' -Consumed ([ref]$consumed)) | Should Be 'x'
        (Test-NfcThrows { Read-NfcCallbackOnce -Target '/callback?code=x&state=y' -ExpectedState 'y' -Consumed ([ref]$consumed) }) | Should Be $true
    }

    It 'detects enabled module logging' {
        Mock Get-ItemProperty { [pscustomobject]@{ EnableModuleLogging = 1 } } -ParameterFilter { $LiteralPath -like '*ModuleLogging' }
        (Test-NfcRecordingPolicy) | Should Be $true
    }

    It 'detects enabled script block logging' {
        Mock Get-ItemProperty { [pscustomobject]@{ EnableScriptBlockLogging = 1 } } -ParameterFilter { $LiteralPath -like '*ScriptBlockLogging' }
        (Test-NfcRecordingPolicy) | Should Be $true
    }

    It 'detects enabled transcription' {
        Mock Get-ItemProperty { [pscustomobject]@{ EnableTranscripting = 1 } } -ParameterFilter { $LiteralPath -like '*Transcription' }
        (Test-NfcRecordingPolicy) | Should Be $true
    }
}

Describe 'NFC G0 ruleset safety functions' {
    BeforeAll {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../Set-NfcRulesets.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    It 'serializes final bypass bodies as arrays for both owner decisions' {
        $template = Get-Content "$PSScriptRoot/../rulesets/RS-1a-to-1k.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($choice in @('Yes', 'No')) {
            $bypass = @()
            if ($choice -eq 'Yes') { $bypass = @($template.bypass_actors) }
            $body = @{ bypass_actors = $bypass } | ConvertTo-Json -Compress -Depth 10
            $parsed = $body | ConvertFrom-Json -AsHashtable
            $parsed.bypass_actors.GetType().IsArray | Should Be $true
            $parsed.bypass_actors.Count | Should Be $(if ($choice -eq 'Yes') { 1 } else { 0 })
        }
    }

    It 'rejects disabled and excluded release tags' {
        $template = Get-Content "$PSScriptRoot/../rulesets/RS-4.json" -Raw | ConvertFrom-Json -AsHashtable
        $tag = @{ enforcement = 'active'; target = 'tag';
            conditions = @{ ref_name = @{ include = @('refs/tags/v*'); exclude = @() } };
            bypass_actors = @(); rules = @(@{ type = 'update' }, @{ type = 'deletion' }) }
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $false
        $tag.enforcement = 'disabled'
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
        $tag.enforcement = 'active'; $tag.conditions.ref_name.exclude = @('refs/tags/v1*')
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
    }

    It 'requires the complete RS-4 target, inclusion, rules, and empty bypass' {
        $template = Get-Content "$PSScriptRoot/../rulesets/RS-4.json" -Raw | ConvertFrom-Json -AsHashtable
        $tag = @{ enforcement = 'active'; target = 'tag';
            conditions = @{ ref_name = @{ include = @('refs/tags/v*'); exclude = @() } };
            bypass_actors = @(); rules = @(@{ type = 'update' }, @{ type = 'deletion' }) }
        $tag.target = 'branch'
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
        $tag.target = 'tag'; $tag.conditions.ref_name.include = @('refs/tags/v*', 'refs/tags/test*')
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
        $tag.conditions.ref_name.include = @('refs/tags/v*'); $tag.rules = @(@{ type = 'update' })
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
        $tag.rules = @(@{ type = 'update' }, @{ type = 'deletion' }); $tag.bypass_actors = @(@{ actor_type = 'Team' })
        (Test-NfcThrows { Assert-NfcTagRuleset $tag $template }) | Should Be $true
    }

    It 'reads every ruleset page' {
        Mock Invoke-NfcGhGet {
            if ($Path -like '*page=1') { return 1..100 | ForEach-Object { @{ id = $_ } } }
            return @{ id = 101 }
        }
        $items = @(Get-NfcPagedRulesets '/repos/owner/repo')
        $items.Count | Should Be 101
        $items[100].id | Should Be 101
        Assert-MockCalled Invoke-NfcGhGet -Times 2
    }

    It 'compares only the recorded ruleset body for precise restore' {
        $original = @{ id = 2; name = 'main'; target = 'branch'; enforcement = 'active';
            conditions = @{ ref_name = @{ include = @('main'); exclude = @() } };
            rules = @(); bypass_actors = @() }
        $same = $original.Clone()
        (Test-NfcSameBody $original $same) | Should Be $true
        $same.enforcement = 'disabled'
        (Test-NfcSameBody $original $same) | Should Be $false
    }
}

function New-NfcMockRulesets {
    $main = @{ id = 42; name = 'main'; target = 'branch'; enforcement = 'active';
        conditions = @{ ref_name = @{ include = @('refs/heads/main'); exclude = @() } };
        bypass_actors = @(); rules = @(@{ type = 'required_status_checks'; parameters = @{
            required_status_checks = @(
                @{ context = 'a'; integration_id = 1 },
                @{ context = 'b'; integration_id = 2 },
                @{ context = 'c'; integration_id = 3 });
            strict_required_status_checks_policy = $true; do_not_enforce_on_create = $true } }) }
    $tag = @{ id = 43; name = 'release tags'; target = 'tag'; enforcement = 'active';
        conditions = @{ ref_name = @{ include = @('refs/tags/v*'); exclude = @() } };
        bypass_actors = @(); rules = @(@{ type = 'update' }, @{ type = 'deletion' }) }
    return @{ '42' = $main; '43' = $tag }
}

Describe 'NFC G0 offline ruleset transaction' {
    BeforeEach {
        $global:NfcMockRulesets = New-NfcMockRulesets
        $global:NfcMockWrites = [Collections.Generic.List[object]]::new()
        $global:NfcMockApproval = 'YES'
        $global:NfcMockApprovalCount = 0
        $global:NfcMockRejectAt = 0
        $global:NfcMockFailWriteAt = 0
        $global:NfcMockMutateAtApproval = 0
        $global:NfcMockMutateReadbackAt = 0
        $global:NfcMockFailGetId = ''
        $global:NfcMockFailGetAt = 0
        $global:NfcMockGetCount = 0
        function global:Read-Host {
            param($Prompt)
            $global:NfcMockApprovalCount++
            if ($global:NfcMockApprovalCount -eq $global:NfcMockMutateAtApproval) {
                $global:NfcMockRulesets['42'].rules[0].parameters.required_status_checks[0].context = 'parallel-change'
            }
            if ($global:NfcMockApprovalCount -eq $global:NfcMockRejectAt) { return 'NO' }
            return $global:NfcMockApproval
        }
        function global:gh {
            $arguments = @($args)
            $global:LASTEXITCODE = 0
            if ('user' -in $arguments) { return 'owner' }
            $method = $arguments[[array]::IndexOf($arguments, '--method') + 1]
            $path = $arguments[[array]::IndexOf($arguments, $method) + 1]
            if ($method -eq 'GET') {
                if ($path -match 'rulesets\?') {
                    if ($path -match 'page=1') {
                        return (ConvertTo-Json -InputObject @($global:NfcMockRulesets.Values | Sort-Object id) -Depth 50 -Compress)
                    }
                    return '[]'
                }
                if ($path -match '/rulesets/(\d+)$') {
                    if ($Matches[1] -eq $global:NfcMockFailGetId) {
                        $global:NfcMockGetCount++
                        if ($global:NfcMockGetCount -eq $global:NfcMockFailGetAt) {
                            $global:LASTEXITCODE = 1
                            return ''
                        }
                    }
                    return ($global:NfcMockRulesets[$Matches[1]] | ConvertTo-Json -Depth 50 -Compress)
                }
                return '{"full_name":"owner/repo"}'
            }
            $body = (@($input) -join '') | ConvertFrom-Json -AsHashtable -Depth 50
            $global:NfcMockWrites.Add(@{ method = $method; path = $path; body = $body })
            if ($global:NfcMockFailWriteAt -eq $global:NfcMockWrites.Count) {
                $global:LASTEXITCODE = 1
                return ''
            }
            if ($method -eq 'POST') {
                $id = 43 + $global:NfcMockWrites.Count
                $body.id = $id
                $global:NfcMockRulesets[[string]$id] = $body
            } else {
                $id = [regex]::Match($path, '/rulesets/(\d+)$').Groups[1].Value
                $body.id = [long]$id
                $global:NfcMockRulesets[$id] = $body
            }
            if ($global:NfcMockWrites.Count -eq $global:NfcMockMutateReadbackAt) {
                $body.enforcement = 'disabled'
            }
            return ($body | ConvertTo-Json -Depth 50 -Compress)
        }
    }

    AfterEach {
        Remove-Item Function:\gh -ErrorAction SilentlyContinue
        Remove-Item Function:\Read-Host -ErrorAction SilentlyContinue
    }

    It 'sends no writes for WhatIf or a failed RS-4 preflight' {
        $backup = Join-Path $TestDrive 'whatif'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No -WhatIf
        $global:NfcMockWrites.Count | Should Be 0
        $global:NfcMockRulesets['43'].enforcement = 'disabled'
        $backup = Join-Path $TestDrive 'badtag'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'requires every approval before the first remote write' {
        $global:NfcMockRejectAt = 1
        $backup = Join-Path $TestDrive 'reject'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable Yes }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'records exact created IDs and restores only the G0 changes' {
        $backup = Join-Path $TestDrive 'apply'
        $originalMain = $global:NfcMockRulesets['42'] | ConvertTo-Json -Depth 50 | ConvertFrom-Json -AsHashtable -Depth 50
        $originalTag = $global:NfcMockRulesets['43'] | ConvertTo-Json -Depth 50 | ConvertFrom-Json -AsHashtable -Depth 50
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable Yes
        $global:NfcMockWrites.Count | Should Be 3
        foreach ($write in $global:NfcMockWrites) {
            $write.body.bypass_actors.GetType().IsArray | Should Be $true
            $write.body.bypass_actors.Count | Should Be 1
        }
        $index = Get-Content (Join-Path $backup 'index.json') -Raw | ConvertFrom-Json -AsHashtable
        @($index.transaction.changes).Count | Should Be 3
        @($index.transaction.changes.id) -join ',' | Should Be '42,45,46'
        $backedUpMain = Get-Content (Join-Path $backup 'ruleset-42.json') -Raw | ConvertFrom-Json -AsHashtable -Depth 50
        (ConvertTo-NfcCanonicalJson $backedUpMain) | Should Be (ConvertTo-NfcCanonicalJson $originalMain)
        $created = @{}
        foreach ($id in @('45', '46')) {
            $created[$id] = $global:NfcMockRulesets[$id] | ConvertTo-Json -Depth 50 | ConvertFrom-Json -AsHashtable -Depth 50
        }
        $global:NfcMockWrites.Clear()
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -Restore
        $global:NfcMockWrites.Count | Should Be 3
        ($global:NfcMockWrites.method -join ',') | Should Be 'PUT,PUT,PUT'
        ($global:NfcMockWrites.path -join ',') | Should Be '/repos/owner/repo/rulesets/42,/repos/owner/repo/rulesets/45,/repos/owner/repo/rulesets/46'
        (ConvertTo-NfcCanonicalJson $global:NfcMockWrites[0].body) | Should Be (ConvertTo-NfcCanonicalJson $backedUpMain)
        for ($i = 1; $i -le 2; $i++) {
            $id = [string](44 + $i)
            $expected = $created[$id]
            $expected.enforcement = 'disabled'
            (ConvertTo-NfcCanonicalJson $global:NfcMockWrites[$i].body) | Should Be (ConvertTo-NfcCanonicalJson $expected)
        }
        (ConvertTo-NfcCanonicalJson $global:NfcMockRulesets['43']) | Should Be (ConvertTo-NfcCanonicalJson $originalTag)
    }

    It 'records a partial failure and refuses ambiguous restore' {
        $global:NfcMockFailWriteAt = 2
        $backup = Join-Path $TestDrive 'partial'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No }) | Should Be $true
        $index = Get-Content (Join-Path $backup 'index.json') -Raw | ConvertFrom-Json -AsHashtable
        $index.transaction.changes[1].status | Should Be 'pending'
        $global:NfcMockWrites[0].body.bypass_actors.GetType().IsArray | Should Be $true
        $global:NfcMockWrites[0].body.bypass_actors.Count | Should Be 0
        $global:NfcMockWrites.Clear()
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -Restore }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'sends all three No-branch bodies with empty bypass and saved checks' {
        $backup = Join-Path $TestDrive 'no-bypass'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No
        $global:NfcMockWrites.Count | Should Be 3
        ($global:NfcMockWrites.method -join ',') | Should Be 'PUT,POST,POST'
        foreach ($write in $global:NfcMockWrites) {
            $write.body.bypass_actors.GetType().IsArray | Should Be $true
            $write.body.bypass_actors.Count | Should Be 0
            $check = @($write.body.rules | Where-Object type -eq 'required_status_checks')
            $check.Count | Should Be 1
            (@($check[0].parameters.required_status_checks.context) -join ',') | Should Be 'a,b,c'
            (@($check[0].parameters.required_status_checks.integration_id) -join ',') | Should Be '1,2,3'
        }
        $global:NfcMockWrites[1].body.conditions.ref_name.include[0] | Should Be 'refs/heads/*.*.x'
        $global:NfcMockWrites[2].body.conditions.ref_name.exclude[0] | Should Be 'refs/heads/*.*.x'
    }

    It 'stops before writing when a check changes during approvals' {
        $global:NfcMockMutateAtApproval = 1
        $backup = Join-Path $TestDrive 'parallel-apply'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'keeps a mismatched readback pending and stops subsequent writes' {
        $global:NfcMockMutateReadbackAt = 1
        $backup = Join-Path $TestDrive 'readback'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 1
        $index = Get-Content (Join-Path $backup 'index.json') -Raw | ConvertFrom-Json -AsHashtable
        $index.transaction.changes[0].status | Should Be 'pending'
    }

    It 'checks restore state after approvals and permits a safe rerun after a prewrite failure' {
        $backup = Join-Path $TestDrive 'restore-rerun'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable No
        $global:NfcMockWrites.Clear()
        $global:NfcMockApprovalCount = 0
        $global:NfcMockMutateAtApproval = 1
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
        $global:NfcMockRulesets['42'].rules[0].parameters.required_status_checks[0].context = 'a'
        $global:NfcMockMutateAtApproval = 0
        $global:NfcMockFailGetId = '45'
        $global:NfcMockFailGetAt = 2
        $global:NfcMockGetCount = 0
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 1
        $global:NfcMockFailGetId = ''
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore
        $global:NfcMockWrites.Count | Should Be 3
        $index = Get-Content (Join-Path $backup 'index.json') -Raw | ConvertFrom-Json -AsHashtable
        @($index.transaction.changes.status | Where-Object { $_ -eq 'restored' }).Count | Should Be 3
    }

    It 'refuses restore when a recorded ruleset changed afterward' {
        $backup = Join-Path $TestDrive 'modified-after'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable Yes
        $global:NfcMockWrites.Clear()
        $global:NfcMockRulesets['45'].enforcement = 'disabled'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -Restore }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'sends no remote write for each declined approval' {
        $backup = Join-Path $TestDrive 'approval-count'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable Yes
        $approvalCount = $global:NfcMockApprovalCount
        $approvalCount | Should BeGreaterThan 10
        for ($n = 1; $n -le $approvalCount; $n++) {
            $global:NfcMockRulesets = New-NfcMockRulesets
            $global:NfcMockWrites.Clear()
            $global:NfcMockApprovalCount = 0
            $global:NfcMockRejectAt = $n
            $backup = Join-Path $TestDrive "decline-$n"
            (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -MainRulesetId 42 -AdminBypassAvailable Yes }) | Should Be $true
            $global:NfcMockWrites.Count | Should Be 0
        }
    }
}

Describe 'NFC G0 process isolation with fake secrets' {
    It 'writes a dry run manifest without requiring Bitwarden or opening a callback flow' {
        Push-Location $TestDrive
        try {
            $output = & "$PSScriptRoot/../New-NfcGitHubApp.ps1" -Owner owner -Repo repo -AppName fake-app -DryRun
            $output | Should Match 'No callback listener was opened'
            (Test-Path -LiteralPath (Join-Path $TestDrive 'nfc-manifest.dry-run.json')) | Should Be $true
            (Test-Path -LiteralPath (Join-Path $TestDrive 'nfc-manifest.dry-run.html')) | Should Be $true
        } finally { Pop-Location }
    }

    It 'keeps the fake helper token out of wrapper stdout, stderr, and the parent environment' {
        $wrapper = Join-Path $TestDrive 'Invoke-NfcGh.ps1'
        Copy-Item "$PSScriptRoot/../Invoke-NfcGh.ps1" $wrapper
        $helper = Join-Path $TestDrive 'nfc-app-token-helper.ps1'
        Set-Content -LiteralPath $helper -Value "[Console]::Out.Write('FAKE_TOKEN_12345'); [Console]::Error.Write('FAKE_TOKEN_12345')"
        Copy-Item $env:ComSpec (Join-Path $TestDrive 'gh.exe')
        $oldPath = $env:PATH
        $oldGhToken = $env:GH_TOKEN
        try {
            $env:PATH = "$TestDrive;$oldPath"
            $env:GH_TOKEN = 'parent-token'
            $psi = [Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            foreach ($arg in @('-NoProfile', '-File', $wrapper, '-Owner', 'owner', '-TargetRepository', 'repo',
                '-ClientId', 'Iv1.fake', '-InstallationId', '1', '-DpapiPath', 'unused',
                '/d', '/c', 'echo', '%GH_TOKEN%', '--repo', 'owner/repo')) { [void]$psi.ArgumentList.Add($arg) }
            $proc = [Diagnostics.Process]::Start($psi)
            try {
                $outTask = $proc.StandardOutput.ReadToEndAsync()
                $errTask = $proc.StandardError.ReadToEndAsync()
                $proc.WaitForExit()
                $output = $outTask.GetAwaiter().GetResult() + $errTask.GetAwaiter().GetResult()
                if ($proc.ExitCode -ne 0) { throw "Fake wrapper failed: $($output.Replace('FAKE_TOKEN_12345', '[redacted]'))" }
                $proc.ExitCode | Should Be 0
                $output.Contains('FAKE_TOKEN_12345') | Should Be $false
                $output | Should Match '\[redacted\]'
                $output | Should Match '--repo owner/repo'
                $env:GH_TOKEN | Should Be 'parent-token'
            } finally { $proc.Dispose() }

            Set-Content -LiteralPath $helper -Value "[Console]::Error.Write('FAKE_PEM FAKE_JWT FAKE_CONVERSION_SECRET FAKE_TOKEN_12345'); exit 1"
            $proc = [Diagnostics.Process]::Start($psi)
            try {
                $outTask = $proc.StandardOutput.ReadToEndAsync()
                $errTask = $proc.StandardError.ReadToEndAsync()
                $proc.WaitForExit()
                $failureOutput = $outTask.GetAwaiter().GetResult() + $errTask.GetAwaiter().GetResult()
                $proc.ExitCode | Should Be 1
                foreach ($fakeSecret in @('FAKE_PEM', 'FAKE_JWT', 'FAKE_CONVERSION_SECRET', 'FAKE_TOKEN_12345')) {
                    $failureOutput.Contains($fakeSecret) | Should Be $false
                }
            } finally { $proc.Dispose() }
        } finally {
            $env:PATH = $oldPath
            $env:GH_TOKEN = $oldGhToken
        }
    }

    It 'ignores a Git credential request without a repo path before reading the fake store' {
        $psi = [Diagnostics.ProcessStartInfo]::new()
        $psi.FileName = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        foreach ($arg in @('-NoProfile', '-File', "$PSScriptRoot/../nfc-app-token-helper.ps1",
            '-Mode', 'git', '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake',
            '-InstallationId', '1', '-DpapiPath', (Join-Path $TestDrive 'nonexistent.dpapi'), 'get')) {
            [void]$psi.ArgumentList.Add($arg)
        }
        $proc = [Diagnostics.Process]::Start($psi)
        try {
            $proc.StandardInput.Write("capability[]=authtype`ncapability[]=state`nprotocol=https`nhost=github.com`nwwwauth[]=Basic realm=GitHub`n`n")
            $proc.StandardInput.Close()
            $outTask = $proc.StandardOutput.ReadToEndAsync()
            $errTask = $proc.StandardError.ReadToEndAsync()
            $proc.WaitForExit()
            $proc.ExitCode | Should Be 0
            $outTask.GetAwaiter().GetResult() | Should Be ''
            $errTask.GetAwaiter().GetResult() | Should Be ''
        } finally { $proc.Dispose() }
    }
}

function Invoke-NfcMockSetup {
    param([string]$WorkDir, [string]$Scenario)
    $source = Get-Content -LiteralPath "$PSScriptRoot/../New-NfcGitHubApp.ps1" -Raw
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
    $replacements = @{
        'Invoke-NfcBitwarden' = "function Invoke-NfcBitwarden { param(`$Arguments) return '{`"status`":`"unlocked`"}' }"
        'Set-NfcPrivateFile' = 'function Set-NfcPrivateFile { param($Path, $Bytes) $global:NfcDpapiCalls++; if ($global:NfcSetupScenario -in @(''dpapi-fail'', ''dpapi-leak'')) { throw ''FAKE_PEM_SECRET DPAPI failed'' } }'
        'Save-NfcBitwardenNote' = 'function Save-NfcBitwardenNote { param($Pem, $Name) $global:NfcBwCalls++; if ($global:NfcSetupScenario -eq ''bw-fail'') { throw ''FAKE_PEM_SECRET Bitwarden failed'' } }'
    }
    $definitions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $replacements.ContainsKey($node.Name) }, $true) | Sort-Object { $_.Extent.StartOffset } -Descending)
    foreach ($definition in $definitions) {
        $start = $definition.Extent.StartOffset
        $end = $definition.Extent.EndOffset
        $source = $source.Substring(0, $start) + $replacements[$definition.Name] + $source.Substring($end)
    }
    if ($Scenario -eq 'dpapi-leak') {
        $source = $source.Replace('Set-NfcPrivateFile -Path $DpapiPath -Bytes $protected',
            "Write-Output 'FAKE_PEM_SECRET_STDOUT'; Write-Error 'FAKE_PEM_SECRET_STDERR' -ErrorAction Continue; Set-NfcPrivateFile -Path `$DpapiPath -Bytes `$protected")
    }
    $source = $source.Replace('. "$PSScriptRoot/NfcG0.Common.ps1"', '. "$PSScriptRoot/NfcG0.Common.ps1"' + "`n" +
        'function Assert-NfcRuntime { }' + "`n" +
        'function Test-NfcRecordingPolicy { return ($global:NfcSetupScenario -eq ''recording'') }')
    Copy-Item "$PSScriptRoot/../NfcG0.Common.ps1" (Join-Path $WorkDir 'NfcG0.Common.ps1')
    $scriptPath = Join-Path $WorkDir 'New-NfcGitHubApp.ps1'
    [IO.File]::WriteAllText($scriptPath, $source)
    $global:NfcSetupScenario = $Scenario
    $global:NfcDpapiCalls = 0
    $global:NfcBwCalls = 0
    $global:NfcConversionCalls = 0
    $oldSession = $env:BW_SESSION
    $env:BW_SESSION = 'FAKE_SESSION_ONLY'
    function global:Read-Host { param($Prompt) return 'YES' }
    function global:Start-Process {
        param($FilePath)
        $socket = [Net.Sockets.TcpClient]::new('127.0.0.1', $port)
        try {
            $bad = [Text.Encoding]::ASCII.GetBytes("GET /callback?code=bad&state=wrong HTTP/1.1`r`nHost: 127.0.0.1:$port`r`n`r`n")
            $socket.GetStream().Write($bad, 0, $bad.Length)
        } finally { $socket.Dispose() }
        $socket = [Net.Sockets.TcpClient]::new('127.0.0.1', $port)
        try {
            $good = [Text.Encoding]::ASCII.GetBytes("GET /callback?code=FAKE_CODE&state=$state HTTP/1.1`r`nHost: 127.0.0.1:$port`r`n`r`n")
            $socket.GetStream().Write($good, 0, $good.Length)
        } finally { $socket.Dispose() }
    }
    function global:Invoke-RestMethod {
        $global:NfcConversionCalls++
        if ($global:NfcSetupScenario -eq 'lost-reply') { throw 'FAKE_PEM_SECRET response lost' }
        if ($global:NfcSetupScenario -eq 'malformed-reply') { return @{ id = 1; pem = 'FAKE_PEM_SECRET' } }
        return @{ id = 1; client_id = 'Iv1.fake'; slug = 'fake-app'; html_url = 'https://example.invalid/app'; pem = 'FAKE_PEM_SECRET' }
    }
    $captured = [Collections.Generic.List[string]]::new()
    try {
        & $scriptPath -Owner owner -Repo repo -AppName fake-app -DpapiPath (Join-Path $WorkDir 'fake.dpapi') 2>&1 |
            ForEach-Object { $captured.Add([string]$_) }
        return @{ output = ($captured -join "`n"); error = '' }
    } catch {
        return @{ output = ($captured -join "`n"); error = $_.Exception.Message }
    } finally {
        $env:BW_SESSION = $oldSession
        Remove-Item Function:\Read-Host -ErrorAction SilentlyContinue
        Remove-Item Function:\Start-Process -ErrorAction SilentlyContinue
        Remove-Item Function:\Invoke-RestMethod -ErrorAction SilentlyContinue
    }
}

Describe 'NFC G0 helper recording stop with a fake secret source' {
    It 'exits before calling the fake DPAPI source when recording is detected' {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../nfc-app-token-helper.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        $definition = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-NfcInstallationToken' }, $true)[0]
        $replacement = 'function Get-NfcInstallationToken { [IO.File]::WriteAllText($env:NFC_TEST_MARKER, ''FAKE_SECRET_SOURCE_CALLED''); return ''FAKE_TOKEN'' }'
        $source = $source.Substring(0, $definition.Extent.StartOffset) + $replacement + $source.Substring($definition.Extent.EndOffset)
        [IO.File]::WriteAllText((Join-Path $TestDrive 'nfc-app-token-helper.ps1'), $source)
        [IO.File]::WriteAllText((Join-Path $TestDrive 'NfcG0.Common.ps1'), 'function Assert-NfcRuntime { }' + "`n" + 'function Test-NfcRecordingPolicy { return $true }')
        $marker = Join-Path $TestDrive 'source-called.txt'
        $oldMarker = $env:NFC_TEST_MARKER
        try {
            $env:NFC_TEST_MARKER = $marker
            $psi = [Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            foreach ($arg in @('-NoProfile', '-File', (Join-Path $TestDrive 'nfc-app-token-helper.ps1'),
                '-Mode', 'token', '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake',
                '-InstallationId', '1', '-DpapiPath', (Join-Path $TestDrive 'fake.dpapi'))) {
                [void]$psi.ArgumentList.Add($arg)
            }
            $proc = [Diagnostics.Process]::Start($psi)
            try {
                $outTask = $proc.StandardOutput.ReadToEndAsync()
                $errTask = $proc.StandardError.ReadToEndAsync()
                $proc.WaitForExit()
                $proc.ExitCode | Should Be 1
                (Test-Path -LiteralPath $marker) | Should Be $false
                $outTask.GetAwaiter().GetResult() | Should Be ''
                $errTask.GetAwaiter().GetResult().Contains('FAKE_TOKEN') | Should Be $false
            } finally { $proc.Dispose() }
        } finally { $env:NFC_TEST_MARKER = $oldMarker }
    }
}

Describe 'NFC G0 setup control flow with fake secrets' {
    It 'stops on recording before touching either fake store or conversion' {
        $result = Invoke-NfcMockSetup -WorkDir $TestDrive -Scenario recording
        $result.error | Should Match 'did not complete'
        $global:NfcDpapiCalls | Should Be 0
        $global:NfcBwCalls | Should Be 0
        $global:NfcConversionCalls | Should Be 0
    }

    It 'reports unknown conversion after a lost or malformed response without exposing fake secrets' {
        foreach ($scenario in @('lost-reply', 'malformed-reply')) {
            $result = Invoke-NfcMockSetup -WorkDir $TestDrive -Scenario $scenario
            $result.error | Should Match 'outcome is unknown'
            $result.error | Should Match 'check GitHub'
            $result.error.Contains('FAKE_PEM_SECRET') | Should Be $false
            $global:NfcDpapiCalls | Should Be 0
            $global:NfcBwCalls | Should Be 0
        }
    }

    It 'covers DPAPI failure, Bitwarden uncertainty, and complete dual storage' {
        foreach ($scenario in @('dpapi-fail', 'bw-fail', 'success')) {
            $result = Invoke-NfcMockSetup -WorkDir $TestDrive -Scenario $scenario
            $result.error.Contains('FAKE_PEM_SECRET') | Should Be $false
            $result.output.Contains('FAKE_PEM_SECRET') | Should Be $false
            $global:NfcConversionCalls | Should Be 1
            $global:NfcDpapiCalls | Should Be 1
            if ($scenario -eq 'dpapi-fail') {
                $result.error | Should Match 'no protected key copy'
                $global:NfcBwCalls | Should Be 0
            } elseif ($scenario -eq 'bw-fail') {
                $result.error | Should Match 'Bitwarden creation may have an unknown outcome'
                $global:NfcBwCalls | Should Be 1
            } else {
                $result.error | Should Be ''
                $result.output | Should Match 'App ID: 1'
                $global:NfcBwCalls | Should Be 1
            }
        }
    }

    It 'detects fake secrets emitted to stdout and stderr before a DPAPI failure' {
        $result = Invoke-NfcMockSetup -WorkDir $TestDrive -Scenario 'dpapi-leak'
        $result.error | Should Match 'no protected key copy'
        $result.output.Contains('FAKE_PEM_SECRET_STDOUT') | Should Be $true
        $result.output.Contains('FAKE_PEM_SECRET_STDERR') | Should Be $true
        (Test-NfcThrows { $result.output.Contains('FAKE_PEM_SECRET') | Should Be $false }) | Should Be $true
    }
}

Describe 'NFC G0 Bitwarden process deadlines with a fake CLI' {
    BeforeAll {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../New-NfcGitHubApp.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        $definition = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-NfcBitwarden' }, $true)[0]
        . ([scriptblock]::Create($definition.Extent.Text))
    }

    It 'drains both pipes and times out a stalled mock process' {
        $fakeBw = Join-Path $TestDrive 'bw.cmd'
        $oldPath = $env:PATH
        try {
            $env:PATH = "$TestDrive;$oldPath"
            [IO.File]::WriteAllText($fakeBw, ((@(
                '@echo off',
                'for /L %%i in (1,1,2000) do @echo fake diagnostic 1>&2',
                'echo {"status":"unlocked"}'
            )) -join "`r`n"), [Text.Encoding]::ASCII)
            $result = Invoke-NfcBitwarden -Arguments @('status') -TimeoutSeconds 10
            ($result | ConvertFrom-Json).status | Should Be 'unlocked'
            [IO.File]::WriteAllText($fakeBw, ((@(
                '@echo off',
                'powershell -NoProfile -Command "Start-Sleep -Seconds 3"',
                'echo {"status":"unlocked"}'
            )) -join "`r`n"), [Text.Encoding]::ASCII)
            (Test-NfcThrows { Invoke-NfcBitwarden -Arguments @('status') -TimeoutSeconds 1 }) | Should Be $true
        } finally { $env:PATH = $oldPath }
    }
}
