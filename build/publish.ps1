<#
.SYNOPSIS
    Publishes Enigma Markdown Editor self-contained for one runtime identifier.

.DESCRIPTION
    Produces artifacts/publish/<rid> - a plain publish folder, self-contained and untrimmed, which
    is what both release artifacts are built from: the WixSharp MSI harvests the win-x64 folder,
    and packaging/linux/pack-linux.ps1 tars the linux-x64 one.

    Self-contained means the .NET runtime ships inside the folder, so neither artifact asks the
    user to install anything first. Trimming and single-file are deliberately off: trimming an
    Avalonia app needs a reflection audit this release has not done, and a single-file bundle
    would leave the MSI nothing to harvest.

    Runs on Windows PowerShell 5.1 and on PowerShell 7+ (Windows or Linux); see build/Common.ps1
    for the language subset that keeps that true. Either RID can be published from either OS.

.PARAMETER Rid
    The runtime identifier to publish: win-x64 or linux-x64.

.PARAMETER Version
    Version to stamp into the build. Defaults to <Version> in the Desktop csproj.

.PARAMETER NoClean
    Publish over the existing output instead of deleting it first. Off by default: a stale file
    left behind by an earlier publish would be harvested into the installer as if it belonged.

.EXAMPLE
    ./build/publish.ps1 -Rid win-x64

.EXAMPLE
    pwsh ./build/publish.ps1 -Rid linux-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'linux-x64')]
    [string] $Rid,

    [string] $Version,

    [switch] $NoClean
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common.ps1')

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found on PATH. Install the .NET 10 SDK (see global.json) and retry.'
}

$ctx = Get-BuildContext -Version $Version
$outDir = Join-Path $ctx.PublishRoot $Rid

# The apphost carries the platform's extension, which follows the RID being published and not the
# OS doing the publishing - a linux-x64 publish from Windows still produces an extensionless host.
$hostName = $ctx.AssemblyName
if ($Rid -eq 'win-x64') {
    $hostName = $hostName + '.exe'
}

Write-Host ''
Write-Host ("{0} {1} - publishing {2}" -f $ctx.ProductName, $ctx.Version, $Rid) -ForegroundColor Cyan
Write-Host ("  project : {0}" -f $ctx.ProjectPath)
Write-Host ("  output  : {0}" -f $outDir)
Write-Host ''

if (Test-Path -LiteralPath $outDir) {
    if ($NoClean) {
        Write-Host '  (-NoClean) publishing over the existing output.'
    }
    else {
        Write-Host '  cleaning previous output...'
        Remove-Item -LiteralPath $outDir -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$arguments = @(
    'publish', $ctx.ProjectPath,
    '-c', 'Release',
    '-r', $Rid,
    '--self-contained', 'true',
    '-o', $outDir,
    "-p:Version=$($ctx.Version)",
    '--nologo'
)

Write-Host ("  dotnet {0}" -f ($arguments -join ' '))
Write-Host ''

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# A publish that "succeeded" without producing an apphost means the RID or the output path is not
# what this script thinks it is; failing here beats shipping an installer around a missing exe.
$hostPath = Join-Path $outDir $hostName
if (-not (Test-Path -LiteralPath $hostPath)) {
    throw "Publish finished but $hostName is missing from $outDir."
}

$files = @(Get-ChildItem -LiteralPath $outDir -Recurse -File)
$bytes = ($files | Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host ("Published {0} {1} ({2})" -f $ctx.ProductName, $ctx.Version, $Rid) -ForegroundColor Green
Write-Host ("  folder    : {0}" -f $outDir)
Write-Host ("  entry     : {0}" -f $hostName)
Write-Host ("  files     : {0}" -f $files.Count)
Write-Host ("  size      : {0:N1} MB" -f ($bytes / 1MB))
Write-Host ''
