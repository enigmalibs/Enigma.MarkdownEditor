namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// What the user answered when a move ran into something already using the name.
/// </summary>
/// <remarks>
/// <see cref="Cancel"/> is first so that it is the default value: a resolution nobody set has to mean
/// "do nothing", because every other answer touches a file the user did not ask about.
/// </remarks>
public enum MoveResolution
{
    /// <summary>
    /// Leave everything as it is. The move is abandoned.
    /// </summary>
    Cancel,

    /// <summary>
    /// Replace what is already there. Whatever the destination held is gone, and a folder goes with
    /// everything under it.
    /// </summary>
    Overwrite,

    /// <summary>
    /// Keep both, by numbering the entry being moved.
    /// </summary>
    KeepBoth,
}
