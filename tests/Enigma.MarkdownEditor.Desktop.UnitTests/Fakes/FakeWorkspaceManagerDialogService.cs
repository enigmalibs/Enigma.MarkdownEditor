using System;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Counts the switcher's requests to manage workspaces. The real service builds an Avalonia control
/// and hosts it in the application's <c>ContentDialog</c>, neither of which exists in a unit test.
/// </summary>
internal sealed class FakeWorkspaceManagerDialogService : IWorkspaceManagerDialogService
{
    public int ShowCount { get; private set; }

    /// <summary>
    /// Set to make <see cref="ShowAsync"/> fail, standing in for a dialog host that is not there.
    /// </summary>
    public Exception? Failure { get; init; }

    public Task ShowAsync()
    {
        ShowCount++;

        return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
