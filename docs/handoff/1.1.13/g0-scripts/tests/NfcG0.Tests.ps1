. "$PSScriptRoot/../NfcG0.Common.ps1"

function Test-NfcThrows {
    param([scriptblock]$Action)
    try { $null = & $Action; return $false } catch { return $true }
}

function Get-NfcThrownMessage {
    param([scriptblock]$Action)
    try { $null = & $Action; return $null } catch { return $_.Exception.Message }
}

# GitHub host, repository and credential variables, Git credential prompts and Bitwarden sessions.
function Test-NfcCredentialVariableName {
    param([string]$Name)
    return ($Name -match '^(GH_|GITHUB_|GCM_|BW_|GIT_CONFIG_)' -or $Name -in @('GIT_ASKPASS', 'SSH_ASKPASS', 'GIT_TERMINAL_PROMPT'))
}

# The suite runs only in its own process without such variables (tests/Invoke-NfcG0Tests.ps1), so no
# test reads, saves or restores a real value. Only the names are reported.
$nfcCredentialNames = @(Get-ChildItem Env: | Where-Object { Test-NfcCredentialVariableName $_.Name } | ForEach-Object Name)
if ($nfcCredentialNames.Count -gt 0) {
    throw "Run the G0 tests with tests/Invoke-NfcG0Tests.ps1; this process has: $($nfcCredentialNames -join ', ')"
}

# Every child process gets the same cleaned environment plus the fake values of its test.
function Invoke-NfcTestProcess {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$Arguments, [hashtable]$Environment = @{}, [string]$StandardInput = '',
          [string]$WorkingDirectory = '', [switch]$RawOutput)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
    $psi.UseShellExecute = $false
    if ($WorkingDirectory) { $psi.WorkingDirectory = $WorkingDirectory }
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    foreach ($name in @($psi.Environment.Keys)) {
        if (Test-NfcCredentialVariableName $name) { [void]$psi.Environment.Remove($name) }
    }
    foreach ($key in $Environment.Keys) { $psi.Environment[$key] = $Environment[$key] }
    foreach ($arg in $Arguments) { [void]$psi.ArgumentList.Add($arg) }
    $proc = [Diagnostics.Process]::Start($psi)
    try {
        $proc.StandardInput.Write($StandardInput)
        $proc.StandardInput.Close()
        if ($RawOutput) {
            $outBuffer = [IO.MemoryStream]::new()
            $errBuffer = [IO.MemoryStream]::new()
            $outTask = $proc.StandardOutput.BaseStream.CopyToAsync($outBuffer)
            $errTask = $proc.StandardError.BaseStream.CopyToAsync($errBuffer)
            $proc.WaitForExit()
            $null = $outTask.GetAwaiter().GetResult()
            $null = $errTask.GetAwaiter().GetResult()
            return @{ ExitCode = $proc.ExitCode; OutputBytes = $outBuffer.ToArray(); ErrorBytes = $errBuffer.ToArray() }
        }
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $proc.WaitForExit()
        return @{ ExitCode = $proc.ExitCode; Output = $outTask.GetAwaiter().GetResult()
            Error = $errTask.GetAwaiter().GetResult() }
    } finally { $proc.Dispose() }
}

# A fake gh.exe that records each argument it received (CommandLineToArgvW, UTF-8, base64 per line)
# in NFC_TEST_GH_ARGV and prints GH_TOKEN on stdout and stderr; with NFC_TEST_GH_RAW_PREFIX_B64 it writes
# those raw bytes, then the token and a line feed, to both streams.
function New-NfcFakeGh {
    param([Parameter(Mandatory)][string]$Directory)
    $exe = Join-Path $Directory 'gh.exe'
    if (Test-Path -LiteralPath $exe) { return $exe }
    $null = New-Item -ItemType Directory -Force -Path $Directory
    $source = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

static class FakeGh {
    [DllImport("shell32.dll", SetLastError = true)]
    static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);
    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr memory);

    static int Main() {
        int count;
        IntPtr argv = CommandLineToArgvW(Environment.CommandLine, out count);
        StringBuilder lines = new StringBuilder();
        for (int i = 1; i < count; i++) {
            string arg = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
            lines.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(arg))).Append('\n');
        }
        LocalFree(argv);
        File.WriteAllText(Environment.GetEnvironmentVariable("NFC_TEST_GH_ARGV"), lines.ToString());
        string token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? "";
        string prefix = Environment.GetEnvironmentVariable("NFC_TEST_GH_RAW_PREFIX_B64");
        if (prefix != null) {
            MemoryStream raw = new MemoryStream();
            byte[] head = Convert.FromBase64String(prefix);
            byte[] tail = Encoding.ASCII.GetBytes(token + "\n");
            raw.Write(head, 0, head.Length);
            raw.Write(tail, 0, tail.Length);
            byte[] bytes = raw.ToArray();
            using (Stream output = Console.OpenStandardOutput()) { output.Write(bytes, 0, bytes.Length); }
            using (Stream error = Console.OpenStandardError()) { error.Write(bytes, 0, bytes.Length); }
            return 0;
        }
        Console.Out.Write("gh-token=" + token + "\n");
        Console.Error.Write("gh-token=" + token + "\n");
        return 0;
    }
}
'@
    [IO.File]::WriteAllText((Join-Path $Directory 'FakeGh.cs'), $source)
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $output = & $csc /nologo /target:exe "/out:$exe" (Join-Path $Directory 'FakeGh.cs') 2>&1
    if ($LASTEXITCODE -ne 0) { throw "The fake gh receiver did not compile: $output" }
    return $exe
}

