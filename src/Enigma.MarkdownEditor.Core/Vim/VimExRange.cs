namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The lines an ex command was given — <c>%</c>, <c>3,7</c>, <c>.,$</c>, <c>'&lt;,'&gt;</c>.
/// </summary>
/// <param name="Start">The first address.</param>
/// <param name="End">The second, or the first again when only one was typed.</param>
/// <remarks>
/// Two addresses whatever was typed, because one address is a range of one line and <c>%</c> is
/// <c>1,$</c>: the engine has one shape to resolve. Which of the two comes first in the buffer is
/// settled when they are resolved, not here — <c>5,3</c> is the same three lines as <c>3,5</c>.
/// </remarks>
internal readonly record struct VimExRange(VimExAddress Start, VimExAddress End);
