using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The rules a file or folder name has to satisfy, as answers a dialog can show while the user is
/// still typing.
/// </summary>
/// <remarks>
/// Same contract as <see cref="WorkspaceValidation"/>: each method returns the sentence to
/// put under the box, or <see langword="null"/> when the name is fine. The rules are deliberately
/// stricter than any single file system — a name that only works on one platform is a workspace that
/// stops opening when it is synchronised to another, and that is a far worse failure than being told
/// to pick a different name.
/// </remarks>
public static class FileNameValidation
{
    /// <summary>
    /// The longest name accepted, in characters. This is what the common file systems allow for a
    /// single path component.
    /// </summary>
    public const int MaxNameLength = 255;

    private static readonly char[] Separators = ['\\', '/'];

    private static readonly char[] ForbiddenCharacters = Path.GetInvalidFileNameChars();

    private static readonly string ForbiddenCharactersMessage = BuildForbiddenCharactersMessage();

    // Windows refuses these whatever the extension and wherever they sit, because they name devices
    // rather than files. They are listed as stems: "nul.md" is just as reserved as "nul".
    private static readonly IReadOnlySet<string> ReservedStems = BuildReservedStems();

    /// <summary>
    /// Checks a name on its own, without looking at what is already on disk.
    /// </summary>
    /// <param name="name">The name to check, without any directory.</param>
    /// <param name="isDirectory">Whether the name is meant for a folder, which only changes wording.</param>
    /// <returns>The problem with the name, or <see langword="null"/> when there is none.</returns>
    public static string? Validate(string? name, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "A name cannot be empty.";
        }

        if (name.IndexOfAny(Separators) >= 0)
        {
            return @"A name cannot contain \ or /.";
        }

        if (name.IndexOfAny(ForbiddenCharacters) >= 0)
        {
            return ForbiddenCharactersMessage;
        }

        if (name is "." or "..")
        {
            return "'.' and '..' name a folder rather than something inside one.";
        }

        // Windows silently drops a trailing space or dot, so a file created as "notes " opens as
        // "notes" and the tree is left showing a name that no longer exists. Refused everywhere, so
        // that the same workspace behaves the same on every machine it is opened on.
        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return "A name cannot end with a space or a dot.";
        }

        if (ReservedStems.Contains(Stem(name)))
        {
            return $"A {Kind(isDirectory)} cannot be called '{Stem(name)}': Windows keeps that name for a device.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"A name cannot be longer than {MaxNameLength} characters.";
        }

        return null;
    }

    /// <summary>
    /// Checks a name against the rules and against what the folder already holds.
    /// </summary>
    /// <param name="name">The name to check, without any directory.</param>
    /// <param name="parentDirectory">The folder the name would be used in.</param>
    /// <param name="isDirectory">Whether the name is meant for a folder.</param>
    /// <param name="currentName">
    /// The name the entry has right now, when one is being renamed, so that keeping its own name is
    /// not reported as a collision.
    /// </param>
    /// <returns>The problem with the name, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentException"><paramref name="parentDirectory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parentDirectory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <paramref name="currentName"/> is compared with
    /// <see cref="WorkspaceValidation.PathComparer"/> rather than ordinally, so that
    /// changing only the case of a name is understood as the entry keeping itself: on Windows the
    /// file system would otherwise report the entry as a collision with its own new name.
    /// </remarks>
    public static string? ValidateIn(
        string? name,
        string parentDirectory,
        bool isDirectory,
        string? currentName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);

        string? problem = Validate(name, isDirectory);
        if (problem is not null)
        {
            return problem;
        }

        string candidate = name!;

        if (currentName is not null && WorkspaceValidation.PathComparer.Equals(currentName, candidate))
        {
            return null;
        }

        try
        {
            string path = Path.Combine(parentDirectory, candidate);

            if (File.Exists(path))
            {
                return $"There is already a file called '{candidate}' here.";
            }

            if (Directory.Exists(path))
            {
                return $"There is already a folder called '{candidate}' here.";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be looked at answers "no collision": the create or the rename is
            // about to run against the same folder and will report the real failure, whereas a dialog
            // that refuses to close over an unreachable share leaves the user with nothing to do.
        }

        return null;
    }

    private static string Kind(bool isDirectory) => isDirectory ? "folder" : "file";

    // Windows compares the part before the first dot, not the part before the last one, so both
    // "nul.md" and "nul.tar.gz" name the same device.
    private static string Stem(string name)
    {
        int dot = name.IndexOf('.');

        return dot < 0 ? name : name[..dot];
    }

    private static string BuildForbiddenCharactersMessage()
    {
        // The platform's list also holds control characters, and there is no way to show one of those
        // in a sentence — so only the visible ones are named, sorted so the message is the same on
        // every run rather than following the order the runtime happens to list them in.
        string visible = string.Join(
            ' ',
            ForbiddenCharacters
                .Where(character => !char.IsControl(character) && !Separators.Contains(character))
                .Order());

        return visible.Length == 0
            ? "A name cannot contain that character."
            : $"A name cannot use any of these characters: {visible}";
    }

    private static IReadOnlySet<string> BuildReservedStems()
    {
        HashSet<string> stems = new(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL" };

        for (int port = 1; port <= 9; port++)
        {
            stems.Add($"COM{port}");
            stems.Add($"LPT{port}");
        }

        return stems;
    }
}
