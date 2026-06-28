#Requires -Version 5.1
<#
.SYNOPSIS
    Ver2.9 回归 — Git 工作区守卫检查（只读，不 commit/push/reset/clean）
#>
param(
    [string]$RepoRoot = "C:\Users\Administrator\Documents\ERP",
    [string]$ExpectedRoot = "C:\Users\Administrator\Documents\ERP"
)

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "_ver29_common.ps1")

$exitCode = 0
$ts = Get-Ver29Timestamp

Write-Host "=== Ver2.9 Git Guard ==="
Write-Host "执行时间: $ts"

# 1. 目录检查
$normalizedRoot = (Resolve-Path -LiteralPath $RepoRoot -ErrorAction SilentlyContinue)
$normalizedExpected = (Resolve-Path -LiteralPath $ExpectedRoot -ErrorAction SilentlyContinue)

if (-not $normalizedRoot) {
    Write-Host "[FAIL] 目录不存在: $RepoRoot" -ForegroundColor Red
    exit 1
}

if ($normalizedRoot.Path -ne $normalizedExpected.Path) {
    Write-Host "[FAIL] 当前目录不是开发仓库" -ForegroundColor Red
    Write-Host "  期望: $ExpectedRoot"
    Write-Host "  实际: $($normalizedRoot.Path)"
    exit 1
}

Write-Host "[OK] 工作目录: $($normalizedRoot.Path)"

# 2. 分支
$branchResult = Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("branch", "--show-current")
$branch = if ($branchResult.Output) { $branchResult.Output.Trim() } else { "(unknown)" }
Write-Host "当前分支: $branch"

# 3. git status --short
$statusResult = Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("status", "--short")
$statusShort = if ($statusResult.Output) { $statusResult.Output } else { "(clean)" }
Write-Host ""
Write-Host "git status --short:"
Write-Host $statusShort

$hasChanges = [bool]$statusResult.Output
if ($hasChanges) {
    Write-Host ""
    Write-Host "[WARN] 当前工作区有未提交变更，请人工确认是否为预期改动。" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "[OK] 工作区无未提交变更"
}

# 4. 最近 8 条 log
$logResult = Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("log", "--oneline", "-8")
Write-Host ""
Write-Host "git log --oneline -8:"
Write-Host $logResult.Output

Write-Host ""
Write-Host "说明: 本脚本不会自动 commit / push / reset / clean。"

if (-not $hasChanges) {
    exit 0
}
exit 0
