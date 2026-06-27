#Requires -Version 5.1
<#
.SYNOPSIS
    ERP Ver2.9 一键安全检查：git 状态、build、启动 ERP、接口探测、生成报告。
.DESCRIPTION
    只读/低风险操作。内置 R4 风险拦截，禁止 commit/push/清数据等危险命令。
#>
param(
    [string]$RepoRoot = "C:\Users\Administrator\Documents\ERP",
    [int]$StartupWaitSeconds = 3
)

$ErrorActionPreference = "Continue"

# ── 风险拦截：危险命令模式（供引用与日志说明） ──
$Script:DangerousPatterns = @(
    'git\s+(add|commit|push|reset|clean)',
    'git\s+push\s+--force',
    'Remove-Item.*冠誉制造ERP\\Data',
    'Remove-Item.*冠誉制造ERP\\App',
    'Remove-Item.*冠誉制造ERP\\Backups',
    'robocopy.*冠誉制造ERP\\App',
    'tag\s+-d',
    'tag\s+-f',
    'clear.*data',
    '清空'
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$ts] [$Level] $Message"
    Write-Host $line
    $logDir = Join-Path $RepoRoot "tools\erp-agent\logs"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    $logFile = Join-Path $logDir ("safe_check_{0:yyyyMMdd}.log" -f (Get-Date))
    Add-Content -Path $logFile -Value $line -Encoding UTF8
}

function Test-DangerousCommand {
    param([string]$CommandText)
    foreach ($pat in $Script:DangerousPatterns) {
        if ($CommandText -match $pat) { return $true }
    }
    return $false
}

function Get-GitBranch {
    Push-Location $RepoRoot
    try {
        $branch = git rev-parse --abbrev-ref HEAD 2>$null
        if ($LASTEXITCODE -ne 0) { return "(unknown)" }
        return $branch.Trim()
    }
    finally { Pop-Location }
}

function Get-GitLatestCommit {
    Push-Location $RepoRoot
    try {
        $log = git log --oneline -1 2>$null
        if ($LASTEXITCODE -ne 0) { return "(unknown)" }
        return $log.Trim()
    }
    finally { Pop-Location }
}

function Invoke-GitCapture {
    param([string[]]$GitArgs)
    Push-Location $RepoRoot
    try {
        $output = & git @GitArgs 2>&1
        return @{
            ExitCode = $LASTEXITCODE
            Output   = ($output | Out-String).Trim()
        }
    }
    finally { Pop-Location }
}

function Stop-ErpProcesses {
    $stopped = @()
    $procs = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -like "*冠誉制造ERP*" -or $_.ProcessName -like "*GuanYuERP*"
    }
    foreach ($p in $procs) {
        try {
            Stop-Process -Id $p.Id -Force -ErrorAction Stop
            $stopped += "$($p.ProcessName) (PID $($p.Id))"
        }
        catch {
            Write-Log "无法停止进程 $($p.ProcessName) PID $($p.Id): $($_.Exception.Message)" "WARN"
        }
    }
    if ($stopped.Count -gt 0) {
        Start-Sleep -Seconds 1
    }
    return $stopped
}

function Start-ErpApp {
    param([string]$ExePath)
    if (-not (Test-Path $ExePath)) {
        return @{ Success = $false; Message = "可执行文件不存在: $ExePath" }
    }
    try {
        $proc = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath -Parent) -PassThru
        return @{ Success = $true; Message = "已启动 PID $($proc.Id)"; ProcessId = $proc.Id }
    }
    catch {
        return @{ Success = $false; Message = $_.Exception.Message }
    }
}

function Test-HttpEndpoint {
    param(
        [string]$Url,
        [int]$TimeoutSec = 10
    )
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        return @{
            Ok         = $true
            StatusCode = $resp.StatusCode
            Note       = "HTTP $($resp.StatusCode)"
        }
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        # 401 表示服务在运行但需登录，对 owner-summary 可接受
        if ($statusCode -eq 401) {
            return @{
                Ok         = $true
                StatusCode = 401
                Note       = "HTTP 401（需登录，服务正常）"
            }
        }
        return @{
            Ok         = $false
            StatusCode = $statusCode
            Note       = $_.Exception.Message
        }
    }
}

