<#
.SYNOPSIS
    Packs the linux-x64 publish output into the release tarball.

.DESCRIPTION
    Produces artifacts/EnigmaMarkdownEditor-<version>-linux-x64.tar.gz, laid out as:

        EnigmaMarkdownEditor-<version>-linux-x64/
          install.sh                            0755
          uninstall.sh                          0755
          LICENSE.md                            0644
          enigma-markdown-editor.desktop.in     0644
          icons/
            enigma-markdown-editor-<N>.png      0644, N = 16 32 48 64 128 256
          app/                                  the self-contained publish output
            Enigma.MarkdownEditor.Desktop       0755
            ...                                 0755 for native code, 0644 otherwise

    That is packaging/linux as it stands in the repository, plus app/: install.sh installs the
    app/ it finds beside itself instead of building one, so the same two scripts serve a checkout
    and the unpacked tarball.

    The archive is written a header block at a time rather than by shelling out to tar, for one
    reason: the mode bits. A Windows publish folder carries no POSIX permissions, so any tar built
    from it on Windows would mark the application host 0644 and hand the user a tarball that
    unpacks into something they cannot run. Deciding each entry's mode from the file itself makes
    the result identical whichever OS packs it, and needs no tar binary on either.

    Text entries are written with LF endings whatever the checkout produced: a CR byte on the
    shebang line stops install.sh and uninstall.sh from running at all.

    Runs on Windows PowerShell 5.1 and on PowerShell 7+; see build/Common.ps1 for the shared
    language subset.

.PARAMETER Version
    Version for the archive name. Defaults to <Version> in the Desktop csproj.

.PARAMETER PublishDir
    The linux-x64 publish folder to pack. Defaults to artifacts/publish/linux-x64.

.PARAMETER OutputPath
    Full path of the .tar.gz to write. Defaults to artifacts/<archive name>.tar.gz.

.EXAMPLE
    ./build/publish.ps1 -Rid linux-x64
    ./packaging/linux/pack-linux.ps1
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $PublishDir,
    [string] $OutputPath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '../../build/Common.ps1')

# -----------------------------------------------------------------------------------------------
# ustar writing
# -----------------------------------------------------------------------------------------------

$script:TarBlockSize = 512
$script:ModeExecutable = '0000755'
$script:ModeRegular = '0000644'

function Set-TarField {
    param(
        [byte[]] $Block,
        [int] $Offset,
        [int] $Length,
        [string] $Value
    )

    $bytes = [System.Text.Encoding]::ASCII.GetBytes($Value)
    if ($bytes.Length -gt $Length) {
        throw "Tar field at offset $Offset overflows ($($bytes.Length) > $Length): '$Value'."
    }
    [System.Array]::Copy($bytes, 0, $Block, $Offset, $bytes.Length)
}

function ConvertTo-TarOctal {
    param(
        [long] $Value,
        [int] $Digits
    )

    return [System.Convert]::ToString($Value, 8).PadLeft($Digits, '0')
}

function New-TarHeader {
    <#
    .SYNOPSIS
        Builds one 512-byte POSIX ustar header block.
    #>
    param(
        [string] $Name,
        [long] $Size,
        [string] $Mode,
        [char] $TypeFlag,
        [long] $ModifiedUnixTime
    )

    $nul = [string][char]0

    # A name longer than 100 bytes is split across the prefix field at a '/' boundary; ustar has no
    # other way to express one, and silently truncating would corrupt the archive.
    $namePart = $Name
    $prefixPart = ''
    if ([System.Text.Encoding]::ASCII.GetByteCount($Name) -gt 100) {
        $split = $Name.LastIndexOf('/', [System.Math]::Min($Name.Length - 1, 155))
        if ($split -le 0 -or ($Name.Length - $split - 1) -gt 100) {
            throw "Path too long for the ustar format: $Name"
        }
        $prefixPart = $Name.Substring(0, $split)
        $namePart = $Name.Substring($split + 1)
    }

    $block = New-Object byte[] $script:TarBlockSize

    Set-TarField $block 0 100 $namePart
    Set-TarField $block 100 8 ($Mode + $nul)
    Set-TarField $block 108 8 ('0000000' + $nul)                              # uid - root
    Set-TarField $block 116 8 ('0000000' + $nul)                              # gid - root
    Set-TarField $block 124 12 ((ConvertTo-TarOctal $Size 11) + $nul)
    Set-TarField $block 136 12 ((ConvertTo-TarOctal $ModifiedUnixTime 11) + $nul)
    Set-TarField $block 148 8 '        '                                      # checksum placeholder
    $block[156] = [byte][char]$TypeFlag
    Set-TarField $block 257 6 ('ustar' + $nul)
    Set-TarField $block 263 2 '00'
    Set-TarField $block 265 32 'root'
    Set-TarField $block 297 32 'root'
    Set-TarField $block 345 155 $prefixPart

    # The checksum is the unsigned sum of every header byte, computed while the field itself reads
    # as eight spaces - then written back over it as six octal digits, NUL, space.
    $sum = 0
    foreach ($b in $block) { $sum += $b }
    Set-TarField $block 148 8 ((ConvertTo-TarOctal $sum 6) + $nul + ' ')

    return $block
}

