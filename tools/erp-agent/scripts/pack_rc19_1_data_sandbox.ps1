# ERP Ver2.9-rc19.1 test data sandbox pack + verification script
# Boundary: no commit/push/tag/sync/clear/delete formal Data/006

$ErrorActionPreference = "Continue"

$ProjectRoot = "C:\Users\Administrator\Documents\ERP"
$BranchName = "codex/ver2.8-basic-business-framework"
$BaseUrl = "http://127.0.0.1:8787"
$FormalDataDir = "D:\冠誉制造ERP\Data"
$FormalApp = "D:\冠誉制造ERP\App\冠誉制造ERP.exe"
$DefaultDataDir = "D:\冠誉制造ERP\Data"
$E2EMarker = "E2E_RC19_MFG_"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Desktop = [Environment]::GetFolderPath("Desktop")

$PackLabel = "ERP_Ver29_rc19_1测试沙盒数据目录成果给ChatGPT"
$LogDir = Join-Path $Desktop ($PackLabel + "_" + $Timestamp)
$LogFile = Join-Path $LogDir ("pack_rc19_1_log_" + $Timestamp + ".txt")
$SummaryFile = Join-Path $LogDir ("pack_rc19_1_summary_" + $Timestamp + ".md")
$IssuesFile = Join-Path $LogDir ("issues_rc19_1_" + $Timestamp + ".md")
$ZipFile = Join-Path $Desktop ($PackLabel + "_" + $Timestamp + ".zip")

$SandboxEnvDir = Join-Path "D:\冠誉制造ERP\TestData" ("E2E_BlankMachineFactory_" + $Timestamp)
$SandboxArgDir = Join-Path "D:\冠誉制造ERP\TestData" ("E2E_ArgPriority_" + $Timestamp)

$FilesToZip = New-Object System.Collections.Generic.List[string]
$StepFailed = $false
$buildCode = -1
$httpOk = $false
$issues = New-Object System.Collections.Generic.List[string]

$testResults = @{
    DefaultMode = "SKIP"
    EnvSandbox  = "SKIP"
    ArgSandbox  = "SKIP"
    FormalData  = "SKIP"
}

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

function Add-Issue {
    param([string]$Level, [string]$Text)
    [void]$issues.Add("- **$Level** $Text")
    Log-Line ("ISSUE [$Level] $Text")
    if ($Level -eq "P0") { $script:StepFailed = $true }
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

function Resolve-ErpExe {
    param([string]$BuildOutputDir)
    $dirs = @(
        (Join-Path $ProjectRoot "bin\Debug\net8.0-windows"),
        (Join-Path $ProjectRoot $BuildOutputDir)
    )
    foreach ($dir in $dirs) {
        if (-not (Test-Path $dir)) { continue }
        $exe = Get-ChildItem -Path $dir -Filter "*.exe" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne "apphost.exe" } |
            Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }
    return $null
}

function Stop-ErpProcesses {
    Log-Step "Stop existing ERP processes"
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessName -like "*ERP*" -or
            ($_.Path -and ($_.Path -like "*冠誉制造ERP*" -or $_.Path -like "*Documents\ERP\bin*"))
        } |
        ForEach-Object {
            Log-Line ("Stopping PID " + $_.Id + " (" + $_.ProcessName + ")")
            try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch { }
            Start-Process -FilePath "taskkill" -ArgumentList @("/F", "/PID", $_.Id) -Verb RunAs -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null
        }
    Start-Sleep -Seconds 4
}

function Get-FormalDataSnapshot {
    param([string]$Label)
    $dataRoot = Join-Path "D:\冠誉制造ERP" "Data"
    $snap = [ordered]@{
        Label           = $Label
        Exists          = Test-Path -LiteralPath $dataRoot
        JsonCount       = 0
        HasE2E          = $false
        E2EFiles        = @()
        Deleted         = $false
    }
    if ($snap.Exists) {
        $jsonFiles = Get-ChildItem -LiteralPath $dataRoot -Filter "*.json" -File -ErrorAction SilentlyContinue
        $snap.JsonCount = @($jsonFiles).Count
        foreach ($f in $jsonFiles) {
            try {
                $content = Get-Content -Path $f.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
                if ($content -and ($content -like ("*" + $E2EMarker + "*"))) {
                    $snap.HasE2E = $true
                    $snap.E2EFiles += $f.Name
                }
            }
            catch { }
        }
    }
    else {
        $snap.Deleted = $true
    }
    return $snap
}

