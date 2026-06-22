#Requires -Version 5.1
$ErrorActionPreference = "Stop"

function New-UnicodeString {
    param([int[]]$CodePoints)
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Get-PackageNamePrefix {
    return (New-UnicodeString @(0x51A0, 0x8A89)) + "ERP" + (New-UnicodeString @(0x7BA1, 0x7406, 0x7CFB, 0x7EDF))
}

function Get-FixedOutputDirectory {
    $documents = [Environment]::GetFolderPath("MyDocuments")
    $projectFolder = (New-UnicodeString @(0x51A0, 0x8A89)) + "ERP" + (New-UnicodeString @(0x9879, 0x76EE))
    $packageFolder = "02-" + (New-UnicodeString @(0x5B89, 0x88C5, 0x5305))
    return Join-Path (Join-Path $documents $projectFolder) $packageFolder
}

function Resolve-OutputDirectory {
    param([string]$ScriptDir)

    $fixedPath = Get-FixedOutputDirectory
    if (Test-Path (Split-Path $fixedPath -Parent)) {
        return $fixedPath
    }

    $innerRoot = Split-Path $ScriptDir -Parent
    $erpRoot = Split-Path $innerRoot -Parent
    $fallback = Get-ChildItem $erpRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "02-*" } |
        Select-Object -First 1 -ExpandProperty FullName

    if ($fallback) {
        Write-Host "Fixed output path unavailable, using fallback: $fallback" -ForegroundColor Yellow
        return $fallback
    }

    throw "Output directory not found. Expected: $fixedPath"
}

function Get-LatestPackageVersion {
    param(
        [string]$OutputDir,
        [string]$Prefix
    )

    $maxMajor = 0
    $maxMinor = 0
    $found = $false
    $zipPattern = [regex]::Escape($Prefix) + "Ver(\d+)\.(\d+)\.zip$"

    if (Test-Path $OutputDir) {
        foreach ($file in Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue) {
            if ($file.Name -match $zipPattern) {
                $found = $true
                $major = [int]$Matches[1]
                $minor = [int]$Matches[2]
                if ($major -gt $maxMajor -or ($major -eq $maxMajor -and $minor -gt $maxMinor)) {
                    $maxMajor = $major
                    $maxMinor = $minor
                }
            }
        }
    }

    if (-not $found) {
        return @{
            Major = 1
            Minor = 7
            Found = $false
        }
    }

    return @{
        Major = $maxMajor
        Minor = $maxMinor
        Found = $true
    }
}

function Get-NextPackageVersion {
    param(
        [int]$Major,
        [int]$Minor
    )

    if ($Minor -lt 9) {
        return @{
            Major = $Major
            Minor = $Minor + 1
        }
    }

    return @{
        Major = $Major + 1
        Minor = 0
    }
}

function Format-VersionLabel {
    param(
        [int]$Major,
        [int]$Minor
    )
    return "Ver$Major.$Minor"
}

function Test-PackageItemName {
    param(
        [string]$Name,
        [string]$Prefix
    )

    $pattern = "^" + [regex]::Escape($Prefix) + "Ver\d+\.\d+(\.zip)?$"
    return $Name -match $pattern
}

