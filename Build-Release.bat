@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
set "NOPAUSE="
if /i "%~1"=="/ci" set "NOPAUSE=1"
if defined CI set "NOPAUSE=1"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -find MSBuild\**\Bin\Roslyn\csc.exe`) do set "CSC=%%I"
if not exist "%CSC%" goto :FAIL
if not exist "CMIMAPSicherung\bin\Release" mkdir "CMIMAPSicherung\bin\Release"
"%CSC%" /nologo /target:winexe /main:CMIMAPSicherung.Program /platform:x64 /optimize+ /utf8output /codepage:65001 ^
 /win32icon:"CMIMAPSicherung\Resources\cm_logo.ico" ^
 /resource:"CMIMAPSicherung\Resources\cm_logo.png",CMIMAPSicherung.Resources.cm_logo.png ^
 /resource:"CMIMAPSicherung\Resources\emoji-icons.png",CMIMAPSicherung.Resources.emoji-icons.png ^
 /resource:"CMIMAPSicherung\Resources\cm_logo.ico",CMIMAPSicherung.Resources.cm_logo.ico ^
 /resource:"CMIMAPSicherung\Archive\archive_engine.py",CMIMAPSicherung.Archive.archive_engine.py ^
 /out:"CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe" ^
 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll ^
 CMIMAPSicherung\*.cs CMIMAPSicherung\Properties\AssemblyInfo.cs
if errorlevel 1 goto :FAIL
"%CSC%" /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 /out:"CMIMAPSicherung\bin\Release\py.exe" /reference:System.dll /reference:System.Core.dll tools\PythonLauncher.cs
if errorlevel 1 goto :FAIL
if not exist "CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe" goto :FAIL
echo [OK] CM IMAP Sicherung 1.4.0.5 erfolgreich kompiliert.
echo Fuer das portable Windows-Paket: tools\Package-Windows.ps1 verwenden.
if not defined NOPAUSE pause
exit /b 0
:FAIL
echo [FEHLER] Build fehlgeschlagen. Kein Release freigegeben.
if not defined NOPAUSE pause
exit /b 1
