#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes Enigma Markdown Editor (Enigma.MarkdownEditor) for Windows x64.

.DESCRIPTION
    Runs, from wherever it is started:

        dotnet publish .\src\Enigma.MarkdownEditor.Desktop\Enigma.MarkdownEditor.Desktop.csproj -c Release -r win-x64 -p:PublishReadyToRun=true

    A Release build for win-x64, compiled ReadyToRun so it starts faster, which runs on an installed
    .NET 10 runtime. The result lands in

        src\Enigma.MarkdownEditor.Desktop\bin\Release\net10.0\win-x64\publish\

    and dotnet's last line names that folder. Start Enigma.MarkdownEditor.Desktop.exe in it.

    This is the quick, local publish. The release artifacts - the self-contained folders the MSI and
    the Linux tarball are built from - come from build\publish.ps1 instead (see docs\RELEASE.md).

.EXAMPLE
    .\packaging\windows\publish.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File packaging\windows\publish.ps1

    Windows PowerShell's default policy refuses to run local scripts; this runs this one without
    changing the policy. packaging\windows\publish.bat does the same without PowerShell at all.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolved from this script rather than the current directory, so it runs from anywhere.
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..\src\Enigma.MarkdownEditor.Desktop\Enigma.MarkdownEditor.Desktop.csproj')).Path

dotnet publish $project -c Release -r win-x64 -p:PublishReadyToRun=true

# A native command's failure is not a PowerShell error: pass its exit code on, so a failed publish
# fails the script.
exit $LASTEXITCODE