function Assert-PathUnderDirectory {
    param(
        [string]$Path,
        [string]$RootDir
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($RootDir)
    $rootWithSep = if ($fullRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) { $fullRoot } else { $fullRoot + [System.IO.Path]::DirectorySeparatorChar }

    if ($fullPath -eq $fullRoot) {
        return
    }

    if (-not $fullPath.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Safety check failed. Path is outside output directory: $fullPath"
    }
}

function Send-PathToRecycleBin {
    param(
        [string]$Path,
        [string]$OutputDir
    )

    if (-not (Test-Path $Path)) {
        return
    }

    Assert-PathUnderDirectory -Path $Path -RootDir $OutputDir

    Add-Type -AssemblyName Microsoft.VisualBasic

    if ([System.IO.Directory]::Exists($Path)) {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory(
            $Path,
            [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
            [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
        )
    }
    else {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
            $Path,
            [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
            [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin
        )
    }
}

function Move-OldPackagesToRecycleBin {
    param(
        [string]$OutputDir,
        [string]$Prefix,
        [string]$CurrentPackageRootName
    )

    $recycled = @()

    if (-not (Test-Path $OutputDir)) {
        return $recycled
    }

    foreach ($file in Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue) {
        if ($file.Name -eq ($CurrentPackageRootName + ".zip")) {
            continue
        }

        if (-not (Test-PackageItemName -Name $file.Name -Prefix $Prefix)) {
            continue
        }

        try {
            Send-PathToRecycleBin -Path $file.FullName -OutputDir $OutputDir
            $recycled += $file.Name
        }
        catch {
            throw "Failed to move file to Recycle Bin: $($file.FullName)`n$($_.Exception.Message)"
        }
    }

    foreach ($dir in Get-ChildItem $OutputDir -Directory -ErrorAction SilentlyContinue) {
        if ($dir.Name -eq $CurrentPackageRootName) {
            continue
        }

        if (-not (Test-PackageItemName -Name $dir.Name -Prefix $Prefix)) {
            continue
        }

        try {
            Send-PathToRecycleBin -Path $dir.FullName -OutputDir $OutputDir
            $recycled += ($dir.Name + "\")
        }
        catch {
            throw "Failed to move folder to Recycle Bin: $($dir.FullName)`n$($_.Exception.Message)"
        }
    }

    return $recycled
}

function Find-MsBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($msbuild -and (Test-Path $msbuild)) { return $msbuild }
    }

    $candidates = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }

    throw "MSBuild not found. Install Visual Studio, Build Tools, or .NET Framework 4.x MSBuild."
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "GuanYuERP.csproj"
$ExePath = Join-Path $ScriptDir "bin\Release\GuanYuERP.exe"
$StagingDir = Join-Path $ScriptDir "bin\package-staging"
$Prefix = Get-PackageNamePrefix
$OutputDir = Resolve-OutputDirectory -ScriptDir $ScriptDir
$InstallGuide = Get-ChildItem $ScriptDir -File | Where-Object { $_.Extension -eq ".txt" -and $_.Name -ne "LICENSE.txt" } | Select-Object -First 1

$latest = Get-LatestPackageVersion -OutputDir $OutputDir -Prefix $Prefix
$next = Get-NextPackageVersion -Major $latest.Major -Minor $latest.Minor
$versionLabel = Format-VersionLabel -Major $next.Major -Minor $next.Minor
$PackageRootName = $Prefix + $versionLabel
$ZipPath = Join-Path $OutputDir ($PackageRootName + ".zip")
$ExtractDir = Join-Path $OutputDir $PackageRootName

Write-Host "== GuanYu ERP Build ==" -ForegroundColor Cyan
Write-Host "Source: $ScriptDir"
Write-Host "Output: $OutputDir"
Write-Host "Latest detected: $(Format-VersionLabel -Major $latest.Major -Minor $latest.Minor)"
Write-Host "Next version: $versionLabel"

if (-not (Test-Path $ProjectFile)) {
    throw "Project file not found: $ProjectFile"
}

if (Test-Path $ZipPath) {
    throw "Target package already exists. Refusing to overwrite: $ZipPath"
}

if (Test-Path $ExtractDir) {
    throw "Target extract folder already exists. Refusing to overwrite: $ExtractDir"
}

$msbuild = Find-MsBuild
Write-Host "MSBuild: $msbuild"

Push-Location $ScriptDir
try {
    & $msbuild $ProjectFile /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code: $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $ExePath)) {
    throw "Executable not found after build: $ExePath"
}

Write-Host "Build OK: $ExePath" -ForegroundColor Green

if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}

$PackageDir = Join-Path $StagingDir $PackageRootName
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

Copy-Item $ExePath (Join-Path $PackageDir "GuanYuERP.exe")
Copy-Item (Join-Path $ScriptDir "VERSION.md") (Join-Path $PackageDir "VERSION.md")

if ($InstallGuide) {
    Copy-Item $InstallGuide.FullName (Join-Path $PackageDir $InstallGuide.Name)
}
else {
    throw "Install guide txt not found in source directory."
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PackageDir, $ZipPath)

if (-not (Test-Path $ZipPath)) {
    throw "Package zip was not created: $ZipPath"
}

$zipInfo = Get-Item $ZipPath
if ($zipInfo.Length -le 0) {
    throw "Package zip is empty: $ZipPath"
}

Write-Host "Package zip created: $ZipPath" -ForegroundColor Green

[System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $ExtractDir)

if (-not (Test-Path $ExtractDir)) {
    throw "Package folder was not extracted: $ExtractDir"
}

Write-Host "Package folder extracted: $ExtractDir" -ForegroundColor Green

$recycledItems = Move-OldPackagesToRecycleBin -OutputDir $OutputDir -Prefix $Prefix -CurrentPackageRootName $PackageRootName

Remove-Item $StagingDir -Recurse -Force

Write-Host ""
Write-Host "== Build Result ==" -ForegroundColor Cyan
Write-Host ("Version: {0}" -f $versionLabel)
Write-Host ("Package: {0}" -f $ZipPath)
Write-Host ("Folder:  {0}" -f $ExtractDir)
if ($recycledItems.Count -gt 0) {
    Write-Host ("Recycled: {0}" -f ($recycledItems -join ", "))
}
else {
    Write-Host "Recycled: (none)"
}
Write-Host "Status: SUCCESS" -ForegroundColor Green
Write-Host "Contents:"
Write-Host "  - GuanYuERP.exe"
Write-Host "  - VERSION.md"
Write-Host "  - $($InstallGuide.Name)"
