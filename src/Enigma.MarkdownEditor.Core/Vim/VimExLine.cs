namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// An ex line read into its parts: which lines, which command, whether it had a <c>!</c>, and what
/// followed.
/// </summary>
/// <param name="Range">The lines it was given, or <see langword="null"/> when none were.</param>
/// <param name="Name">
/// The command's name — <c>s</c>, <c>set</c>, <c>wq</c> — or the empty string for a range on its
/// own, which is a jump to its last line.
/// </param>
/// <param name="Bang">Whether a <c>!</c> followed the name.</param>
/// <param name="Argument">
/// Everything after that, trimmed — <c>/a/b/g</c>, <c>ic</c> — or the empty string.
/// </param>
/// <remarks>
/// The shape of an answer between <see cref="ExCommandParser"/> and <see cref="VimEngine"/>. What a
/// name means is the engine's question; this only says what was typed, which is why an unknown name is
/// still a line here and is turned down over there, by name.
/// </remarks>
internal readonly record struct VimExLine(VimExRange? Range, string Name, bool Bang, string Argument);
