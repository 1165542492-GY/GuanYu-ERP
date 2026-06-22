#Requires -Version 5.1
$ErrorActionPreference = "Stop"

function New-UnicodeString {
    param([int[]]$CodePoints)
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptDir "GuanYuERP.csproj"
$ExePath = Join-Path $ScriptDir "bin\Release\GuanYuERP.exe"
$StagingDir = Join-Path $ScriptDir "bin\package-staging"

# GuanYu ERP package folder name: GuanYuERP Ver1.6 (Unicode)
$PackageRootName = (New-UnicodeString @(0x51A0, 0x8A89)) + "ERP" + (New-UnicodeString @(0x7BA1, 0x7406, 0x7CFB, 0x7EDF)) + "Ver1.7"

$InnerRoot = Split-Path $ScriptDir -Parent
$ErpRoot = Split-Path $InnerRoot -Parent
$OutputDir = Get-ChildItem $ErpRoot -Directory | Where-Object { $_.Name -like "02-*" } | Select-Object -First 1 -ExpandProperty FullName
if (-not $OutputDir) {
    throw "Output directory not found. Expected a sibling folder named 02-* under: $ErpRoot"
}

$ZipPath = Join-Path $OutputDir ($PackageRootName + ".zip")
$InstallGuide = Get-ChildItem $ScriptDir -File | Where-Object { $_.Extension -eq ".txt" -and $_.Name -ne "LICENSE.txt" } | Select-Object -First 1

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

Write-Host "== GuanYu ERP Build ==" -ForegroundColor Cyan
Write-Host "Source: $ScriptDir"

if (-not (Test-Path $ProjectFile)) {
    throw "Project file not found: $ProjectFile"
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

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PackageDir, $ZipPath)

Remove-Item $StagingDir -Recurse -Force

Write-Host ""
Write-Host "Package created:" -ForegroundColor Green
Write-Host $ZipPath
Write-Host "Contents:"
Write-Host "  - GuanYuERP.exe"
Write-Host "  - VERSION.md"
Write-Host "  - $($InstallGuide.Name)"
