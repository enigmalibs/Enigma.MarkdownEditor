<#
.SYNOPSIS
    Shared context for the Enigma Markdown Editor build and packaging scripts.

.DESCRIPTION
    Dot-source this file; it has no side effects of its own:

        . (Join-Path $PSScriptRoot 'Common.ps1')

    Written against the Windows PowerShell 5.1 language subset so one copy of each script runs
    under Windows PowerShell 5.1 and under PowerShell 7+ on both Windows and Linux. That rules
    out, throughout build/ and packaging/: the ternary and ?? operators, the && / || pipeline
    chain operators, three-argument Join-Path, and the $IsWindows / $IsLinux automatic variables
    (all PowerShell 6+ only). Forward slashes in a relative path are accepted by both platforms,
    so paths are joined in one step rather than nested.
#>

Set-StrictMode -Version 2.0

function Get-AppVersion {
    <#
    .SYNOPSIS
        Reads <Version> out of a project file.

    .DESCRIPTION
        Falls back to 0.0.0-dev with a warning while the element is absent: the version is set by
        FEATURE-5B28 PHASE02, so a PHASE01 checkout has no <Version> yet and must still publish.

    .PARAMETER ProjectPath
        Absolute path of the .csproj to read.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath
    )

    $xml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
    $node = $xml.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -ne $node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
        return $node.InnerText.Trim()
    }

    Write-Warning ("No <Version> in {0} yet (set by FEATURE-5B28 PHASE02); using 0.0.0-dev." -f (Split-Path -Leaf $ProjectPath))
    return '0.0.0-dev'
}

function Get-BuildContext {
    <#
    .SYNOPSIS
        Resolves every path and name the build and packaging scripts share.

    .PARAMETER Version
        Overrides the version read from the project file. Used to pack a version other than the
        one currently in the csproj without editing it.
    #>
    [CmdletBinding()]
    param(
        [string] $Version
    )

    # $PSScriptRoot inside a function is the directory of the file that *defined* it - build/ -
    # whichever script dot-sourced this one and wherever that script lives.
    $repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
    $project = Join-Path $repoRoot 'src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj'

    if (-not (Test-Path -LiteralPath $project)) {
        throw "Project file not found: $project. Run this script from a full checkout."
    }

    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = Get-AppVersion -ProjectPath $project
    }

    return [pscustomobject]@{
        RepoRoot     = $repoRoot
        ProjectPath  = $project
        # The build output's file name, which is what the apphost, the MSI shortcut target and the
        # .desktop Exec line all point at - deliberately not the display name.
        AssemblyName = 'Enigma.MarkdownEditor.Desktop'
        ProductName  = 'Enigma Markdown Editor'
        # Lower-case, hyphenated: the Linux install directory, icon and .desktop file base name.
        PackageId    = 'enigma-markdown-editor'
        Version      = $Version
        ArtifactsDir = Join-Path $repoRoot 'artifacts'
        PublishRoot  = Join-Path $repoRoot 'artifacts/publish'
    }
}
