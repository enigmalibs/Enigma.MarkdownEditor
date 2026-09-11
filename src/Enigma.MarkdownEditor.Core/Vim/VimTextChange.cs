namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One change to the buffer's text, as <see cref="IVimBuffer.Changed"/> reports it.
/// </summary>
/// <param name="Offset">Where the change happened.</param>
/// <param name="RemovedLength">How many characters went, starting at <paramref name="Offset"/>.</param>
/// <param name="InsertedText">What took their place; empty for a pure removal.</param>
/// <remarks>
/// The shape every text editor's change notification already has, reduced to the three values the
/// engine reads. It reads them for one purpose: to know what was typed during an insert session,
/// which the engine never sees as keys — in insert mode a key is the editor's, and the only trace it
/// leaves is the change it made. That record is what <c>.</c> replays and what a macro holds.
/// </remarks>
public readonly record struct VimTextChange(int Offset, int RemovedLength, string InsertedText);
