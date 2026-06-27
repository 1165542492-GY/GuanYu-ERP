#Requires -Version 5.1
<#
.SYNOPSIS
    ERP Ver2.9 任务循环：从 inbox 取任务 → 安全检查 → 生成执行提示 → 可选 Cursor CLI → 更新台账。
.PARAMETER MaxRounds
    最多处理任务数，默认 1。
.PARAMETER LoopDelaySeconds
    每轮之间的等待秒数，默认 10。
#>
param(
    [int]$MaxRounds = 1,
    [int]$LoopDelaySeconds = 10,
    [string]$RepoRoot = "C:\Users\Administrator\Documents\ERP",
    # Cursor CLI 命令模板，{promptFile} 和 {repoRoot} 会被替换
    [string]$CursorCliCommandTemplate = "",
    [string[]]$CursorCliCandidates = @("cursor-agent", "cursor", "agent")
)

$ErrorActionPreference = "Continue"

# ── R4 禁止自动执行的危险命令黑名单 ──
$Script:ForbiddenPatterns = @(
    'git\s+add',
    'git\s+commit',
    'git\s+push',
    'git\s+reset',
    'git\s+clean',
    'git\s+push\s+--force',
    'Remove-Item.*\\Data',
    'Remove-Item.*\\App',
    'Remove-Item.*\\Backups',
    'robocopy.*冠誉制造ERP\\App',
    'tag\s+-d',
    'tag\s+-f',
    '/api/.*clear',
    'ClearData',
    '清空'
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$ts] [$Level] $Message"
    Write-Host $line
    $logDir = Join-Path $RepoRoot "tools\erp-agent\logs"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    $logFile = Join-Path $logDir ("task_loop_{0:yyyyMMdd}.log" -f (Get-Date))
    Add-Content -Path $logFile -Value $line -Encoding UTF8
}

function Get-ScannableTaskContent {
    param([string]$Text)

    $lines = $Text -split "`r?`n"
    $skipSection = $false
    $result = New-Object System.Collections.Generic.List[string]

    $prohibitionLinePatterns = @(
        '禁止',
        '不允许',
        '不要',
        '不得',
        '完成后是否允许自动提交'
    )

    foreach ($line in $lines) {
        $trimmed = $line.Trim()

        if ($trimmed -match '^#{1,3}\s+禁止动作') {
            $skipSection = $true
            continue
        }

        if ($skipSection -and $trimmed -match '^#\s+\S') {
            $skipSection = $false
        }

        if ($skipSection) { continue }

        if ($trimmed -match '^(禁止动作|不允许|不要执行)\s*[:：]\s*$') { continue }

        $isProhibitionLine = $false
        foreach ($pat in $prohibitionLinePatterns) {
            if ($line -match $pat) {
                $isProhibitionLine = $true
                break
            }
        }
        if ($isProhibitionLine) { continue }

        [void]$result.Add($line)
    }

    return ($result -join "`n")
}

function Test-ForbiddenContent {
    param([string]$Text)
    $scannable = Get-ScannableTaskContent -Text $Text
    $found = @()
    foreach ($pat in $Script:ForbiddenPatterns) {
        if ($scannable -match $pat) { $found += $pat }
    }
    return $found
}

function Get-NextInboxTask {
    $inbox = Join-Path $RepoRoot "tools\erp-agent\tasks\inbox"
    if (-not (Test-Path $inbox)) { return $null }
    $tasks = Get-ChildItem -Path $inbox -Filter "*.md" -File | Sort-Object Name
    if ($tasks.Count -eq 0) { return $null }
    return $tasks[0]
}

function Move-TaskFile {
    param(
        [System.IO.FileInfo]$TaskFile,
        [ValidateSet("running", "done", "failed")]
        [string]$TargetFolder
    )
    $destDir = Join-Path $RepoRoot "tools\erp-agent\tasks\$TargetFolder"
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
    $dest = Join-Path $destDir $TaskFile.Name
    Move-Item -Path $TaskFile.FullName -Destination $dest -Force
    return $dest
}

