namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One complete command, as the parser hands it to the engine.
/// </summary>
/// <param name="Action">What to do.</param>
/// <param name="Motion">Where to do it, for <see cref="VimAction.Move"/> and <see cref="VimAction.Operate"/>.</param>
/// <param name="Count">
/// How many times, or zero when no count was typed. Zero rather than one because two motions mean a
/// different place with a count than without one: <c>G</c> is the last line and <c>5G</c> is line 5,
/// which a command that could not tell 1 from "none" would have no way to express.
/// </param>
/// <param name="Operator">What to do to the motion's text, for <see cref="VimAction.Operate"/>.</param>
/// <param name="Argument">The character a command reads after itself, for <c>r</c> and the finds.</param>
/// <param name="TextObject">
/// The text an <c>i</c> or an <c>a</c> named, for the commands that say which text instead of where
/// to move. Set on an <see cref="VimAction.Operate"/> in place of the <see cref="Motion"/>, and on a
/// <see cref="VimAction.Move"/> in a visual mode, where naming the text <em>is</em> the command.
/// </param>
/// <param name="Register">
/// The register a <c>"</c> in front of the command named — <c>a</c> for <c>"ayy</c>, <c>A</c> to
/// append, <c>0</c>, <c>_</c> or <c>"</c> — or <c>'\0'</c> when none was, which means the unnamed one.
/// </param>
/// <param name="Pattern">
/// What a <c>/</c> or a <c>?</c> had typed after it, for <see cref="VimMotion.SearchForward"/> and
/// <see cref="VimMotion.SearchBackward"/>; <see langword="null"/> on every other command, and on a
/// search typed with nothing after the slash, which means the last pattern again.
/// </param>
/// <remarks>
/// The shape the grammar produces. The count and the operator live here rather than in the engine's
/// own state, so that "what was asked for" stays one value a test can name in one expression.
/// </remarks>
public readonly record struct VimCommand(
    VimAction Action,
    VimMotion Motion = VimMotion.None,
    int Count = 0,
    VimOperator Operator = VimOperator.None,
    char Argument = '\0',
    VimTextObject TextObject = VimTextObject.None,
    char Register = '\0',
    string? Pattern = null)
{
    /// <summary>
    /// Gets a value indicating whether a count was typed.
    /// </summary>
    public bool HasCount => Count > 0;

    /// <summary>
    /// Gets how many times the command asks for, with an untyped count reading as one.
    /// </summary>
    public int EffectiveCount => Count > 0 ? Count : 1;

    /// <summary>
    /// A pure motion.
    /// </summary>
    /// <param name="motion">Where to move.</param>
    /// <param name="count">How many times, or zero for a count that was not typed.</param>
    /// <returns>The command.</returns>
    public static VimCommand Move(VimMotion motion, int count = 0) => new(VimAction.Move, motion, count);

    /// <summary>
    /// An operator over a motion.
    /// </summary>
    /// <param name="operator">What to do to the text.</param>
    /// <param name="motion">Which text.</param>
    /// <param name="count">How many times, or zero for a count that was not typed.</param>
    /// <returns>The command.</returns>
    public static VimCommand Operate(VimOperator @operator, VimMotion motion, int count = 0)
        => new(VimAction.Operate, motion, count, @operator);

    /// <summary>
    /// An operator over a text object.
    /// </summary>
    /// <param name="operator">What to do to the text.</param>
    /// <param name="textObject">Which text.</param>
    /// <param name="count">How many times, or zero for a count that was not typed.</param>
    /// <returns>The command.</returns>
    /// <remarks>
    /// Still an <see cref="VimAction.Operate"/>: only the noun differs, and the operator half of
    /// <c>diw</c> is the same half as in <c>dw</c>.
    /// </remarks>
    public static VimCommand Object(VimOperator @operator, VimTextObject textObject, int count = 0)
        => new(VimAction.Operate, VimMotion.None, count, @operator, '\0', textObject);
}
