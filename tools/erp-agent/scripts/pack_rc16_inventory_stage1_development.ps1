# ============================================================
# ERP Ver2.9-rc16 006 / 库存类型深化阶段1开发完成后打包脚本
#
# 使用时间：
# Cursor 执行完 017_Ver29_rc16_006_inventory_stage1_development.md 之后运行
#
# 作用：
# 1. 检查 Git 状态
# 2. build
# 3. 回归
# 4. 收集修改文件清单
# 5. 收集 rc16 报告与任务文件
# 6. 打包给 ChatGPT
#
# 边界：
# 不同步正式 App / 不 tag / 不清空数据 / 不 commit / 不 push
#
# 结尾：
# 按回车后自动关闭 PowerShell 窗口
# ============================================================

$ErrorActionPreference = "Continue"

$ProjectRoot = "C:\Users\Administrator\Documents\ERP"
$BranchName = "codex/ver2.8-basic-business-framework"
$ExpectedBase = "dc91549"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Desktop = [Environment]::GetFolderPath("Desktop")

$LogDir = Join-Path $Desktop "ERP_Ver29_rc16_inventory_stage1_$Timestamp"
$LogFile = Join-Path $LogDir "ERP_Ver29_rc16_inventory_stage1_log_$Timestamp.txt"
$SummaryFile = Join-Path $LogDir "ERP_Ver29_rc16_inventory_stage1_summary_$Timestamp.md"
$ZipFile = Join-Path $Desktop "ERP_Ver29_rc16库存类型阶段1成果给ChatGPT_$Timestamp.zip"

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

function Run-Native {
    param(
        [string]$Title,
        [string]$Exe,
        [string[]]$ToolArgs,
        [bool]$StopOnFail = $true
    )

    Log-Step $Title
    Log-Line ("命令: " + $Exe + " " + ($ToolArgs -join " "))

    try {
        $output = & $Exe @ToolArgs 2>&1
        $code = $LASTEXITCODE
        if ($null -eq $code) { $code = 0 }

        if ($output) {
            foreach ($line in $output) {
                Log-Line (($line | Out-String).TrimEnd())
            }
        }

        Log-Line "ExitCode: $code"

        if ($StopOnFail -and $code -ne 0) {
            throw "命令失败：$Title，ExitCode=$code"
        }

        return [int]$code
    }
    catch {
        Log-Line "异常：$($_.Exception.Message)"
        if ($StopOnFail) { throw }
        return 1
    }
}

try {
    Log-Step "基础信息"
    Log-Line "ProjectRoot: $ProjectRoot"
    Log-Line "BranchName: $BranchName"
    Log-Line "ExpectedBase: $ExpectedBase"
    Log-Line "Timestamp: $Timestamp"
    Log-Line "边界：只复核打包，不同步正式App、不tag、不commit、不push"

    if (!(Test-Path $ProjectRoot)) {
        throw "项目目录不存在：$ProjectRoot"
    }

    Set-Location $ProjectRoot

    Run-Native "git branch --show-current" "git" @("branch", "--show-current")
    Run-Native "git log -1 --oneline" "git" @("log", "-1", "--oneline")
    Run-Native "git status -sb" "git" @("status", "-sb")
    Run-Native "git status --short" "git" @("status", "--short")
    Run-Native "git diff --name-status" "git" @("diff", "--name-status")
    Run-Native "git diff --stat" "git" @("diff", "--stat")

    Log-Step "执行 build"
    Run-Native "dotnet build .\ERP.csproj" "dotnet" @("build", ".\ERP.csproj")

    Log-Step "执行 Ver2.9 回归"

    $RegressionScript = $null
    $Candidates = @(
        (Join-Path $ProjectRoot "tools\erp-agent\scripts\run_ver29_regression.ps1"),
        (Join-Path $ProjectRoot "tools\run_ver29_regression.ps1")
    )

    foreach ($c in $Candidates) {
        if (Test-Path $c) {
            $RegressionScript = $c
            break
        }
    }

    if ($null -eq $RegressionScript) {
        $found = Get-ChildItem -Path $ProjectRoot -Filter "run_ver29_regression.ps1" -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { $RegressionScript = $found.FullName }
    }

    if ($null -eq $RegressionScript) {
        throw "未找到 run_ver29_regression.ps1"
    }

    Run-Native "run_ver29_regression.ps1" "powershell.exe" @("-ExecutionPolicy", "Bypass", "-File", $RegressionScript)

    Log-Step "收集文件"

    $FilesToZip = @(
        $LogFile
    )

    $CandidateFiles = @(
        "tools\erp-agent\tasks\todo\017_Ver29_rc16_006_inventory_stage1_development.md",
        "docs\worklog\reports\ver29_rc16_inventory_rules_confirmed_*.md",
        "docs\worklog\reports\ver29_rc15_006_inventory_design.md",
        "docs\worklog\ERP-Ver2.9-任务总表.md",
        "docs\worklog\ERP-Ver2.9-执行台账.md",
        "VERSION.md",
        "README.md",
        "安装说明.txt"
    )

    foreach ($pattern in $CandidateFiles) {
        Get-ChildItem -Path $ProjectRoot -Filter (Split-Path $pattern -Leaf) -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $rel = $_.FullName.Substring($ProjectRoot.Length + 1)
                $rel -like $pattern
            } |
            ForEach-Object {
                Log-Line "收集：$($_.FullName)"
                $FilesToZip += $_.FullName
            }
    }

    $LatestReports = Get-ChildItem -Path (Join-Path $ProjectRoot "docs\worklog\reports") -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -like "*rc16*" -or
            $_.Name -like "*inventory*" -or
            $_.Name -like "ver29_regression_summary_*.md"
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 20

    foreach ($f in $LatestReports) {
        Log-Line "收集报告：$($f.FullName)"
        $FilesToZip += $f.FullName
    }

@"
# ERP Ver2.9-rc16 库存类型阶段1成果打包摘要

时间：$Timestamp

## 当前状态

本脚本只复核和打包 Cursor 开发成果。

## 已执行

- git status
- git diff
- dotnet build
- run_ver29_regression.ps1
- 收集相关报告和任务文件

## 边界确认

- 未同步正式 App
- 未 tag
- 未清空数据
- 未 commit
- 未 push

## 下一步

把本 zip 发给 ChatGPT，由 ChatGPT 判断是否可进入人工验收或修复。
"@ | Out-File -Encoding UTF8 $SummaryFile

    $FilesToZip += $SummaryFile

    $UniqueFiles = $FilesToZip | Select-Object -Unique
    Compress-Archive -Path $UniqueFiles -DestinationPath $ZipFile -Force

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "rc16 库存类型阶段1成果已打包。" -ForegroundColor Green
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
