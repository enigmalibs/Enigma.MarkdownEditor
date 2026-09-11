namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What one keystroke did to the parser.
/// </summary>
/// <param name="Status">Whether a command came out, more keys are needed, or the sequence is dead.</param>
/// <param name="Command">The command, when <paramref name="Status"/> is <see cref="VimParseStatus.Complete"/>.</param>
/// <param name="Sequence">
/// The keys the result covers, as typed — <c>"gx"</c> for a dead sequence — so an invalid one can be
/// named in the message rather than reported as "something went wrong".
/// </param>
public readonly record struct VimParseResult(VimParseStatus Status, VimCommand Command, string Sequence)
{
    /// <summary>
    /// A finished command.
    /// </summary>
    /// <param name="command">What it is.</param>
    /// <param name="sequence">The keys it was typed as.</param>
    /// <returns>The result.</returns>
    public static VimParseResult Complete(VimCommand command, string sequence)
        => new(VimParseStatus.Complete, command, sequence);

    /// <summary>
    /// A sequence that needs another key.
    /// </summary>
    /// <param name="sequence">The keys so far.</param>
    /// <returns>The result.</returns>
    public static VimParseResult Pending(string sequence)
        => new(VimParseStatus.Pending, default, sequence);

    /// <summary>
    /// A sequence that cannot become a command.
    /// </summary>
    /// <param name="sequence">The keys it was typed as.</param>
    /// <returns>The result.</returns>
    public static VimParseResult Invalid(string sequence)
        => new(VimParseStatus.Invalid, default, sequence);
}