function New-SafeCheckReport {
    param([hashtable]$Data)
    $reportDir = Join-Path $RepoRoot "docs\worklog\reports"
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
    $reportPath = Join-Path $reportDir ("ver29_safe_check_{0:yyyyMMdd_HHmmss}.md" -f (Get-Date))

    $buildOk = $Data.BuildSuccess
    $erpOk = $Data.ErpStartSuccess
    $homeOk = $Data.HomePageOk
    $apiMonthOk = $Data.ApiMonthOk
    $apiAllOk = $Data.ApiAllOk
    $needsFix = (-not $buildOk) -or (-not $erpOk) -or (-not $homeOk) -or (-not $apiMonthOk) -or (-not $apiAllOk)

    $suggestContinue = $buildOk -and $erpOk -and $homeOk

    $buildLabel = if ($buildOk) { '[OK] success' } else { '[FAIL] failed' }
    $erpLabel = if ($erpOk) { "[OK] $($Data.ErpStartMessage)" } else { "[FAIL] $($Data.ErpStartMessage)" }
    $homeLabel = if ($homeOk) { "[OK] $($Data.HomePageNote)" } else { "[FAIL] $($Data.HomePageNote)" }
    $apiMonthLabel = if ($apiMonthOk) { "[OK] $($Data.ApiMonthNote)" } else { "[FAIL] needs fix - $($Data.ApiMonthNote)" }
    $apiAllLabel = if ($apiAllOk) { "[OK] $($Data.ApiAllNote)" } else { "[FAIL] needs fix - $($Data.ApiAllNote)" }
    $workspaceNote = if ($Data.HasUncommittedChanges) { '[WARN] uncommitted changes' } else { 'workspace clean' }
    $continueLabel = if ($suggestContinue) { '[OK] yes' } else { '[FAIL] no, fix issues first' }
    $overallLabel = if ($needsFix) { '[WARN] needs fix' } else { '[OK] pass' }

    $md = @"
# ERP Ver2.9 安全检查报告

## 基本信息

| 项目 | 值 |
|------|-----|
| **时间** | $($Data.Timestamp) |
| **当前分支** | $($Data.Branch) |
| **最新提交** | $($Data.LatestCommit) |
| **开发仓库** | ``$RepoRoot`` |

## 工作区状态

### git status --short

``````
$($Data.GitStatusShort)
``````

### git status -sb

``````
$($Data.GitStatusSb)
``````

### git log --oneline -5

``````
$($Data.GitLog5)
``````

## Build 结果

| 项目 | 值 |
|------|-----|
| **命令** | ``dotnet build ERP.csproj`` |
| **结果** | $buildLabel |
| **详情** | $($Data.BuildDetail) |

## ERP 启动结果

| 项目 | 值 |
|------|-----|
| **停止旧进程** | $($Data.StoppedProcesses -join '; ') |
| **启动** | $erpLabel |
| **可执行文件** | ``$($Data.ExePath)`` |

## 接口检查结果

| 接口 | 结果 |
|------|------|
| ``http://127.0.0.1:8787`` | $homeLabel |
| ``/api/dashboard/owner-summary?range=month`` | $apiMonthLabel |
| ``/api/dashboard/owner-summary?range=all`` | $apiAllLabel |

## 风险提醒

- 本脚本**不会**执行 commit、push、清数据、同步正式 App。
- 禁止操作清单见 ``docs/worklog/ERP-Ver2.9-风险清单.md`` R4 节。
- $workspaceNote

## 结论

| 项目 | 建议 |
|------|------|
| **是否建议继续** | $continueLabel |
| **是否建议提交** | [NO] 否 - 提交须人工决定，脚本永不自动 commit/push |
| **整体状态** | $overallLabel |

---
*由 ``tools/erp-agent/run_safe_check.ps1`` 自动生成*
"@

    Set-Content -Path $reportPath -Value $md -Encoding UTF8
    return @{
        Path            = $reportPath
        Success         = $buildOk -and $erpOk
        NeedsFix        = $needsFix
        SuggestContinue = $suggestContinue
    }
}

# ── 主流程 ──
Write-Log "=== ERP Ver2.9 安全检查开始 ==="

if (-not (Test-Path $RepoRoot)) {
    Write-Log "开发仓库不存在: $RepoRoot" "ERROR"
    exit 1
}

