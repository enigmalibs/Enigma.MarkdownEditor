using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Records what was written to the clipboard. The real writer needs a window, which a unit test has
/// none of.
/// </summary>
internal sealed class FakeClipboardWriter : IClipboardWriter
{
    private readonly List<Write> _writes = [];

    /// <summary>
    /// Set to make the platform report that it has no clipboard, as a headless run does.
    /// </summary>
    public bool Declines { get; init; }

    /// <summary>
    /// Set to make the write throw, as a clipboard another application is holding open does.
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// Every write, oldest first — the plain text of each, whether or not it carried HTML too.
    /// </summary>
    public IReadOnlyList<string> Written => _writes.ConvertAll(write => write.Text);

    /// <summary>
    /// The plain text last written, or <see langword="null"/> when nothing has been.
    /// </summary>
    public string? Last => (_writes.Count > 0) ? _writes[^1].Text : null;

    /// <summary>
    /// The HTML flavour of the last write, or <see langword="null"/> when it carried none — which is
    /// what a selection copy is expected to look like.
    /// </summary>
    public string? LastHtml => (_writes.Count > 0) ? _writes[^1].Html : null;

    /// <summary>
    /// Whether the last write carried both flavours.
    /// </summary>
    public bool LastCarriedHtml => LastHtml is not null;

    public Task<bool> WriteTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Record(new Write(text, Html: null));
    }

    public Task<bool> WriteTextAndHtmlAsync(string text, string html)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(html);

        return Record(new Write(text, html));
    }

    private Task<bool> Record(Write write)
    {
        if (Failure is not null)
        {
            return Task.FromException<bool>(Failure);
        }

        if (Declines)
        {
            return Task.FromResult(false);
        }

        _writes.Add(write);

        return Task.FromResult(true);
    }

    private readonly record struct Write(string Text, string? Html);
}
