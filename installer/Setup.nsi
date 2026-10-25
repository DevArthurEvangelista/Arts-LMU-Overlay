Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

Name "Arts LMU OVERLAY"
OutFile "../Instalar-Arts-LMU-Overlay-0.12.4.exe"
InstallDir "$LOCALAPPDATA\Programs\ArthurLMUOverlay"
InstallDirRegKey HKCU "Software\ArthurLMUOverlay" "InstallDirectory"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Arts LMU OVERLAY"
VIProductVersion "0.12.4.0"
VIAddVersionKey /LANG=1046 "ProductName" "Arts LMU OVERLAY"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalação local do Arts LMU OVERLAY para Windows"
VIAddVersionKey /LANG=1046 "FileVersion" "0.12.4"
VIAddVersionKey /LANG=1046 "LegalCopyright" "Arts LMU OVERLAY • Arthur"

!define MUI_ICON "../assets/App.ico"
!define MUI_UNICON "../assets/App.ico"
!define MUI_WELCOMEPAGE_TITLE "Instalar o Arts LMU OVERLAY"
!define MUI_WELCOMEPAGE_TEXT "Este instalador coloca o aplicativo no seu Windows e cria atalhos na Área de Trabalho e no menu Iniciar.$\r$\n$\r$\nInstalação para seu usuário, sem administrador. Seus ajustes são preservados. Os cards ficam ocultos no menu, na garagem e durante pausas.$\r$\n$\r$\nDepois de instalar, abra o Le Mans Ultimate em janela sem bordas e entre na pista."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "Arts LMU OVERLAY instalado"
!define MUI_FINISHPAGE_TEXT "Abra o aplicativo pelo atalho Arts LMU OVERLAY na Área de Trabalho ou no menu Iniciar.$\r$\n$\r$\nFechar o painel mantém os overlays funcionando. Para encerrar, use Encerrar o app no painel ou no ícone junto ao relógio do Windows."
!define MUI_FINISHPAGE_RUN "$INSTDIR\LMUOverlay.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir Arts LMU OVERLAY agora"
!insertmacro MUI_PAGE_FINISH
!define MUI_UNCONFIRMPAGE_TEXT_TOP "O aplicativo e seus atalhos serão removidos. Seus ajustes, diagnósticos e histórico de voltas serão preservados."
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

!macro CheckRunning PREFIX
Function ${PREFIX}CheckRunning
  again:
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\ArthurLMUOverlay") p .r0'
  StrCmp $0 0 ready
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_RETRYCANCEL|MB_ICONINFORMATION "O overlay ainda está aberto.$\r$\n$\r$\nEscolha Encerrar o app no painel ou no ícone junto ao relógio.$\r$\n$\r$\nSe o menu da versão antiga desaparecer, abra o Gerenciador de Tarefas e encerre somente LMUOverlay.exe. Depois, clique em Repetir." IDRETRY again
  Abort
  ready:
FunctionEnd
!macroend
!insertmacro CheckRunning ""
!insertmacro CheckRunning "un."

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Este aplicativo precisa do Windows de 64 bits."
    Quit
  ${EndIf}
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 528040
    MessageBox MB_OK|MB_ICONSTOP "Instale o .NET Framework 4.8 pelo Windows Update ou pela Microsoft e execute este instalador novamente."
    Quit
  ${EndIf}
  SetShellVarContext current
  Call CheckRunning
FunctionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext current
  Call un.CheckRunning
FunctionEnd

Section "Arts LMU OVERLAY" SEC_MAIN
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File "../LMUOverlay.exe"
  File "../LMUOverlay.exe.config"
  File "../assets/SectorRuntime-1.0.0.zip"
  File "../LEIA-ME.txt"
  SetOutPath "$INSTDIR\Licencas"
  File /oname=SharedMemory-MIT.txt "../third_party/License.txt"
  File /oname=Bandeiras-MIT.txt "../third_party/flag-icons-LICENSE.txt"
  File /oname=Instalador-NSIS.txt "../third_party/NSIS-LICENSE.txt"
  File /oname=SimpleIcons-CC0.txt "../third_party/SimpleIcons-LICENSE.md"
  File /oname=Marcas-dos-carros.txt "../third_party/CarLogos-NOTICE.txt"
  File /oname=Sector-MIT.txt "../third_party/Sector-MIT.txt"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Desinstalar-LMU-Overlay.exe"
  Delete "$DESKTOP\LMU Overlay.lnk"
  Delete "$SMPROGRAMS\LMU Overlay.lnk"
  CreateShortcut "$DESKTOP\Arts LMU OVERLAY.lnk" "$INSTDIR\LMUOverlay.exe" "" "$INSTDIR\LMUOverlay.exe" 0
  CreateShortcut "$SMPROGRAMS\Arts LMU OVERLAY.lnk" "$INSTDIR\LMUOverlay.exe" "" "$INSTDIR\LMUOverlay.exe" 0
  WriteRegStr HKCU "Software\ArthurLMUOverlay" "InstallDirectory" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "DisplayName" "Arts LMU OVERLAY"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "DisplayVersion" "0.12.4"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "Publisher" "Arthur"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "DisplayIcon" "$INSTDIR\LMUOverlay.exe,0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "UninstallString" '"$INSTDIR\Desinstalar-LMU-Overlay.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay" "EstimatedSize" $0
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\LMU Overlay.lnk"
  Delete "$SMPROGRAMS\LMU Overlay.lnk"
  Delete "$DESKTOP\Arts LMU OVERLAY.lnk"
  Delete "$SMPROGRAMS\Arts LMU OVERLAY.lnk"
  Delete "$INSTDIR\LMUOverlay.exe"
  Delete "$INSTDIR\LMUOverlay.exe.config"
  Delete "$INSTDIR\SectorRuntime-1.0.0.zip"
  Delete "$INSTDIR\LEIA-ME.txt"
  Delete "$INSTDIR\Licencas\SharedMemory-MIT.txt"
  Delete "$INSTDIR\Licencas\Bandeiras-MIT.txt"
  Delete "$INSTDIR\Licencas\Instalador-NSIS.txt"
  Delete "$INSTDIR\Licencas\SimpleIcons-CC0.txt"
  Delete "$INSTDIR\Licencas\Marcas-dos-carros.txt"
  Delete "$INSTDIR\Licencas\Sector-MIT.txt"
  RMDir "$INSTDIR\Licencas"
  Delete "$INSTDIR\Desinstalar-LMU-Overlay.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ArthurLMUOverlay"
  DeleteRegKey HKCU "Software\ArthurLMUOverlay"
SectionEnd