function Write-TarPadding {
    param(
        [System.IO.Stream] $Stream,
        [long] $BytesWritten
    )

    $remainder = [int]($BytesWritten % $script:TarBlockSize)
    if ($remainder -ne 0) {
        $padding = $script:TarBlockSize - $remainder
        $Stream.Write((New-Object byte[] $padding), 0, $padding)
    }
}

function Get-LfBytes {
    <#
    .SYNOPSIS
        Reads a text file as UTF-8 without a BOM and with LF line endings.
    #>
    param([string] $Path)

    $text = [System.IO.File]::ReadAllText($Path)
    $text = $text.Replace([string][char]13 + [string][char]10, [string][char]10)
    return (New-Object System.Text.UTF8Encoding($false)).GetBytes($text)
}

function Get-EntryMode {
    <#
    .SYNOPSIS
        Decides a published file's permission bits from the file itself.

    .DESCRIPTION
        Native code needs the executable bit and managed assemblies do not. In a .NET publish the
        native files are the application host and createdump, plus the shared objects; everything
        else is data as far as the loader is concerned.
    #>
    param(
        [string] $Leaf,
        [string] $AppHostName
    )

    # The host is named after the assembly, so a dotted product name leaves it looking like a file
    # with an extension - GetExtension('Enigma.MarkdownEditor.Desktop') is '.Desktop'. It has to be
    # matched by name; the extensionless rule below only reaches createdump.
    if ($Leaf -eq $AppHostName) { return $script:ModeExecutable }
    if ([System.IO.Path]::GetExtension($Leaf) -eq '') { return $script:ModeExecutable }
    if ($Leaf -like '*.so' -or $Leaf -like '*.so.*') { return $script:ModeExecutable }
    return $script:ModeRegular
}

# -----------------------------------------------------------------------------------------------
# Inputs
# -----------------------------------------------------------------------------------------------

$ctx = Get-BuildContext -Version $Version

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $ctx.PublishRoot 'linux-x64'
}
if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish folder not found: $PublishDir. Run build/publish.ps1 -Rid linux-x64 first."
}
$PublishDir = (Resolve-Path -LiteralPath $PublishDir).ProviderPath

$appHost = Join-Path $PublishDir $ctx.AssemblyName
if (-not (Test-Path -LiteralPath $appHost)) {
    throw "$($ctx.AssemblyName) is missing from $PublishDir - that folder is not a linux-x64 publish."
}

$archiveName = "EnigmaMarkdownEditor-$($ctx.Version)-linux-x64"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ctx.ArtifactsDir "$archiveName.tar.gz"
}

# The hicolor sizes install.sh and uninstall.sh expect; the three lists move together.
$iconSizes = @(16, 32, 48, 64, 128, 256)

$desktopSource = Join-Path $PSScriptRoot "$($ctx.PackageId).desktop.in"
$installSource = Join-Path $PSScriptRoot 'install.sh'
$uninstallSource = Join-Path $PSScriptRoot 'uninstall.sh'
$licenseSource = Join-Path $ctx.RepoRoot 'LICENSE.md'

$iconSources = @()
foreach ($size in $iconSizes) {
    $iconSources += Join-Path $PSScriptRoot "icons/$($ctx.PackageId)-$size.png"
}

foreach ($required in (@($desktopSource, $installSource, $uninstallSource, $licenseSource) + $iconSources)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required packaging file not found: $required"
    }
}

# -----------------------------------------------------------------------------------------------
# Entry list
# -----------------------------------------------------------------------------------------------

$entries = New-Object System.Collections.Generic.List[object]

function Add-Entry {
    param(
        [string] $Name,
        [string] $Kind,
        [string] $Mode,
        [string] $Path,
        [byte[]] $Bytes
    )

    $entries.Add([pscustomobject]@{
            Name  = $Name
            Kind  = $Kind
            Mode  = $Mode
            Path  = $Path
            Bytes = $Bytes
        })
}