function Read-NfcFakeGhArguments {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $values = [Collections.Generic.List[string]]::new()
    foreach ($line in @([IO.File]::ReadAllText($Path) -split "`n" | Select-Object -SkipLast 1)) {
        $values.Add([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($line)))
    }
    return , $values.ToArray()
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
        $m.default_permissions.Contains('issues') | Should Be $false
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
        foreach ($branch in @($main, $trunk, $release)) {
            $pr = @($branch.rules | Where-Object type -eq 'pull_request')[0].parameters
            $pr.required_approving_review_count | Should Be 0
            $pr.require_code_owner_review | Should Be $true
            $pr.dismiss_stale_reviews_on_push | Should Be $true
            $pr.require_last_push_approval | Should Be $true
        }
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

function Add-NfcMockApprovalRulesets {
    # Existing rulesets with four checks, distinct sources, scope, bypass and extra rules.
    $savedChecks = @($global:NfcMockRulesets['42'].rules | Where-Object type -eq 'required_status_checks') |
        ConvertTo-Json -Depth 50
    foreach ($item in @(@{ id = 42; template = 'RS-1a-to-1k' },
        @{ id = 44; template = 'RS-2' }, @{ id = 45; template = 'RS-3' })) {
        $body = Get-Content "$PSScriptRoot/../rulesets/$($item.template).json" -Raw |
            ConvertFrom-Json -AsHashtable -Depth 50
        $body.id = $item.id
        $body.name = "confirmed live $($item.id)"
        $body.conditions.ref_name.exclude += 'refs/heads/owner-excluded'
        $body.bypass_actors = @(@{ actor_type = 'RepositoryRole'; actor_id = 5; bypass_mode = 'pull_request' })
        $pr = @($body.rules | Where-Object type -eq 'pull_request')[0]
        $pr.parameters.required_approving_review_count = 1
        $pr.parameters.require_code_owner_review = $false
        $pr.parameters.dismiss_stale_reviews_on_push = $false
        $pr.parameters.require_last_push_approval = $false
        $pr.parameters.required_review_thread_resolution = $false
        $pr.parameters.allowed_merge_methods = @('merge', 'squash')
        $body.rules = @($body.rules | Where-Object type -ne 'required_status_checks') +
            @($savedChecks | ConvertFrom-Json -AsHashtable -Depth 50)
        $checks = @($body.rules | Where-Object type -eq 'required_status_checks')[0]
        $checks.parameters.required_status_checks += @{ context = 'governance / authority'; integration_id = 9876 }
        $body.rules += @{ type = 'required_signatures' }
        $global:NfcMockRulesets[[string]$item.id] = $body
    }
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
                $checks = @($global:NfcMockRulesets['42'].rules | Where-Object type -eq 'required_status_checks')[0]
                $checks.parameters.required_status_checks[0].context = 'parallel-change'
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

    It 'updates only four approval fields of confirmed live IDs and restores exact before bodies' {
        Add-NfcMockApprovalRulesets
        $before = $global:NfcMockRulesets | ConvertTo-Json -Depth 50 | ConvertFrom-Json -AsHashtable -Depth 50
        $backup = Join-Path $TestDrive 'r41-update'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44,45
        $global:NfcMockWrites.Count | Should Be 3
        ($global:NfcMockWrites.method -join ',') | Should Be 'PUT,PUT,PUT'
        foreach ($id in @('42', '44', '45')) {
            $expected = Get-NfcWriteBody ($before[$id] | ConvertTo-Json -Depth 50 | ConvertFrom-Json -AsHashtable -Depth 50)
            $pr = @($expected.rules | Where-Object type -eq 'pull_request')[0].parameters
            $pr.required_approving_review_count = 0
            $pr.require_code_owner_review = $true
            $pr.dismiss_stale_reviews_on_push = $true
            $pr.require_last_push_approval = $true
            (Test-NfcSameBody $global:NfcMockRulesets[$id] $expected) | Should Be $true
        }
        $global:NfcMockWrites.Clear()
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore
        $global:NfcMockWrites.Count | Should Be 3
        foreach ($id in @('42', '44', '45', '43')) {
            (Test-NfcSameBody $global:NfcMockRulesets[$id] $before[$id]) | Should Be $true
        }
    }

    It 'requires explicit unique existing active branch IDs and one PR rule for an approval update' {
        foreach ($case in @('missing', 'duplicate', 'absent', 'tag', 'inactive', 'no-pr', 'multiple-pr', 'no-check', 'mixed-mode', 'ids-without-mode', 'bypass-option')) {
            $global:NfcMockRulesets = New-NfcMockRulesets
            Add-NfcMockApprovalRulesets
            $global:NfcMockWrites.Clear()
            $arguments = @{ Owner = 'owner'; Repo = 'repo'; BackupDirectory = (Join-Path $TestDrive "r41-$case"); UpdateApprovals = $true; RulesetIds = @(42) }
            switch ($case) {
                missing { $arguments.Remove('RulesetIds') }
                duplicate { $arguments.RulesetIds = @(42, 42) }
                absent { $arguments.RulesetIds = @(99) }
                tag { $arguments.RulesetIds = @(43) }
                inactive { $global:NfcMockRulesets['42'].enforcement = 'disabled' }
                no-pr { $global:NfcMockRulesets['42'].rules = @($global:NfcMockRulesets['42'].rules | Where-Object type -ne 'pull_request') }
                multiple-pr { $global:NfcMockRulesets['42'].rules += @($global:NfcMockRulesets['42'].rules | Where-Object type -eq 'pull_request')[0] }
                no-check { $global:NfcMockRulesets['42'].rules = @($global:NfcMockRulesets['42'].rules | Where-Object type -ne 'required_status_checks') }
                mixed-mode { $arguments.Restore = $true }
                ids-without-mode { $arguments.Remove('UpdateApprovals'); $arguments.AdminBypassAvailable = 'No' }
                bypass-option { $arguments.AdminBypassAvailable = 'No' }
            }
            $message = Get-NfcThrownMessage { & "$PSScriptRoot/../Set-NfcRulesets.ps1" @arguments }
            $expected = switch ($case) {
                missing { 'explicit positive unique confirmed RulesetIds' }
                duplicate { 'explicit positive unique confirmed RulesetIds' }
                absent { 'Confirmed ruleset 99 is absent' }
                mixed-mode { 'separate modes' }
                ids-without-mode { 'RulesetIds requires UpdateApprovals' }
                bypass-option { 'preserves live bypass' }
                default { 'active branch ruleset with one pull-request rule and one required-check rule' }
            }
            $message | Should Match ([regex]::Escape($expected))
            $global:NfcMockWrites.Count | Should Be 0
        }
    }

    It 'sends no approval update for WhatIf or any declined ID or field confirmation' {
        Add-NfcMockApprovalRulesets
        $backup = Join-Path $TestDrive 'r41-whatif'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44 -WhatIf
        $global:NfcMockWrites.Count | Should Be 0
        $global:NfcMockApprovalCount | Should Be 0
        $backup = Join-Path $TestDrive 'r41-count'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44
        $count = $global:NfcMockApprovalCount
        $count | Should BeGreaterThan 1
        for ($n = 1; $n -le $count; $n++) {
            $global:NfcMockRulesets = New-NfcMockRulesets
            Add-NfcMockApprovalRulesets
            $global:NfcMockWrites.Clear()
            $global:NfcMockApprovalCount = 0
            $global:NfcMockRejectAt = $n
            $backup = Join-Path $TestDrive "r41-decline-$n"
            (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44 }) | Should Be $true
            $global:NfcMockWrites.Count | Should Be 0
        }
    }

    It 'stops an approval update if live checks change during confirmation' {
        Add-NfcMockApprovalRulesets
        $global:NfcMockMutateAtApproval = 1
        $backup = Join-Path $TestDrive 'r41-race'
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44 }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
    }

    It 'retains an approval update partial failure or bad readback as pending and blocks restore' {
        foreach ($case in @('write', 'readback')) {
            $global:NfcMockRulesets = New-NfcMockRulesets
            Add-NfcMockApprovalRulesets
            $global:NfcMockWrites.Clear()
            $global:NfcMockFailWriteAt = if ($case -eq 'write') { 2 } else { 0 }
            $global:NfcMockMutateReadbackAt = if ($case -eq 'readback') { 2 } else { 0 }
            $backup = Join-Path $TestDrive "r41-partial-$case"
            (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44,45 }) | Should Be $true
            $global:NfcMockWrites.Count | Should Be 2
            $index = Get-Content (Join-Path $backup 'index.json') -Raw | ConvertFrom-Json -AsHashtable
            $index.transaction.changes[0].status | Should Be 'applied'
            $index.transaction.changes[1].status | Should Be 'pending'
            $global:NfcMockWrites.Clear()
            (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore }) | Should Be $true
            $global:NfcMockWrites.Count | Should Be 0
        }
    }

    It 'refuses approval-update restore if a live body changed after the transaction' {
        Add-NfcMockApprovalRulesets
        $backup = Join-Path $TestDrive 'r41-stale-restore'
        $null = & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -UpdateApprovals -RulesetIds 42,44
        $global:NfcMockRulesets['44'].conditions.ref_name.exclude += 'refs/heads/later-change'
        $global:NfcMockWrites.Clear()
        (Test-NfcThrows { & "$PSScriptRoot/../Set-NfcRulesets.ps1" -Owner owner -Repo repo -BackupDirectory $backup -Restore }) | Should Be $true
        $global:NfcMockWrites.Count | Should Be 0
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

    It 'keeps the fake helper token out of wrapper output and gives gh only that token' {
        $wrapper = Join-Path $TestDrive 'Invoke-NfcGh.ps1'
        Copy-Item "$PSScriptRoot/../Invoke-NfcGh.ps1" $wrapper
        $helper = Join-Path $TestDrive 'nfc-app-token-helper.ps1'
        Set-Content -LiteralPath $helper -Value "[Console]::Out.Write('FAKE_TOKEN_12345'); [Console]::Error.Write('FAKE_TOKEN_12345')"
        $null = New-NfcFakeGh -Directory (Join-Path $TestDrive 'bin')
        $environment = @{ PATH = (Join-Path $TestDrive 'bin') + ';' + $env:PATH; GH_TOKEN = 'FAKE_CALLER_TOKEN'
            GH_CONFIG_DIR = (Join-Path $TestDrive 'gh-config'); NFC_TEST_GH_ARGV = (Join-Path $TestDrive 'gh-argv.txt') }
        $arguments = @('-NoProfile', '-File', $wrapper, '-Owner', 'owner', '-Repo', 'repo',
            '-ClientId', 'Iv1.fake', '-InstallationId', '1', '-DpapiPath', 'unused', 'api', 'user')
        $result = Invoke-NfcTestProcess -Arguments $arguments -Environment $environment
        $output = $result.Output + $result.Error
        if ($result.ExitCode -ne 0) { throw "Fake wrapper failed: $($output.Replace('FAKE_TOKEN_12345', '[redacted]'))" }
        $output.Contains('FAKE_TOKEN_12345') | Should Be $false
        $output.Contains('FAKE_CALLER_TOKEN') | Should Be $false
        $result.Output | Should Match '(?m)^gh-token=\[redacted\]$'
        $result.Error | Should Match '(?m)^gh-token=\[redacted\]$'

        Set-Content -LiteralPath $helper -Value "[Console]::Error.Write('FAKE_PEM FAKE_JWT FAKE_CONVERSION_SECRET FAKE_TOKEN_12345'); exit 1"
        $result = Invoke-NfcTestProcess -Arguments $arguments -Environment $environment
        $result.ExitCode | Should Be 1
        foreach ($fakeSecret in @('FAKE_PEM', 'FAKE_JWT', 'FAKE_CONVERSION_SECRET', 'FAKE_TOKEN_12345')) {
            ($result.Output + $result.Error).Contains($fakeSecret) | Should Be $false
        }
    }

    It 'ignores a Git credential request without a repo path before reading the fake store' {
        $result = Invoke-NfcTestProcess -Arguments @('-NoProfile', '-File', "$PSScriptRoot/../nfc-app-token-helper.ps1",
            '-Mode', 'git', '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake',
            '-InstallationId', '1', '-DpapiPath', (Join-Path $TestDrive 'nonexistent.dpapi'), 'get') `
            -StandardInput "capability[]=authtype`ncapability[]=state`nprotocol=https`nhost=github.com`nwwwauth[]=Basic realm=GitHub`n`n"
        $result.ExitCode | Should Be 0
        $result.Output | Should Be ''
        $result.Error | Should Be ''
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
        Remove-Item Env:\BW_SESSION -ErrorAction SilentlyContinue
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
        $result = Invoke-NfcTestProcess -Environment @{ NFC_TEST_MARKER = $marker } -Arguments @('-NoProfile', '-File',
            (Join-Path $TestDrive 'nfc-app-token-helper.ps1'), '-Mode', 'token', '-Owner', 'owner', '-Repo', 'repo',
            '-ClientId', 'Iv1.fake', '-InstallationId', '1', '-DpapiPath', (Join-Path $TestDrive 'fake.dpapi'))
        $result.ExitCode | Should Be 1
        (Test-Path -LiteralPath $marker) | Should Be $false
        $result.Output | Should Be ''
        $result.Error.Contains('FAKE_TOKEN') | Should Be $false
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

function New-NfcFakeTokenReply {
    param([hashtable]$Permissions, [string[]]$FullNames = @('owner/repo'), [string]$Token = 'FAKE_INSTALLATION_TOKEN')
    $reply = @{ token = $Token; expires_at = '2026-09-27T01:00:00Z'; permissions = $Permissions
        repositories = @($FullNames | ForEach-Object { @{ full_name = $_ } }) }
    return ($reply | ConvertTo-Json -Depth 5 | ConvertFrom-Json)
}

Describe 'NFC G0 token permission set' {
    BeforeAll {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../nfc-app-token-helper.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -in @('New-NfcTokenPermissionSet', 'Request-NfcInstallationToken') }, $true)) {
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    It 'requests the A1 manifest set without workflows or issues by default' {
        $default = New-NfcTokenPermissionSet
        (($default.Keys | Sort-Object) -join ',') | Should BeExactly 'actions,checks,contents,metadata,pull_requests,statuses'
        $manifest = New-NfcManifest -Owner 'example-owner' -Repo 'example-repo' -AppName 'nfc-agent-example' -Port 49152
        $default.Count | Should Be $manifest.default_permissions.Count
        foreach ($key in $manifest.default_permissions.Keys) {
            ($default[$key] -ceq $manifest.default_permissions[$key]) | Should Be $true
        }
        $default.ContainsKey('workflows') | Should Be $false
        (New-NfcTokenPermissionSet -IncludeWorkflowsWrite:$false).ContainsKey('workflows') | Should Be $false
        $default.ContainsKey('issues') | Should Be $false
        $manifest.default_permissions.Contains('issues') | Should Be $false
        (New-NfcTokenPermissionSet -IncludeIssuesWrite:$false).ContainsKey('issues') | Should Be $false
    }

    It 'adds only workflows write when explicitly asked and never carries it into a later default' {
        $with = New-NfcTokenPermissionSet -IncludeWorkflowsWrite
        $default = New-NfcTokenPermissionSet
        $with.Count | Should Be ($default.Count + 1)
        ($with['workflows'] -ceq 'write') | Should Be $true
        $with.ContainsKey('issues') | Should Be $false
        foreach ($key in $default.Keys) { ($with[$key] -ceq $default[$key]) | Should Be $true }
        $with['contents'] = 'admin'
        (New-NfcTokenPermissionSet).ContainsKey('workflows') | Should Be $false
        ((New-NfcTokenPermissionSet)['contents'] -ceq 'write') | Should Be $true
    }

    It 'adds only issues write when explicitly asked and never carries it into a later default' {
        $with = New-NfcTokenPermissionSet -IncludeIssuesWrite
        $default = New-NfcTokenPermissionSet
        (($with.Keys | Sort-Object) -join ',') | Should BeExactly 'actions,checks,contents,issues,metadata,pull_requests,statuses'
        $with.Count | Should Be ($default.Count + 1)
        ($with['issues'] -ceq 'write') | Should Be $true
        foreach ($key in $default.Keys) { ($with[$key] -ceq $default[$key]) | Should Be $true }
        $with['contents'] = 'admin'
        (New-NfcTokenPermissionSet).ContainsKey('issues') | Should Be $false
        ((New-NfcTokenPermissionSet)['contents'] -ceq 'write') | Should Be $true
    }

    It 'adds only workflows and issues write when both switches are explicitly given' {
        $with = New-NfcTokenPermissionSet -IncludeWorkflowsWrite -IncludeIssuesWrite
        $default = New-NfcTokenPermissionSet
        (($with.Keys | Sort-Object) -join ',') | Should BeExactly 'actions,checks,contents,issues,metadata,pull_requests,statuses,workflows'
        $with.Count | Should Be ($default.Count + 2)
        ($with['workflows'] -ceq 'write') | Should Be $true
        ($with['issues'] -ceq 'write') | Should Be $true
        foreach ($key in $default.Keys) { ($with[$key] -ceq $default[$key]) | Should Be $true }
    }

    It 'builds every token request from the one permission function' {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../nfc-app-token-helper.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        $sets = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.HashtableAst] -and
            @($node.KeyValuePairs | Where-Object { $_.Item1.Extent.Text -eq 'contents' }).Count -gt 0 }, $true))
        $sets.Count | Should Be 1
        $parent = $sets[0].Parent
        while ($parent -isnot [Management.Automation.Language.FunctionDefinitionAst]) { $parent = $parent.Parent }
        $parent.Name | Should Be 'New-NfcTokenPermissionSet'
        $get = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq 'Get-NfcInstallationToken' }, $true)[0]
        $calls = @($get.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -eq 'New-NfcTokenPermissionSet' }, $true))
        $calls.Count | Should Be 1
        $calls[0].Extent.Text | Should BeExactly 'New-NfcTokenPermissionSet -IncludeWorkflowsWrite:$IncludeWorkflowsWrite -IncludeIssuesWrite:$IncludeIssuesWrite'
    }

    It 'sends a fake request limited to one repository with exactly the chosen permissions' {
        Mock Invoke-RestMethod {
            $global:NfcTokenRequests.Add(@{ Uri = "$Uri"; Method = "$Method"; Body = $Body; Redirect = $MaximumRedirection })
            return $global:NfcFakeReply
        }
        foreach ($include in @(@($false, $false), @($true, $false), @($false, $true), @($true, $true))) {
            $global:NfcTokenRequests = [Collections.Generic.List[object]]::new()
            $permissions = New-NfcTokenPermissionSet -IncludeWorkflowsWrite:($include[0]) -IncludeIssuesWrite:($include[1])
            $global:NfcFakeReply = New-NfcFakeTokenReply -Permissions $permissions
            $token = Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $permissions
            $token | Should BeExactly 'FAKE_INSTALLATION_TOKEN'
            $global:NfcTokenRequests.Count | Should Be 1
            $request = $global:NfcTokenRequests[0]
            $request.Uri | Should BeExactly 'https://api.github.com/app/installations/7/access_tokens'
            $request.Method | Should Be 'Post'
            $request.Redirect | Should Be 0
            $body = $request.Body | ConvertFrom-Json -AsHashtable
            (($body.Keys | Sort-Object) -join ',') | Should BeExactly 'permissions,repositories'
            (@($body.repositories) -join ',') | Should BeExactly 'repo'
            $expected = 'actions=read,checks=read,contents=write,metadata=read,pull_requests=write,statuses=read'
            if ($include[1]) { $expected = $expected.Replace('metadata=read', 'issues=write,metadata=read') }
            if ($include[0]) { $expected += ',workflows=write' }
            ((@($body.permissions.Keys | Sort-Object) | ForEach-Object { "$_=$($body.permissions[$_])" }) -join ',') | Should BeExactly $expected
        }
    }

    It 'rejects fake replies that widen, narrow or leave the requested scope' {
        $default = New-NfcTokenPermissionSet
        $with = New-NfcTokenPermissionSet -IncludeWorkflowsWrite
        $workflowsRead = New-NfcTokenPermissionSet -IncludeWorkflowsWrite
        $workflowsRead['workflows'] = 'read'
        $contentsAdmin = New-NfcTokenPermissionSet
        $contentsAdmin['contents'] = 'admin'
        $cases = @(
            @{ Requested = $default; Reply = (New-NfcFakeTokenReply -Permissions $with) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $default) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $workflowsRead) },
            @{ Requested = $default; Reply = (New-NfcFakeTokenReply -Permissions $contentsAdmin) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $with -FullNames @('owner/other')) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $with -FullNames @('Owner/Repo')) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $with -FullNames @('owner/repo', 'owner/other')) },
            @{ Requested = $with; Reply = (New-NfcFakeTokenReply -Permissions $with -Token '') }
        )
        Mock Invoke-RestMethod { return $global:NfcFakeReply }
        foreach ($case in $cases) {
            $global:NfcFakeReply = $case.Reply
            (Test-NfcThrows { Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $case.Requested }) | Should Be $true
        }
        foreach ($requested in @($default, $with)) {
            $global:NfcFakeReply = New-NfcFakeTokenReply -Permissions $requested
            (Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $requested) | Should BeExactly 'FAKE_INSTALLATION_TOKEN'
        }
    }

    It 'rejects a fake token that could break the Git credential response' {
        Mock Invoke-RestMethod { return $global:NfcFakeReply }
        $default = New-NfcTokenPermissionSet
        foreach ($token in @("FAKE_TOKEN`n", "FAKE`rprotocol=https", "FAKE_A`nusername=other", "FAKE$([char]0)TOKEN",
            'FAKE TOKEN', "FAKE`tTOKEN", "FAKE$([char]0x212A)TOKEN", "FAKE$([char]0xE9)TOKEN")) {
            $global:NfcFakeReply = New-NfcFakeTokenReply -Permissions $default -Token $token
            (Get-NfcThrownMessage { Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $default }) |
                Should BeExactly 'Installation token has an unexpected form.'
        }
    }

    It 'rejects missing, read-only or unrequested issues permissions in fake replies' {
        $default = New-NfcTokenPermissionSet
        $with = New-NfcTokenPermissionSet -IncludeIssuesWrite
        $issuesRead = New-NfcTokenPermissionSet -IncludeIssuesWrite
        $issuesRead['issues'] = 'read'
        $cases = @(
            @{ Requested = $with; Granted = $default; Message = 'Installation token lacks the requested issues permission.' },
            @{ Requested = $with; Granted = $issuesRead; Message = 'Installation token permissions differ from the requested set.' },
            @{ Requested = $default; Granted = $with; Message = 'Installation token permissions differ from the requested set.' }
        )
        Mock Invoke-RestMethod { return $global:NfcFakeReply }
        foreach ($case in $cases) {
            $global:NfcFakeReply = New-NfcFakeTokenReply -Permissions $case.Granted
            (Get-NfcThrownMessage { Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $case.Requested }) |
                Should BeExactly $case.Message
        }
    }

    It 'checks the configured owner and repository to the end before reading the key file' {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../nfc-app-token-helper.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        . ([scriptblock]::Create($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq 'Get-NfcInstallationToken' }, $true)[0].Extent.Text))
        $missing = Join-Path $TestDrive 'nonexistent.dpapi'
        foreach ($case in @(@("owner`n", 'repo'), @('owner', "repo`n"), @('own er', 'repo'), @('owner', 'repo/other'),
            @("$([char]0x212A)owner", 'repo'), @('owner', "rep$([char]0x212A)"))) {
            (Get-NfcThrownMessage { Get-NfcInstallationToken -Owner $case[0] -Repo $case[1] -ClientId 'Iv1.fake' -InstallationId 7 -DpapiPath $missing }) |
                Should BeExactly 'Invalid helper configuration.'
        }
        (Get-NfcThrownMessage { Get-NfcInstallationToken -Owner 'owner' -Repo 'repo' -ClientId 'Iv1.fake' -InstallationId 7 -DpapiPath $missing }) |
            Should Not Be 'Invalid helper configuration.'
    }

    It 'accepts a fake reply that omits a requested base permission, as the reviewed version did' {
        # Established handling: the reply may be narrower than the request, never wider or at another level;
        # requested workflows and issues permissions must be present. A missing base permission makes the call
        # that needs it fail at GitHub, and the owner compares the App's permissions with A1.
        Mock Invoke-RestMethod { return $global:NfcFakeReply }
        foreach ($include in @(@($false, $false), @($true, $false), @($false, $true), @($true, $true))) {
            $requested = New-NfcTokenPermissionSet -IncludeWorkflowsWrite:($include[0]) -IncludeIssuesWrite:($include[1])
            $narrower = New-NfcTokenPermissionSet -IncludeWorkflowsWrite:($include[0]) -IncludeIssuesWrite:($include[1])
            $narrower.Remove('actions')
            $global:NfcFakeReply = New-NfcFakeTokenReply -Permissions $narrower
            (Request-NfcInstallationToken -Owner owner -Repo repo -InstallationId 7 -Jwt 'FAKE.JWT.VALUE' -Permissions $requested) | Should BeExactly 'FAKE_INSTALLATION_TOKEN'
        }
    }
}

