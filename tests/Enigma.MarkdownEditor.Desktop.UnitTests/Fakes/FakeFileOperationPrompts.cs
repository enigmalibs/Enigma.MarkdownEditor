using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// The answers a user would have given, decided up front by the test.
/// </summary>
/// <remarks>
/// Every method of the real implementation ends in a <c>ContentDialog</c>, which needs a registered
/// host and therefore a running application. This is what lets the tree's operations be driven end to
/// end without one: the ViewModel asks exactly the same questions and cannot tell where the answers
/// come from. A <see langword="null"/> name is the user backing out.
/// </remarks>
internal sealed class FakeFileOperationPrompts : IFileOperationPrompts
{
    private readonly List<string> _askedForNewFileIn = [];
    private readonly List<string> _askedForNewFolderIn = [];
    private readonly List<string> _askedToRename = [];

    /// <summary>
    /// What the user types for a new file, or <see langword="null"/> to back out.
    /// </summary>
    public string? NewFileName { get; init; }

    /// <summary>
    /// What the user types for a new folder, or <see langword="null"/> to back out.
    /// </summary>
    public string? NewFolderName { get; init; }

    /// <summary>
    /// What the user types for a rename, or <see langword="null"/> to back out.
    /// </summary>
    public string? NewName { get; init; }

    /// <summary>
    /// What the user picks when a name is already taken. Cancel unless a test says otherwise, which is
    /// the same default the real dialog applies to a dismissal.
    /// </summary>
    public MoveResolution Resolution { get; init; } = MoveResolution.Cancel;

    /// <summary>
    /// Whether the user confirms a delete that cannot be undone.
    /// </summary>
    public bool ConfirmsPermanentDelete { get; init; }

    public IReadOnlyList<string> AskedForNewFileIn => _askedForNewFileIn;

    public IReadOnlyList<string> AskedForNewFolderIn => _askedForNewFolderIn;

    public IReadOnlyList<string> AskedToRename => _askedToRename;

    public int ConflictsAsked { get; private set; }

    public int PermanentDeletesAsked { get; private set; }

    public Task<string?> AskForNewFileNameAsync(string directory)
    {
        _askedForNewFileIn.Add(directory);

        return Task.FromResult(NewFileName);
    }

    public Task<string?> AskForNewFolderNameAsync(string directory)
    {
        _askedForNewFolderIn.Add(directory);

        return Task.FromResult(NewFolderName);
    }

    public Task<string?> AskForNewNameAsync(string path, bool isDirectory)
    {
        _askedToRename.Add(path);

        return Task.FromResult(NewName);
    }

    public Task<MoveResolution> AskHowToResolveAsync(string name, bool isDirectory, string targetDirectory)
    {
        ConflictsAsked++;

        return Task.FromResult(Resolution);
    }

    public Task<bool> ConfirmPermanentDeleteAsync(string name, bool isDirectory)
    {
        PermanentDeletesAsked++;

        return Task.FromResult(ConfirmsPermanentDelete);
    }
}
