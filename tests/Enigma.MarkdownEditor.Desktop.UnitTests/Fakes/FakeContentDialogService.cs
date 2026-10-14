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

    /// <summary>
    /// What every dialog resolves as: the button the user would have pressed, or
    /// <see cref="DialogResult.None"/> for an Escape or a click on the scrim.
    /// </summary>
    public DialogResult Result { get; init; } = DialogResult.Primary;

    /// <summary>
    /// Every dialog shown, configured as the caller left it.
    /// </summary>
    public IReadOnlyList<ContentDialog> Shown => _shown;

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

        ContentDialog dialog = new();
        configure(dialog);
        _shown.Add(dialog);

        return Task.FromResult(Result);
    }

    public Task HideAsync() => Task.CompletedTask;
}
