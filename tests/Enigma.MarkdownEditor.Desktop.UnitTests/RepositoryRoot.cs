using System;
using System.IO;

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// The checkout this suite was built from, for the tests that pin files nothing in the build reads —
/// the Linux installer's icons, the Windows installer's script.
/// </summary>
/// <remarks>
/// Found by walking up from the test output to the directory that holds the solution, so it holds
/// for any configuration and any output path, and for a checkout anywhere on disk.
/// </remarks>
internal static class RepositoryRoot
{
    private const string SolutionFile = "Enigma.MarkdownEditor.slnx";

    private static readonly Lazy<string> Root = new(Find);

    /// <summary>
    /// The absolute path of a file in the checkout.
    /// </summary>
    /// <param name="segments">The path below the repository root, one segment per argument.</param>
    public static string File(params string[] segments)
        => Path.Combine([Root.Value, .. segments]);

    private static string Find()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No {SolutionFile} was found above {AppContext.BaseDirectory}.");
    }
}
