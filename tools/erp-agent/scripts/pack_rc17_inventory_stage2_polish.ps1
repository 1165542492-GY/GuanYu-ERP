# ERP Ver2.9-rc17 inventory stage2 polish pack script
# Boundary: no commit/push/tag/sync/clear/006

$ErrorActionPreference = "Continue"

$ProjectRoot = "C:\Users\Administrator\Documents\ERP"
$BranchName = "codex/ver2.8-basic-business-framework"
$BaseUrl = "http://127.0.0.1:8787"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Desktop = [Environment]::GetFolderPath("Desktop")

$PackLabel = "ERP_Ver29_rc17" + "库存类型阶段2成果给ChatGPT"
$LogDir = Join-Path $Desktop ($PackLabel + "_" + $Timestamp)
$LogFile = Join-Path $LogDir ("pack_rc17_log_" + $Timestamp + ".txt")
$SummaryFile = Join-Path $LogDir ("pack_rc17_summary_" + $Timestamp + ".md")
$ZipFile = Join-Path $Desktop ($PackLabel + "_" + $Timestamp + ".zip")

$FilesToZip = New-Object System.Collections.Generic.List[string]
$StepFailed = $false
$buildCode = -1
$httpOk = $false

function Log-Step {
    param([string]$Text)
    $line = "`n========== $Text =========="
    Write-Host $line -ForegroundColor Cyan
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
}

function Log-Line {
    param([string]$Text)
    Write-Host $Text
    Add-Content -Path $LogFile -Value $Text -Encoding UTF8
}

function Add-ToZipList {
    param([string]$Path)
    if ($Path -and (Test-Path $Path)) {
        if (-not $FilesToZip.Contains($Path)) {
            [void]$FilesToZip.Add($Path)
            Log-Line ("Collect: " + $Path)
        }
    }
}

function Run-Capture {
    param(
        [string]$Title,
        [string]$Exe,
        [string[]]$ToolArgs,
        [string]$OutFile,
        [bool]$StopOnFail = $false
    )

    Log-Step $Title
    Log-Line ("Command: " + $Exe + " " + ($ToolArgs -join " "))

    $output = @()
    $code = 0
    try {
        $output = & $Exe @ToolArgs 2>&1
        $code = $LASTEXITCODE
        if ($null -eq $code) { $code = 0 }
    }
    catch {
        $output = @($_.Exception.Message)
        $code = 1
    }

    $text = ($output | ForEach-Object { ($_.ToString()).TrimEnd() }) -join "`n"
    if ($OutFile) {
        $text | Out-File -Encoding UTF8 $OutFile
        Add-ToZipList $OutFile
    }
    if ($text) { Log-Line $text }
    Log-Line ("ExitCode: " + $code)

    if ($StopOnFail -and $code -ne 0) {
        $script:StepFailed = $true
        Log-Line ("Step failed (continue pack): " + $Title)
    }
    return [int]$code
}

