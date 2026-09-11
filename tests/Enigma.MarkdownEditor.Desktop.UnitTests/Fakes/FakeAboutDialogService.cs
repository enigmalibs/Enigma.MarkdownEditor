using System;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Counts the shell's requests to open the About dialog. The real service raises a
/// <c>ContentDialog</c> through a host registered on the main window, which a unit test has neither
/// of.
/// </summary>
internal sealed class FakeAboutDialogService : IAboutDialogService
{
    public int ShowCount { get; private set; }

    /// <summary>
    /// Set to make <see cref="ShowAsync"/> fail, standing in for a host that was never registered.
    /// </summary>
    public Exception? Failure { get; init; }

    public Task ShowAsync()
    {
        ShowCount++;

        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
