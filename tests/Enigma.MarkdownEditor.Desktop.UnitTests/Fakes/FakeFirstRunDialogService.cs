using System;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Counts the shell's requests to show the welcome dialog. The real service raises a
/// <c>ContentDialog</c> through a host registered on the main window, which a unit test has neither
/// of.
/// </summary>
internal sealed class FakeFirstRunDialogService : IFirstRunDialogService
{
    public int ShowCount { get; private set; }

    /// <summary>
    /// Set to make <see cref="ShowAsync"/> fail, standing in for a host that was never registered.
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// Runs in place of the user answering the dialog — the seam a test edits the live settings
    /// through, so that what the shell does afterwards can be observed.
    /// </summary>
    public Action? OnShow { get; init; }

    public Task ShowAsync()
    {
        ShowCount++;

        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        OnShow?.Invoke();

        return Task.CompletedTask;
    }
}