function Invoke-NfcFakeTokenSource {
    param([string]$WorkDir, [string[]]$Arguments, [string]$StandardInput = '')
    $source = Get-Content -LiteralPath "$PSScriptRoot/../nfc-app-token-helper.ps1" -Raw
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
    $definition = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-NfcInstallationToken' }, $true)[0]
    $replacement = @'
function Get-NfcInstallationToken {
    param([string]$Owner, [string]$Repo, [string]$ClientId, [long]$InstallationId, [string]$DpapiPath,
          [switch]$IncludeWorkflowsWrite, [switch]$IncludeIssuesWrite)
    $set = New-NfcTokenPermissionSet -IncludeWorkflowsWrite:$IncludeWorkflowsWrite -IncludeIssuesWrite:$IncludeIssuesWrite
    [IO.File]::WriteAllText($env:NFC_TEST_MARKER, ((@($set.Keys | Sort-Object) | ForEach-Object { "$_=$($set[$_])" }) -join ','))
    return 'FAKE_TOKEN_67890'
}
'@
    $source = $source.Substring(0, $definition.Extent.StartOffset) + $replacement + $source.Substring($definition.Extent.EndOffset)
    [IO.File]::WriteAllText((Join-Path $WorkDir 'nfc-app-token-helper.ps1'), $source)
    $common = (Get-Content -LiteralPath "$PSScriptRoot/../NfcG0.Common.ps1" -Raw) +
        "`nfunction Assert-NfcRuntime { }`nfunction Test-NfcRecordingPolicy { return `$false }`n"
    [IO.File]::WriteAllText((Join-Path $WorkDir 'NfcG0.Common.ps1'), $common)
    $marker = Join-Path $WorkDir 'token-source.txt'
    if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    $result = Invoke-NfcTestProcess -Environment @{ NFC_TEST_MARKER = $marker } -StandardInput $StandardInput `
        -Arguments (@('-NoProfile', '-File', (Join-Path $WorkDir 'nfc-app-token-helper.ps1')) + $Arguments)
    $result.Marker = $null
    if (Test-Path -LiteralPath $marker) { $result.Marker = [IO.File]::ReadAllText($marker) }
    return $result
}

Describe 'NFC G0 helper workflows and issues switches with a fake token source' {
    It 'passes the explicit switch to the token request in token and Git modes, and only then' {
        $default = 'actions=read,checks=read,contents=write,metadata=read,pull_requests=write,statuses=read'
        $common = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '1',
            '-DpapiPath', (Join-Path $TestDrive 'nonexistent.dpapi'))
        $inScope = "capability[]=authtype`nprotocol=https`nhost=github.com`npath=owner/repo.git`n`n"

        $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'token') + $common)
        $result.ExitCode | Should Be 0
        $result.Output | Should BeExactly 'FAKE_TOKEN_67890'
        $result.Marker | Should BeExactly $default

        $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'token') + $common + @('-IncludeWorkflowsWrite'))
        $result.ExitCode | Should Be 0
        $result.Marker | Should BeExactly "$default,workflows=write"

        $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'git') + $common + @('get')) -StandardInput $inScope
        $result.ExitCode | Should Be 0
        $result.Output | Should BeExactly "username=x-access-token`npassword=FAKE_TOKEN_67890`n`n"
        $result.Marker | Should BeExactly $default

        $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'git') + $common + @('-IncludeWorkflowsWrite', 'get')) -StandardInput $inScope
        $result.ExitCode | Should Be 0
        $result.Output | Should BeExactly "username=x-access-token`npassword=FAKE_TOKEN_67890`n`n"
        $result.Marker | Should BeExactly "$default,workflows=write"
    }

    It 'passes the issues switch independently and with workflows in token and Git modes' {
        $common = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '1',
            '-DpapiPath', (Join-Path $TestDrive 'nonexistent.dpapi'))
        $inScope = "capability[]=authtype`nprotocol=https`nhost=github.com`npath=owner/repo.git`n`n"
        $issues = 'actions=read,checks=read,contents=write,issues=write,metadata=read,pull_requests=write,statuses=read'
        $cases = @(
            @{ Switches = @('-IncludeIssuesWrite'); Expected = $issues },
            @{ Switches = @('-IncludeWorkflowsWrite', '-IncludeIssuesWrite'); Expected = "$issues,workflows=write" }
        )
        foreach ($case in $cases) {
            $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'token') + $common + $case.Switches)
            $result.ExitCode | Should Be 0
            $result.Output | Should BeExactly 'FAKE_TOKEN_67890'
            $result.Marker | Should BeExactly $case.Expected
            $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments (@('-Mode', 'git') + $common + $case.Switches + @('get')) -StandardInput $inScope
            $result.ExitCode | Should Be 0
            $result.Output | Should BeExactly "username=x-access-token`npassword=FAKE_TOKEN_67890`n`n"
            $result.Marker | Should BeExactly $case.Expected
        }
    }

    It 'keeps the exact-repository Git scope and store and erase no-ops with each switch or both' {
        $common = @('-Mode', 'git', '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '1',
            '-DpapiPath', (Join-Path $TestDrive 'nonexistent.dpapi'))
        foreach ($switches in @(@('-IncludeWorkflowsWrite'), @('-IncludeIssuesWrite'), @('-IncludeWorkflowsWrite', '-IncludeIssuesWrite'))) {
            $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments ($common + $switches + @('get')) -StandardInput "protocol=https`nhost=github.com`npath=owner/repo`n`n"
            $result.Output | Should BeExactly "username=x-access-token`npassword=FAKE_TOKEN_67890`n`n"
            foreach ($request in @("protocol=https`nhost=github.com`npath=other/repo.git`n`n",
                "protocol=https`nhost=github.com`n`n", "protocol=https`nhost=evil.example`npath=owner/repo.git`n`n")) {
                $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments ($common + $switches + @('get')) -StandardInput $request
                $result.ExitCode | Should Be 0
                $result.Output | Should BeExactly ''
                ($null -eq $result.Marker) | Should Be $true
            }
            foreach ($action in @('store', 'erase')) {
                $result = Invoke-NfcFakeTokenSource -WorkDir $TestDrive -Arguments ($common + $switches + @($action)) -StandardInput "protocol=https`nhost=github.com`npath=owner/repo.git`n`n"
                $result.ExitCode | Should Be 0
                $result.Output | Should BeExactly ''
                ($null -eq $result.Marker) | Should Be $true
            }
        }
    }
}

