#Requires -Version 5.1
# 共享辅助函数，供 Ver2.9 回归脚本 dot-source 使用。

function Get-Ver29RepoRoot {
    param([string]$RepoRoot = "C:\Users\Administrator\Documents\ERP")
    return $RepoRoot
}

function Get-Ver29Timestamp {
    return Get-Date -Format "yyyy-MM-dd HH:mm:ss"
}

function Get-Ver29FileTimestamp {
    return Get-Date -Format "yyyyMMdd_HHmmss"
}

function Invoke-Ver29Git {
    param(
        [string]$RepoRoot,
        [string[]]$GitArgs
    )
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

function Get-Ver29GitMeta {
    param([string]$RepoRoot)
    $branch = (Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("rev-parse", "--abbrev-ref", "HEAD")).Output
    $commit = (Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("rev-parse", "HEAD")).Output
    $log1 = (Invoke-Ver29Git -RepoRoot $RepoRoot -GitArgs @("log", "--oneline", "-1")).Output
    return @{
        Branch       = if ($branch) { $branch.Trim() } else { "(unknown)" }
        Commit       = if ($commit) { $commit.Trim() } else { "(unknown)" }
        LatestCommit = if ($log1) { $log1.Trim() } else { "(unknown)" }
    }
}

function Test-Ver29Http {
    param(
        [string]$Url,
        [int]$TimeoutSec = 10,
        [int[]]$AcceptableStatus = @(200, 401, 403)
    )
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        $code = [int]$resp.StatusCode
        $ok = $AcceptableStatus -contains $code
        return @{
            Ok         = $ok
            StatusCode = $code
            Body       = $resp.Content
            Note       = if ($ok) { "HTTP $code" } else { "HTTP $code（非预期状态）" }
            Reachable  = $true
        }
    }
    catch {
        $code = $null
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
        }
        if ($code -and ($AcceptableStatus -contains $code)) {
            $note = switch ($code) {
                401 { "HTTP 401（需登录，服务正常）" }
                403 { "HTTP 403（权限拦截正常）" }
                default { "HTTP $code" }
            }
            return @{
                Ok         = $true
                StatusCode = $code
                Body       = $null
                Note       = $note
                Reachable  = $true
            }
        }
        return @{
            Ok         = $false
            StatusCode = $code
            Body       = $null
            Note       = if ($code) { "HTTP $code" } else { $_.Exception.Message }
            Reachable  = $false
        }
    }
}

function Get-Ver29SafetyFooter {
    return @"

## 安全声明

- **本轮仅回归检查，不修改业务数据**
- 未 commit
- 未 push
- 未 tag
- 未同步正式 App（``D:\冠誉制造ERP\App``）
- 未清空数据
"@
}

function Ensure-Ver29ReportDir {
    param([string]$RepoRoot)
    $dir = Join-Path $RepoRoot "docs\worklog\reports"
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    return $dir
}
