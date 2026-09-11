using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Numbers a name until nothing in a folder is using it — the "keep both" half of a move, and the
/// name a duplicate is offered under.
/// </summary>
/// <remarks>
/// The shape is the one every desktop file manager uses: <c>notes (2).md</c>. It is worth matching
/// exactly, because a user reading the tree afterwards has to be able to tell at a glance which of the
/// two files is the one they just dropped.
/// </remarks>
public static class UniqueName
{
    // Numbering has to stop somewhere. A thousand is far past any folder a person curates by hand, so
    // reaching it means something is wrong with the folder rather than with the name, and saying so
    // beats spinning through every integer.
    private const int MaxAttempts = 1000;

    /// <summary>
    /// Builds the name of one numbered attempt.
    /// </summary>
    /// <param name="name">The name as the user knows it.</param>
    /// <param name="attempt">Which attempt this is. Attempt 1 is the name itself.</param>
    /// <param name="isDirectory">Whether the name belongs to a folder.</param>
    /// <returns>The candidate name, without any directory.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is below 1.</exception>
    /// <remarks>
    /// A folder keeps its whole name and takes the number at the end, because a dot in a folder name is
    /// part of the name rather than an extension. A file takes it before the extension, where the
    /// extension is whatever <see cref="Path.GetExtension(string)"/> says: a compound suffix such as
    /// <c>.tar.gz</c> counts as the single trailing <c>.gz</c>, which gives
    /// <c>archive.tar (2).gz</c>. That is not what the user would have typed, but it keeps the rule to
    /// one the operating system also applies, and the file still opens.
    /// </remarks>
    public static string Candidate(string name, int attempt, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        if (attempt == 1)
        {
            return name;
        }

        if (isDirectory)
        {
            return $"{name} ({attempt})";
        }

        string extension = Path.GetExtension(name);
        string stem = name[..^extension.Length];

        // A dotfile is all extension and no stem — ".gitignore" — so there is nowhere sensible to put
        // the number except the end, where it also keeps the leading dot doing its job.
        return stem.Length == 0 ? $"{name} ({attempt})" : $"{stem} ({attempt}){extension}";
    }

    /// <summary>
    /// Finds the first name nothing in a folder is using, starting from the name itself.
    /// </summary>
    /// <param name="directory">The folder the name has to be free in.</param>
    /// <param name="name">The name as the user knows it.</param>
    /// <param name="isDirectory">Whether the name belongs to a folder.</param>
    /// <returns>The free name, without any directory.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">
    /// The folder already holds every numbered name this is willing to try.
    /// </exception>
    /// <remarks>
    /// A file and a folder cannot share a name, so both are looked for: numbering a file past a folder
    /// that happens to be called the same thing is the only way the result is actually free.
    /// </remarks>
    public static string In(string directory, string name, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            string candidate = Candidate(name, attempt, isDirectory);
            string path = Path.Combine(directory, candidate);

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return candidate;
            }
        }

        throw new FileOperationException($"There is no free name left for '{name}' in this folder.");
    }
}