Describe 'NFC G0 gh wrapper argument handling' {
    BeforeAll {
        $source = Get-Content -LiteralPath "$PSScriptRoot/../Invoke-NfcGh.ps1" -Raw
        $tokens = $null; $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
        foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
            . ([scriptblock]::Create($definition.Extent.Text))
        }
        $script:NfcWrapperPath = 'C:\nfc-fake\scripts\Invoke-NfcGh.ps1'
        $script:NfcWrapperPrefix = @('C:\fake\pwsh.dll', '-NoProfile', '-File', $script:NfcWrapperPath,
            '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'C:\fake\app-key.dpapi')
    }

    It 'keeps the existing call forms and forwards their gh arguments unchanged' {
        $forms = @(
            @('pr', 'create', '--base', '9.9.x', '--title', 'G0 check', '--body', 'fake body'),
            @('pr', 'create', '--repo', 'owner/repo', '--title', 't', '--body', 'b'),
            @('api', 'graphql', '-f', 'query=mutation { resolveReviewThread(input: {threadId: "FAKE_THREAD"}) { thread { isResolved } } }'),
            @('pr', 'merge', '461', '--merge', '--match-head-commit', 'c524e7d2b3244288e8169e87a932dc8752379c2d')
        )
        foreach ($gh in $forms) {
            foreach ($separator in @(@(), @('--'))) {
                $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + $separator + $gh) -ScriptPath $script:NfcWrapperPath
                ($parsed.GhArguments -join "`n") | Should BeExactly ($gh -join "`n")
                $parsed.Owner | Should BeExactly 'owner'
                $parsed.Repo | Should BeExactly 'repo'
                $parsed.ClientId | Should BeExactly 'Iv1.fake'
                $parsed.InstallationId | Should BeExactly '7'
                $parsed.DpapiPath | Should BeExactly 'C:\fake\app-key.dpapi'
                $parsed.IncludeWorkflowsWrite | Should Be $false
                $parsed.IncludeIssuesWrite | Should Be $false
                (Test-NfcThrows { Assert-NfcGhRepository -GhArguments $parsed.GhArguments -Owner owner -Repo repo -EnvironmentRepo $null }) | Should Be $false
            }
        }
    }

    It 'forwards gh options that share names with wrapper or PowerShell parameters' {
        $gh = @('--', 'pr', 'list', '--owner', 'x', '-c', '-d', '-i', '-o', '-r', '-e', '-p', '-w', '-v', '--verbose', '--debug',
            '-Owner', '-ClientId', '-ErrorAction', '--head=owner:feature/x', '--jq=.a:b', 'two words', '')
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + $gh) -ScriptPath $script:NfcWrapperPath
        ($parsed.GhArguments -join "`n") | Should BeExactly (($gh | Select-Object -Skip 1) -join "`n")
        $parsed.GhArguments.Count | Should Be ($gh.Count - 1)
        (Test-NfcThrows { Assert-NfcGhRepository -GhArguments $parsed.GhArguments -Owner owner -Repo repo -EnvironmentRepo $null }) | Should Be $false
    }

    It 'accepts every repository option form that names the configured repository' {
        foreach ($gh in @(@('pr', 'view', '--repo', 'owner/repo'), @('pr', 'view', '--repo=owner/repo'),
            @('pr', 'view', '-R', 'owner/repo'), @('pr', 'view', '-Rowner/repo'), @('pr', 'view', '-R=owner/repo'))) {
            $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + $gh) -ScriptPath $script:NfcWrapperPath
            ($parsed.GhArguments -join "`n") | Should BeExactly ($gh -join "`n")
            (Test-NfcThrows { Assert-NfcGhRepository -GhArguments $parsed.GhArguments -Owner owner -Repo repo -EnvironmentRepo 'owner/repo' }) | Should Be $false
        }
    }

    It 'refuses any other repository selection with a message naming the configured one' {
        foreach ($gh in @(@('pr', 'view', '--repo', 'other/repo'), @('pr', 'view', '--repo=owner/other'),
            @('pr', 'view', '-R', 'Owner/Repo'), @('pr', 'view', '-R', 'github.com/owner/repo'),
            @('pr', 'view', '-Rother/repo'), @('pr', 'view', '-R=other/repo'), @('pr', 'view', '--repo'),
            @('pr', 'view', '-wR', 'owner/repo'), @('pr', 'create', '--title', '-Release'))) {
            $message = Get-NfcThrownMessage { Assert-NfcGhRepository -GhArguments $gh -Owner owner -Repo repo -EnvironmentRepo $null }
            $message | Should Match 'limited to owner/repo'
        }
        $message = Get-NfcThrownMessage { Assert-NfcGhRepository -GhArguments @('pr', 'list') -Owner owner -Repo repo -EnvironmentRepo 'other/repo' }
        $message | Should Match 'GH_REPO names another repository'
        $message | Should Match 'limited to owner/repo'
    }

    It 'checks the wrapper owner and repository case-sensitively, so a Kelvin sign is not a letter' {
        foreach ($pair in @(@("$([char]0x212A)owner", 'repo'), @('owner', "rep$([char]0x212A)"))) {
            $line = @('C:\fake\pwsh.dll', '-NoProfile', '-File', $script:NfcWrapperPath, '-Owner', $pair[0], '-Repo', $pair[1],
                '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'C:\fake\app-key.dpapi', 'pr', 'list')
            (Get-NfcThrownMessage { Split-NfcGhCommandLine -CommandLine $line -ScriptPath $script:NfcWrapperPath }) |
                Should BeExactly 'Wrapper options -Owner and -Repo must name one GitHub repository.'
        }
    }

    It 'sets the workflows switch only when given among the wrapper options' {
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-IncludeWorkflowsWrite', '--', 'pr', 'list')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeWorkflowsWrite | Should Be $true
        ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list'
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-includeworkflowswrite', 'pr', 'list')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeWorkflowsWrite | Should Be $true
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('pr', 'list', '-IncludeWorkflowsWrite')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeWorkflowsWrite | Should Be $false
        ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list -IncludeWorkflowsWrite'
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('--', '-IncludeWorkflowsWrite', 'pr')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeWorkflowsWrite | Should Be $false
        $message = Get-NfcThrownMessage { Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-IncludeWorkflowsWrite', '-IncludeWorkflowsWrite', 'pr')) -ScriptPath $script:NfcWrapperPath }
        $message | Should Match 'more than once'
    }

    It 'sets the issues switch only when given among the wrapper options' {
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-IncludeIssuesWrite', '--', 'pr', 'list')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeIssuesWrite | Should Be $true
        $parsed.IncludeWorkflowsWrite | Should Be $false
        ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list'
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-includeissueswrite', 'pr', 'list')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeIssuesWrite | Should Be $true
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('pr', 'list', '-IncludeIssuesWrite')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeIssuesWrite | Should Be $false
        ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list -IncludeIssuesWrite'
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('--', '-IncludeIssuesWrite', 'pr')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeIssuesWrite | Should Be $false
        ($parsed.GhArguments -join ' ') | Should BeExactly '-IncludeIssuesWrite pr'
        foreach ($duplicate in @('-IncludeIssuesWrite', '-includeissueswrite')) {
            (Get-NfcThrownMessage { Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-IncludeIssuesWrite', $duplicate, 'pr')) -ScriptPath $script:NfcWrapperPath }) |
                Should BeExactly 'Wrapper option -IncludeIssuesWrite is given more than once.'
        }
        $parsed = Split-NfcGhCommandLine -CommandLine ($script:NfcWrapperPrefix + @('-IncludeIssuesWrite', '-IncludeWorkflowsWrite', 'pr', 'list')) -ScriptPath $script:NfcWrapperPath
        $parsed.IncludeIssuesWrite | Should Be $true
        $parsed.IncludeWorkflowsWrite | Should Be $true
        ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list'
    }

    It 'refuses malformed wrapper options and other launch forms with clear messages' {
        $base = @('C:\fake\pwsh.dll', '-NoProfile', '-File', $script:NfcWrapperPath)
        $cases = @(
            @{ Line = $base + @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-DpapiPath', 'k', 'pr', 'list'); Message = '-InstallationId is required' },
            @{ Line = $script:NfcWrapperPrefix + @('-Repo', 'repo', 'pr', 'list'); Message = '-Repo is given more than once' },
            @{ Line = $script:NfcWrapperPrefix + @('--repo', 'owner/repo', 'pr', 'list'); Message = "Unknown wrapper option '--repo'" },
            @{ Line = $script:NfcWrapperPrefix + @('-R', 'owner/repo', 'pr', 'list'); Message = "Unknown wrapper option '-R'" },
            @{ Line = $base + @('-Owner', '-Repo', 'repo', 'pr'); Message = '-Owner needs a value' },
            @{ Line = $base + @('-Owner', 'own/er', '-Repo', 'repo', '-ClientId', 'c', '-InstallationId', '7', '-DpapiPath', 'k', 'pr'); Message = 'must name one GitHub repository' },
            @{ Line = $base + @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'c', '-InstallationId', '0', '-DpapiPath', 'k', 'pr'); Message = 'positive number' },
            @{ Line = $script:NfcWrapperPrefix; Message = 'No gh arguments' },
            @{ Line = $script:NfcWrapperPrefix + @('--'); Message = 'No gh arguments' }
        )
        foreach ($case in $cases) {
            $message = Get-NfcThrownMessage { Split-NfcGhCommandLine -CommandLine $case.Line -ScriptPath $script:NfcWrapperPath }
            $message | Should Match ([regex]::Escape($case.Message))
        }
    }

    It 'reads only the PowerShell start-up options and lets the first execution mode decide' {
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'k')
        foreach ($launch in @(@('-NoProfile', '-File'), @('-NonInteractive', '-NoLogo', '-NoProfile', '-f'), @('-noprofile', '-file'))) {
            $parsed = Split-NfcGhCommandLine -CommandLine (@('C:\fake\pwsh.dll') + $launch + @($script:NfcWrapperPath) + $options + @('pr', 'list')) -ScriptPath $script:NfcWrapperPath
            ($parsed.GhArguments -join ' ') | Should BeExactly 'pr list'
        }
        $refused = @(
            @('-NoProfile', '-Command', "& $($script:NfcWrapperPath) pr list"),
            (@('-NoProfile', '-Command', '-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-NoProfile', '-File', 'C:\nfc-fake\scripts\Other.ps1', '-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-NoProfile', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-EncodedCommand', 'ZgBhAGsAZQA=', '-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            @('-NoProfile', '-File'),
            (@('-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-NonInteractive', '-File', $script:NfcWrapperPath) + $options + @('pr', 'list')),
            (@('-NoProfile', '-File', '.\Invoke-NfcGh.ps1') + $options + @('pr', 'list')),
            (@('-NoProfile', '-File', 'Invoke-NfcGh.ps1') + $options + @('pr', 'list')),
            (@('-NoProfile', '-File', '\nfc-fake\scripts\Invoke-NfcGh.ps1') + $options + @('pr', 'list')),
            (@('-NoProfile', '-File', 'C:nfc-fake\scripts\Invoke-NfcGh.ps1') + $options + @('pr', 'list'))
        )
        foreach ($launch in $refused) {
            $message = Get-NfcThrownMessage { Split-NfcGhCommandLine -CommandLine (@('C:\fake\pwsh.dll') + $launch) -ScriptPath $script:NfcWrapperPath }
            $message | Should Match 'Start the wrapper as its own process'
        }
    }

    It 'requires that the process ran the wrapper itself, not another script, a profile or a command' {
        $path = $script:NfcWrapperPath
        (Test-NfcThrows { Assert-NfcStandaloneInvocation -CommandOrigin 'Runspace' -CallStackScripts @($path) -ScriptPath $path }) | Should Be $false
        (Test-NfcThrows { Assert-NfcStandaloneInvocation -CommandOrigin 'Runspace' -CallStackScripts @($path.ToUpperInvariant()) -ScriptPath $path }) | Should Be $false
        foreach ($case in @(
            @{ Origin = 'Internal'; Stack = @($path, 'C:\nfc-fake\scripts\Other.ps1') },
            @{ Origin = 'Internal'; Stack = @($path, 'C:\Users\fake\Documents\PowerShell\Microsoft.PowerShell_profile.ps1') },
            @{ Origin = 'Internal'; Stack = @($path) },
            @{ Origin = 'Runspace'; Stack = @($path, '') },
            @{ Origin = 'Runspace'; Stack = @('C:\nfc-fake\impostor\Invoke-NfcGh.ps1') },
            @{ Origin = 'Runspace'; Stack = @() }
        )) {
            $message = Get-NfcThrownMessage { Assert-NfcStandaloneInvocation -CommandOrigin $case.Origin -CallStackScripts $case.Stack -ScriptPath $path }
            $message | Should Match 'Start the wrapper as its own process'
        }
    }
}

function New-NfcFakeWrapperDirectory {
    param([string]$WorkDir)
    $null = New-NfcFakeGh -Directory (Join-Path $WorkDir 'bin')
    Copy-Item "$PSScriptRoot/../Invoke-NfcGh.ps1" (Join-Path $WorkDir 'Invoke-NfcGh.ps1') -Force
    [IO.File]::WriteAllText((Join-Path $WorkDir 'nfc-app-token-helper.ps1'),
        "[IO.File]::WriteAllText(`$env:NFC_TEST_HELPER_ARGS, (`$args -join `"``n`")); [Console]::Out.Write('FAKE_TOKEN_24680')")
    [IO.File]::WriteAllText((Join-Path $WorkDir 'Other.ps1'),
        "& (Join-Path `$PSScriptRoot 'Invoke-NfcGh.ps1') -Owner owner -Repo repo -ClientId Iv1.fake -InstallationId 7 -DpapiPath unused pr list`nexit `$LASTEXITCODE`n")
    # A script with the wrapper's name elsewhere: it moves the process working directory to the real
    # wrapper's folder, so a relative -File path would now name the real wrapper, and then calls it.
    $impostor = Join-Path $WorkDir 'impostor'
    $null = New-Item -ItemType Directory -Force -Path $impostor
    $real = Join-Path $WorkDir 'Invoke-NfcGh.ps1'
    [IO.File]::WriteAllText((Join-Path $impostor 'Invoke-NfcGh.ps1'),
        "[Environment]::CurrentDirectory = '$WorkDir'`nSet-Location -LiteralPath '$WorkDir'`n" +
        "& '$real' -Owner owner -Repo repo -ClientId Iv1.fake -InstallationId 7 -DpapiPath unused pr list`nexit `$LASTEXITCODE`n")
}

function Invoke-NfcFakeWrapper {
    param([string]$WorkDir, [string[]]$Arguments = @(), [string[]]$Launch, [string]$WorkingDirectory = '',
          [hashtable]$ExtraEnvironment = @{}, [switch]$RawOutput)
    $helperArgs = Join-Path $WorkDir 'helper-args.txt'
    $ghArgv = Join-Path $WorkDir 'gh-argv.txt'
    foreach ($file in @($helperArgs, $ghArgv)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    if (-not $Launch) { $Launch = @('-NoProfile', '-File', (Join-Path $WorkDir 'Invoke-NfcGh.ps1')) }
    $environment = @{ PATH = (Join-Path $WorkDir 'bin') + ';' + $env:PATH; GH_CONFIG_DIR = (Join-Path $WorkDir 'gh-config')
        NFC_TEST_HELPER_ARGS = $helperArgs; NFC_TEST_GH_ARGV = $ghArgv }
    foreach ($key in $ExtraEnvironment.Keys) { $environment[$key] = $ExtraEnvironment[$key] }
    $result = Invoke-NfcTestProcess -Arguments ($Launch + $Arguments) -Environment $environment -WorkingDirectory $WorkingDirectory -RawOutput:$RawOutput
    $result.HelperArguments = $null
    if (Test-Path -LiteralPath $helperArgs) { $result.HelperArguments = [IO.File]::ReadAllText($helperArgs) }
    $result.GhArguments = Read-NfcFakeGhArguments -Path $ghArgv
    return $result
}

function ConvertTo-NfcArgumentJson {
    param([AllowNull()][AllowEmptyCollection()][string[]]$Values)
    return (ConvertTo-Json -InputObject @($Values) -Compress)
}

Describe 'NFC G0 gh wrapper process with a fake helper and a fake gh' {
    It 'runs the existing call forms and the fixed --repo form through one fake gh process' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $helperExpected = (@('-Mode', 'token') + $options) -join "`n"
        $forms = @(
            @('pr', 'create', '--base', '9.9.x', '--head', 'feature/1.1.13/g0-check', '--title', 't', '--body', 'b'),
            @('pr', 'create', '--repo', 'owner/repo', '--title', 't', '--body', 'b'),
            @('api', 'graphql', '-f', 'query=mutation { resolveReviewThread(input: {threadId: "FAKE_THREAD"}) { thread { isResolved } } }'),
            @('pr', 'merge', '461', '--merge', '--match-head-commit', 'c524e7d2b3244288e8169e87a932dc8752379c2d')
        )
        foreach ($gh in $forms) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $gh)
            if ($result.ExitCode -ne 0) { throw "Fake wrapper failed: $($result.Error)" }
            (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
            $result.Output | Should Match '(?m)^gh-token=\[redacted\]$'
            ($result.Output + $result.Error).Contains('FAKE_TOKEN_24680') | Should Be $false
            $result.HelperArguments | Should BeExactly $helperExpected
        }
    }

    It 'passes quotes, empty and Unicode arguments and trailing backslashes exactly and asks for workflows only when told to' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $unicode = "$([char]0x7E41)$([char]0x9AD4) $([char]0x2713) caf$([char]0xE9)"
        $gh = @('api', 'graphql', '-f', "query=query {`n  viewer { login }`n}", '-f', 'body=say "hi there"', '-F', 'a\"b', '',
            $unicode, 'C:\path\', 'C:\my dir\', '--head=owner:feature/x', '-R', 'owner/repo', '%PATH%', '^&|<>', '-v')
        $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + @('-IncludeWorkflowsWrite', '--') + $gh)
        if ($result.ExitCode -ne 0) { throw "Fake wrapper failed: $($result.Error)" }
        $result.GhArguments.Count | Should Be $gh.Count
        (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
        $result.HelperArguments | Should BeExactly ((@('-Mode', 'token') + $options + @('-IncludeWorkflowsWrite')) -join "`n")
        $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + @('--') + $gh)
        $result.ExitCode | Should Be 0
        (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
        $result.HelperArguments | Should BeExactly ((@('-Mode', 'token') + $options) -join "`n")
    }

    It 'asks for issues only among wrapper options and passes workflows before issues when both are requested' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $gh = @('issue', 'view', '1', '--repo', 'owner/repo')
        $cases = @(
            @{ Switches = @('-IncludeIssuesWrite'); Helper = @('-IncludeIssuesWrite') },
            @{ Switches = @('-IncludeWorkflowsWrite', '-IncludeIssuesWrite'); Helper = @('-IncludeWorkflowsWrite', '-IncludeIssuesWrite') },
            @{ Switches = @('-IncludeIssuesWrite', '-IncludeWorkflowsWrite'); Helper = @('-IncludeWorkflowsWrite', '-IncludeIssuesWrite') }
        )
        foreach ($case in $cases) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $case.Switches + @('--') + $gh)
            if ($result.ExitCode -ne 0) { throw "Fake wrapper failed: $($result.Error)" }
            (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
            $result.HelperArguments | Should BeExactly ((@('-Mode', 'token') + $options + $case.Helper) -join "`n")
        }
        $gh = @('issue', 'list', '-IncludeIssuesWrite')
        foreach ($separator in @(@(), @('--'))) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $separator + $gh)
            $result.ExitCode | Should Be 0
            (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
            $result.HelperArguments | Should BeExactly ((@('-Mode', 'token') + $options) -join "`n")
        }
    }

    It 'stops another repository before starting the helper or gh' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        foreach ($gh in @(@('pr', 'create', '--repo', 'other/repo'), @('--', 'pr', 'view', '-R', 'owner/other'))) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $gh)
            $result.ExitCode | Should Be 64
            $result.Error | Should Match 'NFC gh wrapper usage error'
            $result.Error | Should Match 'limited to owner/repo'
            $result.Output | Should BeExactly ''
            ($null -eq $result.HelperArguments) | Should Be $true
            ($null -eq $result.GhArguments) | Should Be $true
        }
    }

    It 'refuses a launch through -Command or another script before starting the helper or gh' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $wrapper = Join-Path $TestDrive 'Invoke-NfcGh.ps1'
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $launches = @(
            @{ Launch = @('-NoProfile', '-Command', "& '$wrapper' $($options -join ' ') pr list"); Arguments = @() },
            @{ Launch = @('-NoProfile', '-File', (Join-Path $TestDrive 'Other.ps1'), '-File', $wrapper)
               Arguments = ($options + @('pr', 'view', '--repo', 'owner/repo')) }
        )
        foreach ($case in $launches) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Launch $case.Launch -Arguments $case.Arguments
            $result.ExitCode | Should Not Be 0
            $result.Error | Should Match 'NFC gh wrapper usage error: Start the wrapper as its own process'
            ($null -eq $result.HelperArguments) | Should Be $true
            ($null -eq $result.GhArguments) | Should Be $true
        }
    }

    It 'passes gh output bytes unchanged and redacts the token after CJK text, a byte order mark, invalid UTF-8 or binary bytes' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $prefixes = @(
            [Text.Encoding]::UTF8.GetBytes("$([char]0x7E41)$([char]0x9AD4) $([char]0x2713) caf$([char]0xE9) x$([char]0x9AD4)"),
            [byte[]]@(0xFF, 0xFE, 0x41),
            [byte[]]@(0xC3, 0x28, 0xE4, 0xBD),
            [byte[]]@(0x00, 0xFF, 0x80, 0x0D, 0x0A, 0x1B)
        )
        $redacted = [Text.Encoding]::ASCII.GetBytes("[redacted]`n")
        foreach ($prefix in $prefixes) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + @('pr', 'view', '1')) `
                -ExtraEnvironment @{ NFC_TEST_GH_RAW_PREFIX_B64 = [Convert]::ToBase64String($prefix) } -RawOutput
            $result.ExitCode | Should Be 0
            $expected = [Convert]::ToBase64String([byte[]]($prefix + $redacted))
            [Convert]::ToBase64String($result.OutputBytes) | Should BeExactly $expected
            [Convert]::ToBase64String($result.ErrorBytes) | Should BeExactly $expected
        }
    }

    It 'refuses gh alias and gh extension before starting the helper or gh' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        $refused = @(@('alias', 'set', 'x', '--shell', 'echo $GH_TOKEN'), @('extension', 'install', 'owner/gh-x'),
            @('ext', 'list'), @('extensions', 'list'), @('--', 'alias', 'list'))
        foreach ($gh in $refused) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $gh)
            $result.ExitCode | Should Be 64
            $result.Error | Should Match 'NFC gh wrapper usage error: gh (alias|extension|extensions|ext) is refused'
            $result.Output | Should BeExactly ''
            ($null -eq $result.HelperArguments) | Should Be $true
            ($null -eq $result.GhArguments) | Should Be $true
        }
        foreach ($gh in @(@('--', '--help=false', 'alias', 'set', 'x', 'version'), @('--', '--version'), @('--', '-R', 'owner/repo', 'pr', 'list'))) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $gh)
            $result.ExitCode | Should Be 64
            $result.Error | Should Match 'NFC gh wrapper usage error: gh arguments must start with the gh command'
            ($null -eq $result.HelperArguments) | Should Be $true
            ($null -eq $result.GhArguments) | Should Be $true
        }
        $gh = @('search', 'repos', 'alias', 'extension')
        $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -Arguments ($options + $gh)
        $result.ExitCode | Should Be 0
        (ConvertTo-NfcArgumentJson $result.GhArguments) | Should BeExactly (ConvertTo-NfcArgumentJson $gh)
    }

    It 'refuses a same-name script that moves the working directory and then calls the wrapper' {
        New-NfcFakeWrapperDirectory -WorkDir $TestDrive
        $impostor = Join-Path $TestDrive 'impostor'
        $options = @('-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused')
        foreach ($launchPath in @('.\Invoke-NfcGh.ps1', (Join-Path $impostor 'Invoke-NfcGh.ps1'))) {
            $result = Invoke-NfcFakeWrapper -WorkDir $TestDrive -WorkingDirectory $impostor `
                -Launch @('-NoProfile', '-File', $launchPath) -Arguments ($options + @('pr', 'view', '--repo', 'owner/repo'))
            $result.ExitCode | Should Be 64
            $result.Error | Should Match 'NFC gh wrapper usage error: Start the wrapper as its own process'
            ($null -eq $result.HelperArguments) | Should Be $true
            ($null -eq $result.GhArguments) | Should Be $true
        }
    }
}

