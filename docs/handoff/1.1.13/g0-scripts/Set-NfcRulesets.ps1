#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Owner,
    [Parameter(Mandatory)][string]$Repo,
    [Parameter(Mandatory)][string]$BackupDirectory,
    [ValidateSet('Yes', 'No')][string]$AdminBypassAvailable,
    [long]$MainRulesetId = 22009240,
    [switch]$UpdateApprovals,
    [long[]]$RulesetIds,
    [switch]$WhatIf,
    [switch]$Restore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-NfcGhGet {
    param([string]$Path)
    $json = & gh api --method GET $Path
    if ($LASTEXITCODE -ne 0) { throw 'A GitHub read failed.' }
    return ($json | ConvertFrom-Json -AsHashtable -Depth 50)
}

function Get-NfcPagedRulesets {
    param([string]$Base)
    $result = [Collections.Generic.List[object]]::new()
    for ($page = 1; $page -le 100; $page++) {
        $items = @(Invoke-NfcGhGet "$Base/rulesets?per_page=100&page=$page")
        foreach ($item in $items) { $result.Add($item) }
        if ($items.Count -lt 100) { return $result.ToArray() }
    }
    throw 'Ruleset pagination exceeded the safety limit.'
}

function Invoke-NfcGhWrite {
    param([ValidateSet('POST', 'PUT')][string]$Method, [string]$Path, [hashtable]$Body)
    $json = $Body | ConvertTo-Json -Depth 50 -Compress
    $reply = $json | & gh api --method $Method $Path --input -
    if ($LASTEXITCODE -ne 0) { throw 'A GitHub ruleset write failed. Inspect the transaction record and live state.' }
    if (-not $reply) { throw 'GitHub write returned no ruleset. Inspect the transaction record and live state.' }
    return ($reply | ConvertFrom-Json -AsHashtable -Depth 50)
}

function Get-NfcWriteBody {
    param([hashtable]$Snapshot)
    $body = @{}
    foreach ($key in @('name', 'target', 'enforcement', 'conditions', 'rules', 'bypass_actors')) {
        if ($Snapshot.ContainsKey($key)) { $body[$key] = $Snapshot[$key] }
    }
    return $body
}

function ConvertTo-NfcCanonicalJson {
    param($Value)
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [System.Collections.IDictionary]) {
        $parts = foreach ($key in @($Value.Keys | Sort-Object)) {
            (($key | ConvertTo-Json -Compress) + ':' + (ConvertTo-NfcCanonicalJson $Value[$key]))
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [array]) {
        $parts = foreach ($item in $Value) { ConvertTo-NfcCanonicalJson $item }
        return '[' + ($parts -join ',') + ']'
    }
    return ($Value | ConvertTo-Json -Depth 50 -Compress)
}

function Test-NfcSameBody {
    param([hashtable]$Left, [hashtable]$Right)
    return (ConvertTo-NfcCanonicalJson (Get-NfcWriteBody $Left)) -ceq
        (ConvertTo-NfcCanonicalJson (Get-NfcWriteBody $Right))
}

function Assert-NfcLiveRulesets {
    param([string]$Base, [hashtable]$Expected)
    $ids = @(Get-NfcPagedRulesets $Base | ForEach-Object { [string]$_.id } | Sort-Object)
    if (($ids -join ',') -cne (@($Expected.Keys | Sort-Object) -join ',')) {
        throw 'Ruleset list changed during owner review or transaction. Stop and reconcile live state.'
    }
    foreach ($id in $Expected.Keys) {
        $live = Invoke-NfcGhGet "$Base/rulesets/$id"
        if (-not (Test-NfcSameBody $live $Expected[$id])) {
            throw "Ruleset $id changed during owner review or transaction. Stop and reconcile live state."
        }
    }
}

function Show-NfcApproval {
    param([string]$Label, $Before, $After, [switch]$WhatIf)
    $old = if ($null -eq $Before) { '<absent>' } else { $Before | ConvertTo-Json -Depth 50 -Compress }
    $new = if ($null -eq $After) { '<absent>' } else { $After | ConvertTo-Json -Depth 50 -Compress }
    Write-Output "$Label`n  current: $old`n  proposed: $new"
    if ($WhatIf) { return }
    if ((Read-Host "Approve ${Label}? Type YES") -cne 'YES') { throw "Owner declined $Label. No remote write was sent." }
}

