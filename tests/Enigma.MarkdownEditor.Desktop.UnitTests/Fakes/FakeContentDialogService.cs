using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A dialog host that shows nothing: it applies each <c>configure</c> delegate to a fresh
/// <see cref="ContentDialog"/>, keeps that dialog for the test to read, and answers with the result
/// the test chose.
/// </summary>
/// <remarks>
/// Unlike <see cref="FakeInfoBarService"/>, the delegates are applied rather than kept, because what
/// they write — the title, the message, the buttons — is what the tests over this fake are about.
/// Building a <see cref="ContentDialog"/> needs an initialized Avalonia application, so a test using
/// this runs under <c>[AvaloniaFact]</c>.
/// </remarks>
internal sealed class FakeContentDialogService : IContentDialogService
{
    private readonly List<ContentDialog> _shown = [];
    private readonly List<TaskCompletionSource<DialogResult>> _held = [];

    /// <summary>
    /// What every dialog resolves as: the button the user would have pressed, or
    /// <see cref="DialogResult.None"/> for an Escape or a click on the scrim.
    /// </summary>
    public DialogResult Result { get; init; } = DialogResult.Primary;

    /// <summary>
    /// Set to keep every dialog open until <see cref="Release"/>, as a user who has not answered yet
    /// would.
    /// </summary>
    public bool Hold { get; init; }

    /// <summary>
    /// Set to make every showing fail with this, as a host that is not there would.
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// How many times a dialog was asked for, including the ones that failed.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Every dialog shown, configured as the caller left it.
    /// </summary>
    public IReadOnlyList<ContentDialog> Shown => _shown;

    /// <summary>
    /// Closes every dialog <see cref="Hold"/> kept open, with <see cref="Result"/>.
    /// </summary>
    public void Release()
    {
        TaskCompletionSource<DialogResult>[] held = [.. _held];
        _held.Clear();

        foreach (TaskCompletionSource<DialogResult> dialog in held)
        {
            dialog.SetResult(Result);
        }
    }

    public void RegisterHost(ContentDialog dialog)
    {
    }

    public Task<DialogResult> ShowMessageAsync(string title, string message, string closeButtonText = "OK")
        => ShowAsync(dialog =>
        {
            dialog.Title = title;
            dialog.Content = message;
            dialog.CloseButtonText = closeButtonText;
        });

    public Task<DialogResult> ShowAsync(Action<ContentDialog> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Attempts++;

        if (Failure is not null)
        {
            return Task.FromException<DialogResult>(Failure);
        }

        ContentDialog dialog = new();
        configure(dialog);
        _shown.Add(dialog);

        if (Hold)
        {
            TaskCompletionSource<DialogResult> held = new();
            _held.Add(held);

            return held.Task;
        }

        return Task.FromResult(Result);
    }

    public Task HideAsync() => Task.CompletedTask;
}
