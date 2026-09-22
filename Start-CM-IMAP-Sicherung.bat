@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0CMIMAPSicherung\bin\Release\CM IMAP Sicherung.exe"
if not exist "%EXE%" (
  call "%~dp0Build-Release.bat"
  if errorlevel 1 exit /b 1
)
start "" "%EXE%"