function Invoke-NfcFakeOpenPr {
    param([string]$WorkDir, [string[]]$Arguments, [string]$Scenario = 'match',
          [string]$Author = 'app/nfc-agent-dennis40816', [switch]$RealProcess, [hashtable]$Environment = @{})
    $source = Get-Content -LiteralPath "$PSScriptRoot/../open-pr.ps1" -Raw
    $fake = @'
function Invoke-NfcPrWrapper {
    param([string[]]$Arguments)
    if ($env:NFC_TEST_PR_CHECK_HOST_ENV -eq '1') {
        foreach ($name in @('GH_HOST', 'GH_ENTERPRISE_TOKEN', 'GITHUB_ENTERPRISE_TOKEN')) {
            if (Test-Path "Env:$name") { throw "Unexpected wrapper environment variable: $name" }
        }
    }
    [IO.File]::AppendAllText($env:NFC_TEST_PR_CALLS, (ConvertTo-Json -InputObject $Arguments -Compress) + "`n")
    $command = $Arguments[([Array]::IndexOf($Arguments, '--') + 2)]
    $scenario = $env:NFC_TEST_PR_SCENARIO
    if ($command -eq 'create') {
        if ($scenario -eq 'create-fail') { return @{ ExitCode = 1; Output = 'fake create output'; Error = 'fake gh create failed' } }
        if ($scenario -eq 'no-url') { return @{ ExitCode = 0; Output = 'fake missing PR URL'; Error = '' } }
        return @{ ExitCode = 0; Output = "Creating pull request...`nhttps://github.com/owner/repo/pull/42`n"; Error = '' }
    }
    if ($command -eq 'close') {
        if ($scenario -eq 'close-fail') { return @{ ExitCode = 1; Output = ''; Error = 'fake gh close failed' } }
        return @{ ExitCode = 0; Output = 'Closed pull request'; Error = '' }
    }
    if ($command -ne 'view') { throw "Unexpected fake command: $command" }
    if ($scenario -eq 'view-throws') { throw 'fake view process failed' }
    if ($scenario -eq 'view-fail') { return @{ ExitCode = 1; Output = ''; Error = 'fake gh view failed' } }
    $reply = @{ author = @{ login = $env:NFC_TEST_PR_AUTHOR }; url = 'https://github.com/owner/repo/pull/42'; number = 42 }
    if ($scenario -eq 'no-author') { $reply.Remove('author') }
    if ($scenario -eq 'blank-author') { $reply.author.login = ' ' }
    if ($scenario -eq 'invalid-author') { $reply.author.login = @('fake', 'author') }
    if ($scenario -eq 'other-number') { $reply.number = 99 }
    if ($scenario -eq 'other-url') { $reply.url = 'https://github.com/owner/repo/pull/99' }
    $output = if ($scenario -eq 'bad-json') { 'not JSON' } else { $reply | ConvertTo-Json -Depth 4 -Compress }
    return @{ ExitCode = 0; Output = $output; Error = '' }
}
'@
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) { throw 'open-pr.ps1 has parse errors.' }
    $definitions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Invoke-NfcPrWrapper' }, $true))
    if ($definitions.Count -ne 1) { throw 'The open-pr wrapper call seam is missing or duplicated.' }
    if (-not $RealProcess) {
        $definition = $definitions[0]
        $source = $source.Substring(0, $definition.Extent.StartOffset) + $fake + $source.Substring($definition.Extent.EndOffset)
    }
    $scriptPath = Join-Path $WorkDir 'open-pr.ps1'
    [IO.File]::WriteAllText($scriptPath, $source)
    # This sibling is always fake, even if a test accidentally reaches the real process seam.
    $wrapper = $fake + @'

