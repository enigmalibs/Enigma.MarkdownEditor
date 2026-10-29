@echo off
rem Publishes Enigma Markdown Editor (Enigma.MarkdownEditor) for Windows x64:
rem
rem     dotnet publish <Desktop project> -c Release -r win-x64 -p:PublishReadyToRun=true
rem
rem The result lands in src\Enigma.MarkdownEditor.Desktop\bin\Release\net10.0\win-x64\publish\.
rem The same command as publish.ps1 beside it, for a machine whose PowerShell execution policy
rem refuses local scripts. Release artifacts come from build\publish.ps1 (see docs\RELEASE.md).
setlocal

rem Resolved from this script rather than the current directory, so it runs from anywhere.
set "PROJECT=%~dp0..\..\src\Enigma.MarkdownEditor.Desktop\Enigma.MarkdownEditor.Desktop.csproj"

dotnet publish "%PROJECT%" -c Release -r win-x64 -p:PublishReadyToRun=true
exit /b %ERRORLEVEL%
