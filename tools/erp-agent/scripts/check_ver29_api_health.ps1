#Requires -Version 5.1
<#
.SYNOPSIS
    Ver2.9 回归 — ERP 服务与 API 健康检查（只读，不杀进程、不清数据）
#>
param(
    [string]$RepoRoot = "C:\Users\Administrator\Documents\ERP",
    [string]$BaseUrl = "http://127.0.0.1:8787",
    [int]$TimeoutSec = 10
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "_ver29_common.ps1")

$ts = Get-Ver29Timestamp
$fileTs = Get-Ver29FileTimestamp
$meta = Get-Ver29GitMeta -RepoRoot $RepoRoot
$reportDir = Ensure-Ver29ReportDir -RepoRoot $RepoRoot
$reportPath = Join-Path $reportDir "ver29_api_health_$fileTs.md"

Write-Host "=== Ver2.9 API Health Check ==="
Write-Host "执行时间: $ts"

$checks = @(
    @{ Name = "首页"; Url = "$BaseUrl/"; Accept = @(200) }
    @{ Name = "数据目录 /api/system/data-dir"; Url = "$BaseUrl/api/system/data-dir"; Accept = @(200) }
    @{ Name = "系统信息 /api/info"; Url = "$BaseUrl/api/info"; Accept = @(200, 401, 403) }
    @{ Name = "老板看板 owner-summary (month)"; Url = "$BaseUrl/api/dashboard/owner-summary?range=month"; Accept = @(200, 401) }
    @{ Name = "老板看板 owner-summary (all)"; Url = "$BaseUrl/api/dashboard/owner-summary?range=all"; Accept = @(200, 401) }
)

$results = @()
$serviceUp = $false
$needsManual = $false
$allPass = $true

foreach ($c in $checks) {
    $r = Test-Ver29Http -Url $c.Url -TimeoutSec $TimeoutSec -AcceptableStatus $c.Accept
    if ($r.Reachable) { $serviceUp = $true }

    $status = if ($r.Ok) { "PASS" } else { "FAIL"; $allPass = $false }
    if (-not $r.Reachable) { $needsManual = $true }

    $results += [PSCustomObject]@{
        Check  = $c.Name
        Url    = $c.Url
        Status = $status
        Code   = if ($r.StatusCode) { $r.StatusCode } else { "—" }
        Note   = $r.Note
    }

    $color = if ($r.Ok) { "Green" } else { "Red" }
    Write-Host "[$status] $($c.Name) — $($r.Note)" -ForegroundColor $color
}

if (-not $serviceUp) {
    Write-Host ""
    Write-Host "[提示] ERP 服务未启动或不可访问。请先运行 bin\Debug\net8.0-windows\冠誉制造ERP.exe，再重试。" -ForegroundColor Yellow
    Write-Host "       本脚本不会强行杀进程或自动启动 ERP。"
    $needsManual = $true
    $allPass = $false
}

$overall = if ($allPass) { "PASS" } elseif ($serviceUp) { "PARTIAL" } else { "FAIL — 服务未启动" }

$tableRows = ($results | ForEach-Object {
    "| $($_.Check) | ``$($_.Url)`` | $($_.Status) | $($_.Code) | $($_.Note) |"
}) -join "`n"

$md = @"
# Ver2.9 API 健康检查报告

## 基本信息

| 项目 | 值 |
|------|-----|
| **执行时间** | $ts |
| **当前分支** | $($meta.Branch) |
| **当前 commit** | ``$($meta.Commit)`` |
| **最新提交** | $($meta.LatestCommit) |
| **服务地址** | ``$BaseUrl`` |

## 检查项目与结果

| 检查项 | URL | 结果 | HTTP | 说明 |
|--------|-----|------|------|------|
$tableRows

## 整体结论

| 项目 | 值 |
|------|-----|
| **整体状态** | $overall |
| **服务是否可达** | $(if ($serviceUp) { '是' } else { '否 — 请先启动 ERP' }) |
| **是否需要人工复核** | $(if ($needsManual) { '是' } else { '否' }) |

## 异常说明

$(if (-not $serviceUp) {
"- ERP 进程未运行或端口 8787 不可访问。请手动启动开发版 ERP 后重跑本脚本。"
} elseif (-not $allPass) {
"- 部分接口返回非预期状态，请对照上表逐项排查。"
} else {
"- 无异常。owner-summary 与 /api/info 返回 401/403 表示「服务正常但未登录或权限拦截」，属预期行为。"
})

$(Get-Ver29SafetyFooter)

---
*由 ``tools/erp-agent/scripts/check_ver29_api_health.ps1`` 自动生成*
"@

Set-Content -Path $reportPath -Value $md -Encoding UTF8
Write-Host ""
Write-Host "报告已写入: $reportPath"

if (-not $serviceUp) { exit 1 }
if (-not $allPass) { exit 2 }
exit 0
