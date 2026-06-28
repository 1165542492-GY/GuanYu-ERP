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

$sourceMarkers = @(
    @{ Page = "售后维修操作记录"; Source = "AfterSalesServiceOrder.cs"; Marker = "AuditAfterSalesServiceOrder"; Risk = "P1" }
    @{ Page = "售后维修权限键"; Source = "Permissions.cs"; Marker = "after_sales.view"; Risk = "P1" }
    @{ Page = "售后维修生成应收记录"; Source = "AfterSalesServiceOrder.cs"; Marker = "售后维修生成应收"; Risk = "P1" }
    @{ Page = "操作记录售后模块解析"; Source = "OperationLog.cs"; Marker = "售后维修工单"; Risk = "P1" }
    @{ Page = "操作记录列表精简"; Source = "OperationLog.html"; Marker = "oplog-table-compact"; Risk = "P1" }
    @{ Page = "全系统提示人话化"; Source = "App.html"; Marker = "erpHumanizeError"; Risk = "P1" }
    @{ Page = "全系统空数据提示"; Source = "App.html"; Marker = "erpEmptyHtml"; Risk = "P2" }
    @{ Page = "rc12列表空态区分"; Source = "App.html"; Marker = "erpTableEmptyHtml"; Risk = "P2" }
    @{ Page = "rc12详情提示弹窗"; Source = "App.html"; Marker = "erpShowDetailMessage"; Risk = "P2" }
    @{ Page = "rc16物料库存类型字段"; Source = "Material.html"; Marker = "materialStockType"; Risk = "P1" }
    @{ Page = "rc16库存汇总状态列"; Source = "Business.html"; Marker = "stock-status-out"; Risk = "P1" }
    @{ Page = "rc16出入库库存类型带出"; Source = "Business.html"; Marker = "MaterialStockType"; Risk = "P1" }
    @{ Page = "rc17库存汇总类型筛选"; Source = "Business.html"; Marker = "stockTypeFilter"; Risk = "P1" }
    @{ Page = "rc17库存汇总状态筛选"; Source = "Business.html"; Marker = "stockStatusFilter"; Risk = "P1" }
    @{ Page = "rc17库存汇总是否库存物料"; Source = "Business.html"; Marker = "stockInvFilter"; Risk = "P1" }
    @{ Page = "rc17库存参考导出"; Source = "Business.html"; Marker = "stockExportBtn"; Risk = "P1" }
    @{ Page = "rc17成品入库提示"; Source = "Business.html"; Marker = "FinishedHint"; Risk = "P1" }
    @{ Page = "rc17物料是否成品设备列"; Source = "Material.html"; Marker = "是否成品设备"; Risk = "P1" }
    @{ Page = "rc17物料是否维修备件列"; Source = "Material.html"; Marker = "是否维修备件"; Risk = "P1" }
    @{ Page = "rc17物料成本方式列"; Source = "Material.html"; Marker = "成本方式"; Risk = "P1" }
    @{ Page = "rc17物料库存属性说明"; Source = "Material.html"; Marker = "暂不上移动加权平均"; Risk = "P1" }
    @{ Page = "rc17库存导出API"; Source = "Program.cs"; Marker = "/api/stocks/export"; Risk = "P1" }
    @{ Page = "rc18表格省略样式"; Source = "App.html"; Marker = "table-cell-ellipsis"; Risk = "P1" }
    @{ Page = "rc18金额列样式"; Source = "App.html"; Marker = "col-money"; Risk = "P1" }
    @{ Page = "rc18数量列样式"; Source = "App.html"; Marker = "col-qty"; Risk = "P1" }
    @{ Page = "rc18操作列样式"; Source = "App.html"; Marker = "col-action"; Risk = "P1" }
    @{ Page = "rc19销售订单转出库"; Source = "Business.html"; Marker = "转出库"; Risk = "P1" }
    @{ Page = "rc19采购单转入库"; Source = "Business.html"; Marker = "转入库"; Risk = "P1" }
    @{ Page = "rc19可出库数量"; Source = "Business.html"; Marker = "可出库数量"; Risk = "P1" }
    @{ Page = "rc19可入库数量"; Source = "Business.html"; Marker = "可入库数量"; Risk = "P1" }
    @{ Page = "rc19已无可出库数量"; Source = "Business.html"; Marker = "已无可出库数量"; Risk = "P1" }
    @{ Page = "rc19已无可入库数量"; Source = "Business.html"; Marker = "已无可入库数量"; Risk = "P1" }
    @{ Page = "rc19.1环境变量沙盒"; Source = "Program.cs"; Marker = "ERP_DATA_DIR"; Risk = "P1" }
    @{ Page = "rc19.1启动参数沙盒"; Source = "Program.cs"; Marker = "--data-dir"; Risk = "P1" }
    @{ Page = "rc19.1数据目录接口"; Source = "Program.cs"; Marker = "/api/system/data-dir"; Risk = "P1" }
    @{ Page = "rc19.1DataDirectory字段"; Source = "Program.cs"; Marker = "dataDirectorySource"; Risk = "P1" }
    @{ Page = "rc19.1默认正式Data"; Source = "Program.cs"; Marker = "DefaultDataDir"; Risk = "P1" }
    @{ Page = "rc19.1沙盒校验"; Source = "Program.cs"; Marker = "ValidateSandboxDataDirectory"; Risk = "P1" }
    @{ Page = "rc19.1测试沙盒文档"; Source = "README.md"; Marker = "测试沙盒"; Risk = "P1" }
    @{ Page = "rc19.1正式Data路径文档"; Source = "README.md"; Marker = "D:\冠誉制造ERP\Data"; Risk = "P1" }
    @{ Page = "rc17库存类型列"; Source = "Material.html"; Marker = "库存类型"; Risk = "P1" }
    @{ Page = "rc17安全库存"; Source = "Material.html"; Marker = "安全库存"; Risk = "P1" }
    @{ Page = "rc17固定成本价"; Source = "Material.html"; Marker = "固定成本价"; Risk = "P1" }
    @{ Page = "rc17成品设备"; Source = "Material.html"; Marker = "成品设备"; Risk = "P1" }
    @{ Page = "rc17维修备件"; Source = "Material.html"; Marker = "维修备件"; Risk = "P1" }
    @{ Page = "rc17是否库存物料"; Source = "Material.html"; Marker = "是否库存物料"; Risk = "P1" }
    @{ Page = "rc17库存状态"; Source = "Business.html"; Marker = "stockStatusFilter"; Risk = "P1" }
    @{ Page = "rc17默认仓库"; Source = "Material.html"; Marker = "默认仓库"; Risk = "P1" }
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
        @{ Name = "数据目录 API"; Url = "$BaseUrl/api/system/data-dir" }
        @{ Name = "供应商列表 API"; Url = "$BaseUrl/api/suppliers" }
        @{ Name = "客户列表 API"; Url = "$BaseUrl/api/customers" }
        @{ Name = "生产工单 API"; Url = "$BaseUrl/api/production-work-orders" }
        @{ Name = "售后维修 API"; Url = "$BaseUrl/api/after-sales-service-orders" }
    )
    Write-Host ""
    Write-Host "--- API 权限拦截抽样 ---"
    foreach ($a in $apiSamples) {
        $ar = Test-Ver29Http -Url $a.Url -TimeoutSec $TimeoutSec -AcceptableStatus $(if ($a.Url -like "*/api/system/data-dir") { @(200) } else { @(200, 401, 403) })
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

foreach ($p in $sourceMarkers) {
    $filePath = Join-Path $RepoRoot $p.Source
    $found = $false
    $note = "源文件不存在"
    if (Test-Path $filePath) {
        $content = Get-Content -Path $filePath -Raw -Encoding UTF8
        $found = $content -like "*$($p.Marker)*"
        $note = if ($found) { "源码标记存在" } else { "源码标记缺失" }
    }
    $status = if ($found) { "PASS" } else { "FAIL"; if ($homeResult.Ok) { $allPass = $false } }
    $results += [PSCustomObject]@{
        Page   = $p.Page
        Source = $p.Source
        Marker = $p.Marker
        Status = $status
        Note   = $note
        Risk   = $p.Risk
    }
    if ($homeResult.Ok) {
        $color = if ($found) { "Green" } else { "Red" }
        Write-Host "[$status] $($p.Page) ($($p.Source))" -ForegroundColor $color
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
