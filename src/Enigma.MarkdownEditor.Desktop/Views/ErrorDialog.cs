using System;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The error dialog every Enigma app shows for an exception nothing handled: a warning glyph in amber,
/// "Something went wrong", what happened, and a single Close.
/// </summary>
/// <remarks>
/// One configuration for both of the places it is shown — the main window's dialog host, and the
/// window a failed start-up opens instead — so the two cannot drift apart. The look is
/// Enigma.GitClient's, which is the reference for the family.
/// </remarks>
public static class ErrorDialog
{
    /// <summary>
    /// The dialog's title.
    /// </summary>
    public const string Title = "Something went wrong";

    /// <summary>
    /// The colour of the warning glyph.
    /// </summary>
    public static readonly Color IconColor = Color.Parse("#E8A33D");

    /// <summary>
    /// Configures <paramref name="dialog"/> as the error dialog.
    /// </summary>
    /// <param name="dialog">The dialog, already reset by whoever owns it.</param>
    /// <param name="message">What happened, and where the details are.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static void Configure(ContentDialog dialog, string message)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(message);

        dialog.Title = Title;

        // Selectable, so the type and the message can be copied straight into a bug report.
        dialog.Content = new SelectableTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };

        // One button, and it only dismisses: there is nothing to decide.
        dialog.CloseButtonText = "Close";
        dialog.DefaultButton = DefaultButton.Close;

        // Built per showing rather than held in a static: a geometry needs the platform, and a type
        // initializer that touched it would fail wherever this class is merely referenced first.
        dialog.IconData = PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.Warning, PhosphorWeight.Regular).ToGeometry();
        dialog.IconBrush = new SolidColorBrush(IconColor);
    }

    /// <summary>
    /// Describes an exception in one line: its type and its message.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><c>Type: message</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return $"{exception.GetType().Name}: {exception.Message}";
    }
}
