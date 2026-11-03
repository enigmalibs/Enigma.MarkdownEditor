#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the Windows installer of Enigma Markdown Editor: artifacts\Enigma.MarkdownEditor.X.Y.Z.msi.

.DESCRIPTION
    Runs, from wherever it is started, on the repository it sits in:

        1. git clean -dxf      deletes every file git does not track, ignored ones included
                               (bin\, obj\, artifacts\, .vs\, and any new file not yet added)
        2. build\publish.ps1   dotnet publish of src\Enigma.MarkdownEditor.Desktop for win-x64,
                               self-contained, into artifacts\publish\win-x64
        3. Enigma.Msi          packages that folder into the MSI, uncompressed for speed

    The publish is the release one, self-contained: the .NET runtime ships inside the MSI, so the
    installed application needs nothing else on the machine. That is what every MSI of Enigma Markdown
    Editor has shipped.

    X.Y.Z is the <Version> of src\Enigma.MarkdownEditor.Desktop\Enigma.MarkdownEditor.Desktop.csproj,
    which src\Enigma.MarkdownEditor.Core\Enigma.MarkdownEditor.Core.csproj must repeat. The MSI installs
    "Enigma Markdown Editor" for all users in %ProgramFiles%\Enigma Markdown Editor, with a shortcut on
    the desktop and one in the program menu. It upgrades a version installed from an earlier MSI: every
    MSI carries the same upgrade code. The Enigma.Msi profile it was built from is kept beside it, as
    Enigma.MarkdownEditor.X.Y.Z.msipkg.json.

    git, the .NET 10 SDK and the WiX CLI (dotnet tool install --global wix) must be on the PATH. They
    are checked before anything is cleaned. Enigma.Msi's worker is in Enigma.Msi.Worker\, next to this
    script.

.EXAMPLE
    .\packaging\windows\buildmsi.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File packaging\windows\buildmsi.ps1

    Windows PowerShell's default policy refuses to run local scripts; this runs this one without
    changing the policy. buildmsi.bat, beside this script, does the same from a cmd prompt.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The product line's identity in Windows Installer, the same in every MSI of Enigma Markdown Editor
# since 1.0.0: it is what makes a new version replace the installed one instead of installing beside
# it. Never change it.
$UpgradeCode = 'a7b550b9-1338-458f-bf2b-66dcf1d25380'
$ProductName = 'Enigma Markdown Editor'
# Josue Clement with its two e-acutes: this file is ASCII because Windows PowerShell 5.1 reads a script
# without a byte-order mark in the ANSI code page.
$Manufacturer = "Josu$([char]0x00E9) Cl$([char]0x00E9)ment"
$Assembly = 'Enigma.MarkdownEditor.Desktop'

function Stop-Build([string] $Reason, [int] $ExitCode = 1) {
    [Console]::Error.WriteLine("buildmsi: $Reason")
    exit $ExitCode
}

function Write-Step([string] $Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

# The <Version> of a csproj, or an empty string when it has none. XmlDocument.Load honours the file's
# UTF-8 encoding, which Get-Content would read as ANSI.
function Get-ProjectVersion([string] $Project) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($Project)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')

    if ($null -eq $node) {
        return ''
    }

    return $node.InnerText.Trim()
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Resolved from this script rather than the current directory, so it runs from anywhere.
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$worker = Join-Path $PSScriptRoot 'Enigma.Msi.Worker\Enigma.Msi.Worker.exe'
$publishScript = Join-Path $root 'build\publish.ps1'
$artifacts = Join-Path $root 'artifacts'
# Where build\publish.ps1 puts a win-x64 publish.
$publish = Join-Path $artifacts 'publish\win-x64'

# Everything that can be missing is checked before git clean deletes anything.
$missing = @(
    @{ Command = 'git'; Hint = 'install Git for Windows' },
    @{ Command = 'dotnet'; Hint = 'install the .NET 10 SDK' },
    @{ Command = 'wix'; Hint = 'dotnet tool install --global wix' }
) | Where-Object { -not (Get-Command $_.Command -ErrorAction SilentlyContinue) }

foreach ($tool in $missing) {
    Stop-Build "$($tool.Command) is not on the PATH: $($tool.Hint)."
}

if (-not (Test-Path -LiteralPath $worker -PathType Leaf)) {
    Stop-Build "Enigma.Msi's worker is missing: $worker"
}

# The installed application is Desktop, so its csproj names the MSI's version. The About dialog shows
# Core's, so the two must agree.
$version = Get-ProjectVersion (Join-Path $root "src\$Assembly\$Assembly.csproj")
$coreVersion = Get-ProjectVersion (Join-Path $root 'src\Enigma.MarkdownEditor.Core\Enigma.MarkdownEditor.Core.csproj')

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Stop-Build "$Assembly.csproj has no <Version> of the form X.Y.Z."
}