$result = Invoke-NfcPrWrapper -Arguments ([Environment]::GetCommandLineArgs() | Select-Object -Skip 1)
[Console]::Out.Write($result.Output)
[Console]::Error.Write($result.Error)
exit $result.ExitCode
'@
    [IO.File]::WriteAllText((Join-Path $WorkDir 'Invoke-NfcGh.ps1'), $wrapper)
    [IO.File]::WriteAllText((Join-Path $WorkDir 'body file.md'), 'fake PR body')
    $calls = Join-Path $WorkDir 'pr-calls.jsonl'
    if (Test-Path -LiteralPath $calls) { Remove-Item -LiteralPath $calls }
    $processEnvironment = @{ NFC_TEST_PR_CALLS = $calls; NFC_TEST_PR_SCENARIO = $Scenario; NFC_TEST_PR_AUTHOR = $Author }
    foreach ($key in $Environment.Keys) { $processEnvironment[$key] = $Environment[$key] }
    $result = Invoke-NfcTestProcess -Arguments (@('-NoProfile', '-File', $scriptPath) + $Arguments) `
        -Environment $processEnvironment
    $result.Calls = @()
    if (Test-Path -LiteralPath $calls) {
        $result.Calls = @(Get-Content -LiteralPath $calls | ForEach-Object { ,($_ | ConvertFrom-Json) })
    }
    return $result
}

Describe 'NFC G0 open PR as the App' {
    BeforeAll {
        $script:NfcOpenPrArguments = @('-Repo', 'owner/repo', '-Base', '1.2.x', '-Head', 'feature/1.2.5/test',
            '-Title', 'fix: test "quoted" title', '-BodyFile', (Join-Path $TestDrive 'body file.md'),
            '-ClientId', 'Iv1.fake', '-InstallationId', '7', '-DpapiPath', 'unused fake path')
        $script:NfcOpenPrPrefix = @('-NoProfile', '-File', (Join-Path $TestDrive 'Invoke-NfcGh.ps1'),
            '-Owner', 'owner', '-Repo', 'repo', '-ClientId', 'Iv1.fake', '-InstallationId', '7',
            '-DpapiPath', 'unused fake path', '--')
    }

    It 'rejects missing required options with 64 before any wrapper call' {
        for ($i = 0; $i -lt $script:NfcOpenPrArguments.Count; $i += 2) {
            $arguments = @($script:NfcOpenPrArguments[0..($script:NfcOpenPrArguments.Count - 1)] |
                Select-Object -Index (@(0..($script:NfcOpenPrArguments.Count - 1)) | Where-Object { $_ -notin @($i, ($i + 1)) }))
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $arguments
            $result.ExitCode | Should Be 64
            $result.Calls.Count | Should Be 0
            $result.Error | Should Match 'usage error'
            $result.Output | Should BeExactly ''
        }
    }

    It 'validates all values, unknown and duplicate options before any wrapper call' {
        $cases = @(
            @{ Index = 1; Value = 'owner' }, @{ Index = 1; Value = 'owner/repo/extra' },
            @{ Index = 1; Value = 'owner/rep ' }, @{ Index = 1; Value = "owner/rep`n" },
            @{ Index = 1; Value = "$([char]0x212A)owner/repo" }, @{ Index = 1; Value = "owner/rep$([char]0x212A)" },
            @{ Index = 3; Value = ' ' }, @{ Index = 5; Value = "branch`n" },
            @{ Index = 7; Value = '' }, @{ Index = 7; Value = ' ' }, @{ Index = 7; Value = '-Release' },
            @{ Index = 9; Value = (Join-Path $TestDrive 'missing.md') }, @{ Index = 9; Value = $TestDrive },
            @{ Index = 11; Value = ' ' }, @{ Index = 11; Value = '-client' },
            @{ Index = 13; Value = '0' }, @{ Index = 13; Value = '-1' }, @{ Index = 13; Value = '01' },
            @{ Index = 13; Value = '1234567890123456789' }, @{ Index = 13; Value = "7`n" },
            @{ Index = 15; Value = ' ' }, @{ Index = 15; Value = "fake`npath" }
        )
        foreach ($case in $cases) {
            $arguments = [string[]]$script:NfcOpenPrArguments.Clone()
            $arguments[$case.Index] = $case.Value
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $arguments
            $result.ExitCode | Should Be 64
            $result.Calls.Count | Should Be 0
        }
        foreach ($extra in @(@('-Unknown', 'value'), @('-Repo', 'owner/repo'), @('-Draft', '-Draft'),
            @('-ExpectedAuthor', ''), @('-ExpectedAuthor', ' '), @('-ExpectedAuthor', "app/fake`n"), @('-Title'))) {
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments ($script:NfcOpenPrArguments + $extra)
            $result.ExitCode | Should Be 64
            $result.Calls.Count | Should Be 0
        }
    }

    It 'passes exactly the create and view arguments without extra permissions and prints the URL last' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments
        $result.ExitCode | Should Be 0
        $result.Calls.Count | Should Be 2
        ($result.Calls[0] -join "`n") | Should BeExactly (($script:NfcOpenPrPrefix + @('pr', 'create',
            '--repo', 'owner/repo', '--base', '1.2.x', '--head', 'feature/1.2.5/test', '--title', 'fix: test "quoted" title',
            '--body-file', (Join-Path $TestDrive 'body file.md'))) -join "`n")
        ($result.Calls[1] -join "`n") | Should BeExactly (($script:NfcOpenPrPrefix +
            @('pr', 'view', '42', '--repo', 'owner/repo', '--json', 'author,url,number')) -join "`n")
        ($result.Output.TrimEnd() -split "`r?`n")[-1] | Should BeExactly 'https://github.com/owner/repo/pull/42'
        $result.Error | Should BeExactly ''
    }

    It 'rejects ExpectedAuthor as an unknown option with 64 before any wrapper call' {
        foreach ($author in @('app/nfc-agent-dennis40816', 'app/fake-agent')) {
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive `
                -Arguments ($script:NfcOpenPrArguments + @('-ExpectedAuthor', $author))
            $result.ExitCode | Should Be 64
            $result.Calls.Count | Should Be 0
            $result.Error | Should Match 'usage error: Unknown option'
            $result.Error | Should Match ([regex]::Escape('-ExpectedAuthor'))
            $result.Output | Should BeExactly ''
        }
    }

    It 'rejects a non-GitHub.com GH_HOST with 64 before any wrapper call' {
        foreach ($hostName in @('enterprise.example', 'github.com.example', 'https://github.com', ' github.com', 'github.com ')) {
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments `
                -Environment @{ GH_HOST = $hostName }
            $result.ExitCode | Should Be 64
            $result.Calls.Count | Should Be 0
            $result.Error | Should Match 'usage error: GH_HOST'
            $result.Output | Should BeExactly ''
        }
    }

    It 'removes host and enterprise credential variables from the wrapper process environment' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -RealProcess `
            -Environment @{ GH_HOST = 'GiThUb.CoM'; GH_ENTERPRISE_TOKEN = 'fake enterprise value';
                GITHUB_ENTERPRISE_TOKEN = 'another fake enterprise value'; NFC_TEST_PR_CHECK_HOST_ENV = '1' }
        $result.ExitCode | Should Be 0
        $result.Calls.Count | Should Be 2
        $result.Error | Should BeExactly ''
        $result.Output.TrimEnd() | Should BeExactly 'https://github.com/owner/repo/pull/42'
    }

    It 'adds draft only to create when requested' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments ($script:NfcOpenPrArguments + @('-Draft'))
        $result.ExitCode | Should Be 0
        $result.Calls[0][-1] | Should BeExactly '--draft'
        ($result.Calls[1] -contains '--draft') | Should Be $false
        (($result.Calls | ForEach-Object { $_ }) -match '^-Include(Workflows|Issues)Write$').Count | Should Be 0
    }

    It 'closes a wrong author including a case-only mismatch and reports both authors and URL' {
        foreach ($author in @('owner', 'app/fake-agent', 'app/NFC-agent-dennis40816')) {
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -Author $author
            $result.ExitCode | Should Be 1
            $result.Calls.Count | Should Be 3
            ($result.Calls[2][0..($script:NfcOpenPrPrefix.Count + 5)] -join "`n") | Should BeExactly (($script:NfcOpenPrPrefix +
                @('pr', 'close', '42', '--repo', 'owner/repo', '--comment')) -join "`n")
            $reason = $result.Calls[2][-1]
            $reason | Should Match ([regex]::Escape('app/nfc-agent-dennis40816'))
            $reason | Should Match ([regex]::Escape($author))
            $reason | Should Match 'https://github.com/owner/repo/pull/42'
            $result.Error | Should Match ([regex]::Escape($reason))
            $result.Output | Should BeExactly ''
        }
    }

    It 'closes when the author cannot be read or the readback identifies a different PR' {
        foreach ($scenario in @('view-fail', 'view-throws', 'bad-json', 'no-author', 'blank-author',
            'invalid-author', 'other-number', 'other-url')) {
            $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -Scenario $scenario
            $result.ExitCode | Should Be 1
            $result.Calls.Count | Should Be 3
            $result.Calls[2][$script:NfcOpenPrPrefix.Count + 1] | Should BeExactly 'close'
            $result.Calls[2][$script:NfcOpenPrPrefix.Count + 2] | Should BeExactly '42'
            $result.Error | Should Match 'app/nfc-agent-dennis40816'
            $result.Error | Should Match 'unreadable'
            $result.Error | Should Match 'https://github.com/owner/repo/pull/42'
        }
    }

    It 'prints gh create failure and never views or closes a PR' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -Scenario 'create-fail'
        $result.ExitCode | Should Be 1
        $result.Calls.Count | Should Be 1
        $result.Error | Should Match 'fake gh create failed'
        $result.Error | Should Match 'fake create output'
    }

    It 'fails explicitly when create returns no PR URL' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -Scenario 'no-url'
        $result.ExitCode | Should Be 1
        $result.Calls.Count | Should Be 1
        $result.Error | Should Match 'URL'
    }

    It 'reports a failed close without claiming success' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -Scenario 'close-fail' -Author 'owner'
        $result.ExitCode | Should Be 1
        $result.Calls.Count | Should Be 3
        $result.Error | Should Match 'fake gh close failed'
        $result.Error | Should Match 'owner'
        $result.Error | Should Match 'https://github.com/owner/repo/pull/42'
    }

    It 'starts the sibling fake wrapper in its own no-profile process with a fully qualified path' {
        $result = Invoke-NfcFakeOpenPr -WorkDir $TestDrive -Arguments $script:NfcOpenPrArguments -RealProcess
        $result.ExitCode | Should Be 0
        $result.Calls.Count | Should Be 2
        foreach ($call in $result.Calls) {
            ($call[0..2] -join "`n") | Should BeExactly (($script:NfcOpenPrPrefix[0..2]) -join "`n")
            [IO.Path]::IsPathFullyQualified($call[2]) | Should Be $true
        }
        $result.Output.TrimEnd() | Should BeExactly 'https://github.com/owner/repo/pull/42'
    }
}

