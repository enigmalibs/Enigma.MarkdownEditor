namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One character find, with everything <see cref="VimFind"/> needs to run it already worked out.
/// </summary>
/// <param name="Motion">
/// Which of the four finds to run. Always one of <see cref="VimMotion.FindForward"/>,
/// <see cref="VimMotion.FindBackward"/>, <see cref="VimMotion.TillForward"/> and
/// <see cref="VimMotion.TillBackward"/> — never <c>;</c> or <c>,</c>, which are resolved away first.
/// </param>
/// <param name="Character">The character to look for.</param>
/// <param name="Repeat">
/// Whether this is <c>;</c> or <c>,</c> rather than a freshly typed find. It is the one thing the
/// scan cannot work out for itself and the one thing that changes it: a repeated <c>t</c> starts one
/// character further along, because the caret is already sitting where the last one left it and a
/// scan from there would find the same character again and go nowhere.
/// </param>
/// <remarks>
/// A carrier rather than three loose parameters, because both callers of the scan — the engine for a
/// bare motion, <see cref="VimRanges"/> for one behind an operator — need all three, and only the
/// engine holds the state the resolution reads.
/// </remarks>
internal readonly record struct VimFindRequest(VimMotion Motion, char Character, bool Repeat)
{
    /// <summary>
    /// Gets a value indicating whether the find runs towards the end of the line.
    /// </summary>
    public bool IsForward => Motion is VimMotion.FindForward or VimMotion.TillForward;

    /// <summary>
    /// Gets a value indicating whether the find stops one short of the character it looks for.
    /// </summary>
    public bool IsTill => Motion is VimMotion.TillForward or VimMotion.TillBackward;
}
