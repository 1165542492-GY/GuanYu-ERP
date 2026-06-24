@echo off
chcp 65001 >nul
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "PUBLISH_DIR=D:\冠誉制造ERP\App"
set "EXE_NAME=冠誉制造ERP.exe"
set "DESKTOP=%USERPROFILE%\Desktop"

echo ========================================
echo   冠誉制造 ERP - Windows 双击启动版发布
echo ========================================
echo.

echo [1/5] 清理旧发布目录...
if exist "%PUBLISH_DIR%" (
    rd /s /q "%PUBLISH_DIR%"
)
if exist "%PUBLISH_DIR%" (
    echo 无法清理目录：%PUBLISH_DIR%
    exit /b 1
)

echo [2/5] 正在发布 win-x64 自包含版本...
pushd "%PROJECT_DIR%"
dotnet publish ERP.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"
set "PUBLISH_EXIT=%ERRORLEVEL%"
popd
if not "%PUBLISH_EXIT%"=="0" (
    echo 发布失败，错误码：%PUBLISH_EXIT%
    exit /b %PUBLISH_EXIT%
)

echo [3/5] 检查发布结果...
if not exist "%PUBLISH_DIR%\%EXE_NAME%" (
    if exist "%PUBLISH_DIR%\ERP.exe" (
        move /y "%PUBLISH_DIR%\ERP.exe" "%PUBLISH_DIR%\%EXE_NAME%" >nul
    )
)
if not exist "%PUBLISH_DIR%\%EXE_NAME%" (
    echo 未找到 %EXE_NAME%，请检查 dotnet publish 输出。
    exit /b 1
)
echo 已生成：%PUBLISH_DIR%\%EXE_NAME%

echo [4/5] 创建桌面快捷方式...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$shell = New-Object -ComObject WScript.Shell; ^
   $lnk = $shell.CreateShortcut('%DESKTOP%\冠誉制造ERP.lnk'); ^
   $lnk.TargetPath = '%PUBLISH_DIR%\%EXE_NAME%'; ^
   $lnk.WorkingDirectory = '%PUBLISH_DIR%'; ^
   $lnk.Description = '冠誉制造 ERP 管理系统'; ^
   $lnk.Save()"
if errorlevel 1 (
    echo 桌面快捷方式创建失败，可手动双击 %PUBLISH_DIR%\%EXE_NAME%
) else (
    echo 已创建桌面快捷方式：%DESKTOP%\冠誉制造ERP.lnk
)

echo [5/5] 打开发布目录...
start "" explorer "%PUBLISH_DIR%"

echo.
echo 发布完成。
echo 主电脑请双击：%PUBLISH_DIR%\%EXE_NAME%
echo 本机访问：http://127.0.0.1:8787
echo 局域网访问：http://主机IP:8787
echo.
pause
endlocal
