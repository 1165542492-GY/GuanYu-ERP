# ERP Ver2.9 system assessment pack script (after rc18-rc19)
# Boundary: no commit/push/tag/sync/backup/clear/006/business code changes

$ErrorActionPreference = "Continue"

$ProjectRoot = "C:\Users\Administrator\Documents\ERP"
$BranchName = "codex/ver2.8-basic-business-framework"
$BaseUrl = "http://127.0.0.1:8787"
$FormalApp = "D:\冠誉制造ERP\App\冠誉制造ERP.exe"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Desktop = [Environment]::GetFolderPath("Desktop")

$PackLabel = "ERP_Ver29_rc18_rc19" + "后系统整体评估给ChatGPT"
$LogDir = Join-Path $Desktop ($PackLabel + "_" + $Timestamp)
$LogFile = Join-Path $LogDir ("pack_assessment_log_" + $Timestamp + ".txt")
$SummaryFile = Join-Path $LogDir ("pack_assessment_summary_" + $Timestamp + ".md")
$ZipFile = Join-Path $Desktop ($PackLabel + "_" + $Timestamp + ".zip")

$AssessmentReport = Join-Path $ProjectRoot "docs\worklog\ERP-Ver2.9-现阶段系统整体评估报告.md"

$FilesToZip = New-Object System.Collections.Generic.List[string]
$StepFailed = $false
$buildCode = -1
$httpOk = $false
$formalAppOk = $false

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
    Log-Line ("AssessmentReport: " + $AssessmentReport)
    Log-Line "Boundary: read-only assessment; no commit/push/tag/sync/backup/clear/006"

    if (!(Test-Path $ProjectRoot)) {
        throw ("Project root missing: " + $ProjectRoot)
    }

    Set-Location $ProjectRoot

    Add-ToZipList $AssessmentReport

    Run-Capture "git branch" "git" @("branch", "--show-current") (Join-Path $LogDir ("git_branch_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git status -sb" "git" @("status", "-sb") (Join-Path $LogDir ("git_status_sb_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git status --short" "git" @("status", "--short") (Join-Path $LogDir ("git_status_short_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git log -8" "git" @("log", "-8", "--oneline", "--decorate") (Join-Path $LogDir ("git_log_8_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git tag v2.9.0" "git" @("tag", "--list", "v2.9.0-*") (Join-Path $LogDir ("git_tags_v290_" + $Timestamp + ".txt")) | Out-Null

    $buildOut = Join-Path $LogDir ("dotnet_build_" + $Timestamp + ".txt")
    $buildCode = Run-Capture "dotnet build" "dotnet" @("build", ".\ERP.csproj") $buildOut $true
    if ($buildCode -ne 0) {
        Log-Line "Primary build failed, trying alt output dir..."
        $buildCode = Run-Capture "dotnet build alt" "dotnet" @("build", ".\ERP.csproj", "-o", ".\bin\assessment-pack-build") (Join-Path $LogDir ("dotnet_build_alt_" + $Timestamp + ".txt")) $true
    }
    if ($buildCode -ne 0) { $script:StepFailed = $true }

    $RegressionScript = Join-Path $ProjectRoot "tools\erp-agent\scripts\run_ver29_regression.ps1"
    if (Test-Path $RegressionScript) {
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

    Log-Step "Formal App check"
    $formalOut = Join-Path $LogDir ("formal_app_check_" + $Timestamp + ".txt")
    if (Test-Path $FormalApp) {
        $formalAppOk = $true
        ("Path: " + $FormalApp + "`nResult: PASS exists") | Out-File -Encoding UTF8 $formalOut
    }
    else {
        ("Path: " + $FormalApp + "`nResult: FAIL missing") | Out-File -Encoding UTF8 $formalOut
        $script:StepFailed = $true
    }
    Add-ToZipList $formalOut
    Log-Line (Get-Content $formalOut -Raw)

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

    $DocFiles = @(
        "README.md",
        "VERSION.md",
        "安装说明.txt",
        "docs\worklog\ERP-Ver2.9-任务总表.md",
        "docs\worklog\ERP-Ver2.9-执行台账.md",
        "docs\worklog\ERP-Ver2.9-现阶段系统整体评估报告.md",
        "tools\erp-agent\scripts\pack_ver29_system_assessment_after_rc18_rc19.ps1"
    )
    foreach ($rel in $DocFiles) {
        Add-ToZipList (Join-Path $ProjectRoot $rel)
    }

    $reportDir = Join-Path $ProjectRoot "docs\worklog\reports"
    if (Test-Path $reportDir) {
        Get-ChildItem -Path $reportDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "ver29_regression_summary_*" -or $_.Name -like "ver29_pages_check_*" -or $_.Name -like "ver29_api_health_*" } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 10 |
            ForEach-Object { Add-ToZipList $_.FullName }
    }

    $buildResult = if ($buildCode -eq 0) { "PASS 0 errors" } else { ("FAIL exit " + $buildCode) }
    $httpResult = if ($httpOk) { "PASS 200" } else { "FAIL" }
    $formalResult = if ($formalAppOk) { "PASS exists" } else { "FAIL missing" }
    $overall = if ($StepFailed) { "PARTIAL" } else { "OK" }

    $summaryText = "# ERP Ver2.9 system assessment pack summary`nTime: $Timestamp`nOverall: $overall`nBuild: $buildResult`nRegression: see ver29_regression log`nPages: see ver29_pages log`nFormalApp: $formalResult`nHTTP: $BaseUrl $httpResult`nReport: $AssessmentReport`nZip: $ZipFile`nConclusion: conditional trial run recommended`nBoundary: no commit/push/tag/sync/backup/clear/006"
    $summaryText | Out-File -Encoding UTF8 $SummaryFile
    Add-ToZipList $SummaryFile

    if ($FilesToZip.Count -gt 0) {
        Compress-Archive -Path $FilesToZip.ToArray() -DestinationPath $ZipFile -Force
        Write-Host ""
        Write-Host "assessment pack done" -ForegroundColor Green
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
