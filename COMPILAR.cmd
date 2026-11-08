@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Instale o SDK .NET com suporte ao .NET Framework 4.8.
 pause
 exit /b 1
)
dotnet build LMUOverlay.csproj -c Release
if errorlevel 1 (pause & exit /b 1)
copy /y "bin\Release\net48\LMUOverlay.exe" "LMUOverlay.exe" >nul
copy /y "bin\Release\net48\NAudio*.dll" . >nul
echo Compilacao concluida. Para instalar, use NSIS em installer\Setup.nsi.
pause