if ($coreVersion -ne $version) {
    Stop-Build "the csprojs disagree on the version: $Assembly says '$version', Enigma.MarkdownEditor.Core says '$coreVersion'."
}

$name = "Enigma.MarkdownEditor.$version"

Write-Step "Cleaning the repository: git clean -dxf"
& git -C $root clean -dxf
if ($LASTEXITCODE -ne 0) {
    Stop-Build "git clean failed (exit code $LASTEXITCODE)." $LASTEXITCODE
}

Write-Step "Publishing $version for win-x64, self-contained: build\publish.ps1"
# build\publish.ps1 reports a failure by throwing, dotnet's exit code in the message.
try {
    & $publishScript -Rid win-x64
}
catch {
    Stop-Build "the publish failed: $($_.Exception.Message)"
}

$executable = Join-Path $publish "$Assembly.exe"
$icon = Join-Path $publish 'app.ico'

foreach ($file in $executable, $icon) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        Stop-Build "the publish folder has no $(Split-Path $file -Leaf): $publish"
    }
}

Write-Step "Building $name.msi: Enigma.Msi"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Enigma.Msi's profile format (schema 1). Its paths are absolute, because the worker resolves relative
# ones against the current directory. The product code is new for every build.
$shortcutTarget = "[INSTALLDIR]\$Assembly.exe"
$package = [ordered] @{
    schemaVersion = 1
    appName       = $ProductName
    version       = $version
    productId     = [guid]::NewGuid().ToString()
    upgradeCode   = $UpgradeCode
    manufacturer  = $Manufacturer
    scope         = 'PerMachine'
    install       = [ordered] @{
        installPath = "%ProgramFiles%\$ProductName"
        releasePath = $publish
    }
    output        = [ordered] @{
        outputPath  = $artifacts
        msiFilename = $name
    }
    compression   = 'None'
    controlPanel  = [ordered] @{
        productIcon = $icon
    }
    shortcuts     = @(
        [ordered] @{
            shortcutPath = '%Desktop%'
            shortcutName = $ProductName
            targetPath   = $shortcutTarget
            iconPath     = $icon
        },
        [ordered] @{
            shortcutPath = '%ProgramMenu%'
            shortcutName = $ProductName
            targetPath   = $shortcutTarget
            iconPath     = $icon
        }
    )
}

$profilePath = Join-Path $artifacts "$name.msipkg.json"
# -Depth: the shortcuts sit three levels down, below ConvertTo-Json's default of two.
$json = $package | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText($profilePath, $json, (New-Object System.Text.UTF8Encoding $false))

& $worker build $profilePath
if ($LASTEXITCODE -ne 0) {
    Stop-Build "Enigma.Msi could not build the MSI (exit code $LASTEXITCODE): see its reasons above." $LASTEXITCODE
}

$msi = Join-Path $artifacts "$name.msi"
if (-not (Test-Path -LiteralPath $msi -PathType Leaf)) {
    Stop-Build "Enigma.Msi reported success, but $msi does not exist."
}

Write-Host ''
Write-Host ("Built {0} in {1:mm\:ss}." -f $msi, $stopwatch.Elapsed) -ForegroundColor Green