$reportData = @{
    Timestamp           = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    Branch              = Get-GitBranch
    LatestCommit        = Get-GitLatestCommit
    GitStatusShort      = ""
    GitStatusSb         = ""
    GitLog5             = ""
    BuildSuccess        = $false
    BuildDetail         = ""
    StoppedProcesses    = @()
    ErpStartSuccess     = $false
    ErpStartMessage     = ""
    ExePath             = Join-Path $RepoRoot "bin\Debug\net8.0-windows\冠誉制造ERP.exe"
    HomePageOk          = $false
    HomePageNote        = ""
    ApiMonthOk          = $false
    ApiMonthNote        = ""
    ApiAllOk            = $false
    ApiAllNote          = ""
    HasUncommittedChanges = $false
}

# Git 状态
$gs = Invoke-GitCapture -GitArgs @("status", "--short")
$reportData.GitStatusShort = if ($gs.Output) { $gs.Output } else { "(clean)" }
$reportData.HasUncommittedChanges = [bool]$gs.Output

$gsb = Invoke-GitCapture -GitArgs @("status", "--short", "--branch")
$reportData.GitStatusSb = $gsb.Output

$gl = Invoke-GitCapture -GitArgs @("log", "--oneline", "-5")
$reportData.GitLog5 = $gl.Output

# Build
Write-Log "执行 dotnet build ERP.csproj ..."
Push-Location $RepoRoot
try {
    $buildOut = & dotnet build ERP.csproj 2>&1
    $buildExit = $LASTEXITCODE
    $buildText = ($buildOut | Out-String).Trim()
    $reportData.BuildDetail = $buildText
    if ($buildExit -eq 0) {
        $reportData.BuildSuccess = $true
        Write-Log "Build 成功"
    }
    else {
        $reportData.BuildSuccess = $false
        Write-Log "Build 失败 (exit $buildExit)" "ERROR"
        $result = New-SafeCheckReport -Data $reportData
        Write-Log "报告已写入: $($result.Path)" "ERROR"
        exit 1
    }
}
finally { Pop-Location }

# 停止旧 ERP
Write-Log "停止旧 ERP 进程 ..."
$reportData.StoppedProcesses = Stop-ErpProcesses
if ($reportData.StoppedProcesses.Count -eq 0) {
    $reportData.StoppedProcesses = @("(无运行中的 ERP 进程)")
}

# 启动 ERP
Write-Log "启动 ERP ..."
$startResult = Start-ErpApp -ExePath $reportData.ExePath
$reportData.ErpStartSuccess = $startResult.Success
$reportData.ErpStartMessage = $startResult.Message

if (-not $startResult.Success) {
    Write-Log "ERP 启动失败: $($startResult.Message)" "ERROR"
    $result = New-SafeCheckReport -Data $reportData
    Write-Log "报告已写入: $($result.Path)" "ERROR"
    exit 1
}

Write-Log "等待 $StartupWaitSeconds 秒 ..."
Start-Sleep -Seconds $StartupWaitSeconds

# HTTP 检查
Write-Log "检查 http://127.0.0.1:8787 ..."
$homeResult = Test-HttpEndpoint -Url "http://127.0.0.1:8787"
$reportData.HomePageOk = $homeResult.Ok
$reportData.HomePageNote = $homeResult.Note

Write-Log "检查 owner-summary?range=month ..."
$apiMonth = Test-HttpEndpoint -Url "http://127.0.0.1:8787/api/dashboard/owner-summary?range=month"
$reportData.ApiMonthOk = $apiMonth.Ok
$reportData.ApiMonthNote = $apiMonth.Note

Write-Log "检查 owner-summary?range=all ..."
$apiAll = Test-HttpEndpoint -Url "http://127.0.0.1:8787/api/dashboard/owner-summary?range=all"
$reportData.ApiAllOk = $apiAll.Ok
$reportData.ApiAllNote = $apiAll.Note

# 生成报告
$result = New-SafeCheckReport -Data $reportData
Write-Log "报告已写入: $($result.Path)"
Write-Log "=== 安全检查完成 === 成功=$($result.Success) 需修复=$($result.NeedsFix)"

if (-not $result.Success) { exit 1 }
if ($result.NeedsFix) { exit 2 }
exit 0
