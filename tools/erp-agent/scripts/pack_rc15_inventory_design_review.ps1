# ============================================================
# ERP Ver2.9-rc15 006 / 库存深化设计完成后打包脚本
#
# 使用时间：
# Cursor 执行完 015_Ver29_rc15_006_inventory_design_review.md 之后运行
#
# 作用：
# 1. 检查 Git 状态
# 2. 确认没有业务代码变更
# 3. 收集 rc15 设计文档、任务文件、台账
# 4. 打包给 ChatGPT
#
# 边界：
# 不改代码 / 不同步正式 App / 不 tag / 不执行 006 / 不接库存
# 不清空数据 / 不 commit / 不 push
#
# 结尾：
# 按回车后自动关闭 PowerShell 窗口
# ============================================================

$ErrorActionPreference = "Continue"

$ProjectRoot = "C:\Users\Administrator\Documents\ERP"
$BranchName = "codex/ver2.8-basic-business-framework"
$ExpectedHead = "dc91549"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Desktop = [Environment]::GetFolderPath("Desktop")

$LogDir = Join-Path $Desktop "ERP_Ver29_rc15_inventory_design_review_$Timestamp"
$LogFile = Join-Path $LogDir "ERP_Ver29_rc15_inventory_design_review_log_$Timestamp.txt"
$SummaryFile = Join-Path $LogDir "ERP_Ver29_rc15_inventory_design_review_summary_$Timestamp.md"
$ZipFile = Join-Path $Desktop "ERP_Ver29_rc15库存深化设计成果给ChatGPT_$Timestamp.zip"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Log-Step {
    param([string]$Text)
    $line = "`n========== $Text =========="
    Write-Host $line -ForegroundColor Cyan
    Add-Content -Path $LogFile -Value $line
}

function Log-Line {
    param([string]$Text)
    Write-Host $Text
    Add-Content -Path $LogFile -Value $Text
}

try {
    Log-Step "基础信息"
    Log-Line "ProjectRoot: $ProjectRoot"
    Log-Line "BranchName: $BranchName"
    Log-Line "ExpectedHead: $ExpectedHead"
    Log-Line "Timestamp: $Timestamp"
    Log-Line "边界：只打包 rc15 设计成果，不改代码、不 commit、不 push"

    if (!(Test-Path $ProjectRoot)) {
        throw "项目目录不存在：$ProjectRoot"
    }

    Set-Location $ProjectRoot

    Log-Step "Git 状态"

    $CurrentBranch = (git branch --show-current).Trim()
    $HeadLine = (git log -1 --oneline) -join "`n"
    $HeadShort = (git rev-parse --short HEAD).Trim()
    $StatusSb = (git status -sb) -join "`n"
    $StatusShortLines = git status --short

    Log-Line "CurrentBranch: $CurrentBranch"
    Log-Line "HeadLine: $HeadLine"
    Log-Line "HeadShort: $HeadShort"
    Log-Line "git status -sb:"
    Log-Line $StatusSb
    Log-Line "git status --short:"
    if ($StatusShortLines) {
        foreach ($line in $StatusShortLines) {
            Log-Line $line
        }
    }

    if ($CurrentBranch -ne $BranchName) {
        throw "当前分支不是 $BranchName，实际是：$CurrentBranch"
    }

    if ($HeadShort -ne $ExpectedHead) {
        throw "当前 HEAD 不是预期 $ExpectedHead，实际是：$HeadShort"
    }

    Log-Step "检查是否有业务代码变更"

    $badChanges = @()

    if ($StatusShortLines) {
        foreach ($line in $StatusShortLines) {
            if (
                $line -notmatch "docs/worklog/" -and
                $line -notmatch "docs\\worklog\\" -and
                $line -notmatch "tools/erp-agent/tasks/" -and
                $line -notmatch "tools\\erp-agent\\tasks\\" -and
                $line -notmatch "tools/erp-agent/scripts/pack_rc15_inventory_design_review.ps1" -and
                $line -notmatch "tools\\erp-agent\\scripts\\pack_rc15_inventory_design_review.ps1"
            ) {
                $badChanges += $line
            }
        }
    }

    if ($badChanges.Count -gt 0) {
        Log-Line "发现疑似业务代码变更："
        foreach ($b in $badChanges) {
            Log-Line $b
        }

        throw "发现非 docs/worklog 或 tools/erp-agent/tasks/scripts 的变更，请先停止。"
    }

    Log-Line "检查通过：未发现业务代码变更。"

    Log-Step "收集 rc15 设计成果"

    $FilesToZip = @(
        $LogFile
    )

    $CandidateFiles = @(
        "docs\worklog\reports\ver29_rc15_006_inventory_design.md",
        "tools\erp-agent\tasks\todo\015_Ver29_rc15_006_inventory_design_review.md",
        "tools\erp-agent\tasks\todo\016_Ver29_rc15_006_inventory_design_user_review.md",
        "docs\worklog\ERP-Ver2.9-任务总表.md",
        "docs\worklog\ERP-Ver2.9-执行台账.md",
        "tools\erp-agent\scripts\pack_rc15_inventory_design_review.ps1"
    )

    foreach ($rel in $CandidateFiles) {
        $full = Join-Path $ProjectRoot $rel
        if (Test-Path $full) {
            Log-Line "收集：$rel"
            $FilesToZip += $full
        } else {
            Log-Line "未找到，跳过：$rel"
        }
    }

@"
# ERP Ver2.9-rc15 库存深化设计成果打包摘要

时间：$Timestamp

## 当前基线

$HeadLine

## 本次检查

- 已检查 Git 分支
- 已检查 HEAD
- 已检查 git status
- 未发现业务代码变更
- 未执行 006
- 未接库存深化代码
- 未同步正式 App
- 未 tag
- 未 commit
- 未 push

## 打包内容

- rc15 006 / 库存深化设计方案
- rc15 设计任务文件
- rc15 用户评审任务文件
- Ver2.9 任务总表 / 执行台账，如存在
- 本打包脚本

## 下一步

把本 zip 发给 ChatGPT，由 ChatGPT 读取设计方案并整理用户确认问题。
"@ | Out-File -Encoding UTF8 $SummaryFile

    $FilesToZip += $SummaryFile

    Compress-Archive -Path $FilesToZip -DestinationPath $ZipFile -Force

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "rc15 库存深化设计成果已打包。" -ForegroundColor Green
    Write-Host "日志包：" -ForegroundColor Green
    Write-Host $ZipFile -ForegroundColor Yellow
    Write-Host "============================================================" -ForegroundColor Green

    explorer.exe "/select,$ZipFile"
}
catch {
    Log-Step "脚本失败"
    Log-Line "失败原因：$($_.Exception.Message)"

    try {
        Compress-Archive -Path (Join-Path $LogDir "*") -DestinationPath $ZipFile -Force
        Write-Host ""
        Write-Host "脚本失败，但已打包日志：" -ForegroundColor Yellow
        Write-Host $ZipFile -ForegroundColor Yellow
    } catch {}

    Write-Host ""
    Write-Host "把日志包发给 ChatGPT。" -ForegroundColor Red
}
finally {
    Write-Host ""
    Write-Host "脚本已经结束。按回车关闭窗口。" -ForegroundColor Yellow
    [void][System.Console]::ReadLine()
    exit
}
