; Gungun installer (NSIS 3).
;
; Why this exists: the MSIX needs a trusted code-signing certificate before Windows will install
; it, and a self-signed one is not trusted anywhere else. This installer is a plain .exe that
; copies the self-contained build into %LOCALAPPDATA%\Programs\Gungun and makes shortcuts - no
; administrator rights, no certificate import, no Windows App Installer.
;
; Build with scripts/package-installer.ps1, which passes APP_VERSION, APP_SOURCE, OUT_DIR and
; ICON_FILE. Requires nothing but the self-contained publish folder.

Unicode true
!include "MUI2.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.3"
!endif
!ifndef APP_SOURCE
  !define APP_SOURCE "..\build\installer\payload"
!endif
!ifndef OUT_DIR
  !define OUT_DIR "..\dist"
!endif
!ifndef ICON_FILE
  !define ICON_FILE "..\Gungun\Assets\gungun.ico"
!endif

Name "Gungun"
OutFile "${OUT_DIR}\Gungun-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\Gungun"
InstallDirRegKey HKCU "Software\Gungun" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 64
BrandingText "Gungun ${APP_VERSION}"
Icon "${ICON_FILE}"
UninstallIcon "${ICON_FILE}"
XPStyle on

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "Gungun"
VIAddVersionKey "FileDescription" "Gungun setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "MIT"

!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\Gungun.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start Gungun now (it opens hidden - press Alt + N)"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Gungun" SecMain
  ; A running copy holds its DLLs open, so close it before replacing files.
  ExecWait 'taskkill /IM Gungun.exe /F'

  SetOutPath "$INSTDIR"
  File /r "${APP_SOURCE}\*.*"

  CreateShortcut "$DESKTOP\Gungun.lnk" "$INSTDIR\Gungun.exe" "" "$INSTDIR\Assets\gungun.ico" 0
  CreateDirectory "$SMPROGRAMS\Gungun"
  CreateShortcut "$SMPROGRAMS\Gungun\Gungun.lnk" "$INSTDIR\Gungun.exe" "" "$INSTDIR\Assets\gungun.ico" 0
  CreateShortcut "$SMPROGRAMS\Gungun\Uninstall Gungun.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "Software\Gungun" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "DisplayName" "Gungun"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "Publisher" "Gungun"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "DisplayIcon" "$INSTDIR\Assets\gungun.ico"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ExecWait 'taskkill /IM Gungun.exe /F'

  Delete "$DESKTOP\Gungun.lnk"
  Delete "$SMPROGRAMS\Gungun\Gungun.lnk"
  Delete "$SMPROGRAMS\Gungun\Uninstall Gungun.lnk"
  RMDir "$SMPROGRAMS\Gungun"

  ; The app's data lives in %LOCALAPPDATA%\Gungun (tasks, note, settings), not here, so a reinstall
  ; keeps it. Delete that folder by hand to start clean.
  RMDir /r "$INSTDIR"

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Gungun"
  DeleteRegKey HKCU "Software\Gungun"
SectionEnd
