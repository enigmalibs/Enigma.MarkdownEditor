using System;
using System.Collections.Generic;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Which colour a path answers to, given a workspace's coloured folders.
/// </summary>
/// <remarks>
/// <para>
/// Two questions, because two surfaces ask different ones. The tree asks
/// <see cref="ColorOf(IEnumerable{FolderColorEntry}?, string?)"/> — "is <em>this</em> folder the one
/// carrying a colour?" — so that a row shows where the colour was actually set rather than tinting a
/// whole subtree. A tab asks <see cref="Resolve(IEnumerable{FolderColorEntry}?, string?)"/> — "which
/// coloured folder does this file belong to?" — because that is the whole point of the feature.
/// </para>
/// <para>
/// Both are decisions over strings and nothing else, so the whole rule is testable without a file
/// system: no path is asked whether it exists, which also means an unplugged root still answers with
/// its colour.
/// </para>
/// </remarks>
public static class FolderColorMap
{
    /// <summary>
    /// The colour set on exactly this folder.
    /// </summary>
    /// <param name="entries">The workspace's coloured folders, or <see langword="null"/> for none.</param>
    /// <param name="path">The folder to ask about.</param>
    /// <returns>Its own colour, or <see cref="FolderColor.None"/> when it has none.</returns>
    public static FolderColor ColorOf(IEnumerable<FolderColorEntry>? entries, string? path)
    {
        if ((entries is null) || (Normalize(path) is not { } folder))
        {
            return FolderColor.None;
        }

        foreach (FolderColorEntry entry in entries)
        {
            if ((Normalize(entry.Path) is { } coloured) && WorkspaceValidation.PathComparer.Equals(coloured, folder))
            {
                return entry.Color;
            }
        }

        return FolderColor.None;
    }

    /// <summary>
    /// The colour a path inherits: the one on the nearest coloured folder at or above it.
    /// </summary>
    /// <param name="entries">The workspace's coloured folders, or <see langword="null"/> for none.</param>
    /// <param name="path">The file or folder to ask about.</param>
    /// <returns>The inherited colour, or <see cref="FolderColor.None"/> when nothing above it carries one.</returns>
    /// <remarks>
    /// The <em>deepest</em> match wins, because a colour on a sub-folder is a deliberate override of
    /// its parent's. Ancestors are walked with <see cref="Path.GetDirectoryName(string?)"/> rather
    /// than compared as prefixes, which is what keeps <c>C:\a\bb</c> from being read as a child of a
    /// coloured <c>C:\a\b</c>.
    /// </remarks>
    public static FolderColor Resolve(IEnumerable<FolderColorEntry>? entries, string? path)
    {
        if (entries is null)
        {
            return FolderColor.None;
        }

        // Materialized once: the walk asks the same list a handful of times, and the caller's
        // enumerable is a live list on the configuration object.
        List<FolderColorEntry> coloured = [.. entries];

        for (string? candidate = Normalize(path); candidate is not null; candidate = Parent(candidate))
        {
            FolderColor colour = ColorOf(coloured, candidate);

            if (colour != FolderColor.None)
            {
                return colour;
            }
        }

        return FolderColor.None;
    }

    // The comparable form of a path: absolute where it can be made so, without a trailing separator.
    // A path that cannot be normalized is compared as it is written, so that a hand-edited entry
    // still matches itself.
    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return WorkspaceValidation.NormalizeDirectory(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return path.Trim();
        }
    }

    // The folder above one, or null at the top. Already-normalized in, so GetDirectoryName is asked
    // one unambiguous question; a root answers with null and ends the walk.
    private static string? Parent(string path)
    {
        string? parent = Path.GetDirectoryName(path);

        return string.IsNullOrEmpty(parent) ? null : parent;
    }
}