function Write-FormalDataLog {
    param(
        [object]$Before,
        [object]$After,
        [string]$OutFile
    )
    $lines = @(
        "# Formal Data safety check",
        "",
        "## Before",
        "- Exists: $($Before.Exists)",
        "- JSON count: $($Before.JsonCount)",
        "- Has $E2EMarker : $($Before.HasE2E)",
        "",
        "## After",
        "- Exists: $($After.Exists)",
        "- JSON count: $($After.JsonCount)",
        "- Has $E2EMarker : $($After.HasE2E)",
        "- Deleted: $($After.Deleted)",
        "",
        "## Result"
    )
    if (-not $After.Exists) {
        $formalRoot = Join-Path "D:\冠誉制造ERP" "Data"
        $After.Exists = Test-Path -LiteralPath $formalRoot
        if ($After.Exists) {
            $After.Deleted = $false
            $jsonFiles = Get-ChildItem -LiteralPath $formalRoot -Filter "*.json" -File -ErrorAction SilentlyContinue
            $After.JsonCount = @($jsonFiles).Count
        }
    }
    if (-not $After.Exists) {
        $lines += "FAIL: formal Data directory missing"
        $script:testResults.FormalData = "FAIL"
        Add-Issue "P0" "Formal Data directory missing or deleted"
    }
    elseif ($After.Deleted) {
        $lines += "FAIL: formal Data marked deleted"
        $script:testResults.FormalData = "FAIL"
        Add-Issue "P0" "Formal Data directory was deleted"
    }
    elseif ($Before.JsonCount -ne $After.JsonCount) {
        $lines += ("WARN: JSON count changed " + $Before.JsonCount + " -> " + $After.JsonCount)
        $script:testResults.FormalData = "WARN"
        Add-Issue "P1" ("Formal Data JSON count changed: " + $Before.JsonCount + " -> " + $After.JsonCount)
    }
    else {
        $lines += "PASS: JSON count unchanged"
    }
    if ($After.HasE2E) {
        $lines += ("FAIL: E2E marker found in formal Data: " + ($After.E2EFiles -join ", "))
        $script:testResults.FormalData = "FAIL"
        Add-Issue "P0" "Formal Data contains E2E test marker"
    }
    elseif ($script:testResults.FormalData -eq "SKIP") {
        if ($After.Exists -and -not $After.Deleted) {
            $script:testResults.FormalData = "PASS"
            $lines += "PASS: no E2E marker in formal Data"
        }
    }
    $lines -join "`n" | Out-File -Encoding UTF8 $OutFile
    Add-ToZipList $OutFile
}

