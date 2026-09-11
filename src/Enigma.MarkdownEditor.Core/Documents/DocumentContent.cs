using System;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// A text file as the editor holds it: the text with line feeds alone, plus what it takes to write it
/// back the way it was found.
/// </summary>
public sealed class DocumentContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentContent"/> class.
    /// </summary>
    /// <param name="text">The text, already normalized to line feeds.</param>
    /// <param name="format">How the file was stored.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public DocumentContent(string text, TextFileFormat format)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Format = format ?? throw new ArgumentNullException(nameof(format));
    }

    /// <summary>
    /// Gets the text, with <c>\n</c> as its only line terminator.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the encoding, byte-order mark and line-ending style the file is written back with.
    /// </summary>
    public TextFileFormat Format { get; }
}
