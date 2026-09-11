using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// Applies a <see cref="TextEdit"/> the way the editor does, so a test can assert on the text that
/// comes out rather than on four numbers.
/// </summary>
internal static class Edits
{
    /// <summary>
    /// Applies an edit that must be there.
    /// </summary>
    public static string Apply(string text, TextEdit? edit)
    {
        TextEdit applied = Assert.IsType<TextEdit>(edit);

        return text.Remove(applied.Start, applied.Length).Insert(applied.Start, applied.Replacement);
    }

    /// <summary>
    /// Where the caret is left by an edit that must be there.
    /// </summary>
    public static int Caret(TextEdit? edit) => Assert.IsType<TextEdit>(edit).CaretOffset;
}
