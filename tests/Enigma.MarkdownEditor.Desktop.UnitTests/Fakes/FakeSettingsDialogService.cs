using System;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Counts the shell's requests to open the settings dialog. The real service builds a view and
/// raises it on the <c>ContentDialog</c> host, neither of which a unit test has an Avalonia
/// application for.
/// </summary>
internal sealed class FakeSettingsDialogService : ISettingsDialogService
{
    public int ShowCount { get; private set; }

    /// <summary>
    /// Set to make <see cref="ShowAsync"/> fail, standing in for a dialog that will not open.
    /// </summary>
    public Exception? Failure { get; init; }

    public Task ShowAsync()
    {
        ShowCount++;

        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
