Unicode true
!define APPNAME "EZ Video/Music Player"
!define VERSION "1.2.1"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\EZVideoMusicPlayer"

Name "${APPNAME} ${VERSION}"
OutFile "out\EZ-Video-Music-Player-Setup-${VERSION}-x64.exe"
InstallDir "$LOCALAPPDATA\Programs\EZ Video-Music Player"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "app.ico"
UninstallIcon "app.ico"
BrandingText "${APPNAME} ${VERSION}"
VIProductVersion "1.2.1.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} Setup"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "GNU GPL v3"

!include "MUI2.nsh"
!include "FileFunc.nsh"
!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"
!define MUI_WELCOMEPAGE_TEXT "This installs ${APPNAME} ${VERSION}.$\r$\n$\r$\nNEW: videos play one at a time, and songs you open are added to the playlist.$\r$\n$\r$\nIf you already have EZ Player, it is updated and your settings and playlists are kept.$\r$\n$\r$\nClick Next to continue."
!define MUI_FINISHPAGE_RUN "$INSTDIR\EZPlayerStart.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start EZ Video/Music Player"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var IsUpdate

Function .onInit
  StrCpy $IsUpdate "0"
  ${GetParameters} $0
  ClearErrors
  ${GetOptions} $0 "/UPDATE" $1
  IfErrors +2
    StrCpy $IsUpdate "1"
  IfFileExists "$INSTDIR\EZPlayerCore.exe" 0 +2
    StrCpy $IsUpdate "1"
  IfFileExists "$INSTDIR\EZPlayer.exe" 0 +2
    StrCpy $IsUpdate "1"
FunctionEnd

Section "!EZ Video/Music Player (required)" SecMain
  SectionIn RO
  ; close the player so its files can be replaced
  nsExec::Exec 'taskkill /IM EZPlayerCore.exe /F'
  nsExec::Exec 'taskkill /IM EZPlayer.exe /F'
  nsExec::Exec 'taskkill /IM EZPlayerStart.exe /F'
  Sleep 800

  SetOutPath "$INSTDIR"
  SetOverwrite ifdiff
  File /r "stage\*.*"
  SetOverwrite on
  File "stage\EZPlayer.exe"
  File "stage\EZPlayerCore.exe"
  File "stage\EZPlayerStart.exe"
  File "stage\README.txt"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS"
  CreateShortcut "$SMPROGRAMS\EZ Video-Music Player.lnk" "$INSTDIR\EZPlayerStart.exe" "" "$INSTDIR\EZPlayerCore.exe" 0
  CreateShortcut "$SMPROGRAMS\EZ Player - How files open.lnk" "$INSTDIR\EZPlayer.exe" "--options" "$INSTDIR\EZPlayerCore.exe" 0

  ; thumbnails in File Explorer
  ExecWait '"$INSTDIR\EZPlayerCore.exe" --register-thumbnails'

  ; make sure "Open with" points at the new smart launcher
  ExecWait '"$INSTDIR\EZPlayer.exe" --launcher-version'

  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "Dr. Szaba"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\EZPlayerCore.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Desktop icon" SecDesktop
  ; on an update, only refresh the icon if it is already there
  StrCmp $IsUpdate "1" 0 +2
    IfFileExists "$DESKTOP\EZ Video-Music Player.lnk" 0 +2
  CreateShortcut "$DESKTOP\EZ Video-Music Player.lnk" "$INSTDIR\EZPlayerStart.exe" "" "$INSTDIR\EZPlayerCore.exe" 0
SectionEnd

Section "Add to $\"Open with$\" for video and music files" SecAssoc
  ; on an update, keep whatever the user had before
  StrCmp $IsUpdate "1" assocdone 0
  ExecWait '"$INSTDIR\EZPlayer.exe" --register-associations'
  assocdone:
  ExecWait '"$INSTDIR\EZPlayer.exe" --launcher-version'
SectionEnd

Section "-finish"
  ; silent update from the built-in updater: start the player again
  IfSilent 0 +2
    Exec '"$INSTDIR\EZPlayerStart.exe"'
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "The player, with videos one at a time and songs to the playlist."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Put an EZ Player icon on your desktop."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAssoc} "Adds EZ Player to the right-click Open with menu. Does not change your default player."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::Exec 'taskkill /IM EZPlayerCore.exe /F'
  nsExec::Exec 'taskkill /IM EZPlayerStart.exe /F'
  ExecWait '"$INSTDIR\EZPlayerCore.exe" --unregister-associations'
  ExecWait '"$INSTDIR\EZPlayerCore.exe" --unregister-thumbnails'
  Delete "$SMPROGRAMS\EZ Video-Music Player.lnk"
  Delete "$SMPROGRAMS\EZ Player - How files open.lnk"
  Delete "$DESKTOP\EZ Video-Music Player.lnk"
  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\EZVideoMusicPlayer\Launcher"
  RMDir /r /REBOOTOK "$INSTDIR"
SectionEnd