try {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    Add-ToZipList $LogFile

    Log-Step "Basic info"
    Log-Line ("ProjectRoot: " + $ProjectRoot)
    Log-Line ("BranchName: " + $BranchName)
    Log-Line ("Timestamp: " + $Timestamp)
    Log-Line ("LogDir: " + $LogDir)
    Log-Line ("ZipFile: " + $ZipFile)
    Log-Line "Boundary: no commit/push/tag/sync/clear/006"

    if (!(Test-Path $ProjectRoot)) {
        throw ("Project root missing: " + $ProjectRoot)
    }

    Set-Location $ProjectRoot

    Run-Capture "git branch" "git" @("branch", "--show-current") (Join-Path $LogDir ("git_branch_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git status -sb" "git" @("status", "-sb") (Join-Path $LogDir ("git_status_sb_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git status --short" "git" @("status", "--short") (Join-Path $LogDir ("git_status_short_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git diff --name-status" "git" @("diff", "--name-status") (Join-Path $LogDir ("git_diff_name_status_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git diff --stat" "git" @("diff", "--stat") (Join-Path $LogDir ("git_diff_stat_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git log -5" "git" @("log", "-5", "--oneline") (Join-Path $LogDir ("git_log_5_" + $Timestamp + ".txt")) | Out-Null

    $buildCode = Run-Capture "dotnet build" "dotnet" @("build", ".\ERP.csproj") (Join-Path $LogDir ("dotnet_build_" + $Timestamp + ".txt")) $true
    if ($buildCode -ne 0) { $script:StepFailed = $true }

    $RegressionScript = $null
    $Candidates = @(
        (Join-Path $ProjectRoot "tools\erp-agent\scripts\run_ver29_regression.ps1"),
        (Join-Path $ProjectRoot "tools\erp-agent\run_ver29_regression.ps1"),
        (Join-Path $ProjectRoot "run_ver29_regression.ps1")
    )
    foreach ($c in $Candidates) {
        if (Test-Path $c) { $RegressionScript = $c; break }
    }
    if ($null -eq $RegressionScript) {
        $found = Get-ChildItem -Path $ProjectRoot -Filter "run_ver29_regression.ps1" -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { $RegressionScript = $found.FullName }
    }

    if ($RegressionScript) {
        Run-Capture "run_ver29_regression" "powershell.exe" @("-ExecutionPolicy", "Bypass", "-File", $RegressionScript) (Join-Path $LogDir ("ver29_regression_" + $Timestamp + ".txt")) | Out-Null
    }
    else {
        Log-Line "run_ver29_regression.ps1 not found"
        $script:StepFailed = $true
    }

    $PagesScript = Join-Path $ProjectRoot "tools\erp-agent\scripts\check_ver29_pages.ps1"
    if (Test-Path $PagesScript) {
        Run-Capture "check_ver29_pages" "powershell.exe" @("-ExecutionPolicy", "Bypass", "-File", $PagesScript) (Join-Path $LogDir ("ver29_pages_check_" + $Timestamp + ".txt")) | Out-Null
    }
    else {
        Log-Line "check_ver29_pages.ps1 not found"
        $script:StepFailed = $true
    }

    Log-Step "8787 HTTP check"
    $httpOut = Join-Path $LogDir ("http_8787_check_" + $Timestamp + ".txt")
    try {
        $resp = Invoke-WebRequest -Uri $BaseUrl -UseBasicParsing -TimeoutSec 15
        $httpOk = ($resp.StatusCode -eq 200)
        ("URL: " + $BaseUrl + "`nStatusCode: " + $resp.StatusCode + "`nResult: PASS") | Out-File -Encoding UTF8 $httpOut
    }
    catch {
        ("URL: " + $BaseUrl + "`nError: " + $_.Exception.Message + "`nResult: FAIL") | Out-File -Encoding UTF8 $httpOut
        $script:StepFailed = $true
    }
    Add-ToZipList $httpOut
    Log-Line (Get-Content $httpOut -Raw)

    Log-Step "Modified file snapshots"
    $snapDir = Join-Path $LogDir "modified_snapshots"
    New-Item -ItemType Directory -Force -Path $snapDir | Out-Null
    $diffNames = git diff --name-only 2>$null
    if ($diffNames) {
        foreach ($rel in $diffNames) {
            $src = Join-Path $ProjectRoot $rel
            if (Test-Path $src) {
                $dest = Join-Path $snapDir ($rel -replace '[\\/]', '_')
                Copy-Item -Path $src -Destination $dest -Force
                Add-ToZipList $dest
            }
        }
    }
    $untracked = git ls-files --others --exclude-standard 2>$null
    if ($untracked) {
        foreach ($rel in $untracked) {
            $src = Join-Path $ProjectRoot $rel
            if (Test-Path $src) {
                $dest = Join-Path $snapDir ("untracked_" + ($rel -replace '[\\/]', '_'))
                Copy-Item -Path $src -Destination $dest -Force
                Add-ToZipList $dest
            }
        }
    }

    $DocFiles = @(
        "README.md",
        "VERSION.md",
        "安装说明.txt",
        "docs\worklog\ERP-Ver2.9-任务总表.md",
        "docs\worklog\ERP-Ver2.9-执行台账.md",
        "Business.html",
        "Material.html",
        "Program.cs",
        "TestDataService.cs",
        "tools\erp-agent\scripts\check_ver29_pages.ps1",
        "tools\erp-agent\scripts\pack_rc17_inventory_stage2_polish.ps1"
    )
    foreach ($rel in $DocFiles) {
        Add-ToZipList (Join-Path $ProjectRoot $rel)
    }

    $reportDir = Join-Path $ProjectRoot "docs\worklog\reports"
    if (Test-Path $reportDir) {
        Get-ChildItem -Path $reportDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "ver29_regression_summary_*" -or $_.Name -like "ver29_pages_check_*" -or $_.Name -like "*rc17*" -or $_.Name -like "*rc16*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 15 |
            ForEach-Object { Add-ToZipList $_.FullName }
    }

    $buildResult = if ($buildCode -eq 0) { "PASS 0 errors" } else { ("FAIL exit " + $buildCode) }
    $httpResult = if ($httpOk) { "PASS 200" } else { "FAIL" }
    $overall = if ($StepFailed) { "PARTIAL" } else { "OK" }

    $summaryText = "# ERP Ver2.9-rc17 pack summary`nTime: $Timestamp`nOverall: $overall`nBuild: $buildResult`nHTTP: $BaseUrl $httpResult`nLogDir: $LogDir`nZip: $ZipFile`nBoundary: no commit/push/tag/sync/clear/006"
    $summaryText | Out-File -Encoding UTF8 $SummaryFile
    Add-ToZipList $SummaryFile

    if ($FilesToZip.Count -gt 0) {
        Compress-Archive -Path $FilesToZip.ToArray() -DestinationPath $ZipFile -Force
        Write-Host ""
        Write-Host "rc17 pack done" -ForegroundColor Green
        Write-Host $ZipFile -ForegroundColor Yellow
        try { explorer.exe ("/select," + $ZipFile) } catch {}
    }
    else {
        throw "No files to zip"
    }
}
catch {
    Log-Step "Script error"
    Log-Line $_.Exception.Message
    $script:StepFailed = $true
    try {
        if (Test-Path $LogDir) {
            Compress-Archive -Path (Join-Path $LogDir "*") -DestinationPath $ZipFile -Force
            Write-Host ("Failure log zip: " + $ZipFile) -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}
finally {
    Write-Host "Press Enter to close" -ForegroundColor Yellow
    [void][System.Console]::ReadLine()
    exit
}
