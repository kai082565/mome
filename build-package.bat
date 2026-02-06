@echo off
chcp 65001 >nul
echo ========================================
echo   點燈系統 - 打包發佈工具
echo ========================================
echo.

:: 設定變數
set PROJECT_DIR=TempleLampSystem
set OUTPUT_DIR=publish
set RUNTIME=win-x64

:: 清理舊的發佈目錄
echo [1/4] 清理舊的發佈目錄...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

:: 還原套件
echo [2/4] 還原 NuGet 套件...
dotnet restore "%PROJECT_DIR%\TempleLampSystem.csproj"
if errorlevel 1 (
    echo 錯誤：套件還原失敗！
    pause
    exit /b 1
)

:: 發佈專案（自包含模式，單一執行檔）
echo [3/4] 編譯並發佈專案...
dotnet publish "%PROJECT_DIR%\TempleLampSystem.csproj" ^
    -c Release ^
    -r %RUNTIME% ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUTPUT_DIR%"

if errorlevel 1 (
    echo 錯誤：發佈失敗！
    pause
    exit /b 1
)

:: 複製設定檔
echo [4/4] 複製設定檔...
copy "%PROJECT_DIR%\appsettings.json" "%OUTPUT_DIR%\" >nul

:: 取得日期作為檔名
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set DATESTAMP=%datetime:~0,8%

:: 設定 ZIP 檔名
set ZIP_NAME=點燈系統_%DATESTAMP%.zip

:: 刪除舊的 ZIP
if exist "%ZIP_NAME%" del "%ZIP_NAME%"

:: 建立 ZIP（使用 PowerShell）
echo [5/5] 建立 ZIP 壓縮檔...
powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%\*' -DestinationPath '%ZIP_NAME%' -Force"

if errorlevel 1 (
    echo 錯誤：建立 ZIP 失敗！
    pause
    exit /b 1
)

echo.
echo ========================================
echo   打包完成！
echo ========================================
echo.
echo ZIP 檔案: %CD%\%ZIP_NAME%
echo.
echo 檔案大小:
for %%A in ("%ZIP_NAME%") do echo %%~zA bytes (約 %%~zA bytes)
echo.
echo 請將此 ZIP 檔傳給其他電腦下載解壓縮即可使用
echo.
pause
