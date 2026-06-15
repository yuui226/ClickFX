@echo off

echo [1/3] Killing ClickFX...
taskkill /f /im ClickFX.exe >nul 2>&1

echo [2/3] Compiling...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:ClickFX.exe /win32icon:icon.ico /reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll Program.cs ClickFX.cs Config.cs Effects.cs

if errorlevel 1 (
    echo.
    echo Build FAILED!
    pause
    exit /b 1
)

echo [3/3] Starting ClickFX...
start "" "%~dp0ClickFX.exe"

echo.
echo Build OK - ClickFX is running
exit 0
