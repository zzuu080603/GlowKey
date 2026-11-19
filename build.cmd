@echo off
rem ============================================================
rem  GlowKey build script
rem  Uses the in-box .NET Framework C# compiler (csc.exe).
rem  No SDK or developer tools required.
rem  Output: dist\GlowKey.exe (copy it anywhere, it just runs)
rem ============================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found
    exit /b 1
)

if not exist dist mkdir dist

"%CSC%" /nologo /target:winexe /optimize+ /warn:4 ^
    /out:dist\GlowKey.exe ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    /r:System.Management.dll ^
    /r:System.Web.Extensions.dll ^
    src\Program.cs src\AppSettings.cs src\NativeMethods.cs src\BrightnessController.cs ^
    src\HotkeyManager.cs src\OsdForm.cs src\SliderFlyout.cs src\SettingsForm.cs src\TrayContext.cs

if errorlevel 1 (
    echo [ERROR] build failed
    exit /b 1
)

echo [OK] dist\GlowKey.exe built
endlocal
