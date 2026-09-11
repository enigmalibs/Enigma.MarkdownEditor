using System;
using Avalonia.Media;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// One run of a code block that a grammar gave the same colour to.
/// </summary>
/// <remarks>
/// The tokenizer's output stops here, at colours and weights rather than at controls: what a fence
/// comes out looking like is then assertable without a window, and the same tokens would build an
/// inline, a run or an export.
/// </remarks>
public sealed class MarkdownCodeToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownCodeToken"/> class.
    /// </summary>
    /// <param name="text">The text of the run.</param>
    /// <param name="foreground">
    /// The colour the theme gave it, or <see langword="null"/> when the theme said nothing — which is
    /// how a token ends up in the preview's own body colour instead of a grammar's idea of black.
    /// </param>
    /// <param name="isBold">Whether the theme asked for bold.</param>
    /// <param name="isItalic">Whether the theme asked for italic.</param>
    /// <param name="isUnderline">Whether the theme asked for an underline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public MarkdownCodeToken(string text, Color? foreground, bool isBold = false, bool isItalic = false, bool isUnderline = false)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Foreground = foreground;
        IsBold = isBold;
        IsItalic = isItalic;
        IsUnderline = isUnderline;
    }

    /// <summary>
    /// The text of the run. Never <see langword="null"/>.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The colour the theme gave the run, or <see langword="null"/> for the preview's body colour.
    /// </summary>
    public Color? Foreground { get; }

    /// <inheritdoc cref="MarkdownCodeToken(string, Color?, bool, bool, bool)"/>
    public bool IsBold { get; }

    /// <inheritdoc cref="IsBold"/>
    public bool IsItalic { get; }

    /// <inheritdoc cref="IsBold"/>
    public bool IsUnderline { get; }
}