function Read-NfcTemplate {
    param([string]$Name)
    return (Get-Content -LiteralPath (Join-Path $PSScriptRoot "rulesets/$Name.json") -Raw |
        ConvertFrom-Json -AsHashtable -Depth 50)
}

function Assert-NfcRulesetShape {
    param([hashtable]$Ruleset)
    if ($Ruleset.target -ne 'branch' -or $Ruleset.enforcement -ne 'active' -or
        @($Ruleset.rules | Where-Object type -eq 'pull_request').Count -ne 1 -or
        @($Ruleset.rules | Where-Object type -eq 'required_status_checks').Count -ne 1) {
        throw 'Expected an active branch ruleset with one pull-request rule and one required-check rule.'
    }
}

function Assert-NfcTagRuleset {
    param([hashtable]$Ruleset, [hashtable]$Template)
    if ($Ruleset.enforcement -cne 'active' -or $Ruleset.target -cne $Template.target -or
        (ConvertTo-NfcCanonicalJson @($Ruleset.conditions.ref_name.include)) -cne
            (ConvertTo-NfcCanonicalJson @($Template.conditions.ref_name.include)) -or
        (ConvertTo-NfcCanonicalJson @($Ruleset.conditions.ref_name.exclude)) -cne
            (ConvertTo-NfcCanonicalJson @($Template.conditions.ref_name.exclude)) -or
        @($Ruleset.bypass_actors).Count -ne 0 -or
        (ConvertTo-NfcCanonicalJson @($Ruleset.rules.type | Sort-Object)) -cne
            (ConvertTo-NfcCanonicalJson @($Template.required_rule_types | Sort-Object))) {
        throw 'RS-4 differs from required active tag protection. Owner must inspect it; no automatic repair is allowed.'
    }
}

function Save-NfcIndex {
    param([string]$Path, [hashtable]$Index)
    $temp = "$Path.tmp"
    $Index | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $temp -Encoding utf8
    [IO.File]::Move($temp, $Path, $true)
}

