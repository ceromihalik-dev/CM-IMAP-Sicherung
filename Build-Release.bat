@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================================
echo   CM IMAP Sicherung 1.4.0.1 - Release erstellen
echo ============================================================
echo.

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
set "CSC="
set "NET48REF=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll"

REM Visual-Studio-Werkzeuge suchen.
if exist "%VSWHERE%" (
  for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined MSBUILD set "MSBUILD=%%I"
  for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -find MSBuild\**\Bin\Roslyn\csc.exe`) do if not defined CSC set "CSC=%%I"
)

REM MSBuild nur verwenden, wenn das .NET-Framework-4.8-Targeting-Pack vorhanden ist.
REM Dadurch erscheint auf PCs ohne Developer Pack nicht mehr zuerst MSB3644.
if defined MSBUILD if exist "%NET48REF%" (
  echo MSBuild: %MSBUILD%
  echo .NET Framework 4.8 Developer Pack: gefunden
  echo.
  "%MSBUILD%" "%~dp0CM-IMAP-Sicherung.sln" /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU" /m
  if not errorlevel 1 goto :OK
  echo.
  echo [HINWEIS] MSBuild war nicht erfolgreich. Fallback auf direkten C#-Compiler ...
  echo.
) else (
  echo [INFO] .NET Framework 4.8 Developer Pack / Targeting Pack ist nicht installiert.
  echo [INFO] Das ist fuer diesen Build nicht zwingend erforderlich.
  echo [INFO] Es wird direkt mit csc.exe gebaut.
  echo.
)

REM Wenn vorhanden, bevorzugt den Roslyn-Compiler aus Visual Studio nutzen.
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not defined CSC (
  echo [FEHLER] Es wurde kein C#-Compiler gefunden.
  echo Bitte Visual Studio mit der Workload ".NET-Desktopentwicklung" installieren.
  pause
  exit /b 1
)

echo C#-Compiler: %CSC%
echo.

if not exist "%~dp0CMIMAPSicherung\bin\Release" mkdir "%~dp0CMIMAPSicherung\bin\Release"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /utf8output ^
 /win32icon:"%~dp0CMIMAPSicherung\Resources\cm_logo.ico" ^
 /resource:"%~dp0CMIMAPSicherung\Resources\cm_logo.png",CMIMAPSicherung.Resources.cm_logo.png ^
 /resource:"%~dp0CMIMAPSicherung\Resources\cm_logo.ico",CMIMAPSicherung.Resources.cm_logo.ico ^
 /out:"%~dp0CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe" ^
 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll ^
 "%~dp0CMIMAPSicherung\Program.cs" "%~dp0CMIMAPSicherung\Models.cs" "%~dp0CMIMAPSicherung\MainForm.cs" ^
 "%~dp0CMIMAPSicherung\AccountEditForm.cs" "%~dp0CMIMAPSicherung\ImapAutoDetect.cs" "%~dp0CMIMAPSicherung\ImapBackupCompatibility.cs" "%~dp0CMIMAPSicherung\UiTheme.cs" "%~dp0CMIMAPSicherung\RestoreForm.cs" "%~dp0CMIMAPSicherung\MboxViewerForm.cs" "%~dp0CMIMAPSicherung\HistoryForm.cs" "%~dp0CMIMAPSicherung\PasswordForm.cs" ^
 "%~dp0CMIMAPSicherung\SchedulerForm.cs" "%~dp0CMIMAPSicherung\SettingsForm.cs" "%~dp0CMIMAPSicherung\InfoForm.cs" ^
 "%~dp0CMIMAPSicherung\GlobalHistoryForm.cs" "%~dp0CMIMAPSicherung\TaskSchedulerManager.cs" "%~dp0CMIMAPSicherung\BackupTools.cs" ^
 "%~dp0CMIMAPSicherung\AdvancedToolsForm.cs" "%~dp0CMIMAPSicherung\Properties\AssemblyInfo.cs"

if errorlevel 1 (
  echo.
  echo [FEHLER] Build fehlgeschlagen.
  echo Bitte die Compiler-Meldungen senden.
  pause
  exit /b 1
)

:OK
if not exist "%~dp0CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe" (
  echo.
  echo [FEHLER] Der Compiler meldete keinen Fehler, aber die EXE wurde nicht gefunden.
  pause
  exit /b 1
)

echo.
echo ============================================================
echo [OK] Build erfolgreich
echo ============================================================
echo   %~dp0CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe
echo.
pause
exit /b 0
