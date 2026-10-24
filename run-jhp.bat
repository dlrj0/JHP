@echo off
rem Launcher for JHP. Publishes the latest source (fast when nothing changed), then starts JHP.exe.
rem   run-jhp.bat        publish, then start
rem   run-jhp.bat fast   skip publishing, just start the last published build
rem Requires the .NET 10 SDK to publish, or the .NET 10 Desktop Runtime (x64) to run a published build.
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%app\JHP.exe"

if /i "%~1"=="fast" goto :start

echo Publishing JHP...
dotnet publish "%ROOT%JHP\JHP.csproj" -p:PublishProfile=win-x64 -nologo -v q
if errorlevel 1 (
  if exist "%EXE%" (
    echo Publish failed ^(JHP may be running from this folder^). Starting the last published build.
  ) else (
    echo Publish failed and there is no published build yet.
    pause
    exit /b 1
  )
)

:start
if not exist "%EXE%" (
  echo JHP.exe not found: %EXE%
  echo Run this file without the "fast" argument first.
  pause
  exit /b 1
)
start "" "%EXE%"
endlocal