try {
    if (-not $IsWindows -or $PSVersionTable.PSVersion -lt [version]'7.4') {
        throw 'Windows and PowerShell 7.4 or later are required.'
    }
    if ($Owner -notmatch '^[A-Za-z0-9-]+$' -or $Repo -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Invalid repository name.' }
    if ($Restore -and ($UpdateApprovals -or $PSBoundParameters.ContainsKey('RulesetIds'))) {
        throw 'Restore and approval update are separate modes; restore uses the recorded IDs.'
    }
    if ($UpdateApprovals) {
        if (-not $RulesetIds -or @($RulesetIds | Where-Object { $_ -le 0 }).Count -gt 0 -or
            @($RulesetIds | Sort-Object -Unique).Count -ne $RulesetIds.Count) {
            throw 'Approval update requires explicit positive unique confirmed RulesetIds.'
        }
        if ($PSBoundParameters.ContainsKey('AdminBypassAvailable')) {
            throw 'Approval update preserves live bypass; do not pass AdminBypassAvailable.'
        }
    } elseif ($PSBoundParameters.ContainsKey('RulesetIds')) {
        throw 'RulesetIds requires UpdateApprovals; the initial G0 mode does not update existing IDs.'
    }
    if (-not $Restore -and -not $UpdateApprovals -and -not $AdminBypassAvailable) { throw 'Specify -AdminBypassAvailable Yes or No after checking the owner UI.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'The owner must install and authenticate gh.' }
    $login = & gh api user --jq .login
    if ($LASTEXITCODE -ne 0 -or $login -cne $Owner) {
        throw 'gh is not authenticated as the specified owner account.'
    }
    $base = "/repos/$Owner/$Repo"
    $backupPath = [IO.Path]::GetFullPath($BackupDirectory)
    $indexPath = Join-Path $backupPath 'index.json'

    if ($Restore) {
        if (-not (Test-Path -LiteralPath $indexPath)) { throw 'Backup index is missing.' }
        $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json -AsHashtable -Depth 50
        if ($index.repository -cne "$Owner/$Repo" -or -not $index.ContainsKey('transaction')) {
            throw 'Backup belongs to another repository or has no precise transaction record.'
        }
        $changes = @($index.transaction.changes)
        if (@($changes | Where-Object { $_.status -like '*pending' }).Count) {
            throw 'A write has an unknown outcome. Owner must reconcile its ID and live state before restore.'
        }
        $restorePlan = [Collections.Generic.List[object]]::new()
        foreach ($change in $changes) {
            if ($change.status -ne 'applied') { continue }
            $current = Invoke-NfcGhGet "$base/rulesets/$($change.id)"
            $after = Get-Content -LiteralPath (Join-Path $backupPath $change.after_file) -Raw |
                ConvertFrom-Json -AsHashtable -Depth 50
            if (-not (Test-NfcSameBody $current $after)) {
                throw "Ruleset $($change.id) changed after G0. Owner must reconcile before restore."
            }
            if ($change.kind -in @('main', 'approval-update')) {
                $before = Get-Content -LiteralPath (Join-Path $backupPath "ruleset-$($change.id).json") -Raw |
                    ConvertFrom-Json -AsHashtable -Depth 50
                $desired = Get-NfcWriteBody $before
            } elseif ($change.kind -in @('RS-2', 'RS-3')) {
                $desired = Get-NfcWriteBody $current
                $desired.enforcement = 'disabled'
            } else {
                throw 'Unknown transaction kind; owner must reconcile before restore.'
            }
            $restorePlan.Add(@{ change = $change; current = $current; desired = $desired })
        }
        foreach ($item in $restorePlan) {
            Show-NfcApproval -Label "Restore G0 $($item.change.kind) ruleset $($item.change.id)" `
                -Before (Get-NfcWriteBody $item.current) -After $item.desired -WhatIf:$WhatIf
        }
        if ($WhatIf) { return }
        foreach ($item in $restorePlan) {
            $change = $item.change
            $live = Invoke-NfcGhGet "$base/rulesets/$($change.id)"
            if (-not (Test-NfcSameBody $live $item.current)) {
                throw "Ruleset $($change.id) changed during restore approval. Stop and reconcile live state."
            }
            $change.status = 'restore_pending'
            Save-NfcIndex $indexPath $index
            $null = Invoke-NfcGhWrite PUT "$base/rulesets/$($change.id)" $item.desired
            $readback = Invoke-NfcGhGet "$base/rulesets/$($change.id)"
            if (-not (Test-NfcSameBody $readback $item.desired)) {
                throw "Restore of ruleset $($change.id) has an unknown outcome. Owner must reconcile live state."
            }
            $change.status = 'restored'
            Save-NfcIndex $indexPath $index
        }
        return
    }

    if (Test-Path -LiteralPath $backupPath) { throw 'Use a new backup directory so existing evidence is preserved.' }
    $null = New-Item -ItemType Directory -Path $backupPath
    $list = @(Get-NfcPagedRulesets $base)
    $listedIds = @($list | ForEach-Object { [string]$_.id })
    if (@($listedIds | Where-Object { $_ -notmatch '^[1-9][0-9]*$' }).Count -gt 0 -or
        @($listedIds | Sort-Object -Unique).Count -ne $listedIds.Count) {
        throw 'Ruleset pages contain a missing or duplicate ID.'
    }
    $repoSettings = Invoke-NfcGhGet $base
    $repoSettings | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $backupPath 'repository.json') -Encoding utf8
    ConvertTo-Json -InputObject $list -Depth 50 | Set-Content -LiteralPath (Join-Path $backupPath 'rulesets-list.json') -Encoding utf8
    $snapshots = @{}
    foreach ($entry in $list) {
        $snapshot = Invoke-NfcGhGet "$base/rulesets/$($entry.id)"
        $snapshots[[string]$entry.id] = $snapshot
        $snapshot | ConvertTo-Json -Depth 50 |
            Set-Content -LiteralPath (Join-Path $backupPath "ruleset-$($entry.id).json") -Encoding utf8
    }
    $verifiedIds = @(Get-NfcPagedRulesets $base | ForEach-Object { [string]$_.id })
    if ((@($listedIds | Sort-Object) -join ',') -cne (@($verifiedIds | Sort-Object) -join ',')) {
        throw 'Ruleset list changed during backup. Start with a new backup directory.'
    }
    $index = @{ repository = "$Owner/$Repo"; rulesets = @($list | ForEach-Object { @{ id = $_.id; name = $_.name } });
        transaction = @{ changes = @() } }
    Save-NfcIndex $indexPath $index
    Write-Output "Backup exported to $backupPath"

    if ($UpdateApprovals) {
        # R41: project only the four reviewed approval values onto deep copies of live bodies.
        # Never seed updates from template checks, scope, bypass, or other rules.
        $template = Read-NfcTemplate 'RS-1a-to-1k'
        $parameters = @($template.rules | Where-Object type -eq 'pull_request')[0].parameters
        $approvalFields = @('required_approving_review_count', 'require_code_owner_review',
            'dismiss_stale_reviews_on_push', 'require_last_push_approval')
        if ($parameters.required_approving_review_count -ne 0 -or
            $parameters.require_code_owner_review -ne $true -or
            $parameters.dismiss_stale_reviews_on_push -ne $true -or
            $parameters.require_last_push_approval -ne $true) {
            throw 'Approval template differs from the R41 trial contract; stop for owner review.'
        }
        $updates = [Collections.Generic.List[object]]::new()
        foreach ($id in $RulesetIds) {
            if (-not $snapshots.ContainsKey([string]$id)) { throw "Confirmed ruleset $id is absent." }
            $before = $snapshots[[string]$id]
            Assert-NfcRulesetShape $before
            $desired = Get-NfcWriteBody ($before | ConvertTo-Json -Depth 50 |
                ConvertFrom-Json -AsHashtable -Depth 50)
            $pr = @($desired.rules | Where-Object type -eq 'pull_request')[0]
            foreach ($field in $approvalFields) { $pr.parameters[$field] = $parameters[$field] }
            $updates.Add(@{ id = $id; before = $before; desired = $desired })
        }
        foreach ($item in $updates) {
            Show-NfcApproval -Label "R41 confirmed ruleset ID $($item.id) (complete live body and scope)" `
                -Before (Get-NfcWriteBody $item.before) -After $item.desired -WhatIf:$WhatIf
            $oldPr = @($item.before.rules | Where-Object type -eq 'pull_request')[0].parameters
            foreach ($field in $approvalFields) {
                $oldValue = if ($oldPr.ContainsKey($field)) { $oldPr[$field] } else { $null }
                Show-NfcApproval -Label "R41 ruleset $($item.id) $field" `
                    -Before $oldValue -After $parameters[$field] -WhatIf:$WhatIf
            }
        }
        if ($WhatIf) { return }
        foreach ($item in $updates) {
            Assert-NfcLiveRulesets $base $snapshots
            $change = @{ kind = 'approval-update'; id = $item.id; status = 'pending'; after_file = "after-$($item.id).json" }
            $index.transaction.changes += $change
            Save-NfcIndex $indexPath $index
            $null = Invoke-NfcGhWrite PUT "$base/rulesets/$($item.id)" $item.desired
            $after = Invoke-NfcGhGet "$base/rulesets/$($item.id)"
            if (-not (Test-NfcSameBody $after $item.desired)) {
                throw "Ruleset $($item.id) approval update readback differs; outcome is unknown. Owner must reconcile live state."
            }
            $after | ConvertTo-Json -Depth 50 |
                Set-Content -LiteralPath (Join-Path $backupPath $change.after_file) -Encoding utf8
            $change.status = 'applied'
            Save-NfcIndex $indexPath $index
            $snapshots[[string]$item.id] = $after
        }
        return
    }

    if (-not $snapshots.ContainsKey([string]$MainRulesetId)) { throw 'The specified main ruleset is absent.' }
    $mainOld = $snapshots[[string]$MainRulesetId]
    if ($mainOld.target -ne 'branch') { throw 'Existing main ruleset is not a branch ruleset.' }
    if ('refs/heads/main' -notin @($mainOld.conditions.ref_name.include) -and
        '~DEFAULT_BRANCH' -notin @($mainOld.conditions.ref_name.include)) {
        throw 'Existing main ruleset does not target main or the default branch.'
    }
    $mainChecks = @($mainOld.rules | Where-Object type -eq 'required_status_checks')
    if ($mainChecks.Count -ne 1 -or @($mainChecks[0].parameters.required_status_checks).Count -ne 3) {
        throw 'Expected exactly one saved required-check rule with three contexts and sources.'
    }
    foreach ($check in $mainChecks[0].parameters.required_status_checks) {
        if (-not $check.context -or $null -eq $check.integration_id) { throw 'A saved check has no context or integration source.' }
    }
    $tagTemplate = Read-NfcTemplate 'RS-4'
    $tagMatches = @($snapshots.Values | Where-Object {
        $_.target -eq 'tag' -and 'refs/tags/v*' -in @($_.conditions.ref_name.include)
    })
    if ($tagMatches.Count -ne 1) { throw 'RS-4 tag ruleset was not found uniquely.' }
    Assert-NfcTagRuleset $tagMatches[0] $tagTemplate

    $mainTemplate = Read-NfcTemplate 'RS-1a-to-1k'
    $main = Get-NfcWriteBody $mainOld
    $main.enforcement = $mainTemplate.enforcement
    $main.target = $mainTemplate.target
    $oldPr = @($main.rules | Where-Object type -eq 'pull_request')
    if ($oldPr.Count -gt 1) { throw 'Main has multiple pull-request rules.' }
    $main.rules = @($main.rules | Where-Object type -ne 'pull_request') + @($mainTemplate.rules[0])
    foreach ($rule in @($mainTemplate.rules | Where-Object type -ne 'pull_request')) {
        if (@($mainOld.rules | Where-Object type -eq $rule.type).Count -eq 0) { $main.rules += $rule }
    }
    $bypass = @()
    if ($AdminBypassAvailable -eq 'Yes') { $bypass = @($mainTemplate.bypass_actors) }
    $main.bypass_actors = $bypass
    $desiredRulesets = [Collections.Generic.List[object]]::new()
    foreach ($id in @('RS-2', 'RS-3')) {
        $desired = Read-NfcTemplate $id
        if (@($list | Where-Object { $_.name -ceq $desired.name }).Count -gt 0) {
            throw "$id already exists; inspect it before retrying."
        }
        $checkRule = @($desired.rules | Where-Object type -eq 'required_status_checks')[0]
        $checkRule.parameters.required_status_checks = $mainChecks[0].parameters.required_status_checks
        $desired.bypass_actors = @($bypass)
        Assert-NfcRulesetShape $desired
        $desiredRulesets.Add(@{ kind = $id; body = $desired })
    }

    Show-NfcApproval -Label 'RS-4 confirmation (unchanged)' -Before (Get-NfcWriteBody $tagMatches[0]) `
        -After (Get-NfcWriteBody $tagMatches[0]) -WhatIf:$WhatIf
    Show-NfcApproval -Label 'RS-1a target branches' -Before $mainOld.conditions -After $main.conditions -WhatIf:$WhatIf
    foreach ($key in @('enforcement', 'target')) {
        Show-NfcApproval -Label "RS-0 main $key" -Before $mainOld[$key] -After $main[$key] -WhatIf:$WhatIf
    }
    $newPr = $mainTemplate.rules[0]
    $approvalItems = [ordered]@{
        'RS-1b pull request' = 'type'; 'RS-1c approvals' = 'required_approving_review_count'
        'RS-1d stale reviews' = 'dismiss_stale_reviews_on_push'
        'RS-1e code owners' = 'require_code_owner_review'
        'RS-1f last push' = 'require_last_push_approval'
        'RS-1g thread resolution' = 'required_review_thread_resolution'
        'RS-1h merge methods' = 'allowed_merge_methods'
    }
    foreach ($label in $approvalItems.Keys) {
        $key = $approvalItems[$label]
        $before = if ($oldPr.Count -eq 1) {
            if ($key -eq 'type') { $oldPr[0].type } else { $oldPr[0].parameters[$key] }
        } else { $null }
        $after = if ($key -eq 'type') { 'pull_request' } else { $newPr.parameters[$key] }
        Show-NfcApproval -Label $label -Before $before -After $after -WhatIf:$WhatIf
    }
    foreach ($rule in @($mainTemplate.rules | Where-Object type -ne 'pull_request')) {
        $before = @($mainOld.rules | Where-Object type -eq $rule.type)
        Show-NfcApproval -Label "RS-1i $($rule.type)" -Before $before -After $rule -WhatIf:$WhatIf
    }
    Show-NfcApproval -Label 'RS-1j required checks' -Before $mainChecks[0] -After $mainChecks[0] -WhatIf:$WhatIf
    Show-NfcApproval -Label 'RS-1k bypass' -Before $mainOld.bypass_actors -After $bypass -WhatIf:$WhatIf
    foreach ($item in $desiredRulesets) {
        foreach ($key in @('name', 'target', 'enforcement', 'conditions')) {
            Show-NfcApproval -Label "$($item.kind) $key" -Before $null -After $item.body[$key] -WhatIf:$WhatIf
        }
        foreach ($rule in $item.body.rules) {
            Show-NfcApproval -Label "$($item.kind) $($rule.type)" -Before $null -After $rule.type -WhatIf:$WhatIf
            if ($rule.type -eq 'pull_request') {
                foreach ($key in @('required_approving_review_count', 'dismiss_stale_reviews_on_push',
                    'require_code_owner_review', 'require_last_push_approval',
                    'required_review_thread_resolution', 'allowed_merge_methods')) {
                    Show-NfcApproval -Label "$($item.kind) $key" -Before $null -After $rule.parameters[$key] -WhatIf:$WhatIf
                }
            } elseif ($rule.type -eq 'required_status_checks') {
                foreach ($key in @('required_status_checks', 'strict_required_status_checks_policy',
                    'do_not_enforce_on_create')) {
                    Show-NfcApproval -Label "$($item.kind) $key" -Before $null -After $rule.parameters[$key] -WhatIf:$WhatIf
                }
            }
        }
        Show-NfcApproval -Label "$($item.kind) bypass_actors" -Before $null -After $item.body.bypass_actors -WhatIf:$WhatIf
    }
    if ($AdminBypassAvailable -eq 'No') { Write-Warning 'All branch bypass lists remain empty. Use the C3 pause procedure.' }
    if ($WhatIf) { return }

    Assert-NfcLiveRulesets $base $snapshots
    $change = @{ kind = 'main'; id = $MainRulesetId; status = 'pending'; after_file = "after-$MainRulesetId.json" }
    $index.transaction.changes += $change
    Save-NfcIndex $indexPath $index
    $null = Invoke-NfcGhWrite PUT "$base/rulesets/$MainRulesetId" $main
    $after = Invoke-NfcGhGet "$base/rulesets/$MainRulesetId"
    if (-not (Test-NfcSameBody $after $main)) {
        throw 'Main ruleset readback differs from the requested body. Outcome is unknown; owner must reconcile live state.'
    }
    $after | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $backupPath $change.after_file) -Encoding utf8
    $change.status = 'applied'
    Save-NfcIndex $indexPath $index
    $snapshots[[string]$MainRulesetId] = $after
    foreach ($item in $desiredRulesets) {
        Assert-NfcLiveRulesets $base $snapshots
        $change = @{ kind = $item.kind; id = $null; status = 'pending'; after_file = $null }
        $index.transaction.changes += $change
        Save-NfcIndex $indexPath $index
        $created = Invoke-NfcGhWrite POST "$base/rulesets" $item.body
        if (-not $created.id) { throw 'Created ruleset ID is missing. Owner must reconcile the pending write.' }
        $change.id = $created.id
        $change.after_file = "after-$($created.id).json"
        Save-NfcIndex $indexPath $index
        $after = Invoke-NfcGhGet "$base/rulesets/$($created.id)"
        if (-not (Test-NfcSameBody $after $item.body)) {
            throw "Created ruleset $($created.id) readback differs from the requested body. Outcome is unknown; owner must reconcile live state."
        }
        $after | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath (Join-Path $backupPath $change.after_file) -Encoding utf8
        $change.status = 'applied'
        Save-NfcIndex $indexPath $index
        $snapshots[[string]$created.id] = $after
    }
} catch {
    throw "Ruleset operation stopped: $($_.Exception.Message)"
}