Add-Entry -Name "$archiveName/" -Kind 'dir' -Mode $script:ModeExecutable
Add-Entry -Name "$archiveName/install.sh" -Kind 'bytes' -Mode $script:ModeExecutable -Bytes (Get-LfBytes $installSource)
Add-Entry -Name "$archiveName/uninstall.sh" -Kind 'bytes' -Mode $script:ModeExecutable -Bytes (Get-LfBytes $uninstallSource)
Add-Entry -Name "$archiveName/LICENSE.md" -Kind 'bytes' -Mode $script:ModeRegular -Bytes (Get-LfBytes $licenseSource)
Add-Entry -Name "$archiveName/$($ctx.PackageId).desktop.in" -Kind 'bytes' -Mode $script:ModeRegular -Bytes (Get-LfBytes $desktopSource)
Add-Entry -Name "$archiveName/icons/" -Kind 'dir' -Mode $script:ModeExecutable
foreach ($iconSource in $iconSources) {
    $iconLeaf = Split-Path -Leaf $iconSource
    Add-Entry -Name "$archiveName/icons/$iconLeaf" -Kind 'file' -Mode $script:ModeRegular -Path $iconSource
}
Add-Entry -Name "$archiveName/app/" -Kind 'dir' -Mode $script:ModeExecutable

# Directories before the files in them, and both in a stable order, so the archive is reproducible
# and an extractor never meets a file whose parent it has not created.
foreach ($dir in @(Get-ChildItem -LiteralPath $PublishDir -Recurse -Directory | Sort-Object FullName)) {
    $relative = $dir.FullName.Substring($PublishDir.Length).TrimStart([char]92, [char]47).Replace([char]92, [char]47)
    Add-Entry -Name "$archiveName/app/$relative/" -Kind 'dir' -Mode $script:ModeExecutable
}

foreach ($file in @(Get-ChildItem -LiteralPath $PublishDir -Recurse -File | Sort-Object FullName)) {
    $relative = $file.FullName.Substring($PublishDir.Length).TrimStart([char]92, [char]47).Replace([char]92, [char]47)
    $mode = Get-EntryMode -Leaf $file.Name -AppHostName $ctx.AssemblyName
    Add-Entry -Name "$archiveName/app/$relative" -Kind 'file' -Mode $mode -Path $file.FullName
}

# -----------------------------------------------------------------------------------------------
# Write
# -----------------------------------------------------------------------------------------------

Write-Host ''
Write-Host ("{0} {1} - packing linux-x64" -f $ctx.ProductName, $ctx.Version) -ForegroundColor Cyan
Write-Host ("  publish : {0}" -f $PublishDir)
Write-Host ("  output  : {0}" -f $OutputPath)
Write-Host ''

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

$mtime = [System.DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$totalBytes = [long]0
$fileStream = $null
$gzipStream = $null

try {
    $fileStream = [System.IO.File]::Create($OutputPath)
    $gzipStream = New-Object System.IO.Compression.GZipStream(
        $fileStream, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    foreach ($entry in $entries) {
        if ($entry.Kind -eq 'dir') {
            $header = New-TarHeader -Name $entry.Name -Size 0 -Mode $entry.Mode -TypeFlag '5' -ModifiedUnixTime $mtime
            $gzipStream.Write($header, 0, $header.Length)
            continue
        }

        if ($entry.Kind -eq 'bytes') {
            $header = New-TarHeader -Name $entry.Name -Size $entry.Bytes.Length -Mode $entry.Mode -TypeFlag '0' -ModifiedUnixTime $mtime
            $gzipStream.Write($header, 0, $header.Length)
            $gzipStream.Write($entry.Bytes, 0, $entry.Bytes.Length)
            Write-TarPadding $gzipStream $entry.Bytes.Length
            $totalBytes += $entry.Bytes.Length
            continue
        }

        $length = (Get-Item -LiteralPath $entry.Path).Length
        $header = New-TarHeader -Name $entry.Name -Size $length -Mode $entry.Mode -TypeFlag '0' -ModifiedUnixTime $mtime
        $gzipStream.Write($header, 0, $header.Length)

        # Streamed rather than buffered: the publish folder is well over 100 MB and reading it into
        # memory to compress it would be the only large allocation in the whole build.
        $source = [System.IO.File]::OpenRead($entry.Path)
        try { $source.CopyTo($gzipStream) } finally { $source.Dispose() }

        Write-TarPadding $gzipStream $length
        $totalBytes += $length
    }

    # Two zero blocks end a tar archive.
    $trailer = New-Object byte[] ($script:TarBlockSize * 2)
    $gzipStream.Write($trailer, 0, $trailer.Length)
}
finally {
    if ($null -ne $gzipStream) { $gzipStream.Dispose() }
    if ($null -ne $fileStream) { $fileStream.Dispose() }
}

$packed = Get-Item -LiteralPath $OutputPath
$executables = @($entries | Where-Object { $_.Kind -ne 'dir' -and $_.Mode -eq $script:ModeExecutable })

Write-Host ("Packed {0}" -f $packed.Name) -ForegroundColor Green
Write-Host ("  entries     : {0}" -f $entries.Count)
Write-Host ("  executable  : {0}" -f $executables.Count)
Write-Host ("  uncompressed: {0:N1} MB" -f ($totalBytes / 1MB))
Write-Host ("  archive     : {0:N1} MB" -f ($packed.Length / 1MB))
Write-Host ''
