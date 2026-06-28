#Requires -Version 5.1
<#
.SYNOPSIS
    Ver2.9 回归 — 核心页面嵌入资源轻量 HTTP 检查（只读）
.DESCRIPTION
    ERP 将所有 *.html 嵌入首页一并下发。本脚本 GET 首页并检查各模块 DOM 标记是否存在。
    401/403 的 API 响应视为权限拦截正常，不判为系统故障。
#>
param(
    [string]$RepoRoot = "C:\Users\Administrator\Documents\ERP",
    [string]$BaseUrl = "http://127.0.0.1:8787",
    [int]$TimeoutSec = 15
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "_ver29_common.ps1")

$ts = Get-Ver29Timestamp
$fileTs = Get-Ver29FileTimestamp
$meta = Get-Ver29GitMeta -RepoRoot $RepoRoot
$reportDir = Ensure-Ver29ReportDir -RepoRoot $RepoRoot
$reportPath = Join-Path $reportDir "ver29_pages_check_$fileTs.md"

Write-Host "=== Ver2.9 Pages Check ==="
Write-Host "执行时间: $ts"

# 页面标记：嵌入在合并后的首页 HTML 中
$pageMarkers = @(
    @{ Page = "登录/首页"; Source = "App.html"; Marker = "dashboardView"; Risk = "P0" }
    @{ Page = "老板首页看板"; Source = "App.html"; Marker = "老板经营驾驶舱"; Risk = "P0" }
    @{ Page = "显示大小切换"; Source = "App.html"; Marker = 'id="displaySize"'; Risk = "P1" }
    @{ Page = "dual-scroll 机制"; Source = "App.html"; Marker = "refreshAllVisibleDualScrolls"; Risk = "P1" }
    @{ Page = "供应商管理"; Source = "App.html"; Marker = "supplierDualScroll"; Risk = "P0" }
    @{ Page = "客户管理"; Source = "Customer.html"; Marker = "customerView"; Risk = "P0" }
    @{ Page = "物料管理"; Source = "Material.html"; Marker = "materialView"; Risk = "P0" }
    @{ Page = "BOM 表"; Source = "App.html"; Marker = "bomView"; Risk = "P0" }
    @{ Page = "机型成本"; Source = "App.html"; Marker = "modelCostView"; Risk = "P0" }
    @{ Page = "销售订单"; Source = "Business.html"; Marker = "salesOrderView"; Risk = "P0" }
    @{ Page = "销售出库"; Source = "Business.html"; Marker = "salesOutboundView"; Risk = "P0" }
    @{ Page = "采购单"; Source = "Business.html"; Marker = "purchaseOrderView"; Risk = "P0" }
    @{ Page = "采购入库"; Source = "Business.html"; Marker = "purchaseInboundView"; Risk = "P0" }
    @{ Page = "生产工单"; Source = "Business.html"; Marker = "productionWorkOrderView"; Risk = "P0" }
    @{ Page = "售后维修工单"; Source = "Business.html"; Marker = "afterSalesServiceOrderView"; Risk = "P0" }
    @{ Page = "售后维修新增按钮"; Source = "Business.html"; Marker = 'id="asoAdd"'; Risk = "P1" }
    @{ Page = "售后维修下一步按钮"; Source = "Business.html"; Marker = "asoBuildListNextActions"; Risk = "P1" }
    @{ Page = "生产领用"; Source = "Business.html"; Marker = "productionPickView"; Risk = "P0" }
    @{ Page = "成品入库"; Source = "Business.html"; Marker = "finishedInboundView"; Risk = "P0" }
    @{ Page = "库存汇总"; Source = "Business.html"; Marker = "stockView"; Risk = "P0" }
    @{ Page = "应收款"; Source = "Business.html"; Marker = "receivableView"; Risk = "P0" }
    @{ Page = "应付款"; Source = "Business.html"; Marker = "payableView"; Risk = "P0" }
    @{ Page = "财务"; Source = "Finance.html"; Marker = "financeView"; Risk = "P0" }
    @{ Page = "合同资料"; Source = "Contract.html"; Marker = "contractSettingView"; Risk = "P1" }
    @{ Page = "合同"; Source = "Contract.html"; Marker = "contractView"; Risk = "P0" }
    @{ Page = "业务下一步按钮"; Source = "Business.html"; Marker = "buildBizView"; Risk = "P1" }
    @{ Page = "测试数据"; Source = "TestData.html"; Marker = "systemSettingTestDataPanel"; Risk = "P1" }
    @{ Page = "测试数据售后维修Sheet"; Source = "TestData.html"; Marker = "售后维修工单 Sheet"; Risk = "P1" }
    @{ Page = "数据备份/维护"; Source = "App.html"; Marker = "backupListTable"; Risk = "P1" }
    @{ Page = "子账号/权限"; Source = "App.html"; Marker = "systemSettingAccountPanel"; Risk = "P1" }
)

$homeResult = Test-Ver29Http -Url "$BaseUrl/" -TimeoutSec $TimeoutSec -AcceptableStatus @(200)
$results = @()
$needsManual = $false
$allPass = $false

