namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What a substitute worked out, ready to write: one replacement over the lines that changed.
/// </summary>
/// <param name="Offset">Where the text to replace starts — the first line that had a match.</param>
/// <param name="Length">How much to replace — through the end of the last line that had one.</param>
/// <param name="Text">What goes there.</param>
/// <param name="Caret">
/// Where the caret's line begins inside <see cref="Text"/>: the last line the last substitution
/// left behind, after any <c>\r</c> in it has split the line it was on.
/// </param>
/// <param name="Substitutions">How many matches were replaced.</param>
/// <param name="Lines">On how many lines.</param>
/// <remarks>
/// One write for the whole command is what makes <c>:%s</c> over a long file a single undo step, and
/// the write is kept to the lines that changed so that a substitute on line 3 does not rewrite line
/// 300.
/// </remarks>
internal readonly record struct VimSubstitution(
    int Offset,
    int Length,
    string Text,
    int Caret,
    int Substitutions,
    int Lines)
{
    /// <summary>
    /// Gets the message vim prints — <c>3 substitutions on 2 lines</c> — with the singulars where
    /// they belong.
    /// </summary>
    public string Message
        => $"{Substitutions} substitution{(Substitutions == 1 ? string.Empty : "s")} on {Lines} line{(Lines == 1 ? string.Empty : "s")}";
}