Describe 'NFC G0 isolated test runner' {
    It 'accepts only Pester 3.4.0, returns 2 without a valid executed test, 1 for a failed test or block and 0 only for a passing run' {
        $runner = "$PSScriptRoot/Invoke-NfcG0Tests.ps1"
        $files = @{ Pass = 'Describe "fake" { It "passes" { 1 | Should Be 1 } }'
            Fail = 'Describe "fake" { It "fails" { 1 | Should Be 2 } }'
            CleanupFails = 'Describe "fake" { AfterAll { throw "fake cleanup failure" }; It "passes" { 1 | Should Be 1 } }'
            Empty = '# no tests'
            Pending = 'Describe "fake" { It "waits" -Pending { }; It "skips" -Skip { } }' }
        foreach ($name in $files.Keys) { [IO.File]::WriteAllText((Join-Path $TestDrive "$name.Tests.ps1"), $files[$name]) }
        # Fake Pester 3.4.0 modules, each first in its own module path: no result, a result without valid
        # counts, and a manifest that cannot load.
        $fakes = @{ 'fake-null' = 'return $null'
            'fake-invalid' = "return [pscustomobject]@{ TotalCount = 'x'; PassedCount = 'y'; FailedCount = 'z' }"
            'fake-broken' = $null }
        # A fake Pester 5.7.1 whose result has a passing test count but a failed cleanup block.
        $fakes['fake-pester5'] = 'return [pscustomobject]@{ TotalCount = 1; PassedCount = 1; FailedCount = 0; FailedBlocksCount = 1; Result = ''Failed'' }'
        foreach ($name in $fakes.Keys) {
            $version = '3.4.0'
            if ($name -eq 'fake-pester5') { $version = '5.7.1' }
            $folder = Join-Path $TestDrive "$name/Pester/$version"
            $null = New-Item -ItemType Directory -Force -Path $folder
            $rootModule = 'Pester.psm1'
            if ($null -eq $fakes[$name]) { $rootModule = 'Missing.psm1' } else {
                [IO.File]::WriteAllText((Join-Path $folder 'Pester.psm1'), "function Invoke-Pester { param(`$Script, [switch]`$PassThru) $($fakes[$name]) }")
            }
            [IO.File]::WriteAllText((Join-Path $folder 'Pester.psd1'),
                "@{ ModuleVersion = '$version'; RootModule = '$rootModule'; FunctionsToExport = @('Invoke-Pester') }")
        }
        $pass = Join-Path $TestDrive 'Pass.Tests.ps1'
        $cases = @(
            @{ Arguments = @('-PesterVersion', '5.7.1', '-TestPath', $pass); Modules = 'fake-pester5'; Exit = 2; Message = "Only Pester 3.4.0 is supported; '5.7.1'" },
            @{ Arguments = @('-PesterVersion', '0.0.1', '-TestPath', $pass); Exit = 2; Message = "Only Pester 3.4.0 is supported; '0.0.1'" },
            @{ Arguments = @('-PesterVersion', '3.4', '-TestPath', $pass); Exit = 2; Message = 'Only Pester 3.4.0 is supported' },
            @{ Arguments = @('-TestPath', (Join-Path $TestDrive 'Empty.Tests.ps1')); Exit = 2; Message = 'no valid result' },
            @{ Arguments = @('-TestPath', (Join-Path $TestDrive 'Pending.Tests.ps1')); Exit = 2; Message = 'no valid result' },
            @{ Arguments = @('-TestPath', (Join-Path $TestDrive 'Missing.Tests.ps1')); Exit = 2; Message = 'does not exist' },
            @{ Arguments = @('-TestPath', $pass); Modules = 'fake-null'; Exit = 2; Message = 'Pester returned no result' },
            @{ Arguments = @('-TestPath', $pass); Modules = 'fake-invalid'; Exit = 2; Message = 'no valid result' },
            @{ Arguments = @('-TestPath', $pass); Modules = 'fake-broken'; Exit = 2; Message = 'fake-broken' },
            @{ Arguments = @('-TestPath', (Join-Path $TestDrive 'Fail.Tests.ps1')); Exit = 1; Message = '1 of 1 failed' },
            @{ Arguments = @('-TestPath', (Join-Path $TestDrive 'CleanupFails.Tests.ps1')); Exit = 1; Message = '1 of 2 failed' },
            @{ Arguments = @('-TestPath', $pass); Exit = 0; Message = '' }
        )
        foreach ($case in $cases) {
            $environment = @{}
            if ($case.ContainsKey('Modules')) { $environment.PSModulePath = (Join-Path $TestDrive $case.Modules) + ';' + $env:PSModulePath }
            $result = Invoke-NfcTestProcess -Arguments (@('-NoProfile', '-File', $runner) + $case.Arguments) -Environment $environment
            if ($result.ExitCode -ne $case.Exit) {
                throw "Runner exit $($result.ExitCode), expected $($case.Exit), for $($case.Arguments -join ' '): $($result.Error)"
            }
            if ($case.Exit -eq 2) { $result.Error | Should Match 'NFC G0 test runner failed' }
            if ($case.Message) { $result.Error | Should Match ([regex]::Escape($case.Message)) }
        }
    }
}