if (-not $homeResult.Ok) {
    Write-Host "[FAIL] 无法获取首页 — $($homeResult.Note)" -ForegroundColor Red
    Write-Host "[提示] 请先启动 ERP（8787 端口），本脚本不会自动启动或杀进程。"
    $needsManual = $true
    foreach ($p in $pageMarkers) {
        $results += [PSCustomObject]@{
            Page   = $p.Page
            Source = $p.Source
            Marker = $p.Marker
            Status = "SKIP"
            Note   = "服务未启动"
            Risk   = $p.Risk
        }
    }
}
else {
    $html = $homeResult.Body
    Write-Host "[OK] 首页 HTTP $($homeResult.StatusCode)，内容长度 $($html.Length) 字符"
    $allPass = $true

    foreach ($p in $pageMarkers) {
        $found = $html -like "*$($p.Marker)*"
        $status = if ($found) { "PASS" } else { "FAIL"; $allPass = $false }
        $note = if ($found) { "嵌入标记存在" } else { "标记缺失，可能页面未打包进 App.html" }

        $results += [PSCustomObject]@{
            Page   = $p.Page
            Source = $p.Source
            Marker = $p.Marker
            Status = $status
            Note   = $note
            Risk   = $p.Risk
        }

        $color = if ($found) { "Green" } else { "Red" }
        Write-Host "[$status] $($p.Page) ($($p.Source))" -ForegroundColor $color
    }

    # 抽样 API 权限拦截（未登录应 401/403，说明路由存活）
    $apiSamples = @(
        @{ Name = "供应商列表 API"; Url = "$BaseUrl/api/suppliers" }
        @{ Name = "客户列表 API"; Url = "$BaseUrl/api/customers" }
        @{ Name = "生产工单 API"; Url = "$BaseUrl/api/production-work-orders" }
        @{ Name = "售后维修 API"; Url = "$BaseUrl/api/after-sales-service-orders" }
    )
    Write-Host ""
    Write-Host "--- API 权限拦截抽样 ---"
    foreach ($a in $apiSamples) {
        $ar = Test-Ver29Http -Url $a.Url -TimeoutSec $TimeoutSec -AcceptableStatus @(200, 401, 403)
        $st = if ($ar.Reachable) { "PASS" } else { "FAIL"; $allPass = $false }
        Write-Host "[$st] $($a.Name) — $($ar.Note)" -ForegroundColor $(if ($ar.Reachable) { "Green" } else { "Red" })
        $results += [PSCustomObject]@{
            Page   = $a.Name
            Source = "Program.cs API"
            Marker = $a.Url
            Status = $st
            Note   = if ($ar.StatusCode -in 401, 403) { "权限拦截正常（未登录）" } else { $ar.Note }
            Risk   = "P1"
        }
    }
}

$overall = if (-not $homeResult.Ok) { "FAIL — 服务未启动" } elseif ($allPass) { "PASS" } else { "PARTIAL — 部分标记缺失" }
if (-not $allPass) { $needsManual = $true }

$tableRows = ($results | ForEach-Object {
    "| $($_.Page) | $($_.Source) | ``$($_.Marker)`` | $($_.Status) | $($_.Risk) | $($_.Note) |"
}) -join "`n"

$md = @"
# Ver2.9 核心页面检查报告

## 基本信息

| 项目 | 值 |
|------|-----|
| **执行时间** | $ts |
| **当前分支** | $($meta.Branch) |
| **当前 commit** | ``$($meta.Commit)`` |
| **最新提交** | $($meta.LatestCommit) |
| **检查方式** | GET ``$BaseUrl/`` 并验证嵌入 HTML 标记 |

> ERP 将所有业务页面嵌入 ``App.html`` 一次性下发，无独立 ``/Business.html`` 路由。401/403 API 响应表示权限拦截正常。

## 检查项目与结果

| 页面/接口 | 源文件 | 标记/URL | 结果 | 风险 | 说明 |
|-----------|--------|----------|------|------|------|
$tableRows

## 整体结论

| 项目 | 值 |
|------|-----|
| **整体状态** | $overall |
| **是否需要人工复核** | $(if ($needsManual) { '是 — 请在浏览器登录后逐项点开菜单验收' } else { '否 — 自动检查通过，仍建议抽样人工确认' }) |

## 异常说明

$(if (-not $homeResult.Ok) {
"- 首页不可访问。请先启动 ERP 后重跑。`n- 本脚本不做登录后复杂流程，删除保护/表单联动等须人工浏览器验收。"
} elseif (-not $allPass) {
"- 部分页面嵌入标记缺失，请检查对应 *.html 是否仍被 ERP.csproj 嵌入并在 ServeApp 中合并。"
} else {
"- 自动检查通过。删除确认、批量删除、影响预检等安全机制须人工验收（P0）。"
})

$(Get-Ver29SafetyFooter)

---
*由 ``tools/erp-agent/scripts/check_ver29_pages.ps1`` 自动生成*
"@

Set-Content -Path $reportPath -Value $md -Encoding UTF8
Write-Host ""
Write-Host "报告已写入: $reportPath"

if (-not $homeResult.Ok) { exit 1 }
if (-not $allPass) { exit 2 }
exit 0
