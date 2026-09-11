using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Answers the editor's questions with whatever the test decided in advance, and records what it was
/// asked.
/// </summary>
internal sealed class FakeDocumentPrompts : IDocumentPrompts
{
    private readonly List<string> _askedAbout = [];

    /// <summary>
    /// What every unsaved-changes question is answered with. Cancel by default — the answer that
    /// makes a test fail loudly if it did not mean to be asked at all.
    /// </summary>
    public UnsavedChangesChoice Choice { get; set; } = UnsavedChangesChoice.Cancel;

    /// <summary>
    /// Where Save As says to write, or <see langword="null"/> for a user who backed out.
    /// </summary>
    public string? SaveAsPath { get; set; }

    /// <summary>
    /// The documents the unsaved-changes question was asked about, in order.
    /// </summary>
    public IReadOnlyList<string> AskedAbout => _askedAbout;

    public int SaveAsCount { get; private set; }

    public Task<UnsavedChangesChoice> AskAboutUnsavedChangesAsync(string fileName)
    {
        _askedAbout.Add(fileName);

        return Task.FromResult(Choice);
    }

    public Task<string?> AskWhereToSaveAsync(string suggestedFileName, string? startDirectory)
    {
        SaveAsCount++;

        return Task.FromResult(SaveAsPath);
    }
}