function Invoke-SafeCheck {
    $script = Join-Path $RepoRoot "tools\erp-agent\run_safe_check.ps1"
    Write-Log "运行安全检查: $script"
    & powershell -ExecutionPolicy Bypass -File $script -RepoRoot $RepoRoot
    $exitCode = $LASTEXITCODE
    $reportDir = Join-Path $RepoRoot "docs\worklog\reports"
    $latestReport = Get-ChildItem -Path $reportDir -Filter "ver29_safe_check_*.md" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    return @{
        ExitCode     = $exitCode
        ReportPath   = if ($latestReport) { $latestReport.FullName } else { $null }
        Success      = ($exitCode -eq 0)
        NeedsFix     = ($exitCode -eq 2)
        Failed       = ($exitCode -ne 0 -and $exitCode -ne 2)
    }
}

function New-CurrentTaskPrompt {
    param(
        [string]$TaskPath,
        [string]$TaskContent
    )
    $reportDir = Join-Path $RepoRoot "docs\worklog\reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $promptPath = Join-Path $reportDir "current_task_prompt.md"

    $forbidden = Test-ForbiddenContent -Text $TaskContent
    $forbiddenNote = if ($forbidden.Count -gt 0) {
        "⚠️ 任务内容含敏感关键词模式，请人工复核：`n" + ($forbidden -join "`n")
    } else {
        "任务内容未命中脚本级黑名单。"
    }

    $md = @"
# 当前待执行任务

> 生成时间：$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
> 任务文件：``$TaskPath``

---

## 风险拦截提醒

$forbiddenNote

**脚本禁止自动执行：** git add/commit/push/reset/clean、清数据、同步正式 App、删除 tag 等。

---

## 任务内容

$TaskContent

---

## 人工执行指引

1. 在 Cursor 中打开开发仓库：``$RepoRoot``
2. 阅读上方任务内容与 ``docs/worklog/ERP-Ver2.9-风险清单.md``
3. 按任务步骤执行；R2+ 任务须浏览器验收
4. 完成后可将任务文件移至 ``tools/erp-agent/tasks/done/`` 或再次运行本脚本

---
*由 ``tools/erp-agent/run_task_loop.ps1`` 自动生成*
"@

    Set-Content -Path $promptPath -Value $md -Encoding UTF8
    return $promptPath
}

function Find-CursorCli {
    foreach ($name in $CursorCliCandidates) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) {
            return @{
                Name       = $name
                Source     = $cmd.Source
                CommandType = $cmd.CommandType
            }
        }
    }
    return $null
}

function Invoke-CursorCliTask {
    param(
        [hashtable]$CliInfo,
        [string]$PromptFile,
        [string]$TaskContent
    )

    if ($CursorCliCommandTemplate) {
        $cmdLine = $CursorCliCommandTemplate `
            -replace '\{promptFile\}', $PromptFile `
            -replace '\{repoRoot\}', $RepoRoot
        Write-Log "使用配置的 Cursor CLI 命令: $cmdLine"
        $output = Invoke-Expression $cmdLine 2>&1
        $exitCode = $LASTEXITCODE
        return @{
            Executed = $true
            ExitCode = $exitCode
            Output   = ($output | Out-String).Trim()
            Command  = $cmdLine
        }
    }

    # 默认尝试常见 headless 调用方式（可配置覆盖）
    $cliName = $CliInfo.Name
    $attempts = @(
        @{ Args = @("--headless", "--prompt-file", $PromptFile); Desc = "$cliName --headless --prompt-file" },
        @{ Args = @("agent", "--print", "--workspace", $RepoRoot, $PromptFile); Desc = "cursor agent --print" },
        @{ Args = @("--version"); Desc = "$cliName --version (探测)" }
    )

    foreach ($attempt in $attempts) {
        Write-Log "尝试 CLI: $($attempt.Desc)"
        try {
            $output = & $cliName @($attempt.Args) 2>&1
            $exitCode = $LASTEXITCODE
            if ($exitCode -eq 0 -and $attempt.Desc -notlike "*--version*") {
                return @{
                    Executed = $true
                    ExitCode = 0
                    Output   = ($output | Out-String).Trim()
                    Command  = "$cliName $($attempt.Args -join ' ')"
                }
            }
        }
        catch {
            Write-Log "CLI 尝试失败: $($_.Exception.Message)" "WARN"
        }
    }

    return @{
        Executed = $false
        ExitCode = -1
        Output   = "未找到可用的 headless 执行方式。请配置 -CursorCliCommandTemplate 或人工在 Cursor 中执行 current_task_prompt.md"
        Command  = ""
    }
}

