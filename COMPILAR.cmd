@echo off
setlocal
cd /d "%~dp0"
set "LMU_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%LMU_CSC%" (
  echo Compilador do .NET Framework nao encontrado. Requer Windows x64 com .NET Framework 4.8.
  pause
  exit /b 1
)
"%LMU_CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32icon:assets\App.ico /win32manifest:src\app.manifest /out:LMUOverlay.exe /resource:assets\Flags.zip,Flags.zip /resource:assets\BrandLogo.png,BrandLogo.png /resource:assets\CarLogos.zip,CarLogos.zip /r:System.IO.Compression.dll /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll src\*.cs
if errorlevel 1 (
  echo A compilacao falhou.
  pause
  exit /b 1
)
echo LMUOverlay.exe compilado.
pause
