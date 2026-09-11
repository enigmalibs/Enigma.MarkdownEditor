namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// What a planned move amounts to, before anything on disk is touched.
/// </summary>
/// <remarks>
/// The three answers map onto three different user interfaces: carry on, ask, or explain. Deciding
/// this before the drop rather than during it is what lets the tree refuse a move with a sentence
/// instead of an exception.
/// </remarks>
public enum FileMovePlanStatus
{
    /// <summary>
    /// Nothing is in the way. The move can be carried out as planned.
    /// </summary>
    Ready,

    /// <summary>
    /// Something already uses that name in the target folder, so the user has to choose.
    /// </summary>
    Conflict,

    /// <summary>
    /// The move makes no sense and no answer would rescue it.
    /// </summary>
    Rejected,
}