function New-FailureReport {
    param(
        [string]$TaskName,
        [string]$Reason,
        [string]$Detail = ""
    )
    $reportDir = Join-Path $RepoRoot "docs\worklog\reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $path = Join-Path $reportDir ("task_failed_{0:yyyyMMdd_HHmmss}.md" -f (Get-Date))
    $md = @"
# 任务失败报告

| 项目 | 值 |
|------|-----|
| **时间** | $(Get-Date -Format "yyyy-MM-dd HH:mm:ss") |
| **任务** | $TaskName |
| **原因** | $Reason |

## 详情

$Detail

---
*由 ``tools/erp-agent/run_task_loop.ps1`` 生成*
"@
    Set-Content -Path $path -Value $md -Encoding UTF8
    return $path
}

function Update-Ledger {
    param(
        [string]$TaskFileName,
        [string]$Phase,
        [string]$Action,
        [string]$BuildResult,
        [string]$ApiCheck,
        [string]$BrowserVerify,
        [string]$RiskLevel,
        [string]$Committed,
        [string]$Note
    )
    $ledgerPath = Join-Path $RepoRoot "docs\worklog\ERP-Ver2.9-执行台账.md"
    if (-not (Test-Path $ledgerPath)) { return }

    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $row = "| $ts | $Phase | $TaskFileName | run_task_loop | — | $Action | $BuildResult | $ApiCheck | $BrowserVerify | $RiskLevel | $Committed | $Note |"
    Add-Content -Path $ledgerPath -Value $row -Encoding UTF8
    Write-Log "已更新执行台账"
}

function Get-TaskPhase {
    param([string]$Content, [string]$FileName)
    if ($Content -match 'Ver2\.9-rc\d' -or $Content -match 'Ver29_rc\d') {
        if ($Content -match '(Ver2\.9-rc\d|Ver29_rc\d)') { return $Matches[1] -replace 'Ver29', 'Ver2.9-' }
    }
    if ($FileName -match 'rc(\d)') { return "Ver2.9-rc$($Matches[1])" }
    return "Ver2.9"
}

function Get-TaskRiskLevel {
    param([string]$Content)
    if ($Content -match '# 风险等级\s*\r?\n\s*\r?\n?\s*(.+)' ) {
        return $Matches[1].Trim()
    }
    return "R0"
}

# ── 主循环 ──
Write-Log "=== ERP Ver2.9 任务循环开始 (MaxRounds=$MaxRounds) ==="

$cursorCli = Find-CursorCli
if ($cursorCli) {
    Write-Log "检测到 Cursor CLI: $($cursorCli.Name) ($($cursorCli.Source))"
}
else {
    Write-Log "未检测到 Cursor CLI（cursor-agent / cursor / agent），将生成人工执行提示" "WARN"
}