function Wait-ForHttp {
    param(
        [string]$Url,
        [int]$TimeoutSec = 60
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            if ($resp.StatusCode -eq 200) { return $true }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    return $false
}

function Get-DataDirApi {
    param([string]$OutFile)
    try {
        $resp = Invoke-WebRequest -Uri ($BaseUrl + "/api/system/data-dir") -UseBasicParsing -TimeoutSec 10
        $json = $resp.Content | ConvertFrom-Json
        $text = $resp.Content
        if ($OutFile) {
            $text | Out-File -Encoding UTF8 $OutFile
            Add-ToZipList $OutFile
        }
        return $json
    }
    catch {
        $msg = $_.Exception.Message
        if ($OutFile) {
            $msg | Out-File -Encoding UTF8 $OutFile
            Add-ToZipList $OutFile
        }
        return $null
    }
}

function Start-ErpInstance {
    param(
        [string]$ExePath,
        [string]$EnvDataDir = $null,
        [string]$ArgDataDir = $null,
        [switch]$ClearEnvDataDir
    )

    if ($EnvDataDir -and -not $ArgDataDir) {
        [Environment]::SetEnvironmentVariable("ERP_DATA_DIR", $EnvDataDir, "User")
        Start-Sleep -Milliseconds 500
        $proc = Start-Process -FilePath $ExePath -PassThru -WindowStyle Hidden -ErrorAction Stop
        return $proc
    }

    $savedEnv = $env:ERP_DATA_DIR
    if ($ClearEnvDataDir -or [string]::IsNullOrWhiteSpace($EnvDataDir)) {
        Remove-Item Env:ERP_DATA_DIR -ErrorAction SilentlyContinue
    }
    elseif ($EnvDataDir) {
        $env:ERP_DATA_DIR = $EnvDataDir
    }

    if ($ArgDataDir) {
        $proc = Start-Process -FilePath $ExePath -ArgumentList @('--data-dir', $ArgDataDir) -PassThru -WindowStyle Hidden -ErrorAction Stop
    }
    else {
        $proc = Start-Process -FilePath $ExePath -PassThru -WindowStyle Hidden -ErrorAction Stop
    }

    if ($ClearEnvDataDir -or [string]::IsNullOrWhiteSpace($EnvDataDir)) {
        if ($null -ne $savedEnv) { $env:ERP_DATA_DIR = $savedEnv } else { Remove-Item Env:ERP_DATA_DIR -ErrorAction SilentlyContinue }
    }
    else {
        if ($null -ne $savedEnv) { $env:ERP_DATA_DIR = $savedEnv } else { Remove-Item Env:ERP_DATA_DIR -ErrorAction SilentlyContinue }
    }

    return $proc
}

function Test-ErpDataDirScenario {
    param(
        [string]$Title,
        [string]$ExePath,
        [string]$ExpectedDir,
        [string]$ExpectedSource,
        [bool]$ExpectedDefault,
        [string]$EnvDataDir = $null,
        [string]$ArgDataDir = $null,
        [string]$OutFile
    )

    Log-Step $Title
    Stop-ErpProcesses

    $proc = $null
    $result = "FAIL"
    $detail = ""
    try {
        $proc = Start-ErpInstance -ExePath $ExePath -EnvDataDir $EnvDataDir -ArgDataDir $ArgDataDir -ClearEnvDataDir:($null -eq $EnvDataDir)
        if (-not (Wait-ForHttp -Url ($BaseUrl + "/api/system/data-dir"))) {
            $detail = "ERP did not respond on 8787"
            Add-Issue "P0" ($Title + ": " + $detail)
            return $result
        }

        $info = Get-DataDirApi -OutFile $OutFile
        if ($null -eq $info) {
            $detail = "Failed to read /api/system/data-dir"
            Add-Issue "P0" ($Title + ": " + $detail)
            return $result
        }

        $actualDir = ($info.dataDirectory + "").TrimEnd('\')
        $sourceOk = ($info.dataDirectorySource -eq $ExpectedSource)
        $defaultOk = ([bool]$info.isDefaultDataDirectory -eq $ExpectedDefault)
        if ($ExpectedDefault) {
            $dirOk = [bool]$info.isDefaultDataDirectory
        }
        else {
            $leaf = Split-Path $ExpectedDir -Leaf
            $dirOk = (-not [bool]$info.isDefaultDataDirectory) -and ($actualDir -like ("*" + $leaf))
        }

        $detail = "dataDirectory=$actualDir; source=$($info.dataDirectorySource); isDefault=$($info.isDefaultDataDirectory)"
        Log-Line $detail

        if ($dirOk -and $sourceOk -and $defaultOk) {
            $result = "PASS"
        }
        else {
            Add-Issue "P0" ($Title + " mismatch: " + $detail)
        }

        if (-not $ExpectedDefault) {
            if (-not (Test-Path -LiteralPath $ExpectedDir)) {
                Add-Issue "P0" ($Title + ": sandbox directory not created")
                $result = "FAIL"
            }
        }
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            try { $proc.Kill() } catch { }
        }
        if ($EnvDataDir -and -not $ArgDataDir) {
            [Environment]::SetEnvironmentVariable("ERP_DATA_DIR", $null, "User")
        }
        Stop-ErpProcesses
    }
    return $result
}

try {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    Add-ToZipList $LogFile

    Log-Step "Basic info"
    Log-Line ("ProjectRoot: " + $ProjectRoot)
    Log-Line ("BranchName: " + $BranchName)
    Log-Line ("Timestamp: " + $Timestamp)
    Log-Line ("FormalDataDir: " + $FormalDataDir)
    Log-Line ("SandboxEnvDir: " + $SandboxEnvDir)
    Log-Line ("SandboxArgDir: " + $SandboxArgDir)
    Log-Line "Boundary: no commit/push/tag/sync/clear/delete formal Data/006"

    if (!(Test-Path $ProjectRoot)) {
        throw ("Project root missing: " + $ProjectRoot)
    }
    Set-Location $ProjectRoot

    Stop-ErpProcesses

    $formalBefore = Get-FormalDataSnapshot -Label "before"
    $formalBeforeLog = Join-Path $LogDir ("formal_data_before_" + $Timestamp + ".txt")
    ($formalBefore | ConvertTo-Json -Depth 4) | Out-File -Encoding UTF8 $formalBeforeLog
    Add-ToZipList $formalBeforeLog

    Run-Capture "git status -sb" "git" @("status", "-sb") (Join-Path $LogDir ("git_status_sb_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git status --short" "git" @("status", "--short") (Join-Path $LogDir ("git_status_short_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git diff --name-status" "git" @("diff", "--name-status") (Join-Path $LogDir ("git_diff_name_status_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git diff --stat" "git" @("diff", "--stat") (Join-Path $LogDir ("git_diff_stat_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git log -8" "git" @("log", "-8", "--oneline", "--decorate") (Join-Path $LogDir ("git_log_8_" + $Timestamp + ".txt")) | Out-Null
    Run-Capture "git tag list" "git" @("tag", "--list", "v2.9.0-*") (Join-Path $LogDir ("git_tag_list_" + $Timestamp + ".txt")) | Out-Null

    $buildOutDir = "bin\rc19_1_sandbox_build_check"
    $buildCode = Run-Capture "dotnet build primary" "dotnet" @("build", ".\ERP.csproj") (Join-Path $LogDir ("dotnet_build_" + $Timestamp + ".txt")) $false
    if ($buildCode -ne 0) {
        $buildCode = Run-Capture "dotnet build alt" "dotnet" @("build", ".\ERP.csproj", "-o", (".\" + $buildOutDir)) (Join-Path $LogDir ("dotnet_build_alt_" + $Timestamp + ".txt")) $true
    }
    if ($buildCode -ne 0) { $script:StepFailed = $true }

    $exePath = Resolve-ErpExe -BuildOutputDir $buildOutDir
    if (-not $exePath) {
        throw "ERP executable not found after build"
    }
    Log-Line ("Using ERP exe: " + $exePath)

    Copy-Item -Path (Join-Path $ProjectRoot "Program.cs") -Destination (Join-Path $LogDir ("Program_cs_snapshot_" + $Timestamp + ".cs")) -Force
    Add-ToZipList (Join-Path $LogDir ("Program_cs_snapshot_" + $Timestamp + ".cs"))

    $testResults.DefaultMode = Test-ErpDataDirScenario `
        -Title "Default mode DataDir verification" `
        -ExePath $exePath `
        -ExpectedDir $DefaultDataDir `
        -ExpectedSource "Default" `
        -ExpectedDefault $true `
        -OutFile (Join-Path $LogDir ("verify_default_datadir_" + $Timestamp + ".json"))

    $testResults.EnvSandbox = Test-ErpDataDirScenario `
        -Title "ERP_DATA_DIR sandbox verification" `
        -ExePath $exePath `
        -ExpectedDir $SandboxEnvDir `
        -ExpectedSource "Environment:ERP_DATA_DIR" `
        -ExpectedDefault $false `
        -EnvDataDir $SandboxEnvDir `
        -OutFile (Join-Path $LogDir ("verify_env_sandbox_" + $Timestamp + ".json"))

    $decoyEnvDir = Join-Path "D:\冠誉制造ERP\TestData" ("E2E_EnvShouldLose_" + $Timestamp)
    $testResults.ArgSandbox = Test-ErpDataDirScenario `
        -Title 'Arg data-dir priority verification' `
        -ExePath $exePath `
        -ExpectedDir $SandboxArgDir `
        -ExpectedSource "Argument:--data-dir" `
        -ExpectedDefault $false `
        -EnvDataDir $decoyEnvDir `
        -ArgDataDir $SandboxArgDir `
        -OutFile (Join-Path $LogDir ("verify_arg_sandbox_" + $Timestamp + ".json"))

    $formalAfter = Get-FormalDataSnapshot -Label "after"
    Write-FormalDataLog -Before $formalBefore -After $formalAfter -OutFile (Join-Path $LogDir ("formal_data_safety_" + $Timestamp + ".md"))

    Stop-ErpProcesses
    $procDefault = Start-ErpInstance -ExePath $exePath -ClearEnvDataDir
    try {
        if (Wait-ForHttp -Url $BaseUrl) {
            $httpOk = $true
            $httpOut = Join-Path $LogDir ("http_8787_check_" + $Timestamp + ".txt")
            ("URL: " + $BaseUrl + "`nResult: PASS") | Out-File -Encoding UTF8 $httpOut
            Add-ToZipList $httpOut
        }
        else {
            $httpOk = $false
            Add-Issue "P1" "8787 HTTP check failed after default ERP start"
        }

        $RegressionScript = Join-Path $ProjectRoot "tools\erp-agent\scripts\run_ver29_regression.ps1"
        if (Test-Path $RegressionScript) {
            Run-Capture "Ver2.9 regression" "powershell" @("-ExecutionPolicy", "Bypass", "-File", $RegressionScript, "-RepoRoot", $ProjectRoot, "-BaseUrl", $BaseUrl) (Join-Path $LogDir ("ver29_regression_" + $Timestamp + ".txt")) | Out-Null
        }

        $PagesScript = Join-Path $ProjectRoot "tools\erp-agent\scripts\check_ver29_pages.ps1"
        if (Test-Path $PagesScript) {
            Run-Capture "Ver2.9 pages check" "powershell" @("-ExecutionPolicy", "Bypass", "-File", $PagesScript, "-RepoRoot", $ProjectRoot, "-BaseUrl", $BaseUrl) (Join-Path $LogDir ("ver29_pages_check_" + $Timestamp + ".txt")) | Out-Null
        }
    }
    finally {
        if ($procDefault -and -not $procDefault.HasExited) {
            try { $procDefault.Kill() } catch { }
        }
        Stop-ErpProcesses
    }

    $DocFiles = @(
        "README.md",
        "VERSION.md",
        "安装说明.txt",
        "docs\worklog\ERP-Ver2.9-任务总表.md",
        "docs\worklog\ERP-Ver2.9-执行台账.md",
        "docs\worklog\ERP-Ver2.9-空白系统测试安全检查报告.md",
        "tools\erp-agent\scripts\pack_rc19_1_data_sandbox.ps1",
        "tools\erp-agent\scripts\check_ver29_pages.ps1",
        "tools\erp-agent\scripts\check_ver29_api_health.ps1"
    )
    foreach ($rel in $DocFiles) {
        Add-ToZipList (Join-Path $ProjectRoot $rel)
    }

    $reportDir = Join-Path $ProjectRoot "docs\worklog\reports"
    if (Test-Path $reportDir) {
        Get-ChildItem -Path $reportDir -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 12 |
            ForEach-Object { Add-ToZipList $_.FullName }
    }

    if ($testResults.DefaultMode -ne "PASS" -or $testResults.EnvSandbox -ne "PASS" -or $testResults.ArgSandbox -ne "PASS" -or $testResults.FormalData -eq "FAIL") {
        $script:StepFailed = $true
    }

    $issueText = if ($issues.Count -gt 0) { ($issues -join "`n") } else { "- (none)" }
    $issueText | Out-File -Encoding UTF8 $IssuesFile
    Add-ToZipList $IssuesFile

    $buildResult = if ($buildCode -eq 0) { "PASS 0 errors" } else { ("FAIL exit " + $buildCode) }
    $httpResult = if ($httpOk) { "PASS 200" } else { "FAIL" }
    $overall = if ($StepFailed) { "PARTIAL/FAIL" } else { "PASS" }

    $summaryText = "# ERP Ver2.9-rc19.1 data sandbox pack summary`n`n" +
        "Time: $Timestamp`n" +
        "Overall: $overall`n" +
        "Build: $buildResult`n" +
        "Default DataDir: $($testResults.DefaultMode)`n" +
        "ERP_DATA_DIR sandbox: $($testResults.EnvSandbox)`n" +
        "Arg data-dir sandbox: $($testResults.ArgSandbox)`n" +
        "Formal Data safety: $($testResults.FormalData)`n" +
        "HTTP 8787: $httpResult`n" +
        "LogDir: $LogDir`n" +
        "Zip: $ZipFile`n`n" +
        "Boundary: no commit/push/tag/sync; no clear/delete formal Data; no 006; no inventory/receivable/payable/finance core changes"
    $summaryText | Out-File -Encoding UTF8 $SummaryFile
    Add-ToZipList $SummaryFile

    if ($FilesToZip.Count -gt 0) {
        Compress-Archive -Path $FilesToZip.ToArray() -DestinationPath $ZipFile -Force
        Write-Host ""
        Write-Host "rc19.1 pack done" -ForegroundColor Green
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
    [Environment]::SetEnvironmentVariable("ERP_DATA_DIR", $null, "User")
    Stop-ErpProcesses
    Write-Host "Press Enter to close" -ForegroundColor Yellow
    [void][System.Console]::ReadLine()
    exit
}

