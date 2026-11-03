@echo off
rem Builds the Windows installer of Enigma Markdown Editor, artifacts\Enigma.MarkdownEditor.X.Y.Z.msi,
rem from a cmd prompt: it runs buildmsi.ps1, beside this script, with no PowerShell execution policy in
rem the way.
rem
rem buildmsi.ps1 cleans the repository (git clean -dxf: every file git does not track is deleted),
rem publishes the release for win-x64, self-contained, with build\publish.ps1, and builds the MSI with
rem Enigma.Msi. Its help says the rest:
rem powershell -NoProfile -Command "Get-Help .\packaging\windows\buildmsi.ps1 -Full"

setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0buildmsi.ps1"

rem buildmsi.ps1's exit code is the script's, so a failed build fails the script.
exit /b %ERRORLEVEL%