$round = 0
while ($round -lt $MaxRounds) {
    $round++
    Write-Log "--- 第 $round / $MaxRounds 轮 ---"

    $task = Get-NextInboxTask
    if (-not $task) {
        Write-Log "inbox 无待处理任务，循环结束"
        break
    }

    Write-Log "取任务: $($task.Name)"
    $runningPath = Move-TaskFile -TaskFile $task -TargetFolder "running"
    $taskContent = Get-Content -Path $runningPath -Raw -Encoding UTF8
    $phase = Get-TaskPhase -Content $taskContent -FileName $task.Name
    $riskLevel = Get-TaskRiskLevel -Content $taskContent

    # 任务内容禁止动作检查（忽略「禁止动作」章节及说明性禁止语句）
    $forbiddenInTask = Test-ForbiddenContent -Text $taskContent
    if ($forbiddenInTask.Count -gt 0) {
        $reason = "任务内容命中禁止模式: $($forbiddenInTask -join ', ')"
        Write-Log $reason "ERROR"
        $failReport = New-FailureReport -TaskName $task.Name -Reason $reason
        Move-TaskFile -TaskFile (Get-Item $runningPath) -TargetFolder "failed"
        Update-Ledger -TaskFileName $task.Name -Phase $phase -Action "任务拦截" `
            -BuildResult "—" -ApiCheck "—" -BrowserVerify "—" -RiskLevel $riskLevel `
            -Committed "否" -Note "禁止模式拦截 → $failReport"
        break
    }

    # 执行前安全检查
    $preCheck = Invoke-SafeCheck
    if (-not $preCheck.Success) {
        $reason = "执行前安全检查失败 (exit $($preCheck.ExitCode))"
        Write-Log $reason "ERROR"
        $failReport = New-FailureReport -TaskName $task.Name -Reason $reason -Detail "报告: $($preCheck.ReportPath)"
        Move-TaskFile -TaskFile (Get-Item $runningPath) -TargetFolder "failed"
        Update-Ledger -TaskFileName $task.Name -Phase $phase -Action "执行前安全检查" `
            -BuildResult "失败" -ApiCheck "—" -BrowserVerify "—" -RiskLevel $riskLevel `
            -Committed "否" -Note "失败 → $failReport"
        break
    }

    # 生成待执行提示
    $promptPath = New-CurrentTaskPrompt -TaskPath $runningPath -TaskContent $taskContent
    Write-Log "待执行提示: $promptPath"

    # Cursor CLI
    $cliResult = @{ Executed = $false; ExitCode = 0; Output = "" }
    if ($cursorCli) {
        $cliResult = Invoke-CursorCliTask -CliInfo $cursorCli -PromptFile $promptPath -TaskContent $taskContent
        if ($cliResult.Executed) {
            Write-Log "Cursor CLI 执行完成 exit=$($cliResult.ExitCode)"
        }
        else {
            Write-Log "Cursor CLI 未能 headless 执行，请人工处理 current_task_prompt.md" "WARN"
        }
    }
    else {
        Write-Log "未检测到 Cursor CLI，请人工在 Cursor 中执行: $promptPath" "WARN"
        $cliResult.Output = "未检测到 Cursor CLI，请人工在 Cursor 中执行 current_task_prompt.md"
    }

    # 执行后安全检查
    $postCheck = Invoke-SafeCheck
    $buildResult = if ($postCheck.Success) { "0 错误" } elseif ($postCheck.NeedsFix) { "通过/接口需修复" } else { "失败" }
    $apiResult = if ($postCheck.Success) { "通过" } elseif ($postCheck.NeedsFix) { "需修复" } else { "失败" }

    $taskFailed = $postCheck.Failed -or ($cliResult.Executed -and $cliResult.ExitCode -ne 0)
    $targetFolder = if ($taskFailed) { "failed" } else { "done" }

    Move-TaskFile -TaskFile (Get-Item $runningPath) -TargetFolder $targetFolder

    $noteParts = @()
    if (-not $cursorCli) { $noteParts += "无 CLI，人工执行" }
    if ($cliResult.Executed) { $noteParts += "CLI exit $($cliResult.ExitCode)" }
    if ($postCheck.ReportPath) { $noteParts += "报告: $(Split-Path $postCheck.ReportPath -Leaf)" }
    if ($taskFailed) { $noteParts += "见 failed 报告" }

    Update-Ledger -TaskFileName $task.Name -Phase $phase -Action "任务循环" `
        -BuildResult $buildResult -ApiCheck $apiResult -BrowserVerify "待人工" `
        -RiskLevel $riskLevel -Committed "否" -Note ($noteParts -join "; ")

    if ($taskFailed) {
        New-FailureReport -TaskName $task.Name -Reason "执行后检查或 CLI 失败" -Detail $cliResult.Output | Out-Null
        Write-Log "任务失败，已移至 failed/" "ERROR"
        break
    }

    Write-Log "任务完成，已移至 done/"

    if ($round -lt $MaxRounds) {
        Write-Log "等待 $LoopDelaySeconds 秒 ..."
        Start-Sleep -Seconds $LoopDelaySeconds
    }
}

Write-Log "=== 任务循环结束 ==="
