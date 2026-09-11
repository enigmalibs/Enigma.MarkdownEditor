using System;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A folder picker that answers with whatever the test told it to, without a window.
/// </summary>
/// <remarks>
/// This is the reason <see cref="IFolderPicker"/> exists at all: Avalonia's storage interfaces carry a
/// marker that makes the compiler refuse an implementation outside Avalonia, so the picker cannot be
/// faked at the layer below this one.
/// </remarks>
internal sealed class FakeFolderPicker : IFolderPicker
{
    /// <summary>
    /// The folder the next pick returns, or <see langword="null"/> for a dismissed dialog.
    /// </summary>
    public string? Picked { get; set; }

    /// <summary>
    /// Set to make the picker fail, standing in for a storage provider that is not there.
    /// </summary>
    public Exception? Failure { get; init; }

    public int PickCount { get; private set; }

    public string? LastTitle { get; private set; }

    public Task<string?> PickAsync(string title)
    {
        PickCount++;
        LastTitle = title;

        return Failure is null ? Task.FromResult(Picked) : Task.FromException<string?>(Failure);
    }
}
